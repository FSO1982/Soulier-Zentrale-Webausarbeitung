using Microsoft.EntityFrameworkCore;
using Soulier.Zentrale.Domain;

namespace Soulier.Zentrale.Infrastructure;

public sealed class SoulierDbContext(DbContextOptions<SoulierDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<HumanPrincipal> HumanPrincipals => Set<HumanPrincipal>();
    public DbSet<RoleDefinition> Roles => Set<RoleDefinition>();
    public DbSet<RoleCapability> RoleCapabilities => Set<RoleCapability>();
    public DbSet<HumanRoleAssignment> HumanRoleAssignments => Set<HumanRoleAssignment>();
    public DbSet<KnowledgeSource> KnowledgeSources => Set<KnowledgeSource>();
    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<KnowledgeRelease> KnowledgeReleases => Set<KnowledgeRelease>();
    public DbSet<AiUseCase> AiUseCases => Set<AiUseCase>();
    public DbSet<AiUseCaseVersion> AiUseCaseVersions => Set<AiUseCaseVersion>();
    public DbSet<ActionDefinition> ActionDefinitions => Set<ActionDefinition>();
    public DbSet<ActionExecutionRecord> ActionExecutions => Set<ActionExecutionRecord>();
    public DbSet<RetentionRule> RetentionRules => Set<RetentionRule>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAuditAppendOnly()
    {
        var mutationDetected = ChangeTracker
            .Entries<AuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (mutationDetected)
            throw new InvalidOperationException("Audit events are append-only and cannot be modified or deleted.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("client", "clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Environment).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.Environment, x.Name }).IsUnique();
        });

        modelBuilder.Entity<HumanPrincipal>(entity =>
        {
            entity.ToTable("human_principal", "identity");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OidcSubject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.OidcSubject).IsUnique();
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<RoleDefinition>(entity =>
        {
            entity.ToTable("role", "identity");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<RoleCapability>(entity =>
        {
            entity.ToTable("role_capability", "identity");
            entity.HasKey(x => new { x.RoleId, x.CapabilityKey });
            entity.Property(x => x.CapabilityKey).HasMaxLength(200).IsRequired();
            entity.HasOne<RoleDefinition>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CapabilityKey);
        });

        modelBuilder.Entity<HumanRoleAssignment>(entity =>
        {
            entity.ToTable("human_role_assignment", "identity");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ResourceScope).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Environment).HasMaxLength(32).IsRequired();
            entity.HasOne<HumanPrincipal>()
                .WithMany()
                .HasForeignKey(x => x.HumanPrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RoleDefinition>()
                .WithMany()
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.HumanPrincipalId, x.Status });
            entity.HasIndex(x => x.RoleId);
        });

        modelBuilder.Entity<KnowledgeSource>(entity =>
        {
            entity.ToTable("source", "knowledge");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
            entity.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("document", "knowledge");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LogicalName).HasMaxLength(500).IsRequired();
            entity.HasOne<KnowledgeSource>()
                .WithMany()
                .HasForeignKey(x => x.KnowledgeSourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.KnowledgeSourceId, x.LogicalName }).IsUnique();
        });

        modelBuilder.Entity<DocumentVersion>(entity =>
        {
            entity.ToTable("document_version", "knowledge");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ContentHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.StorageProvider).HasMaxLength(64).IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.MimeType).HasMaxLength(200).IsRequired();
            entity.HasOne<KnowledgeDocument>()
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => x.ContentHash);
        });

        modelBuilder.Entity<KnowledgeRelease>(entity =>
        {
            entity.ToTable("release", "knowledge");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentContentHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResourceScope).HasMaxLength(500).IsRequired();
            entity.Property(x => x.UseCaseKey).HasMaxLength(200).IsRequired();
            entity.HasOne<DocumentVersion>()
                .WithMany()
                .HasForeignKey(x => x.DocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Client>()
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ClientId, x.ResourceScope, x.UseCaseKey, x.Status });
        });

        modelBuilder.Entity<AiUseCase>(entity =>
        {
            entity.ToTable("use_case", "ai");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(250).IsRequired();
            entity.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<AiUseCaseVersion>(entity =>
        {
            entity.ToTable("use_case_version", "ai");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PromptTemplateHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ModelRouteKey).HasMaxLength(200).IsRequired();
            entity.HasOne<AiUseCase>()
                .WithMany()
                .HasForeignKey(x => x.AiUseCaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AiUseCaseId, x.VersionNumber }).IsUnique();
            entity.HasIndex(x => x.AiUseCaseId)
                .IsUnique()
                .HasFilter("\"Status\" = 1");
        });

        modelBuilder.Entity<ActionDefinition>(entity =>
        {
            entity.ToTable("action_definition", "automation");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(200);
            entity.Property(x => x.ParameterPolicyVersion).HasMaxLength(100);
            entity.HasIndex(x => x.Mode);
        });

        modelBuilder.Entity<ActionExecutionRecord>(entity =>
        {
            entity.ToTable("action_execution", "automation");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActionKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ResourceScope).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResultReference).HasMaxLength(500);
            entity.HasOne<ActionDefinition>()
                .WithMany()
                .HasForeignKey(x => x.ActionKey)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ActionKey, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<RetentionRule>(entity =>
        {
            entity.ToTable("retention_rule", "policy");
            entity.HasKey(x => x.Category);
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("event", "audit");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CapabilityKey).HasMaxLength(200);
            entity.Property(x => x.ResourceType).HasMaxLength(100);
            entity.Property(x => x.ResourceId).HasMaxLength(500);
            entity.Property(x => x.ContentHash).HasMaxLength(128);
            entity.Property(x => x.PolicyVersion).HasMaxLength(100);
            entity.Property(x => x.Result).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SourceAdapter).HasMaxLength(100);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.ClientId, x.OccurredAtUtc });
        });
    }
}

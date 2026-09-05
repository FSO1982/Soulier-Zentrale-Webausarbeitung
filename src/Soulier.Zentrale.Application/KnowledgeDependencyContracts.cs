namespace Soulier.Zentrale.Application;

public enum KnowledgeDependencyState
{
    Healthy,
    Stale,
    Degraded
}

public sealed record KnowledgeDependencyStatus(
    KnowledgeDependencyState State,
    DateTimeOffset CheckedAtUtc,
    string? Detail = null);

public interface IKnowledgeDependencyStatusProvider
{
    KnowledgeDependencyStatus GetStatus();
}

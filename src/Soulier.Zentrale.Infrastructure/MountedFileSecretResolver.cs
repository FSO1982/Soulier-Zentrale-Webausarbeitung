using System.Text;
using Soulier.Zentrale.Application;

namespace Soulier.Zentrale.Infrastructure;

/// <summary>
/// Resolves allowlisted secrets from individual files below a fixed mount directory.
/// Suitable for Docker/Podman secret mounts or equivalent host-managed secret files.
/// The resolver does not enumerate the directory and never accepts caller-provided paths.
/// </summary>
public sealed class MountedFileSecretResolver : ISecretResolver
{
    private const long MaxSecretBytes = 64 * 1024;
    private readonly string _rootDirectory;
    private readonly IReadOnlySet<string> _allowedKeys;

    public MountedFileSecretResolver(
        string rootDirectory,
        IEnumerable<string> allowedKeys)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Secret mount root is required.", nameof(rootDirectory));
        ArgumentNullException.ThrowIfNull(allowedKeys);

        _rootDirectory = Path.GetFullPath(rootDirectory);
        _allowedKeys = allowedKeys
            .Select(key => SecretReference.Create(key).Key)
            .ToHashSet(StringComparer.Ordinal);

        if (_allowedKeys.Count == 0)
            throw new ArgumentException("At least one secret reference must be allowlisted.", nameof(allowedKeys));
    }

    public async ValueTask<SecretValue> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_allowedKeys.Contains(reference.Key))
            throw new KeyNotFoundException($"Secret reference '{reference.Key}' is not allowlisted for this resolver.");

        var candidate = Path.GetFullPath(Path.Combine(_rootDirectory, reference.Key));
        var rootWithSeparator = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            throw new InvalidOperationException("Resolved secret path escaped the configured secret mount root.");

        var file = new FileInfo(candidate);
        if (!file.Exists)
            throw new InvalidOperationException($"Secret reference '{reference.Key}' is configured but unavailable.");
        if (file.LinkTarget is not null)
            throw new InvalidOperationException("Symbolic-link secret files are not accepted.");
        if (file.Length is <= 0 or > MaxSecretBytes)
            throw new InvalidOperationException("Secret file size is outside the accepted range.");

        var value = await File.ReadAllTextAsync(candidate, Encoding.UTF8, cancellationToken);
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Secret reference '{reference.Key}' is configured but unavailable.");

        return new SecretValue(value);
    }
}

using Soulier.Zentrale.Application;

namespace Soulier.Zentrale.Infrastructure;

public sealed class EnvironmentSecretResolver : ISecretResolver
{
    private readonly IReadOnlyDictionary<string, string> _environmentVariableBySecretKey;
    private readonly Func<string, string?> _environmentLookup;

    public EnvironmentSecretResolver(
        IReadOnlyDictionary<string, string> environmentVariableBySecretKey,
        Func<string, string?>? environmentLookup = null)
    {
        ArgumentNullException.ThrowIfNull(environmentVariableBySecretKey);

        foreach (var mapping in environmentVariableBySecretKey)
        {
            _ = SecretReference.Create(mapping.Key);
            if (string.IsNullOrWhiteSpace(mapping.Value))
                throw new ArgumentException("Environment variable mapping must not be empty.", nameof(environmentVariableBySecretKey));
        }

        _environmentVariableBySecretKey = new Dictionary<string, string>(
            environmentVariableBySecretKey,
            StringComparer.Ordinal);
        _environmentLookup = environmentLookup ?? Environment.GetEnvironmentVariable;
    }

    public ValueTask<SecretValue> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_environmentVariableBySecretKey.TryGetValue(reference.Key, out var environmentVariable))
            throw new KeyNotFoundException($"Secret reference '{reference.Key}' is not allowlisted for this resolver.");

        var value = _environmentLookup(environmentVariable);
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Secret reference '{reference.Key}' is configured but unavailable.");

        return ValueTask.FromResult(new SecretValue(value));
    }
}

namespace Soulier.Zentrale.Application;

public sealed record SecretReference
{
    public string Key { get; }

    private SecretReference(string key) => Key = key;

    public static SecretReference Create(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Secret reference key is required.", nameof(key));
        if (key.Length > 128 || !IsValidFirst(key[0]) || key.Skip(1).Any(ch => !IsValidNext(ch)))
            throw new ArgumentException("Secret reference key contains invalid characters.", nameof(key));

        return new SecretReference(key);
    }

    private static bool IsValidFirst(char value) =>
        value is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool IsValidNext(char value) =>
        IsValidFirst(value) || value is '.' or '_' or '-';
}

public sealed class SecretValue : IDisposable
{
    private char[]? _buffer;

    public SecretValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Secret value must not be empty.", nameof(value));

        _buffer = value.ToCharArray();
    }

    public ReadOnlyMemory<char> Memory =>
        _buffer is not null
            ? _buffer
            : throw new ObjectDisposedException(nameof(SecretValue));

    public override string ToString() => "[REDACTED]";

    public void Dispose()
    {
        if (_buffer is null)
            return;

        Array.Clear(_buffer);
        _buffer = null;
    }
}

public interface ISecretResolver
{
    ValueTask<SecretValue> ResolveAsync(
        SecretReference reference,
        CancellationToken cancellationToken = default);
}

namespace Soulier.Zentrale.Application;

public sealed record ContentStoreWriteResult(
    string StorageProvider,
    string StorageKey,
    string ContentHash,
    long SizeBytes);

public interface IContentStore
{
    Task<ContentStoreWriteResult> StoreAsync(
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(
        string storageKey,
        string expectedContentHash,
        CancellationToken cancellationToken = default);
}

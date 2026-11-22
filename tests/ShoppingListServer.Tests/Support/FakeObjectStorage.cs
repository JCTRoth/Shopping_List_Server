using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Tests.Support;

/// <summary>
/// In-memory stand-in for MinIO. Records every stored object, its content type and the URLs the API
/// hands out, so tests can assert what would have been written to the bucket without running one.
/// </summary>
public sealed class FakeObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, StoredObject> _objects = new(StringComparer.Ordinal);

    public string PublicBaseUrl { get; set; } = "https://objects.test";

    public bool EnsureBucketCalled { get; private set; }

    public IReadOnlyDictionary<string, StoredObject> Objects => _objects;

    public bool Contains(string key) => _objects.ContainsKey(key);

    public StoredObject? Get(string key) => _objects.TryGetValue(key, out var stored) ? stored : null;

    public Task EnsureBucketAsync(CancellationToken cancellationToken = default)
    {
        EnsureBucketCalled = true;
        return Task.CompletedTask;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[key] = new StoredObject(buffer.ToArray(), contentType);
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(key, out var stored))
            return Task.FromResult<Stream>(null!);

        return Task.FromResult<Stream>(new MemoryStream(stored.Bytes, writable: false));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_objects.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public string CreatePresignedGetUrl(string key, TimeSpan lifetime)
        => $"{PublicBaseUrl}/{key}?X-Amz-Expires={(int)lifetime.TotalSeconds}";

    public sealed record StoredObject(byte[] Bytes, string ContentType);
}

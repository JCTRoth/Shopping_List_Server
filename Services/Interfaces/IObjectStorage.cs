using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShoppingListServer.Services.Interfaces
{
    /// <summary>
    /// S3-compatible object storage (MinIO or a cloud S3 endpoint).
    ///
    /// This is the platform port for binary data: the API keeps no user data on its own filesystem,
    /// so the pod is stateless and can be rescheduled or scaled without a PersistentVolumeClaim.
    /// </summary>
    public interface IObjectStorage
    {
        /// <summary>Creates the configured bucket if it does not exist yet. Safe to call repeatedly.</summary>
        Task EnsureBucketAsync(CancellationToken cancellationToken = default);

        /// <summary>Stores (or replaces) the object at <paramref name="key"/>.</summary>
        Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>Returns the object's content, or <c>null</c> when the key does not exist.</summary>
        Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Returns whether the key exists. Throws only for failures other than "not found".</summary>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Deletes the object if it exists. Deleting a missing key is not an error.</summary>
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Creates a time-limited GET URL for the object, signed for the public endpoint.</summary>
        string CreatePresignedGetUrl(string key, TimeSpan lifetime);
    }
}

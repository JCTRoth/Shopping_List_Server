namespace ShoppingListServer.Helpers
{
    /// <summary>
    /// S3-compatible object storage configuration (self-hosted MinIO in k3s, or any S3 endpoint).
    ///
    /// Two endpoints are needed because the API talks to the store over the cluster network while
    /// clients follow pre-signed URLs over the public host. The signature covers the Host header, so
    /// the URLs must be signed for the public endpoint even though the bytes are written through the
    /// internal one.
    /// </summary>
    public class ObjectStorageSettings
    {
        /// <summary>Endpoint used for bucket/object I/O from inside the cluster, e.g. <c>http://minio:9000</c>.</summary>
        public string InternalServiceUrl { get; set; }

        /// <summary>Endpoint used only to sign pre-signed URLs, e.g. <c>https://media.shopping-now.net</c>.</summary>
        public string PublicServiceUrl { get; set; }

        public string BucketName { get; set; } = "shoppinglist";

        public string AccessKey { get; set; }

        public string SecretKey { get; set; }

        /// <summary>Signing region. MinIO accepts any value; it must stay stable or URL signatures change.</summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>MinIO serves buckets path-style; virtual-host style would need a wildcard DNS record.</summary>
        public bool ForcePathStyle { get; set; } = true;

        /// <summary>Lifetime of a pre-signed GET URL in seconds.</summary>
        public int PresignExpirySeconds { get; set; } = 120;
    }
}

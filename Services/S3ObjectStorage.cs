using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ShoppingListServer.Helpers;
using ShoppingListServer.Services.Interfaces;

namespace ShoppingListServer.Services
{
    /// <summary>
    /// S3-compatible object storage backed by MinIO (or any S3 endpoint).
    ///
    /// Two clients share the same credentials: one talks to <see cref="ObjectStorageSettings.InternalServiceUrl"/>
    /// for bucket and object I/O, the other is used only to sign URLs against
    /// <see cref="ObjectStorageSettings.PublicServiceUrl"/>. That is required because the signature
    /// covers the Host header — a URL signed for the cluster-internal endpoint would not validate
    /// when a browser follows it through the public ingress.
    /// </summary>
    public class S3ObjectStorage : IObjectStorage
    {
        private const int BucketCreationAttempts = 6;
        private static readonly TimeSpan BucketRetryDelay = TimeSpan.FromSeconds(3);

        private readonly ObjectStorageSettings _settings;
        private readonly IAmazonS3 _client;
        private readonly IAmazonS3 _signingClient;

        public S3ObjectStorage(IOptions<AppSettings> appSettings)
        {
            _settings = appSettings.Value.ObjectStorage;

            if (_settings == null || string.IsNullOrWhiteSpace(_settings.InternalServiceUrl))
                throw new InvalidOperationException("AppSettings:ObjectStorage:InternalServiceUrl is not configured.");
            if (string.IsNullOrWhiteSpace(_settings.BucketName))
                throw new InvalidOperationException("AppSettings:ObjectStorage:BucketName is not configured.");

            _client = CreateClient(_settings.InternalServiceUrl);
            _signingClient = CreateClient(string.IsNullOrWhiteSpace(_settings.PublicServiceUrl)
                ? _settings.InternalServiceUrl
                : _settings.PublicServiceUrl);
        }

        public async Task EnsureBucketAsync(CancellationToken cancellationToken = default)
        {
            for (int attempt = 1; attempt <= BucketCreationAttempts; attempt++)
            {
                try
                {
                    if (await BucketExistsAsync(cancellationToken))
                        return;

                    await _client.PutBucketAsync(new PutBucketRequest { BucketName = _settings.BucketName }, cancellationToken);
                    Console.WriteLine($"Object storage: created bucket '{_settings.BucketName}'.");
                    return;
                }
                catch (Exception exception) when (attempt < BucketCreationAttempts)
                {
                    Console.WriteLine($"Object storage not ready (attempt {attempt}/{BucketCreationAttempts}): {exception.Message}");
                    await Task.Delay(BucketRetryDelay, cancellationToken);
                }
                catch (Exception exception)
                {
                    // The API can still serve lists without object storage; uploads fail loudly instead.
                    Console.Error.WriteLine($"Object storage: could not ensure bucket '{_settings.BucketName}': {exception.Message}");
                    return;
                }
            }
        }

        public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = content,
                // Send one signed body instead of an aws-chunked stream. MinIO accepts both, but
                // chunked framing is an AWS-specific transport detail that other S3-compatible
                // stores (and local emulators) mishandle.
                UseChunkEncoding = false
            };

            if (!string.IsNullOrWhiteSpace(contentType))
                request.ContentType = contentType;

            await _client.PutObjectAsync(request, cancellationToken);
        }

        public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                using GetObjectResponse response = await _client.GetObjectAsync(_settings.BucketName, key, cancellationToken);
                MemoryStream buffer = new MemoryStream();
                await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
                return buffer;
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.GetObjectMetadataAsync(_settings.BucketName, key, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            // S3 semantics: deleting a key that does not exist succeeds.
            await _client.DeleteObjectAsync(_settings.BucketName, key, cancellationToken);
        }

        public string CreatePresignedGetUrl(string key, TimeSpan lifetime)
        {
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(lifetime)
            };

            string url = _signingClient.GetPreSignedURL(request);

            // The SDK always signs a pre-signed URL with https, whatever scheme the endpoint was
            // configured with. Local development runs object storage over plain http, so re-apply the
            // configured scheme. Only the scheme changes, so the signature stays valid.
            Uri publicEndpoint = new Uri(_settings.PublicServiceUrl);
            if (publicEndpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url.Substring("https://".Length);
            }

            return url;
        }

        private async Task<bool> BucketExistsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = _settings.BucketName }, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        private IAmazonS3 CreateClient(string serviceUrl)
        {
            UriBuilder uri = new UriBuilder(serviceUrl);
            AmazonS3Config config = new AmazonS3Config
            {
                ServiceURL = uri.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/'),
                ForcePathStyle = _settings.ForcePathStyle,
                AuthenticationRegion = _settings.Region,
                UseHttp = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            };

            AWSCredentials credentials = new BasicAWSCredentials(
                _settings.AccessKey ?? string.Empty,
                _settings.SecretKey ?? string.Empty);

            return new AmazonS3Client(credentials, config);
        }
    }
}

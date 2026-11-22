using System;
using Microsoft.Extensions.Options;
using ShoppingListServer.Helpers;
using ShoppingListServer.Services;

namespace ShoppingListServer.Tests;

/// <summary>
/// Unit tests for the S3 URL signing rules. No object storage is contacted.
/// </summary>
public sealed class ObjectStorageSigningTests
{
    [Fact]
    public void PresignedUrl_UsesConfiguredHttpSchemeInLocalDevelopment()
    {
        var storage = CreateStorage("http://localhost:9000", "http://localhost:9000");

        var url = storage.CreatePresignedGetUrl("profile-pictures/user.jpg", TimeSpan.FromMinutes(2));

        // The SDK always signs with https; local object storage runs plain http, so the configured
        // scheme has to win or the browser would be sent to a port that does not speak TLS.
        Assert.StartsWith("http://localhost:9000/", url, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PresignedUrl_UsesPublicHostAndKeepsSignature()
    {
        var storage = CreateStorage("http://minio:9000", "https://media.shopping-now.net");

        var url = storage.CreatePresignedGetUrl("profile-pictures/user.jpg", TimeSpan.FromMinutes(2));

        // Signed for the public endpoint: the signature covers the Host, so it must not be signed
        // for the cluster-internal service name a browser cannot resolve.
        Assert.StartsWith("https://media.shopping-now.net/", url, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Signature=", url, StringComparison.Ordinal);
        Assert.Contains("X-Amz-Expires=120", url, StringComparison.Ordinal);
        Assert.DoesNotContain("minio:9000", url, StringComparison.Ordinal);
    }

    private static S3ObjectStorage CreateStorage(string internalUrl, string publicUrl)
    {
        var settings = new AppSettings
        {
            ObjectStorage = new ObjectStorageSettings
            {
                InternalServiceUrl = internalUrl,
                PublicServiceUrl = publicUrl,
                BucketName = "shoppinglist",
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                Region = "us-east-1",
                ForcePathStyle = true,
                PresignExpirySeconds = 120
            }
        };

        return new S3ObjectStorage(Options.Create(settings));
    }
}

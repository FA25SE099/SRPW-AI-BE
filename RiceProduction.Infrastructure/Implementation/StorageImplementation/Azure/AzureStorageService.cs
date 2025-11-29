using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Http;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiceProduction.Application.Common.Interfaces.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RiceProduction.Infrastructure.Implementation.StorageImplementation.Azure
{
    public class AzureStorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;
        private readonly ILogger<AzureStorageService> _logger;
        public AzureStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration, ILogger<AzureStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _containerName = configuration["AzureStorage:ContainerName"] ?? "uploads";
            _logger = logger;
        }

        public async Task<(string Url, string FileName)> UploadAsync(IFormFile file, string folder = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            using var stream = file.OpenReadStream();

            string sha256Hash = await ComputeSha256HashAsync(stream);
            string extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? ".bin";

            string blobName = string.IsNullOrEmpty(folder)
                ? $"{sha256Hash}{extension}"
                : $"{folder.TrimEnd('/')}/{sha256Hash}{extension}";

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(blobName);

            stream.Position = 0;

            var headers = new BlobHttpHeaders { ContentType = file.ContentType };

            try
            {
                await blobClient.UploadAsync(
                    content: stream,
                    httpHeaders: headers,
                    conditions: new BlobRequestConditions { IfNoneMatch = new ETag("*") } 
                );
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                _logger.LogInformation("Upload Failed");
            }

            return (blobClient.Uri.ToString(), $"{sha256Hash}{extension}");
        }
        public async Task<List<(string Url, string FileName)>> UploadMultipleAsync(IEnumerable<IFormFile> files, string folder = null)
        {
            var results = new List<(string Url, string FileName)>();

            foreach (var file in files)
            {
                var result = await UploadAsync(file, folder);
                results.Add(result);
            }

            return results;
        }

        public async Task<bool> DeleteAsync(string blobName, string folder = null)
        {
            var fullBlobName = string.IsNullOrEmpty(folder) ? blobName : $"{folder.TrimEnd('/')}/{blobName}";
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fullBlobName);

            return await blobClient.DeleteIfExistsAsync();
        }

        public async Task<string> GetSasUrlAsync(string blobName, string folder = null, TimeSpan? expiry = null)
        {
            var fullBlobName = string.IsNullOrEmpty(folder) ? blobName : $"{folder.TrimEnd('/')}/{blobName}";
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fullBlobName);

            if (!await blobClient.ExistsAsync())
                throw new FileNotFoundException("Blob not found");

            if (!blobClient.CanGenerateSasUri)
                throw new InvalidOperationException("Cannot generate SAS URI with current credentials");

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = fullBlobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(30))
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }
        private static async Task<string> ComputeSha256HashAsync(Stream stream)
        {
            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}

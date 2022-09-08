using System;
using System.Collections.Concurrent;
using System.Net.Http;
using PortingAssistant.Client.NuGet.Interfaces;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortingAssistant.Client.Model;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace PortingAssistant.Client.NuGet.Utils
{
    public class HttpService : IHttpService
    {
        private readonly HttpClient _S3httpClient;
        private readonly HttpClient _GitHubHttpClient;
        private readonly ConcurrentDictionary<(string, string), string> _fileETagMap = new();
        private readonly string _tempDirectory;
        private bool _cachingEnabled = true;
        private ILogger _logger;

        public HttpService(IHttpClientFactory httpClientFactory, IOptions<PortingAssistantConfiguration> options, ILogger logger,
            string pathToSolution = null)
        {
            _S3httpClient = httpClientFactory.CreateClient("s3");
            _S3httpClient.BaseAddress = new Uri(options.Value.DataStoreSettings.HttpsEndpoint);
            _GitHubHttpClient = httpClientFactory.CreateClient("github");
            _GitHubHttpClient.BaseAddress = new Uri(options.Value.DataStoreSettings.GitHubEndpoint);
            _logger = logger;
            try
            {
                _tempDirectory = CreateTempDirectory(pathToSolution);
            }
            catch (Exception e)
            {
                _cachingEnabled = false;
                logger.LogError(e, "Could not create temp directory for {0}. Caching disabled", pathToSolution);
            }

        }

        public async Task<bool> DoesS3FileExistAsync(string filePath)
        {
            return await CheckIfFileExists(_S3httpClient, filePath);
        }

        public async Task<Stream> DownloadS3FileAsync(string fileToDownload)
        {
            string cachedFilePath;
            if (_fileETagMap.TryGetValue((_S3httpClient.BaseAddress.AbsoluteUri, fileToDownload), out cachedFilePath))
            {
                var headResponse = await GetHeadResponse(_S3httpClient, fileToDownload);

            }
            

            return await _S3httpClient.GetStreamAsync(fileToDownload);
        }

        public async Task<bool> DoesGitHubFileExistAsync(string filePath)
        {
            return await CheckIfFileExists(_GitHubHttpClient, filePath);
        }

        public async Task<Stream> DownloadGitHubFileAsync(string fileToDownload)
        {
            return await _GitHubHttpClient.GetStreamAsync(fileToDownload);
        }

        private async Task<bool> CheckIfFileExists(HttpClient httpClient, string filePath)
        {
            var response = await GetHeadResponse(httpClient, filePath);
            return response.IsSuccessStatusCode;
        }

        private void UpdateFileETagMap(HttpClient httpClient, string filePath, HttpResponseMessage response)
        {
            var eTag = response.Headers.ETag;
            if (eTag is not null)
            {
                if (httpClient.BaseAddress != null)
                    _fileETagMap[(httpClient.BaseAddress.AbsoluteUri, filePath)] = eTag!.Tag;
            }
        }

        private async Task<HttpResponseMessage> GetHeadResponse(HttpClient httpClient, string filePath)
        {
            using HttpRequestMessage checkIfPresentRequestMessage =
                new HttpRequestMessage(HttpMethod.Head, filePath);
            var response = await httpClient.SendAsync(checkIfPresentRequestMessage);
            return response;
        }

        private string CreateTempDirectory(string pathToSolution)
        {
            string solutionId;
            if (pathToSolution != null)
            {
                using var sha = SHA256.Create();
                byte[] textData = System.Text.Encoding.UTF8.GetBytes(pathToSolution);
                byte[] hash = sha.ComputeHash(textData);
                solutionId = BitConverter.ToString(hash);
            }
            else
            {
                solutionId = Path.GetRandomFileName();
            }

            var tempSolutionDirectory = Path.Combine(Path.GetTempPath(), solutionId);
            tempSolutionDirectory = tempSolutionDirectory.Replace("-", "");
            Directory.CreateDirectory(tempSolutionDirectory);
            return tempSolutionDirectory;

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using PortingAssistant.Client.Common.Utils;
using PortingAssistant.Client.Model;
using PortingAssistant.Client.NuGet.Interfaces;
using PortingAssistant.Client.NuGet.Utils;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Security.Cryptography;
using PortingAssistant.Client.Common.Model;

namespace PortingAssistant.Client.NuGet
{
    public class ExternalCompatibilityChecker : ICompatibilityChecker
    {
        private readonly ILogger _logger;
        private readonly ICachedHttpService _httpService;
        private readonly IFileSystem _fileSystem;
        private static readonly int _maxProcessConcurrency = 10;
        private static readonly SemaphoreSlim _semaphore = new(_maxProcessConcurrency);
        private static readonly HashSet<string> _filesNotFound = new HashSet<string>();

        public virtual PackageSourceType CompatibilityCheckerType => PackageSourceType.NUGET;

        public ExternalCompatibilityChecker(
            S3CachedHttpService httpService,
            ILogger<ExternalCompatibilityChecker> logger,
            IFileSystem fileSystem = null)
        {
            _logger = logger;
            _httpService = httpService;
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public Dictionary<PackageVersionPair, Task<PackageDetails>> Check(
            IEnumerable<PackageVersionPair> packageVersions,
            string pathToSolution, bool isIncremental = false, bool refresh = false)
        {
            List<PackageVersionPair> packagesToCheck = default;

            packagesToCheck = CompatibilityCheckerType == PackageSourceType.SDK ? packageVersions.Where(package => package.PackageSourceType == PackageSourceType.SDK).ToList() : packageVersions.ToList();

            var compatibilityTaskCompletionSources = packagesToCheck
                .Select(package => new Tuple<PackageVersionPair, TaskCompletionSource<PackageDetails>>(package, new TaskCompletionSource<PackageDetails>()))
                .ToDictionary(t => t.Item1, t => t.Item2);

            _logger.LogInformation("Checking {0} for compatibility of {1} package(s)", CompatibilityCheckerType, packagesToCheck.Count());
            if (packagesToCheck.Count > 0)
            {
                Task.Run(() =>
                {
                    _semaphore.Wait();
                    try
                    {
                        ProcessCompatibility(packagesToCheck, compatibilityTaskCompletionSources, pathToSolution, isIncremental, refresh);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });
            }

            return compatibilityTaskCompletionSources.ToDictionary(t => t.Key, t => t.Value.Task);
        }

        private async void ProcessCompatibility(List<PackageVersionPair> packageVersions,
            Dictionary<PackageVersionPair, TaskCompletionSource<PackageDetails>> compatibilityTaskCompletionSources,
            string pathToSolution, bool isIncremental, bool incrementalRefresh)
        {
            var packageVersionsFound = new HashSet<PackageVersionPair>();
            var packageVersionsWithErrors = new HashSet<PackageVersionPair>();

            var packageVersionsGroupedByPackageId = packageVersions
                .GroupBy(pv => pv.PackageId)
                .ToDictionary(pvGroup => pvGroup.Key, pvGroup => pvGroup.ToList());

            foreach (var groupedPackageVersions in packageVersionsGroupedByPackageId)
            {
                var packageToDownload = groupedPackageVersions.Key.ToLower();
                var fileToDownload = GetDownloadFilePath(CompatibilityCheckerType, packageToDownload);

                try
                {
                    string tempDirectoryPath = GetTempDirectory(pathToSolution);
                    PackageDetails packageDetails = null;

                    if (isIncremental)
                    {
                        if (incrementalRefresh || !IsPackageInFile(fileToDownload, tempDirectoryPath))
                        {
                            _logger.LogInformation("Downloading {0} from {1}", fileToDownload, CompatibilityCheckerType);
                            if (!_filesNotFound.Contains(fileToDownload))
                            {
                                var packageResponse = await GetPackageDetailFromS3(fileToDownload, _httpService);
                                if (packageResponse.Success)
                                {
                                    packageDetails = packageResponse.PackageDetails;
                                    _logger.LogInformation("Caching {0} from {1} to Temp", fileToDownload,
                                        CompatibilityCheckerType);
                                    CachePackageDetailsToFile(fileToDownload, packageDetails, tempDirectoryPath);
                                }
                                else
                                {
                                    _filesNotFound.Add(fileToDownload);
                                    _logger.LogInformation("Failed to download {fileToDownload}");
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Skipping download {fileToDownload} based on historical failure");
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Fetching {0} from {1} from Temp", fileToDownload, CompatibilityCheckerType);
                            packageDetails = GetPackageDetailFromFile(fileToDownload, tempDirectoryPath);
                        }
                    }
                    else
                    {
                        if (!_filesNotFound.Contains(fileToDownload))
                        {
                            var packageResponse = await GetPackageDetailFromS3(fileToDownload, _httpService);
                            if (packageResponse.Success)
                            {
                                packageDetails = packageResponse.PackageDetails;
                            }
                            else
                            {
                                _filesNotFound.Add(fileToDownload);
                                _logger.LogInformation("Failed to download {fileToDownload}");
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Skipping download {fileToDownload} based on historical failure");
                        }
                    }

                    if (packageDetails != null)
                    {
                        if (packageDetails.Name == null || !string.Equals(packageDetails.Name.Trim().ToLower(),
                                packageToDownload.Trim().ToLower(), StringComparison.OrdinalIgnoreCase))
                        {
                            throw new PackageDownloadMismatchException(
                                actualPackage: packageDetails.Name,
                                expectedPackage: packageToDownload);
                        }
                    }

                    foreach (var packageVersion in groupedPackageVersions.Value)
                    {
                        if (compatibilityTaskCompletionSources.TryGetValue(packageVersion,
                                out var taskCompletionSource))
                        {
                            if (packageDetails != null)
                            {
                                taskCompletionSource.SetResult(packageDetails);
                                packageVersionsFound.Add(packageVersion);
                            }
                            else
                            {
                                taskCompletionSource.SetException(new PortingAssistantClientException(ExceptionMessage.PackageNotFound(packageVersion), new ApplicationException("Download failure")));
                                packageVersionsWithErrors.Add(packageVersion);
                            }
                        }
                    }

                }
                catch (OutOfMemoryException ex)
                {
                    _logger.LogError("Failed when downloading and parsing {0} from {1}, {2}", fileToDownload, CompatibilityCheckerType, ex);
                    MemoryUtils.LogSolutiontSize(_logger, pathToSolution);
                    MemoryUtils.LogMemoryConsumption(_logger);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("404"))
                    {
                        _logger.LogInformation($"Encountered {ex.GetType()} while downloading and parsing {fileToDownload} " +
                                               $"from {CompatibilityCheckerType}, but it was ignored. " +
                                               $"ErrorMessage: {ex.Message}.");
                        // filter all 404 errors
                        ex = null;
                    }
                    else
                    {
                        _logger.LogError("Failed when downloading and parsing {0} from {1}, {2}", fileToDownload, CompatibilityCheckerType, ex);
                    }

                    foreach (var packageVersion in groupedPackageVersions.Value)
                    {
                        if (compatibilityTaskCompletionSources.TryGetValue(packageVersion, out var taskCompletionSource))
                        {
                            taskCompletionSource.SetException(new PortingAssistantClientException(ExceptionMessage.PackageNotFound(packageVersion), ex));
                            packageVersionsWithErrors.Add(packageVersion);
                        }
                    }
                }
            }

            foreach (var packageVersion in packageVersions)
            {
                if (packageVersionsFound.Contains(packageVersion) || packageVersionsWithErrors.Contains(packageVersion))
                {
                    continue;
                }

                if (compatibilityTaskCompletionSources.TryGetValue(packageVersion, out var taskCompletionSource))
                {
                    var errorMessage = $"Could not find package {packageVersion} in external source; try checking an internal source.";
                    _logger.LogInformation(errorMessage);

                    var innerException = new PackageNotFoundException(errorMessage);
                    taskCompletionSource.TrySetException(new PortingAssistantClientException(ExceptionMessage.PackageNotFound(packageVersion), innerException));
                }
            }
        }

        private string GetDownloadFilePath(PackageSourceType CompatibilityCheckerType, string packageToDownload)
        {
            var fileToDownload = $"{packageToDownload}.json.gz";
            var downloadFilePath = fileToDownload;
            switch (CompatibilityCheckerType)
            {
                case PackageSourceType.NUGET:
                    break;
                case PackageSourceType.SDK:
                    downloadFilePath = Path.Combine("namespaces", fileToDownload);
                    break;
                default:
                    break;
            }

            return downloadFilePath;
        }

        public class PackageFromS3
        {
            public PackageDetails Package { get; set; }
            public PackageDetails Namespaces { get; set; }
        }

        public string GetTempDirectory(string pathToSolution)
        {
            if (pathToSolution != null)
            {
                string solutionId;
                using (var sha = SHA256.Create())
                {
                    byte[] textData = System.Text.Encoding.UTF8.GetBytes(pathToSolution);
                    byte[] hash = sha.ComputeHash(textData);
                    solutionId = BitConverter.ToString(hash);
                }
                var tempSolutionDirectory = Path.Combine(_fileSystem.GetTempPath(), solutionId);
                tempSolutionDirectory = tempSolutionDirectory.Replace("-", "");
                return tempSolutionDirectory;
            }
            return null;
        }
        public bool IsPackageInFile(string fileToDownload, string _tempSolutionDirectory)
        {
            string filePath = Path.Combine(_tempSolutionDirectory, fileToDownload);
            return _fileSystem.FileExists(filePath);
        }
        public async Task<S3PackageDetailsResponse> GetPackageDetailFromS3(string fileToDownload, ICachedHttpService httpService)
        {
            var fileExists = await httpService.DoesFileExistAsync(fileToDownload);
            if (fileExists)
            {
                await using var stream = await httpService.DownloadFileAsync(fileToDownload);
                await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
                using var streamReader = new StreamReader(gzipStream);
                var data = JsonConvert.DeserializeObject<PackageFromS3>(streamReader.ReadToEnd());
                var packageDetails = data?.Package ?? data?.Namespaces;
                return new S3PackageDetailsResponse(true, packageDetails);
            }

            return new S3PackageDetailsResponse(false);
        }
        public async void CachePackageDetailsToFile(string fileName, PackageDetails packageDetail, string _tempSolutionDirectory)
        {
            string filePath = Path.Combine(_tempSolutionDirectory, fileName);
            //Create directory will automatically create directory, if it doesn't exist
            _fileSystem.CreateDirectory(Path.GetDirectoryName(filePath));

            var data = JsonConvert.SerializeObject(packageDetail);
            await using Stream compressedFileStream = _fileSystem.FileOpenWrite(filePath);
            await using var gzipStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
            await using var streamWriter = new StreamWriter(gzipStream);
            await streamWriter.WriteAsync(data);
        }
        public PackageDetails GetPackageDetailFromFile(string fileToDownload, string _tempSolutionDirectory)
        {
            string filePath = Path.Combine(_tempSolutionDirectory, fileToDownload);
            using var compressedFileStream = _fileSystem.FileOpenRead(filePath);
            using var gzipStream = new GZipStream(compressedFileStream, CompressionMode.Decompress);
            using var streamReader = new StreamReader(gzipStream);
            var data = JsonConvert.DeserializeObject<PackageDetails>(streamReader.ReadToEnd());
            return data;
        }
    }
}

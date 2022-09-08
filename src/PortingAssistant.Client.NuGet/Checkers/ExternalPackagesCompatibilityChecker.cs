using Microsoft.Extensions.Logging;
using PortingAssistant.Client.Model;
using PortingAssistant.Client.NuGet.Interfaces;
using PortingAssistant.Client.NuGet.Utils;

namespace PortingAssistant.Client.NuGet
{
    public class ExternalPackagesCompatibilityChecker : ExternalCompatibilityChecker
    {
        public override PackageSourceType CompatibilityCheckerType => PackageSourceType.NUGET;

        public ExternalPackagesCompatibilityChecker(
            S3CachedHttpService httpService,
            ILogger<ExternalCompatibilityChecker> logger,
            IFileSystem fileSystem = null
            )
            : base(httpService, logger, fileSystem)
        {
        }
    }
}
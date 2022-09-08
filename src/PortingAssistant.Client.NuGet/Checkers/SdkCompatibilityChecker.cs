using Microsoft.Extensions.Logging;
using PortingAssistant.Client.Model;
using PortingAssistant.Client.NuGet.Interfaces;
using PortingAssistant.Client.NuGet.Utils;

namespace PortingAssistant.Client.NuGet
{
    public class SdkCompatibilityChecker : ExternalCompatibilityChecker
    {
        public override PackageSourceType CompatibilityCheckerType => PackageSourceType.SDK;

        public SdkCompatibilityChecker(
            S3CachedHttpService httpService,
            ILogger<ExternalCompatibilityChecker> logger)
            : base(httpService, logger)
        {
        }
    }
}
using System;
using System.Net.Http;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortingAssistant.Client.Model;

namespace PortingAssistant.Client.NuGet.Utils;

public class S3CachedHttpService : CachedHttpService
{
    public S3CachedHttpService(IHttpClientFactory httpClientFactory, ILogger<S3CachedHttpService> logger, IOptions<PortingAssistantConfiguration> options) : base(httpClientFactory, logger, new CachedHttpServiceOptions("S3")
    {
        BaseAddress = new Uri(options.Value.DataStoreSettings.HttpsEndpoint),
        SolutionId = "NopCommerce"//TODO: handle this hard coding

    })
    {

    }
}
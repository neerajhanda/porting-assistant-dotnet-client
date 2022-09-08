using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortingAssistant.Client.Model;

namespace PortingAssistant.Client.NuGet.Utils;

public class GitHubCachedHttpService : CachedHttpService
{
    public GitHubCachedHttpService(IHttpClientFactory httpClientFactory, ILogger<GitHubCachedHttpService> logger, IOptions<PortingAssistantConfiguration> options) : base(httpClientFactory, logger, new CachedHttpServiceOptions("github")
    {
        BaseAddress = new Uri(options.Value.DataStoreSettings.GitHubEndpoint),
        SolutionId = "NopCommerce"//TODO: handle this hard coding

    })
    {

    }
}
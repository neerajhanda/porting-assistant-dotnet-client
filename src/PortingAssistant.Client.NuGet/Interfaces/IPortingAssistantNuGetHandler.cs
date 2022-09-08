using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PortingAssistant.Client.Model;

namespace PortingAssistant.Client.NuGet
{
    public interface IPortingAssistantNuGetHandler:IDisposable
    {
        public Dictionary<PackageVersionPair, Task<PackageDetails>> GetNugetPackages(List<PackageVersionPair> nugetPackages, string pathToSolution, bool isIncremental, bool incrementalRefresh);
    }
}

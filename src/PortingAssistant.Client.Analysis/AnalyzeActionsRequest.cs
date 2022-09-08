using Codelyzer.Analysis;
using CTA.Rules.Models;

namespace PortingAssistant.Client.Analysis;

public class AnalyzeActionsRequest
{
    public AnalyzeActionsRequest(string pathToProject, string targetFramework, AnalyzerResult analyzerResult, string pathToSolution, PortCoreConfiguration portCoreConfiguration)
    {
        PathToProject = pathToProject;
        TargetFramework = targetFramework;
        AnalyzerResult = analyzerResult;
        PathToSolution = pathToSolution;
        PortCoreConfiguration = portCoreConfiguration;
    }

    public string PathToProject { get; private set; }
    public string TargetFramework { get; private set; }
    public AnalyzerResult AnalyzerResult { get; private set; }
    public string PathToSolution { get; private set; }
    public PortCoreConfiguration PortCoreConfiguration { get; private set; }
}
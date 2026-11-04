using RevitFamilyImporter.VisibilityInspector.Models;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public interface IVisibilityAnalyzer
    {
        string Name { get; }
        int Priority { get; }
        VisibilityIssue Analyze(VisibilityAnalysisContext context);
    }
}

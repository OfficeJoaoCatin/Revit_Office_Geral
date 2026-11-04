using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public interface IVisibilityAnalysisService
    {
        VisibilityReport Analyze(UIApplication uiApp, Document document, RevitView view, Element element);
        VisibilityComparison CompareWithView(UIApplication uiApp, Document document, RevitView baseView, RevitView comparedView, Element element);
        List<ViewVisibilitySummary> FindVisibleViews(UIApplication uiApp, Document document, Element element);
    }
}

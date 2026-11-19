using Autodesk.Revit.DB;

namespace RevitFamilyImporter.LinkConflict.Services
{
    internal static class HostCandidateTypeFilter
    {
        // CurveElement é linha/curva do Revit; Pipe e Conduit são MEPCurve, não CurveElement.
        internal static bool IsExcluded(Element element)
        {
            if (element is RevitLinkInstance || element is CurveElement) return true;
            // Câmeras de vista internas podem aparecer como Element genérico no modelo.
            // Uma FamilyInstance permanece elegível, mesmo se sua categoria for Cameras.
            return element != null && element.GetType() == typeof(Element) &&
                   element.Category?.Id?.IntegerValue == (int)BuiltInCategory.OST_Cameras;
        }
    }
}

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace RevitFamilyImporter.LinkConflict.Services
{
    internal static class NonPhysicalDefinitionFilter
    {
        // PipeSegment define material/tamanhos; MEPSystem representa o sistema lógico.
        // Tubos, dutos e eletrodutos reais são MEPCurve, não PipeSegment ou MEPSystem.
        internal static bool IsExcluded(Element element) => element is PipeSegment || element is MEPSystem;
    }
}

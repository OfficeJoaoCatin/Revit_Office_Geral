using Autodesk.Revit.DB;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class VisibilityTarget
    {
        public VisibilityTarget(Element element, RevitLinkInstance linkInstance = null)
        {
            Element = element;
            LinkInstance = linkInstance;
        }

        public Element Element { get; }
        public RevitLinkInstance LinkInstance { get; }
        public bool IsLinkedElement => LinkInstance != null;
        public Document ElementDocument => Element?.Document;
    }
}

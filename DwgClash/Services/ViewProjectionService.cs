using Autodesk.Revit.DB;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class ViewProjectionService
    {
        private readonly XYZ _origin;
        private readonly XYZ _right;
        private readonly XYZ _up;

        public ViewProjectionService(Autodesk.Revit.DB.View view)
        {
            _origin = view.Origin ?? XYZ.Zero;
            _right = view.RightDirection.Normalize();
            _up = view.UpDirection.Normalize();
        }

        public Point2D Project(XYZ point)
        {
            XYZ delta = point - _origin;
            return new Point2D(delta.DotProduct(_right), delta.DotProduct(_up));
        }
    }
}

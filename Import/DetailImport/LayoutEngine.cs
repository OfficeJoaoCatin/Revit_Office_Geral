using Autodesk.Revit.DB;
using RevitFamilyImporter.Models.DetailImport;
using System;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class LayoutEngine
    {
        private readonly ImportSettings _settings;
        private readonly double _minX;
        private readonly double _maxX;
        private readonly double _minY;
        private readonly double _maxY;
        private int _column;
        private double _cursorX;
        private double _cursorY;
        private double _rowHeight;

        public LayoutEngine(ViewSheet sheet, ImportSettings settings)
        {
            if (sheet == null)
                throw new ArgumentNullException(nameof(sheet));

            _settings = settings ?? new ImportSettings();
            _settings.Normalize();

            BoundingBoxUV outline = sheet.Outline;
            _minX = outline.Min.U + _settings.Margin;
            _maxX = outline.Max.U - _settings.Margin;
            _minY = outline.Min.V + _settings.Margin;
            _maxY = outline.Max.V - _settings.Margin;
            _cursorX = _minX;
            _cursorY = _maxY;
        }

        public XYZ Next(double width, double height)
        {
            width = Math.Max(width, 0.001);
            height = Math.Max(height, 0.001);

            bool shouldWrap = _settings.LayoutMode == ImportLayoutMode.Vertical
                ? _column > 0
                : _column >= _settings.Columns || (_cursorX + width > _maxX && _column > 0);

            if (shouldWrap)
                NewRow();

            XYZ point = new XYZ(_cursorX, _cursorY, 0);

            _cursorX += width + _settings.Spacing;
            _rowHeight = Math.Max(_rowHeight, height);
            _column++;

            if (_cursorY - height < _minY)
            {
                // Keep placing; Revit allows content outside the titleblock, but this makes overflow explicit in the report.
            }

            return point;
        }

        private void NewRow()
        {
            _cursorX = _minX;
            _cursorY -= _rowHeight + _settings.Spacing;
            _rowHeight = 0;
            _column = 0;
        }
    }
}



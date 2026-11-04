using Autodesk.Revit.DB;
using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using System;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class SheetLegendInserter
    {
        private readonly Document _doc;
        private readonly ILogger _logger;

        public SheetLegendInserter(Document doc, ILogger logger)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _logger = logger ?? new Logger("LegendLibrary");
        }

        public GeneratedLegend InsertOnActiveSheet(Autodesk.Revit.DB.View activeView, string dwgPath, LegendComposition composition, LegendSettings settings)
        {
            if (!(activeView is ViewSheet sheet))
                throw new InvalidOperationException("Abra uma folha antes de gerar a legenda CAD.");

            settings = settings ?? new LegendSettings();
            settings.Normalize();

            var options = new DWGImportOptions
            {
                ThisViewOnly = true,
                Placement = ImportPlacement.Origin,
                CustomScale = settings.ImportScale,
                ColorMode = ImportColorMode.Preserved,
                VisibleLayersOnly = false,
                OrientToView = true,
                Unit = ImportUnit.Default
            };

            ElementId importId;
            if (!_doc.Import(dwgPath, options, sheet, out importId) ||
                importId == ElementId.InvalidElementId)
            {
                throw new InvalidOperationException("A Revit API não conseguiu importar o DWG da legenda.");
            }

            TryMoveToLowerRight(sheet, importId);

            return new GeneratedLegend
            {
                Name = "Legenda CAD",
                Width = composition?.Width ?? 0,
                Height = composition?.Height ?? 0,
                ImportId = importId,
                SourceFile = dwgPath
            };
        }

        private void TryMoveToLowerRight(ViewSheet sheet, ElementId importId)
        {
            try
            {
                _doc.Regenerate();
                Element element = _doc.GetElement(importId);
                if (element == null || !element.IsValidObject)
                    return;

                if (element.Pinned)
                    element.Pinned = false;

                BoundingBoxXYZ box = element.get_BoundingBox(sheet);
                if (box == null)
                    return;

                BoundingBoxUV outline = sheet.Outline;
                double margin = 0.20;
                double targetRight = outline.Max.U - margin;
                double targetBottom = outline.Min.V + margin;

                XYZ move = new XYZ(targetRight - box.Max.X, targetBottom - box.Min.Y, 0);
                if (!move.IsZeroLength())
                    ElementTransformUtils.MoveElement(_doc, importId, move);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Legenda importada, mas nao foi possivel posicionar no canto inferior direito: {ex.Message}");
            }
        }
    }
}




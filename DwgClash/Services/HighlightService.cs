using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.DwgClash.Models;

namespace RevitFamilyImporter.DwgClash.Services
{
    public sealed class HighlightService
    {
        private readonly Dictionary<ElementId, OverrideGraphicSettings> _originalOverrides =
            new Dictionary<ElementId, OverrideGraphicSettings>();

        public void Apply(UIApplication application, Autodesk.Revit.DB.View view, ImportInstance importInstance, DwgClashReport report)
        {
            Document document = view.Document;
            using (Transaction transaction = new Transaction(document, "Destacar conflitos DWG"))
            {
                transaction.Start();
                foreach (ElementId elementId in report.Results.Select(result => result.Element.ElementId).Distinct())
                {
                    SaveOriginal(view, elementId);
                    OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
                    overrideSettings.SetProjectionLineColor(new Autodesk.Revit.DB.Color(220, 0, 0));
                    overrideSettings.SetSurfaceForegroundPatternColor(new Autodesk.Revit.DB.Color(255, 120, 0));
                    overrideSettings.SetSurfaceTransparency(35);
                    view.SetElementOverrides(elementId, overrideSettings);
                }

                if (importInstance != null)
                {
                    SaveOriginal(view, importInstance.Id);
                    OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
                    overrideSettings.SetProjectionLineColor(new Autodesk.Revit.DB.Color(255, 190, 0));
                    view.SetElementOverrides(importInstance.Id, overrideSettings);
                }
                transaction.Commit();
            }
        }

        public void Clear(Autodesk.Revit.DB.View view)
        {
            if (_originalOverrides.Count == 0)
                return;

            using (Transaction transaction = new Transaction(view.Document, "Limpar destaques DWG"))
            {
                transaction.Start();
                foreach (KeyValuePair<ElementId, OverrideGraphicSettings> item in _originalOverrides)
                    view.SetElementOverrides(item.Key, item.Value);
                transaction.Commit();
            }
            _originalOverrides.Clear();
        }

        private void SaveOriginal(Autodesk.Revit.DB.View view, ElementId elementId)
        {
            if (!_originalOverrides.ContainsKey(elementId))
                _originalOverrides[elementId] = view.GetElementOverrides(elementId);
        }
    }
}

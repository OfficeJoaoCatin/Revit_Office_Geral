using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;
using RevitFamilyImporter.DwgClash.Views;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter.DwgClash.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class DwgClashCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null)
                return Result.Failed;

            try
            {
                Document document = uiDocument.Document;
                Autodesk.Revit.DB.View view = document.ActiveView;
                List<DwgImportOption> imports = new FilteredElementCollector(document)
                    .WhereElementIsNotElementType()
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .Select(importInstance => new DwgImportOption
                    {
                        ImportInstance = importInstance,
                        DisplayName = GetDisplayName(document, importInstance)
                    })
                    .OrderBy(option => option.DisplayName)
                    .ToList();

                if (imports.Count == 0)
                {
                    TaskDialog.Show("Clash 2D DWG x Revit", "Nenhum DWG importado ou vinculado foi encontrado na vista ativa.");
                    return Result.Cancelled;
                }

                DwgClashExternalEventHandler handler = new DwgClashExternalEventHandler();
                ExternalEvent externalEvent = ExternalEvent.Create(handler);
                handler.Attach(externalEvent);

                DwgClashSetupWindow setup = new DwgClashSetupWindow(view, imports,
                    new RevitElementCollector().GetCategoryOptions(), handler);
                handler.AttachSetup(setup);
                RevitWindowOwner.Attach(setup, commandData.Application);
                setup.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                TaskDialog.Show("Erro - Clash 2D DWG x Revit", ex.Message);
                return Result.Failed;
            }
        }

        private static string GetDisplayName(Document document, ImportInstance importInstance)
        {
            string name = importInstance.Name;
            try
            {
                Element type = document.GetElement(importInstance.GetTypeId());
                if (type != null && !string.IsNullOrWhiteSpace(type.Name) &&
                    (string.IsNullOrWhiteSpace(name) || name.IndexOf("location", StringComparison.OrdinalIgnoreCase) >= 0))
                    name = type.Name;
            }
            catch
            {
                // O nome do arquivo pode não estar disponível em links CAD externos.
            }

            if (string.IsNullOrWhiteSpace(name))
                name = "DWG";

            string ownerView = "Model / todas as vistas";
            try
            {
                if (importInstance.OwnerViewId != ElementId.InvalidElementId)
                {
                    Autodesk.Revit.DB.View view = document.GetElement(importInstance.OwnerViewId) as Autodesk.Revit.DB.View;
                    if (view != null)
                        ownerView = "View: " + view.Name;
                }
            }
            catch
            {
                // A vista proprietária pode não estar disponível em alguns documentos vinculados.
            }

            return string.Format("{0} | {1} | Id {2}", name, ownerView, importInstance.Id.IntegerValue);
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Services;
using RevitFamilyImporter.LinkConflict.Views;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter.LinkConflict.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LinkConflictCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document hostDocument = uiDoc?.Document;
            if (hostDocument == null)
                return Result.Failed;

            try
            {
                LinkConflictAnalysisService service = new LinkConflictAnalysisService();
                var links = GetLoadedLinks(hostDocument, service);
                if (links.Count == 0)
                {
                    TaskDialog.Show("Conflitos em Link", "Nenhum link carregado com categorias de modelo foi encontrado.");
                    return Result.Cancelled;
                }

                LinkConflictExternalEventHandler handler = new LinkConflictExternalEventHandler();
                ExternalEvent externalEvent = ExternalEvent.Create(handler);
                handler.Attach(externalEvent);

                LinkConflictSetupWindow setup = new LinkConflictSetupWindow(links, handler);
                handler.AttachSetup(setup);
                RevitWindowOwner.Attach(setup, commandData.Application);
                setup.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Erro - Conflitos em Link", ex.Message);
                return Result.Failed;
            }
        }

        private static List<LinkedModelOption> GetLoadedLinks(Document hostDocument, LinkConflictAnalysisService service)
        {
            return new FilteredElementCollector(hostDocument)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementClassFilter(typeof(RevitLinkInstance)))
                .ToElements()
                .Cast<RevitLinkInstance>()
                .Select(link =>
                {
                    Document linkedDocument = link.GetLinkDocument();
                    if (linkedDocument == null)
                        return null;

                    var categories = service.GetCategories(linkedDocument);
                    if (categories.Count == 0)
                        return null;

                    return new LinkedModelOption
                    {
                        LinkInstance = link,
                        Name = link.Name,
                        DocumentTitle = linkedDocument.Title,
                        Categories = categories
                    };
                })
                .Where(option => option != null)
                .OrderBy(option => option.DisplayName)
                .ToList();
        }
    }
}


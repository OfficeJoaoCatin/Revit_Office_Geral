using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Services;
using RevitFamilyImporter.VisibilityInspector.ViewModels;
using RevitFamilyImporter.VisibilityInspector.Views;

namespace RevitFamilyImporter.VisibilityInspector.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class VisibilityInspectorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            if (doc == null)
                return Result.Failed;

            try
            {
                VisibilityTarget target = ResolveTarget(commandData.Application, uiDoc, doc);
                if (target?.Element == null)
                    return Result.Cancelled;

                VisibilityAnalysisService service = new VisibilityAnalysisService();
                var report = target.IsLinkedElement
                    ? service.AnalyzeLinkedElement(commandData.Application, doc, doc.ActiveView, target.LinkInstance, target.ElementDocument, target.Element)
                    : service.Analyze(commandData.Application, doc, doc.ActiveView, target.Element);
                var viewModel = new VisibilityInspectorVM(commandData.Application, target.Element, report, service, target.LinkInstance, target.ElementDocument);
                var window = new VisibilityInspectorWindow(commandData.Application, viewModel);
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Erro - Visibility Inspector", ex.Message);
                return Result.Failed;
            }
        }

        private static VisibilityTarget ResolveTarget(UIApplication uiApp, UIDocument uiDoc, Document doc)
        {
            ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
            if (selectedIds.Count == 1)
            {
                Element selected = doc.GetElement(selectedIds.First());
                if (selected != null)
                    return new VisibilityTarget(selected);
            }

            TaskDialog dialog = new TaskDialog("Visibility Inspector")
            {
                MainInstruction = "Escolha como informar o elemento.",
                MainContent =
                    "Se o elemento não aparece na vista ativa, use o ID do elemento. " +
                    "Para elementos de modelos vinculados, use a opção de link para capturar o ID interno do arquivo vinculado.",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Selecionar elemento na tela");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Informar ID do elemento");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Selecionar elemento em link");

            TaskDialogResult result = dialog.Show();
            if (result == TaskDialogResult.CommandLink1)
            {
                Reference reference = uiDoc.Selection.PickObject(ObjectType.Element, "Selecione um elemento para inspecionar a visibilidade.");
                Element element = doc.GetElement(reference);
                return element == null ? null : new VisibilityTarget(element);
            }

            if (result == TaskDialogResult.CommandLink2)
            {
                Element element = ResolveElementById(uiApp, doc);
                return element == null ? null : new VisibilityTarget(element);
            }

            if (result == TaskDialogResult.CommandLink3)
                return ResolveLinkedElement(uiDoc, doc);

            return null;
        }

        private static VisibilityTarget ResolveLinkedElement(UIDocument uiDoc, Document doc)
        {
            Reference reference = uiDoc.Selection.PickObject(ObjectType.LinkedElement, "Selecione um elemento dentro de um modelo vinculado.");
            if (!(doc.GetElement(reference.ElementId) is RevitLinkInstance linkInstance))
            {
                TaskDialog.Show("Visibility Inspector", "A seleção não retornou uma instância de link válida.");
                return null;
            }

            Document linkedDocument = linkInstance.GetLinkDocument();
            if (linkedDocument == null)
            {
                TaskDialog.Show("Visibility Inspector", "O modelo vinculado está descarregado ou inacessível.");
                return null;
            }

            Element linkedElement = linkedDocument.GetElement(reference.LinkedElementId);
            if (linkedElement == null)
            {
                TaskDialog.Show("Visibility Inspector", $"Nenhum elemento foi encontrado no link com o ID {reference.LinkedElementId.IntegerValue}.");
                return null;
            }

            return new VisibilityTarget(linkedElement, linkInstance);
        }

        private static Element ResolveElementById(UIApplication uiApp, Document doc)
        {
            ElementIdInputWindow inputWindow = new ElementIdInputWindow(uiApp);
            if (inputWindow.ShowDialog() != true)
                return null;

            Element element = doc.GetElement(inputWindow.SelectedElementId);
            if (element != null)
                return element;

            TaskDialog.Show(
                "Visibility Inspector",
                $"Nenhum elemento foi encontrado com o ID {inputWindow.SelectedElementId.IntegerValue} no documento ativo.");
            return null;
        }
    }
}

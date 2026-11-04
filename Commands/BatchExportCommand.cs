using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BatchExportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                List<ExportDocumentInfo> documents = CollectOpenDocuments(uiApp);

                if (documents.Count == 0)
                {
                    TaskDialog.Show("Exportacao em Lote",
                        "Nenhum projeto exportavel esta aberto.\n\n" +
                        "Abra um ou mais arquivos RVT antes de executar a exportacao.");
                    return Result.Cancelled;
                }

                CheckpointData? checkpoint = AskForResumeCheckpoint();
                BatchExportWindow window = new BatchExportWindow(documents, checkpoint);
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Erro na Exportacao em Lote",
                    $"{exception.GetType().Name}: {exception.Message}\n\n{exception.StackTrace}");
                return Result.Failed;
            }
        }

        private static List<ExportDocumentInfo> CollectOpenDocuments(UIApplication uiApp)
        {
            List<ExportDocumentInfo> result = new List<ExportDocumentInfo>();

            foreach (Document document in uiApp.Application.Documents)
            {
                if (!IsExportableProject(document))
                    continue;

                int sheetCount = new FilteredElementCollector(document)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Count(sheet => !sheet.IsPlaceholder && sheet.CanBePrinted);

                result.Add(new ExportDocumentInfo
                {
                    ProjectName = GetProjectName(document),
                    FilePath = document.PathName ?? string.Empty,
                    Document = document,
                    Selected = true,
                    SheetCount = sheetCount
                });
            }

            return result
                .OrderBy(document => document.ProjectName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsExportableProject(Document document)
        {
            if (document.IsFamilyDocument || document.IsLinked)
                return false;

            string path = document.PathName ?? string.Empty;
            if (Path.GetExtension(path).Equals(".rte", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static string GetProjectName(Document document)
        {
            string projectInfoName = document.ProjectInformation?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(projectInfoName))
                return projectInfoName.Trim();

            if (!string.IsNullOrWhiteSpace(document.Title))
                return document.Title.Trim();

            return string.IsNullOrWhiteSpace(document.PathName)
                ? "Projeto sem nome"
                : Path.GetFileNameWithoutExtension(document.PathName);
        }

        private static CheckpointData? AskForResumeCheckpoint()
        {
            CheckpointManager checkpointManager = new CheckpointManager();
            CheckpointData? checkpoint = checkpointManager.FindLatestIncomplete();
            if (checkpoint == null)
                return null;

            TaskDialog dialog = new TaskDialog("Exportacao interrompida");
            dialog.MainInstruction = "Existe uma exportacao em lote nao concluida.";
            dialog.MainContent =
                $"Ultima atualizacao: {checkpoint.UpdatedAt:G}\n" +
                $"Projeto: {checkpoint.CurrentProjectPath}\n" +
                $"Folha: {checkpoint.CurrentSheetNumber}\n\n" +
                "Deseja retomar a partir do ultimo checkpoint?";
            dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            dialog.DefaultButton = TaskDialogResult.Yes;

            return dialog.Show() == TaskDialogResult.Yes ? checkpoint : null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DeleteIdenticalInstancesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            if (doc == null)
                return Result.Failed;

            try
            {
                List<DuplicateWarningGroup> groups = CollectDuplicateWarningGroups(doc);
                if (groups.Count == 0)
                {
                    TaskDialog.Show(
                        "Limpar Instâncias Duplicadas",
                        "Nenhum warning de instâncias idênticas no mesmo lugar foi encontrado no projeto ativo.");
                    return Result.Cancelled;
                }

                int elementsToDelete = groups.Sum(group => group.DeleteIds.Count);
                TaskDialog confirmDialog = new TaskDialog("Limpar Instâncias Duplicadas");
                confirmDialog.MainInstruction = "Excluir instâncias duplicadas encontradas nos warnings?";
                confirmDialog.MainContent =
                    $"Warnings encontrados: {groups.Count}\n" +
                    $"Elementos que serão mantidos: {groups.Count}\n" +
                    $"Instâncias que serão deletadas: {elementsToDelete}\n\n" +
                    "O comando mantém o primeiro ID de cada warning e deleta os IDs abaixo dele.";
                confirmDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                confirmDialog.DefaultButton = TaskDialogResult.No;

                if (confirmDialog.Show() != TaskDialogResult.Yes)
                    return Result.Cancelled;

                CleanupReport report = DeleteDuplicates(doc, groups);
                ShowResultDialog(report);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show(
                    "Erro - Limpar Instâncias Duplicadas",
                    $"{exception.GetType().Name}: {exception.Message}");
                return Result.Failed;
            }
        }

        private static List<DuplicateWarningGroup> CollectDuplicateWarningGroups(Document doc)
        {
            List<DuplicateWarningGroup> groups = new List<DuplicateWarningGroup>();
            int warningNumber = 0;

            foreach (FailureMessage warning in doc.GetWarnings())
            {
                if (!IsIdenticalInstancesWarning(warning))
                    continue;

                List<ElementId> ids = GetValidFailingElementIds(doc, warning);
                if (ids.Count < 2)
                    continue;

                warningNumber++;
                ElementId keepId = ids[0];
                List<ElementId> deleteIds = ids.Skip(1).ToList();
                groups.Add(new DuplicateWarningGroup(warningNumber, keepId, deleteIds));
            }

            return groups;
        }

        private static bool IsIdenticalInstancesWarning(FailureMessage warning)
        {
            try
            {
                if (warning.GetFailureDefinitionId() == BuiltInFailures.OverlapFailures.DuplicateInstances)
                    return true;
            }
            catch
            {
            }

            string description = Normalize(warning.GetDescriptionText());
            return description.Contains("identical instances")
                && description.Contains("same place");
        }

        private static List<ElementId> GetValidFailingElementIds(Document doc, FailureMessage warning)
        {
            List<ElementId> result = new List<ElementId>();
            HashSet<long> seen = new HashSet<long>();

            foreach (ElementId id in warning.GetFailingElements() ?? Enumerable.Empty<ElementId>())
            {
                if (id == ElementId.InvalidElementId)
                    continue;

                if (!seen.Add(GetElementIdValue(id)))
                    continue;

                if (doc.GetElement(id) == null)
                    continue;

                result.Add(id);
            }

            return result;
        }

        private static CleanupReport DeleteDuplicates(Document doc, List<DuplicateWarningGroup> groups)
        {
            CleanupReport report = new CleanupReport();

            using (Transaction transaction = new Transaction(doc, "Limpar instancias identicas no mesmo lugar"))
            {
                transaction.Start();

                foreach (DuplicateWarningGroup group in groups)
                {
                    Element keptElement = doc.GetElement(group.KeepId);
                    if (keptElement == null)
                    {
                        report.Failures.Add($"Warning {group.WarningNumber}: ID mantido {FormatId(group.KeepId)} não existe mais.");
                        continue;
                    }

                    DuplicateCleanupRecord record = new DuplicateCleanupRecord
                    {
                        WarningNumber = group.WarningNumber,
                        KeptId = FormatId(group.KeepId),
                        KeptCategory = keptElement.Category?.Name ?? string.Empty,
                        KeptName = GetElementName(keptElement)
                    };
                    record.DeletedIds.AddRange(group.DeleteIds.Select(FormatId));

                    foreach (ElementId deleteId in group.DeleteIds)
                    {
                        if (doc.GetElement(deleteId) == null)
                            continue;

                        try
                        {
                            doc.Delete(deleteId);
                            report.DeletedCount++;
                        }
                        catch (Exception exception)
                        {
                            record.FailedDeleteIds.Add($"{FormatId(deleteId)} ({exception.Message})");
                        }
                    }

                    report.Records.Add(record);
                }

                transaction.Commit();
            }

            return report;
        }

        private static void ShowResultDialog(CleanupReport report)
        {
            TaskDialog dialog = new TaskDialog("Limpar Instâncias Duplicadas");
            dialog.MainInstruction = "Limpeza concluída.";
            dialog.MainContent =
                $"Elementos mantidos: {report.Records.Count}\n" +
                $"Instâncias deletadas: {report.DeletedCount}\n" +
                $"Falhas: {report.FailureCount}\n\n" +
                "Você pode exportar a lista dos IDs mantidos para conferir depois no Revit.";

            if (report.Records.Count > 0)
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Exportar lista de IDs mantidos");

            dialog.CommonButtons = TaskDialogCommonButtons.Close;
            dialog.DefaultButton = TaskDialogResult.Close;

            TaskDialogResult result = dialog.Show();
            if (result == TaskDialogResult.CommandLink1)
                ExportKeptIds(report);
        }

        private static void ExportKeptIds(CleanupReport report)
        {
            WpfSaveFileDialog saveDialog = new WpfSaveFileDialog
            {
                Title = "Exportar IDs mantidos",
                FileName = $"ids_instancias_mantidas_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Filter = "Arquivo CSV (*.csv)|*.csv|Arquivo texto (*.txt)|*.txt",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (saveDialog.ShowDialog() != true)
                return;

            File.WriteAllText(saveDialog.FileName, BuildExportText(report), Encoding.UTF8);
            TaskDialog.Show("Exportar IDs mantidos", $"Lista exportada:\n{saveDialog.FileName}");
        }

        private static string BuildExportText(CleanupReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Warning;IdMantido;Categoria;Nome;IdsDeletados;FalhasAoDeletar");

            foreach (DuplicateCleanupRecord record in report.Records)
            {
                builder.Append(record.WarningNumber.ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
                builder.Append(EscapeCsv(record.KeptId));
                builder.Append(';');
                builder.Append(EscapeCsv(record.KeptCategory));
                builder.Append(';');
                builder.Append(EscapeCsv(record.KeptName));
                builder.Append(';');
                builder.Append(EscapeCsv(string.Join(",", record.DeletedIds)));
                builder.Append(';');
                builder.Append(EscapeCsv(string.Join(",", record.FailedDeleteIds)));
                builder.AppendLine();
            }

            if (report.Failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Falhas gerais");
                foreach (string failure in report.Failures)
                    builder.AppendLine(EscapeCsv(failure));
            }

            return builder.ToString();
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string GetElementName(Element element)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(element.Name))
                    return element.Name;
            }
            catch
            {
            }

            return element.GetType().Name;
        }

        private static string FormatId(ElementId id)
        {
            return GetElementIdValue(id).ToString(CultureInfo.InvariantCulture);
        }

        private static long GetElementIdValue(ElementId id)
        {
            return id.IntegerValue;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private sealed class DuplicateWarningGroup
        {
            public DuplicateWarningGroup(int warningNumber, ElementId keepId, List<ElementId> deleteIds)
            {
                WarningNumber = warningNumber;
                KeepId = keepId;
                DeleteIds = deleteIds;
            }

            public int WarningNumber { get; }
            public ElementId KeepId { get; }
            public List<ElementId> DeleteIds { get; }
        }

        private sealed class DuplicateCleanupRecord
        {
            public int WarningNumber { get; set; }
            public string KeptId { get; set; } = string.Empty;
            public string KeptCategory { get; set; } = string.Empty;
            public string KeptName { get; set; } = string.Empty;
            public List<string> DeletedIds { get; } = new List<string>();
            public List<string> FailedDeleteIds { get; } = new List<string>();
        }

        private sealed class CleanupReport
        {
            public List<DuplicateCleanupRecord> Records { get; } = new List<DuplicateCleanupRecord>();
            public List<string> Failures { get; } = new List<string>();
            public int DeletedCount { get; set; }
            public int FailureCount => Failures.Count + Records.Sum(record => record.FailedDeleteIds.Count);
        }
    }
}

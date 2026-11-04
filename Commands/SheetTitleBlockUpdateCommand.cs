using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;
using RevitFamilyImporter.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SheetTitleBlockUpdateCommand : IExternalCommand
    {
        private const string RoutineName = "Gerador de Folha";
        private const string LogFolder = @"O:\2.Biblioteca\T.I\Log rotinas";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            if (doc == null) return Result.Failed;

            bool failed = false;
            try
            {
                var updater = new SheetTitleBlockUpdater();
                List<FamilySymbol> titleBlocks = updater.GetTitleBlockTypes(doc);

                var settings = new SheetTitleBlockSettingsWindow(commandData.Application, titleBlocks);
                if (settings.ShowDialog() != true)
                    return Result.Cancelled;

                var loading = new SheetTitleBlockLoadWindow(commandData.Application, settings.ExcelPath, settings.Discipline);
                if (loading.ShowDialog() != true)
                {
                    if (loading.Error != null)
                        throw new InvalidOperationException("Nao foi possivel ler a Lista Mestra.", loading.Error);

                    return Result.Cancelled;
                }

                List<SheetTitleBlockRow> rows = loading.Rows;
                int rawRows = loading.RawRows;

                if (rows.Count == 0)
                {
                    TaskDialog.Show(RoutineName, "Nenhuma folha valida foi encontrada para a disciplina selecionada.");
                    return Result.Cancelled;
                }

                if (!settings.UpdateExistingOnly && settings.SelectSpecificSheets)
                {
                    var selection = new SheetTitleBlockSelectionWindow(commandData.Application, rows);
                    if (selection.ShowDialog() != true)
                        return Result.Cancelled;
                }

                SheetTitleBlockUpdateReport report;
                string transactionName = settings.UpdateExistingOnly
                    ? "Atualizar folhas Office existentes"
                    : "Gerar e atualizar folhas Office";
                using (var tx = new Transaction(doc, transactionName))
                {
                    tx.Start();
                    report = updater.Apply(doc, rows, settings.SelectedTitleBlock, settings.UpdateExistingOnly);
                    report.RowsRead = rawRows;
                    report.RowsEligible = rows.Count;
                    tx.Commit();
                }

                TaskDialog.Show(RoutineName, BuildSummary(report));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                failed = true;
                message = ex.Message;
                TaskDialog.Show($"Erro - {RoutineName}", ex.Message);
                return Result.Failed;
            }
            finally
            {
                WriteLog(commandData.Application.Application, failed);
            }
        }

        private static string BuildSummary(SheetTitleBlockUpdateReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Rotina executada com sucesso!");
            builder.AppendLine();
            builder.AppendLine($"Linhas lidas na LMD: {report.RowsRead}");
            builder.AppendLine($"Folhas validas na disciplina: {report.RowsEligible}");
            builder.AppendLine($"Folhas selecionadas: {report.RowsSelected}");
            builder.AppendLine($"Folhas criadas: {report.SheetsCreated}");
            builder.AppendLine($"Folhas existentes atualizadas: {report.SheetsUpdated}");
            builder.AppendLine($"Folhas inexistentes ignoradas: {report.SheetsMissingSkipped}");
            builder.AppendLine($"Folhas renomeadas: {report.SheetsRenamed}");
            builder.AppendLine($"Parametros atualizados: {report.ParametersUpdated}");
            builder.AppendLine($"Parametros ignorados/nao encontrados: {report.ParametersSkipped}");
            builder.AppendLine($"Erros: {report.Errors}");

            if (report.Messages.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Ocorrencias:");
                foreach (string item in report.Messages.Take(8))
                    builder.AppendLine("- " + item);
                if (report.Messages.Count > 8)
                    builder.AppendLine($"- ... mais {report.Messages.Count - 8} ocorrencia(s)");
            }

            return builder.ToString();
        }

        private static void WriteLog(Autodesk.Revit.ApplicationServices.Application app, bool failed)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                string fileName = $"{RoutineName}_{DateTime.Now:yyyy-MM}.txt";
                string path = Path.Combine(LogFolder, SanitizeFileName(fileName));
                string header = "---### DATA-HORA ### USUARIO_PC ### USUARIO_REVIT ### VERSAO_REVIT ### STATUS ###---";
                string pcUser = Environment.UserName;
                string revitUser = app?.Username ?? "unknown";
                string revitVersion = app?.VersionName ?? app?.VersionNumber ?? "unknown";
                string status = failed ? "ERRO" : "OK";
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {pcUser} - {revitUser} - {revitVersion} - {status}";

                List<string> lines = File.Exists(path)
                    ? File.ReadAllLines(path, Encoding.UTF8).ToList()
                    : new List<string>();

                lines.RemoveAll(l => string.Equals(l.Trim(), header, StringComparison.Ordinal));
                lines.Insert(0, line);
                lines.Insert(0, header);
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch { }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}

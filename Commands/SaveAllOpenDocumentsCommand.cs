using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SaveAllOpenDocumentsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            DocumentOperationReport report = new DocumentOperationReport();

            try
            {
                foreach (Document document in commandData.Application.Application.Documents.Cast<Document>().ToList())
                {
                    if (ShouldSkip(document, out string reason))
                    {
                        report.AddSkipped(document, reason);
                        continue;
                    }

                    try
                    {
                        document.Save();
                        report.AddSuccess(document, "salvo");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        report.AddSkipped(document, "operacao cancelada");
                    }
                    catch (Exception exception)
                    {
                        report.AddFailure(document, exception.Message);
                    }
                }

                TaskDialog.Show("Salvar Tudo", report.BuildDialogText("Salvos"));
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Erro ao salvar tudo", $"{exception.GetType().Name}: {exception.Message}");
                return Result.Failed;
            }
        }

        private static bool ShouldSkip(Document document, out string reason)
        {
            if (document.IsLinked)
            {
                reason = "documento vinculado";
                return true;
            }

            if (string.IsNullOrWhiteSpace(document.PathName))
            {
                reason = "sem caminho salvo; use Salvar como";
                return true;
            }

            if (document.IsReadOnly)
            {
                reason = "somente leitura";
                return true;
            }

            if (!document.IsModified)
            {
                reason = "sem alteracoes";
                return true;
            }

            reason = string.Empty;
            return false;
        }
    }
}

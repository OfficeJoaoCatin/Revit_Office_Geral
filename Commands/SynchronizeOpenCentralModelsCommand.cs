using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SynchronizeOpenCentralModelsCommand : IExternalCommand
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
                        Synchronize(document);
                        report.AddSuccess(document, "sincronizado com central");
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

                TaskDialog.Show("Sincronizar Centrais", report.BuildDialogText("Sincronizados"));
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Erro ao sincronizar centrais", $"{exception.GetType().Name}: {exception.Message}");
                return Result.Failed;
            }
        }

        private static void Synchronize(Document document)
        {
            RelinquishOptions relinquishOptions = new RelinquishOptions(true);
            SynchronizeWithCentralOptions synchronizeOptions = new SynchronizeWithCentralOptions
            {
                Comment = "Sincronizado pelo Revit Family Importer",
                Compact = false,
                SaveLocalAfter = true,
                SaveLocalBefore = true
            };

            synchronizeOptions.SetRelinquishOptions(relinquishOptions);
            document.SynchronizeWithCentral(new TransactWithCentralOptions(), synchronizeOptions);
        }

        private static bool ShouldSkip(Document document, out string reason)
        {
            if (document.IsLinked)
            {
                reason = "documento vinculado";
                return true;
            }

            if (!document.IsWorkshared)
            {
                reason = "nao e workshared";
                return true;
            }

            if (document.IsReadOnly)
            {
                reason = "somente leitura";
                return true;
            }

            ModelPath centralPath = document.GetWorksharingCentralModelPath();
            if (centralPath == null)
            {
                reason = "sem caminho de central";
                return true;
            }

            reason = string.Empty;
            return false;
        }
    }
}

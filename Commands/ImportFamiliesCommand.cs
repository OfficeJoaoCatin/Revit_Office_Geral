using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter.Commands
{
    /// <summary>
    /// Comando principal do plugin. É executado quando o usuário clica no botão
    /// "Importar Famílias" na Ribbon. Implementa IExternalCommand.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ImportFamiliesCommand : IExternalCommand
    {
        /// <summary>
        /// Ponto de entrada do comando. Abre diálogo de arquivo, lê famílias
        /// e exibe a janela de seleção/importação.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Referências ao aplicativo e documento ativo
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            // Verificar se há um documento aberto
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("Aviso",
                    "Nenhum projeto está aberto no Revit.\n\n" +
                    "Abra ou crie um projeto antes de usar esta ferramenta.");
                return Result.Cancelled;
            }

            Document targetDoc = uiDoc.Document;

            try
            {
                // ── Passo 1: Abrir diálogo para selecionar arquivo de origem ──
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Selecionar arquivo Revit de origem",
                    Filter = "Arquivos Revit (*.rvt;*.rte)|*.rvt;*.rte|" +
                             "Projetos Revit (*.rvt)|*.rvt|" +
                             "Templates Revit (*.rte)|*.rte",
                    FilterIndex = 1,
                    Multiselect = false,
                    CheckFileExists = true
                };

                // Se o usuário cancelou a seleção
                if (openFileDialog.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                string sourceFilePath = openFileDialog.FileName;

                // Verificar se não selecionou o mesmo arquivo que está aberto
                if (targetDoc.PathName != null &&
                    string.Equals(sourceFilePath, targetDoc.PathName, StringComparison.OrdinalIgnoreCase))
                {
                    TaskDialog.Show("Aviso",
                        "O arquivo selecionado é o mesmo que está aberto no Revit.\n" +
                        "Selecione um arquivo diferente como origem.");
                    return Result.Cancelled;
                }

                // ── Passo 2: Ler famílias do arquivo de origem ──
                FamilyService familyService = new FamilyService();
                Document? sourceDoc = null;
                List<FamilyItem> families;

                try
                {
                    families = familyService.ReadFamiliesFromFile(sourceFilePath, uiApp, out sourceDoc);
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("Erro ao abrir arquivo",
                        $"Não foi possível abrir o arquivo selecionado:\n\n" +
                        $"{ex.Message}\n\n" +
                        "Verifique se o arquivo não está corrompido e se você tem " +
                        "permissão de leitura.");
                    return Result.Failed;
                }

                // Verificar se encontrou famílias
                if (families == null || families.Count == 0)
                {
                    TaskDialog.Show("Informação",
                        "Nenhuma família foi encontrada no arquivo selecionado.\n\n" +
                        "O arquivo pode conter apenas famílias do sistema, que não " +
                        "podem ser importadas desta forma.");

                    // Fechar documento de origem
                    sourceDoc?.Close(false);
                    return Result.Cancelled;
                }

                // ── Passo 3: Exibir janela de seleção e importação ──
                ImportFamiliesWindow window = new ImportFamiliesWindow(
                    families: families,
                    sourceFilePath: sourceFilePath,
                    targetDoc: targetDoc,
                    sourceDoc: sourceDoc,
                    familyService: familyService
                );

                // Exibir a janela como modal (bloqueia a interação com o Revit)
                window.ShowDialog();

                // NOTA: O fechamento do sourceDoc é feito dentro da janela após
                // a importação ou quando o usuário cancela.

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Usuário cancelou uma operação do Revit
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // Erro inesperado
                message = ex.Message;

                TaskDialog.Show("Erro Inesperado",
                    $"Ocorreu um erro inesperado:\n\n" +
                    $"{ex.GetType().Name}: {ex.Message}\n\n" +
                    $"Stack Trace:\n{ex.StackTrace}");

                return Result.Failed;
            }
        }
    }
}

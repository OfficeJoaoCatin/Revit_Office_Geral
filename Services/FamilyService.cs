using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Services
{
    /// <summary>
    /// Serviço responsável por ler famílias de um documento Revit de origem
    /// e importá-las para o documento de destino.
    /// </summary>
    public class FamilyService
    {
        /// <summary>
        /// Abre um documento Revit em background (sem interface) e lê todas as famílias.
        /// Retorna a lista de FamilyItem encontradas e o documento aberto (que deve ser
        /// fechado pelo chamador após a importação).
        /// </summary>
        /// <param name="filePath">Caminho completo do arquivo .rvt ou .rte</param>
        /// <param name="uiApp">Referência ao UIApplication do Revit</param>
        /// <param name="sourceDoc">Documento aberto em background (output)</param>
        /// <returns>Lista de famílias encontradas no arquivo</returns>
        public List<FamilyItem> ReadFamiliesFromFile(string filePath, UIApplication uiApp, out Document sourceDoc)
        {
            var families = new List<FamilyItem>();

            // Abrir o documento em background (sem exibir na interface do Revit)
            // detachFromCentralOption: usado para modelos centralizados em worksharing
            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);

            OpenOptions openOptions = new OpenOptions();
            openOptions.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;

            // Abrir sem interface gráfica (audit = false)
            sourceDoc = uiApp.Application.OpenDocumentFile(modelPath, openOptions);

            // Coletar todas as famílias do documento usando FilteredElementCollector
            FilteredElementCollector collector = new FilteredElementCollector(sourceDoc);
            ICollection<Element> familyElements = collector
                .OfClass(typeof(Family))
                .ToElements();

            foreach (Element element in familyElements)
            {
                if (element is Family family)
                {
                    // Ignorar famílias do sistema (não podem ser copiadas)
                    if (family.IsInPlace)
                        continue;

                    // Obter o nome da categoria
                    string categoryName = "Sem Categoria";
                    if (family.FamilyCategory != null)
                    {
                        categoryName = family.FamilyCategory.Name;
                    }

                    // Contar quantos tipos (FamilySymbol) a família possui
                    int typeCount = family.GetFamilySymbolIds().Count;

                    families.Add(new FamilyItem
                    {
                        Name = family.Name,
                        Category = categoryName,
                        RevitFamily = family,
                        FamilyId = family.Id,
                        TypeCount = typeCount,
                        IsSelected = false
                    });
                }
            }

            // Ordenar por categoria e depois por nome
            families = families
                .OrderBy(f => f.Category)
                .ThenBy(f => f.Name)
                .ToList();

            return families;
        }

        /// <summary>
        /// Importa as famílias selecionadas do documento de origem para o documento de destino.
        /// Utiliza Transaction e ElementTransformUtils.CopyElements para copiar.
        /// Trata conflitos perguntando ao usuário se deseja sobrescrever.
        /// </summary>
        /// <param name="targetDoc">Documento de destino (projeto atual)</param>
        /// <param name="sourceDoc">Documento de origem (aberto em background)</param>
        /// <param name="selectedItems">Lista de famílias selecionadas para importação</param>
        /// <param name="progress">Callback para atualizar barra de progresso (0-100)</param>
        /// <returns>Número de famílias importadas com sucesso</returns>
        public int ImportFamilies(
            Document targetDoc,
            Document sourceDoc,
            List<FamilyItem> selectedItems,
            IProgress<int>? progress = null)
        {
            int importedCount = 0;
            int totalItems = selectedItems.Count;

            // Configurar opções de cópia com tratamento de conflitos
            CopyPasteOptions copyOptions = new CopyPasteOptions();
            copyOptions.SetDuplicateTypeNamesHandler(new DuplicateTypeHandler());

            // Iniciar transação no documento de destino
            using (Transaction transaction = new Transaction(targetDoc, "Importar Famílias"))
            {
                transaction.Start();

                try
                {
                    for (int i = 0; i < totalItems; i++)
                    {
                        FamilyItem item = selectedItems[i];

                        try
                        {
                            // Coletar todos os ElementIds relacionados à família:
                            // A família em si + todos os seus tipos (FamilySymbol)
                            var elementIds = new List<ElementId>();

                            if (item.FamilyId != null)
                            {
                                elementIds.Add(item.FamilyId);

                                // Também incluir todos os FamilySymbols (tipos) da família
                                if (item.RevitFamily != null)
                                {
                                    ISet<ElementId> symbolIds = item.RevitFamily.GetFamilySymbolIds();
                                    elementIds.AddRange(symbolIds);
                                }
                            }

                            if (elementIds.Count > 0)
                            {
                                // Copiar elementos do documento de origem para o destino
                                ElementTransformUtils.CopyElements(
                                    sourceDoc,
                                    elementIds,
                                    targetDoc,
                                    Transform.Identity,
                                    copyOptions
                                );

                                importedCount++;
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                        {
                            // Família específica falhou — informar mas continuar com as outras
                            TaskDialog td = new TaskDialog("Aviso de Importação");
                            td.MainInstruction = $"Não foi possível importar: {item.Name}";
                            td.MainContent = $"Erro: {ex.Message}\n\nDeseja continuar importando as demais famílias?";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.Yes;

                            TaskDialogResult result = td.Show();
                            if (result == TaskDialogResult.No)
                            {
                                break; // Interromper importação
                            }
                        }
                        catch (Exception ex)
                        {
                            // Erro genérico — registrar e continuar
                            TaskDialog td = new TaskDialog("Erro na Importação");
                            td.MainInstruction = $"Erro ao importar: {item.Name}";
                            td.MainContent = $"{ex.GetType().Name}: {ex.Message}\n\nDeseja continuar?";
                            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                            td.DefaultButton = TaskDialogResult.Yes;

                            TaskDialogResult result = td.Show();
                            if (result == TaskDialogResult.No)
                            {
                                break;
                            }
                        }

                        // Atualizar progresso (porcentagem de 0 a 100)
                        int percentComplete = (int)(((double)(i + 1) / totalItems) * 100);
                        progress?.Report(percentComplete);
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    // Se a transação ainda estiver ativa, reverter
                    if (transaction.HasStarted() && !transaction.HasEnded())
                    {
                        transaction.RollBack();
                    }

                    TaskDialog.Show("Erro Crítico",
                        $"A importação foi cancelada devido a um erro:\n\n{ex.Message}");

                    return 0;
                }
            }

            return importedCount;
        }
    }

    /// <summary>
    /// Handler para resolver conflitos quando um tipo com o mesmo nome já existe
    /// no documento de destino. Pergunta ao usuário via TaskDialog.
    /// </summary>
    public class DuplicateTypeHandler : IDuplicateTypeNamesHandler
    {
        /// <summary>
        /// Chamado pelo Revit quando encontra um tipo com nome duplicado.
        /// Exibe um diálogo perguntando ao usuário como proceder.
        /// </summary>
        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            // Montar mensagem informativa sobre o conflito
            string typeName = "tipo existente";

            TaskDialog td = new TaskDialog("Conflito de Tipos");
            td.MainInstruction = "Tipo duplicado encontrado";
            td.MainContent =
                "Um ou mais tipos com o mesmo nome já existem no projeto de destino.\n\n" +
                "Escolha como proceder:\n" +
                "• SIM — Sobrescrever os tipos existentes com os do arquivo de origem\n" +
                "• NÃO — Manter os tipos existentes e usar os nomes atuais";

            td.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            td.DefaultButton = TaskDialogResult.No;

            TaskDialogResult result = td.Show();

            if (result == TaskDialogResult.Yes)
            {
                // Sobrescrever: usa os valores do documento de origem
                return DuplicateTypeAction.UseDestinationTypes;
            }
            else
            {
                // Manter: usa os tipos já existentes no destino
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }
    }
}

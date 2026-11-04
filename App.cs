using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter
{
    /// <summary>
    /// Classe principal do plugin. Implementa IExternalApplication para registrar
    /// a aba "Office - Geral" na Ribbon do Revit com o botão "Importar Famílias".
    /// </summary>
    public class App : IExternalApplication
    {
        // Nome da aba na Ribbon
        private const string TabName = "Office - Geral";

        // Nome dos paineis dentro da aba
        private const string FamilyPanelName = "Famílias";
        private const string FilesPanelName = "Arquivos";
        private const string ModelPanelName = "Modelo";
        private const string DocumentationPanelName = "Documentação";
        private static readonly string StartupLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RevitFamilyImporter",
            "startup.log");

        /// <summary>
        /// Chamado quando o Revit inicia. Cria a aba, painel e botão na Ribbon.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Criar aba personalizada na Ribbon
                bool useCustomTab = true;
                try
                {
                    application.CreateRibbonTab(TabName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // A aba já existe — não é erro, pode ter sido criada por outro plugin
                }
                catch (Exception ex)
                {
                    useCustomTab = false;
                    LogStartupError("Falha ao criar aba personalizada. Usando painel Add-Ins como fallback.", ex);
                }

                // Caminho da DLL atual (para referenciar o comando)
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                RibbonPanel familyPanel = GetOrCreatePanel(application, FamilyPanelName, useCustomTab);
                RibbonPanel filesPanel = GetOrCreatePanel(application, FilesPanelName, useCustomTab);
                RibbonPanel modelPanel = GetOrCreatePanel(application, ModelPanelName, useCustomTab);
                RibbonPanel documentationPanel = GetOrCreatePanel(application, DocumentationPanelName, useCustomTab);

                // Criar o botão "Importar Famílias"
                PushButtonData buttonData = CreateButton(
                    name: "ImportarFamilias",
                    text: "Importar\nFamílias",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.ImportFamiliesCommand",
                    iconKind: RibbonIconKind.ImportFamilies
                );

                // Configurar tooltip e descrição
                buttonData.ToolTip = "Importar famílias de outro arquivo Revit (.rvt ou .rte)";
                buttonData.LongDescription =
                    "Selecione um arquivo Revit de origem para listar todas as famílias disponíveis. " +
                    "Você pode filtrar, selecionar e importar as famílias desejadas para o projeto atual " +
                    "com acompanhamento de progresso em tempo real.";

                AddItemSafe(familyPanel, buttonData);

                PushButtonData batchExportButtonData = CreateButton(
                    name: "ExportacaoEmLote",
                    text: "Exportar\nLote",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.BatchExportCommand",
                    iconKind: RibbonIconKind.BatchExport
                );

                batchExportButtonData.ToolTip = "Exportar PDF e DWG de multiplos projetos abertos";
                batchExportButtonData.LongDescription =
                    "Selecione projetos abertos, folhas, perfis e configuracoes para exportar PDF e DWG em fila " +
                    "com checkpoint, log, progresso e cancelamento seguro.";

                AddItemSafe(familyPanel, batchExportButtonData);

                PushButtonData saveAllButtonData = CreateButton(
                    name: "SalvarTudo",
                    text: "Salvar\nTudo",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.SaveAllOpenDocumentsCommand",
                    iconKind: RibbonIconKind.SaveAll
                );

                saveAllButtonData.ToolTip = "Salvar todos os arquivos abertos";
                saveAllButtonData.LongDescription =
                    "Percorre os documentos abertos no Revit e salva os arquivos modificados que ja possuem caminho, " +
                    "ignorando documentos vinculados, somente leitura, sem alteracoes ou ainda sem caminho salvo.";

                AddItemSafe(filesPanel, saveAllButtonData);

                PushButtonData syncCentralButtonData = CreateButton(
                    name: "SincronizarCentrais",
                    text: "Sincronizar\nCentrais",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.SynchronizeOpenCentralModelsCommand",
                    iconKind: RibbonIconKind.SyncCentral
                );

                syncCentralButtonData.ToolTip = "Sincronizar modelos workshared abertos com o central";
                syncCentralButtonData.LongDescription =
                    "Percorre os modelos workshared abertos e sincroniza com o central usando salvar local antes/depois " +
                    "e relinquish dos elementos do usuario. Documentos nao workshared, vinculados ou somente leitura sao ignorados.";

                AddItemSafe(filesPanel, syncCentralButtonData);

                PushButtonData cleanDuplicatesButtonData = CreateButton(
                    name: "LimparInstanciasDuplicadas",
                    text: "Limpar\nDuplicadas",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.DeleteIdenticalInstancesCommand",
                    iconKind: RibbonIconKind.CleanDuplicates
                );

                cleanDuplicatesButtonData.ToolTip = "Deletar instâncias idênticas no mesmo lugar";
                cleanDuplicatesButtonData.LongDescription =
                    "Procura warnings de instâncias idênticas no mesmo lugar, mantém o primeiro elemento listado em cada warning " +
                    "e deleta as instâncias abaixo dele. Ao final, permite exportar uma lista CSV com os IDs mantidos.";

                AddItemSafe(modelPanel, cleanDuplicatesButtonData);

                PushButtonData selectIdenticalButtonData = CreateButton(
                    name: "SelecionarIdenticos",
                    text: "Selecionar\nIdênticos",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.SelectIdenticalElementsCommand",
                    iconKind: RibbonIconKind.SelectIdentical
                );

                selectIdenticalButtonData.ToolTip = "Selecionar elementos idênticos ao elemento base";
                selectIdenticalButtonData.LongDescription =
                    "Procura elementos com a mesma categoria, família, tipo, parâmetros editáveis relevantes " +
                    "e parâmetros geométricos de identidade como A e Ângulo. Parâmetros de localização, ambiente " +
                    "e documentação são ignorados para evitar falsos negativos.";

                AddItemSafe(modelPanel, selectIdenticalButtonData);

                PushButtonData visibilityInspectorButtonData = CreateButton(
                    name: "VisibilityInspector",
                    text: "Visibility\nInspector",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.VisibilityInspector.Commands.VisibilityInspectorCommand",
                    iconKind: RibbonIconKind.VisibilityInspector
                );

                visibilityInspectorButtonData.ToolTip = "Diagnosticar a visibilidade e os overrides de um elemento";
                visibilityInspectorButtonData.LongDescription =
                    "Selecione um elemento e veja um relatório com categoria, filtros, hide in view, worksets, fases, crop, " +
                    "view range, discipline, detail level, design option, links, section box, owner view, overrides e parâmetros.";

                AddItemSafe(modelPanel, visibilityInspectorButtonData);

                PushButtonData linkConflictButtonData = CreateButton(
                    name: "ConflitosEmLinks",
                    text: "Conflitos\nem Links",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.LinkConflict.Commands.LinkConflictCommand",
                    iconKind: RibbonIconKind.LinkConflicts
                );

                linkConflictButtonData.ToolTip = "Encontrar conflitos de uma categoria dentro de um link";
                linkConflictButtonData.LongDescription =
                    "Selecione um link e uma categoria interna, como Structural Columns, para localizar interseções " +
                    "com elementos do modelo hospedeiro. Navegue pelos avisos, selecione os elementos e enquadre o conflito.";

                AddItemSafe(modelPanel, linkConflictButtonData);

                PushButtonData dwgClashButtonData = CreateButton(
                    name: "Clash2DDwg",
                    text: "Clash 2D\nDWG",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.DwgClash.Commands.DwgClashCommand",
                    iconKind: RibbonIconKind.LinkConflicts
                );

                dwgClashButtonData.ToolTip = "Comparar geometrias 2D de um DWG com elementos Revit";
                dwgClashButtonData.LongDescription =
                    "Selecione um DWG importado ou vinculado, um layer e categorias Revit. " +
                    "O comando encontra conflitos na vista ativa, permite navegar pelos elementos " +
                    "e exportar os resultados em CSV ou JSON.";

                AddItemSafe(modelPanel, dwgClashButtonData);

                PushButtonData detailImportButtonData = CreateButton(
                    name: "ImportarDetalhesDwg",
                    text: "Importar\nDetalhes",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.DetailImportCommand",
                    iconKind: RibbonIconKind.DetailImport
                );

                detailImportButtonData.ToolTip = "Importar detalhes delimitados dentro de um DWG";
                detailImportButtonData.LongDescription =
                    "Selecione um DWG com limites fechados no layer LIMITE_DETS. " +
                    "O comando detecta cada detalhe, mostra preview e cria uma vista de legenda/detalhe para cada item selecionado.";

                PushButtonData sheetTitleBlockButtonData = CreateButton(
                    name: "GeradorFolhasOffice",
                    text: "Gerar\nFolhas",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.SheetTitleBlockUpdateCommand",
                    iconKind: RibbonIconKind.SheetTitleBlock
                );

                sheetTitleBlockButtonData.ToolTip = "Gerar e atualizar folhas Office pela Lista Mestra";
                sheetTitleBlockButtonData.LongDescription =
                    "Selecione a Lista Mestra, disciplina e template OFC-FOL para criar folhas ausentes, " +
                    "renomear folhas existentes e preencher parametros do carimbo.";


                PushButtonData editParametersButtonData = CreateButton(
                    name: "EditarParametrosSelecionados",
                    text: "Editar\nParâmetros",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.BatchTypeParameterUpdateCommand",
                    iconKind: RibbonIconKind.EditParameters
                );

                editParametersButtonData.ToolTip = "Editar parâmetros de instância ou tipo nos elementos selecionados";
                editParametersButtonData.LongDescription =
                    "Selecione elementos no modelo, informe o nome do parâmetro e o valor desejado. " +
                    "O comando altera parâmetros de instância quando existirem nos elementos selecionados; caso contrário, altera o parâmetro nos tipos.";

                PushButtonData legendCadButtonData = CreateButton(
                    name: "GerarLegendaBibliotecaCad",
                    text: "Legenda\nCAD",
                    assemblyName: assemblyPath,
                    className: "RevitFamilyImporter.Commands.GenerateLegendLibraryCommand",
                    iconKind: RibbonIconKind.LegendCad
                );

                legendCadButtonData.ToolTip = "Gerar legenda a partir de uma biblioteca DWG";
                legendCadButtonData.LongDescription =
                    "Selecione um DWG com itens delimitados no layer LIMITE_DETS, escolha os símbolos, " +
                    "organize grupos e gere uma legenda CAD no projeto atual.";
                AddItemSafe(documentationPanel, detailImportButtonData);
                AddItemSafe(documentationPanel, sheetTitleBlockButtonData);
                AddItemSafe(documentationPanel, editParametersButtonData);
                AddItemSafe(documentationPanel, legendCadButtonData);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                LogStartupError("Falha geral ao inicializar a ribbon.", ex);
                return Result.Succeeded;
            }
        }

        /// <summary>
        /// Chamado quando o Revit é encerrado. Limpeza de recursos se necessário.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            // Nenhuma limpeza necessária
            return Result.Succeeded;
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string panelName, bool useCustomTab)
        {
            if (useCustomTab)
            {
                try
                {
                    RibbonPanel? existingPanel = application.GetRibbonPanels(TabName)
                        .FirstOrDefault(panel => panel.Name == panelName);

                    return existingPanel ?? application.CreateRibbonPanel(TabName, panelName);
                }
                catch (Exception ex)
                {
                    LogStartupError($"Falha ao criar painel '{panelName}' na aba '{TabName}'. Usando Add-Ins como fallback.", ex);
                }
            }

            RibbonPanel? addInsPanel = application.GetRibbonPanels()
                .FirstOrDefault(panel => panel.Name == panelName);

            return addInsPanel ?? application.CreateRibbonPanel(panelName);
        }

        private static PushButtonData CreateButton(
            string name,
            string text,
            string assemblyName,
            string className,
            RibbonIconKind iconKind)
        {
            PushButtonData buttonData = new PushButtonData(name, text, assemblyName, className);

            try
            {
                buttonData.LargeImage = RibbonIconFactory.Create(iconKind, 32);
                buttonData.Image = RibbonIconFactory.Create(iconKind, 16);
            }
            catch (Exception ex)
            {
                LogStartupError($"Falha ao criar icone do botao '{name}'.", ex);
            }

            return buttonData;
        }

        private static void AddItemSafe(RibbonPanel panel, PushButtonData buttonData)
        {
            try
            {
                panel.AddItem(buttonData);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException ex)
            {
                LogStartupError($"Botao '{buttonData.Name}' ja existe ou nao pode ser adicionado ao painel '{panel.Name}'.", ex);
            }
            catch (Exception ex)
            {
                LogStartupError($"Falha ao adicionar botao '{buttonData.Name}' ao painel '{panel.Name}'.", ex);
            }
        }

        private static void LogStartupError(string context, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StartupLogPath));
                File.AppendAllText(
                    StartupLogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }
        }
    }
}




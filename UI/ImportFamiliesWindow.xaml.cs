using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;

namespace RevitFamilyImporter.UI
{
    /// <summary>
    /// Janela principal de seleção e importação de famílias.
    /// Contém o ViewModel integrado com ObservableCollection, filtro em tempo real,
    /// e lógica de importação com progresso.
    /// </summary>
    public partial class ImportFamiliesWindow : Window
    {
        // ── Campos privados ──────────────────────────────────────────

        /// <summary>Coleção observável para binding com o DataGrid.</summary>
        private readonly ObservableCollection<FamilyItem> _familyItems;

        /// <summary>View para aplicar filtro sem alterar a coleção original.</summary>
        private readonly CollectionViewSource _collectionViewSource;

        /// <summary>Referência ao ICollectionView para refresh do filtro.</summary>
        private readonly ICollectionView _collectionView;

        /// <summary>Caminho do arquivo de origem selecionado.</summary>
        private readonly string _sourceFilePath;

        /// <summary>Documento de destino (projeto aberto no Revit).</summary>
        private readonly Document _targetDoc;

        /// <summary>Documento de origem (aberto em background).</summary>
        private readonly Document _sourceDoc;

        /// <summary>Serviço de importação de famílias.</summary>
        private readonly FamilyService _familyService;

        /// <summary>Indica se a importação está em andamento.</summary>
        private bool _isImporting;

        // ── Construtor ───────────────────────────────────────────────

        /// <summary>
        /// Inicializa a janela com os dados das famílias encontradas.
        /// </summary>
        /// <param name="families">Lista de famílias lidas do arquivo de origem</param>
        /// <param name="sourceFilePath">Caminho do arquivo de origem</param>
        /// <param name="targetDoc">Documento de destino</param>
        /// <param name="sourceDoc">Documento de origem (aberto em background)</param>
        /// <param name="familyService">Serviço de importação</param>
        public ImportFamiliesWindow(
            List<FamilyItem> families,
            string sourceFilePath,
            Document targetDoc,
            Document sourceDoc,
            FamilyService familyService)
        {
            InitializeComponent();

            // Armazenar referências
            _sourceFilePath = sourceFilePath;
            _targetDoc = targetDoc;
            _sourceDoc = sourceDoc;
            _familyService = familyService;

            // Criar coleção observável a partir da lista
            _familyItems = new ObservableCollection<FamilyItem>(families);

            // Configurar CollectionViewSource para filtro em tempo real
            _collectionViewSource = new CollectionViewSource { Source = _familyItems };
            _collectionView = _collectionViewSource.View;
            _collectionView.Filter = FamilyFilter;

            // Binding do DataGrid com a view filtrada
            dgFamilies.ItemsSource = _collectionView;

            // Exibir informações iniciais
            txtFilePath.Text = sourceFilePath;
            UpdateCounters();

            // Tratar evento de fechamento para garantir que o documento é fechado
            this.Closing += OnWindowClosing;
        }

        // ── Filtro em tempo real ─────────────────────────────────────

        /// <summary>
        /// Predicado de filtro aplicado à CollectionView.
        /// Filtra famílias por nome ou categoria com base no texto de busca.
        /// </summary>
        private bool FamilyFilter(object item)
        {
            if (item is not FamilyItem family)
                return false;

            string searchText = txtSearch.Text?.Trim() ?? string.Empty;

            // Se não há texto de busca, mostrar tudo
            if (string.IsNullOrEmpty(searchText))
                return true;

            // Busca case-insensitive no nome e na categoria
            return family.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   family.Category.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Evento disparado quando o texto de busca muda.
        /// Atualiza o filtro da CollectionView em tempo real.
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Atualizar visibilidade do watermark
            txtSearchWatermark.Visibility = string.IsNullOrEmpty(txtSearch.Text)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            // Refresh do filtro
            _collectionView.Refresh();
        }

        /// <summary>Ocultar watermark quando o campo ganha foco.</summary>
        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            txtSearchWatermark.Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>Mostrar watermark se o campo está vazio ao perder foco.</summary>
        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtSearch.Text))
            {
                txtSearchWatermark.Visibility = System.Windows.Visibility.Visible;
            }
        }

        // ── Botões de seleção ────────────────────────────────────────

        /// <summary>
        /// Seleciona todas as famílias visíveis (respeitando o filtro atual).
        /// </summary>
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyItem item in _collectionView)
            {
                item.IsSelected = true;
            }
            UpdateCounters();
        }

        /// <summary>
        /// Desmarca todas as famílias visíveis (respeitando o filtro atual).
        /// </summary>
        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyItem item in _collectionView)
            {
                item.IsSelected = false;
            }
            UpdateCounters();
        }

        /// <summary>
        /// Atualiza o contador quando um checkbox individual é clicado.
        /// </summary>
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateCounters();
        }

        // ── Importação ──────────────────────────────────────────────

        /// <summary>
        /// Executa a importação das famílias selecionadas.
        /// Mostra barra de progresso e trata erros.
        /// </summary>
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            // Obter famílias selecionadas
            List<FamilyItem> selectedItems = _familyItems
                .Where(f => f.IsSelected)
                .ToList();

            if (selectedItems.Count == 0)
            {
                TaskDialog.Show("Aviso",
                    "Nenhuma família foi selecionada.\n\n" +
                    "Marque as famílias que deseja importar usando os checkboxes na lista.");
                return;
            }

            // Confirmar importação
            TaskDialog confirmDialog = new TaskDialog("Confirmar Importação");
            confirmDialog.MainInstruction = $"Importar {selectedItems.Count} família(s)?";
            confirmDialog.MainContent =
                $"As seguintes famílias serão importadas para o projeto atual:\n\n" +
                $"• Total selecionado: {selectedItems.Count}\n" +
                $"• Categorias: {selectedItems.Select(f => f.Category).Distinct().Count()}\n\n" +
                "Deseja continuar?";
            confirmDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            confirmDialog.DefaultButton = TaskDialogResult.Yes;

            if (confirmDialog.Show() != TaskDialogResult.Yes)
                return;

            // Iniciar importação
            ExecuteImport(selectedItems);
        }

        /// <summary>
        /// Executa a importação com feedback de progresso.
        /// Como a Revit API deve rodar na thread principal, usamos IProgress
        /// para atualizar a UI durante o loop.
        /// </summary>
        private void ExecuteImport(List<FamilyItem> selectedItems)
        {
            _isImporting = true;

            // Mostrar barra de progresso e desabilitar controles
            pnlProgress.Visibility = System.Windows.Visibility.Visible;
            btnImport.IsEnabled = false;
            btnCancel.IsEnabled = false;
            dgFamilies.IsEnabled = false;
            txtSearch.IsEnabled = false;

            txtProgressStatus.Text = "Preparando importação...";
            progressBar.Value = 0;

            try
            {
                // Criar callback de progresso que atualiza a UI
                // NOTA: A Revit API roda na thread principal, então podemos
                // atualizar a UI diretamente usando Dispatcher
                IProgress<int> progress = new Progress<int>(percent =>
                {
                    progressBar.Value = percent;
                    txtProgress.Text = $"{percent}%";
                    txtProgressStatus.Text = $"Importando famílias... ({percent}% concluído)";

                    // Forçar atualização visual da UI
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(delegate { }));
                });

                // Executar importação
                int importedCount = _familyService.ImportFamilies(
                    _targetDoc,
                    _sourceDoc,
                    selectedItems,
                    progress
                );

                // Atualizar progresso para 100%
                progressBar.Value = 100;
                txtProgress.Text = "100%";
                txtProgressStatus.Text = "Importação concluída!";

                // Fechar documento de origem
                CloseSourceDocument();

                // Exibir resultado
                TaskDialog resultDialog = new TaskDialog("Importação Concluída");
                resultDialog.MainInstruction = "✅ Importação realizada com sucesso!";
                resultDialog.MainContent =
                    $"Resultado:\n\n" +
                    $"• Famílias importadas: {importedCount} de {selectedItems.Count}\n" +
                    $"• Arquivo de origem: {System.IO.Path.GetFileName(_sourceFilePath)}\n\n" +
                    "As famílias importadas já estão disponíveis no seu projeto.";
                resultDialog.Show();

                // Fechar a janela
                _isImporting = false;
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                // Erro durante importação
                progressBar.Value = 0;
                txtProgressStatus.Text = "Erro durante a importação.";

                TaskDialog.Show("Erro na Importação",
                    $"Ocorreu um erro durante a importação:\n\n" +
                    $"{ex.Message}\n\n" +
                    "Algumas famílias podem ter sido importadas parcialmente.");

                // Reabilitar controles
                btnImport.IsEnabled = true;
                btnCancel.IsEnabled = true;
                dgFamilies.IsEnabled = true;
                txtSearch.IsEnabled = true;
                pnlProgress.Visibility = System.Windows.Visibility.Collapsed;
            }
            finally
            {
                _isImporting = false;
            }
        }

        // ── Cancelar e fechar ───────────────────────────────────────

        /// <summary>
        /// Cancela a operação e fecha a janela.
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_isImporting)
            {
                TaskDialog.Show("Aviso",
                    "Aguarde a conclusão da importação em andamento.");
                return;
            }

            // Fechar documento de origem antes de sair
            CloseSourceDocument();

            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Trata o evento de fechamento da janela (botão X ou Alt+F4).
        /// Garante que o documento de origem é fechado.
        /// </summary>
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (_isImporting)
            {
                e.Cancel = true;
                TaskDialog.Show("Aviso",
                    "Não é possível fechar a janela durante a importação.\n" +
                    "Aguarde a conclusão.");
                return;
            }

            // Garantir que o documento de origem é fechado
            CloseSourceDocument();
        }

        // ── Métodos auxiliares ───────────────────────────────────────

        /// <summary>
        /// Fecha o documento de origem sem salvar alterações.
        /// </summary>
        private void CloseSourceDocument()
        {
            try
            {
                if (_sourceDoc != null && _sourceDoc.IsValidObject)
                {
                    _sourceDoc.Close(false); // false = não salvar alterações
                }
            }
            catch
            {
                // Ignorar erros ao fechar — pode já ter sido fechado
            }
        }

        /// <summary>
        /// Atualiza os contadores de famílias na interface.
        /// </summary>
        private void UpdateCounters()
        {
            int totalCount = _familyItems.Count;
            int selectedCount = _familyItems.Count(f => f.IsSelected);

            txtFamilyCount.Text = $"{totalCount} famílias encontradas";
            txtSelectedCount.Text = $"{selectedCount} selecionada{(selectedCount != 1 ? "s" : "")}";
        }
    }
}

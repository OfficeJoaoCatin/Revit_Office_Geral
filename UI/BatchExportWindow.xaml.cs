using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.UI
{
    public partial class BatchExportWindow : Window
    {
        public BatchExportWindow(IEnumerable<ExportDocumentInfo> documents, CheckpointData? checkpoint)
        {
            InitializeComponent();
            DataContext = new BatchExportViewModel(documents, checkpoint);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is BatchExportViewModel viewModel && viewModel.IsExporting)
            {
                e.Cancel = true;
                TaskDialog.Show("Exportacao em Lote", "Solicite o cancelamento e aguarde o item atual finalizar.");
                return;
            }

            base.OnClosing(e);
        }
    }
}

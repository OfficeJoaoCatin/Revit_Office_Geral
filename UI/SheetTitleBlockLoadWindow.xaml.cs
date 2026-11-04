using Autodesk.Revit.UI;
using RevitFamilyImporter.Models;
using RevitFamilyImporter.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace RevitFamilyImporter.UI
{
    public partial class SheetTitleBlockLoadWindow : Window
    {
        private readonly string _excelPath;
        private readonly string _discipline;
        private bool _canClose;
        private Thread? _worker;

        public SheetTitleBlockLoadWindow(UIApplication uiApp, string excelPath, string discipline)
        {
            InitializeComponent();
            RevitWindowOwner.Attach(this, uiApp);

            _excelPath = excelPath;
            _discipline = discipline;
        }

        public List<SheetTitleBlockRow> Rows { get; private set; } = new List<SheetTitleBlockRow>();
        public int RawRows { get; private set; }
        public Exception? Error { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _worker = new Thread(LoadRows)
            {
                IsBackground = true,
                Name = "SheetTitleBlockExcelLoader"
            };
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.Start();
        }

        private void LoadRows()
        {
            try
            {
                var dataService = new SheetTitleBlockDataService();
                List<SheetTitleBlockRow> rows = dataService.LoadRows(_excelPath, _discipline);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Rows = rows;
                    RawRows = dataService.LastRawRowCount;
                    _canClose = true;
                    DialogResult = true;
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Error = ex;
                    TxtStatus.Text = "Nao foi possivel ler a Lista Mestra.";
                    _canClose = true;
                    DialogResult = false;
                }));
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = !_canClose;
        }
    }
}

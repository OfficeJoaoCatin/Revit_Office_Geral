using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;
using RevitFamilyImporter.DwgClash.Views;
using RevitFamilyImporter.UI;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Services;

namespace RevitFamilyImporter.DwgClash.Commands
{
    public enum DwgClashRequest
    {
        LoadLayers,
        Analyze,
        Select,
        CreateViewMarkers
    }

    public sealed class DwgClashExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        private DwgClashRequest? _pendingRequest;
        private DwgClashSetupWindow _setupWindow;
        private DwgClashResultsWindow _resultsWindow;

        public void Attach(ExternalEvent externalEvent) => _externalEvent = externalEvent;

        public void AttachSetup(DwgClashSetupWindow setupWindow) => _setupWindow = setupWindow;

        public void AttachResults(DwgClashResultsWindow resultsWindow) => _resultsWindow = resultsWindow;

        public void Request(DwgClashRequest request)
        {
            _pendingRequest = request;
            _externalEvent?.Raise();
        }

        public void Execute(UIApplication application)
        {
            DwgClashRequest? request = _pendingRequest;
            _pendingRequest = null;
            if (!request.HasValue)
                return;

            try
            {
                switch (request.Value)
                {
                    case DwgClashRequest.LoadLayers:
                        LoadLayers();
                        break;
                    case DwgClashRequest.Analyze:
                        Analyze(application);
                        break;
                    case DwgClashRequest.Select:
                        Select(application);
                        break;
                    case DwgClashRequest.CreateViewMarkers:
                        CreateViewMarkers(application);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                if (_setupWindow != null && _setupWindow.IsVisible)
                {
                    if (request == DwgClashRequest.LoadLayers) _setupWindow.ResetImportSelection();
                    _setupWindow.ShowValidationError(request == DwgClashRequest.LoadLayers
                        ? "Carregamento de layers cancelado. Selecione o DWG novamente para tentar outra vez."
                        : "Análise cancelada. Nenhum resultado parcial foi aplicado.");
                }
            }
            catch (Exception ex)
            {
                if (_setupWindow != null && _setupWindow.IsVisible)
                {
                    if (request == DwgClashRequest.LoadLayers) _setupWindow.ResetImportSelection();
                    _setupWindow.ShowValidationError(ex.Message);
                }
                else if (_resultsWindow != null && _resultsWindow.IsVisible)
                    _resultsWindow.SetStatus("Erro: " + ex.Message);
                else
                    TaskDialog.Show("Erro - Clash 2D DWG x Revit", ex.Message);
            }
        }

        public string GetName() => "Clash 2D DWG x Revit";

        private void LoadLayers()
        {
            if (_setupWindow == null)
                return;
            ImportInstance importInstance = _setupWindow.SelectedImport;
            if (importInstance == null)
                return;

            using (var progress = new ClashProgress("Carregando layers do DWG"))
            {
                var layers = new LayerCollector().GetLayers(importInstance.Document, importInstance, progress.Report, progress.Token);
                progress.Token.ThrowIfCancellationRequested();
                _setupWindow.SetLayers(layers);
            }
        }

        private void Analyze(UIApplication application)
        {
            if (_setupWindow == null)
                return;

            DwgClashSettings settings = _setupWindow.BuildSettings();
            DwgClashReport report;
            DwgClashResultsWindow results;
            using (var progress = new ClashProgress("Clash 2D DWG x Revit"))
            {
                DwgGeometryReader geometryReader = new DwgGeometryReader();
                var regions = new DwgRegionBuilder().Build(
                    geometryReader.ReadRegions(settings.ImportInstance.Document, settings.View,
                        settings.ImportInstance, settings.Layer, progress.Report, progress.Token),
                    progress.Report, progress.Token);
                var revitElements = new RevitElementCollector().Collect(
                    settings.View.Document, settings.View, settings.CategoryIds, progress.Report, progress.Token);
                report = new ResultGenerator().Generate(settings, revitElements, regions, progress.Report, progress.Token);
                progress.BeginFinishing();
                results = new DwgClashResultsWindow(settings, report, this);
            }

            RevitWindowOwner.Attach(results, application);
            AttachResults(results);
            _setupWindow.Close();
            results.Show();
        }

        private void Select(UIApplication application)
        {
            DwgClashResult result = _resultsWindow?.SelectedResultForExternalEvent;
            if (result == null)
                return;

            UIDocument uiDocument = application.ActiveUIDocument;
            uiDocument.Selection.SetElementIds(new[] { result.Element.ElementId });
            uiDocument.ShowElements(new[] { result.Element.ElementId });
            _resultsWindow.SetStatus("Elemento " + result.ElementId + " selecionado e enquadrado.");
        }

        private void CreateViewMarkers(UIApplication application)
        {
            if (_resultsWindow == null) return;
            var settings = _resultsWindow.Settings;
            UIDocument ui = application.ActiveUIDocument;
            if (settings.View == null || !settings.View.IsValidObject || ui?.Document == null || !ui.Document.Equals(settings.View.Document))
                throw new InvalidOperationException("Ative o projeto usado na análise para criar os avisos.");
            if (!(settings.View is ViewPlan sourceView))
                throw new InvalidOperationException("A análise precisa ter sido feita em uma vista de planta para criar os avisos.");
            if (_resultsWindow.Report.Results.Count == 0)
                throw new InvalidOperationException("Não há conflitos para marcar.");
            XYZ origin = sourceView.Origin ?? XYZ.Zero;
            XYZ right = sourceView.RightDirection.Normalize();
            XYZ up = sourceView.UpDirection.Normalize();
            var markers = _resultsWindow.Report.Results.Select(result =>
            {
                var center = DwgMarkerPoint.ForResult(result.Element, result.Region);
                return new LinkConflictItem { HostElementId = result.ElementId, MarkerPoint = origin + right * center.X + up * center.Y };
            }).ToList();
            ViewPlan view = new LinkConflictMarkerService().CreateInCopiedView(ui.Document, sourceView, markers, out int created, "Clash 2D DWG", true);
            ui.ActiveView = view;
            _resultsWindow.SetStatus($"{created} aviso(s) criado(s) na cópia '{view.Name}'. As posições são aproximadas.");
        }
    }
}


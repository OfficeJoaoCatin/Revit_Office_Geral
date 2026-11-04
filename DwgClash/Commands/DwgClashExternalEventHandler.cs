using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.DwgClash.Models;
using RevitFamilyImporter.DwgClash.Services;
using RevitFamilyImporter.DwgClash.Views;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter.DwgClash.Commands
{
    public enum DwgClashRequest
    {
        LoadLayers,
        Analyze,
        Select,
        Highlight,
        ClearHighlights
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
                    case DwgClashRequest.Highlight:
                        Highlight(application);
                        break;
                    case DwgClashRequest.ClearHighlights:
                        ClearHighlights();
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_setupWindow != null && _setupWindow.IsVisible)
                    _setupWindow.ShowValidationError(ex.Message);
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

            var layers = new LayerCollector().GetLayers(importInstance.Document, importInstance);
            _setupWindow.SetLayers(layers);
        }

        private void Analyze(UIApplication application)
        {
            if (_setupWindow == null)
                return;

            DwgClashSettings settings = _setupWindow.BuildSettings();
            DwgGeometryReader geometryReader = new DwgGeometryReader();
            var regions = new DwgRegionBuilder().Build(
                geometryReader.ReadRegions(settings.ImportInstance.Document, settings.View,
                    settings.ImportInstance, settings.Layer));
            var revitElements = new RevitElementCollector().Collect(
                settings.View.Document, settings.View, settings.CategoryIds);
            DwgClashReport report = new ResultGenerator().Generate(settings, revitElements, regions);

            DwgClashResultsWindow results = new DwgClashResultsWindow(settings, report,
                new HighlightService(), this);
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

        private void Highlight(UIApplication application)
        {
            if (_resultsWindow == null)
                return;
            _resultsWindow.HighlightService.Apply(application, _resultsWindow.Settings.View,
                _resultsWindow.Settings.ImportInstance, _resultsWindow.Report);
            _resultsWindow.SetStatus("Conflitos destacados em vermelho e DWG destacado em amarelo.");
        }

        private void ClearHighlights()
        {
            if (_resultsWindow == null)
                return;
            _resultsWindow.HighlightService.Clear(_resultsWindow.Settings.View);
            _resultsWindow.SetStatus("Destaques removidos e overrides originais restaurados.");
        }
    }
}


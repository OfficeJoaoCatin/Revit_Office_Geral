using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitFamilyImporter.LinkConflict.Models;
using RevitFamilyImporter.LinkConflict.Services;
using RevitFamilyImporter.LinkConflict.Views;
using RevitFamilyImporter.UI;

namespace RevitFamilyImporter.LinkConflict.Commands
{
    public enum LinkConflictRequest
    {
        None,
        Analyze,
        LoadCategories,
        SelectHost,
        ShowHost,
        SelectLinked,
        ShowLinked,
        CreateViewMarkers
    }

    public sealed class LinkConflictExternalEventHandler : IExternalEventHandler
    {
        private ExternalEvent _externalEvent;
        private LinkConflictSetupWindow _setup;
        private LinkConflictResultsWindow _results;
        private LinkConflictReport _report;
        private LinkConflictRequest _request;
        private RevitLinkInstance _linkInstance;
        private IReadOnlyList<LinkedCategoryOption> _categories;
        private LinkedModelOption _categoriesLink;
        private readonly Dictionary<Document, IReadOnlyList<LinkedCategoryOption>> _categoryCache =
            new Dictionary<Document, IReadOnlyList<LinkedCategoryOption>>();
        private LinkConflictItem _item;
        private string _hostCategoryFilter;

        public void Attach(ExternalEvent externalEvent)
        {
            _externalEvent = externalEvent;
        }

        public void AttachSetup(LinkConflictSetupWindow setup)
        {
            _setup = setup;
        }

        public void RequestCategories(LinkedModelOption link)
        {
            _categoriesLink = link ?? throw new ArgumentNullException(nameof(link));
            Raise(LinkConflictRequest.LoadCategories);
        }

        public void RequestAnalyze(RevitLinkInstance linkInstance, IReadOnlyList<LinkedCategoryOption> categories)
        {
            _linkInstance = linkInstance;
            _categories = categories.ToList();
            Raise(LinkConflictRequest.Analyze);
        }

        public void Request(LinkConflictRequest request, LinkConflictItem item)
        {
            _item = item;
            Raise(request);
        }

        public void Request(LinkConflictRequest request)
        {
            Raise(request);
        }

        public void Request(LinkConflictRequest request, string hostCategoryFilter)
        {
            _hostCategoryFilter = hostCategoryFilter;
            Raise(request);
        }

        public void Close()
        {
            try
            {
                _externalEvent?.Dispose();
            }
            catch
            {
                // O evento pode ja ter sido liberado pelo Revit.
            }
        }

        public void Execute(UIApplication app)
        {
            LinkConflictRequest request = _request;
            _request = LinkConflictRequest.None;

            try
            {
                switch (request)
                {
                    case LinkConflictRequest.Analyze:
                        Analyze(app);
                        break;
                    case LinkConflictRequest.LoadCategories:
                        LoadCategories(app);
                        break;
                    case LinkConflictRequest.SelectHost:
                        SelectHost(app);
                        break;
                    case LinkConflictRequest.ShowHost:
                        ShowHost(app);
                        break;
                    case LinkConflictRequest.SelectLinked:
                        SelectLinked(app);
                        break;
                    case LinkConflictRequest.ShowLinked:
                        ShowLinked(app);
                        break;
                    case LinkConflictRequest.CreateViewMarkers:
                        CreateViewMarkers(app);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                if (request == LinkConflictRequest.Analyze && _setup != null)
                {
                    _setup.Show();
                    _setup.SetAnalysisStatus("Análise cancelada. Nenhum resultado parcial foi aplicado.");
                }
                else if (request == LinkConflictRequest.LoadCategories)
                    _setup?.CategoriesLoadFailed("Carregamento cancelado. Selecione o link novamente.");
            }
            catch (Exception ex)
            {
                if (request == LinkConflictRequest.LoadCategories)
                    _setup?.CategoriesLoadFailed(ex.Message);
                else
                {
                    TaskDialog.Show("Erro - Conflitos em Link", ex.Message);
                    if (request == LinkConflictRequest.Analyze && _setup != null)
                        _setup.Show();
                }
            }
        }

        public string GetName()
        {
            return "Conflitos em Link - Operacoes do modelo";
        }

        private void LoadCategories(UIApplication app)
        {
            LinkedModelOption option = _categoriesLink;
            if (option?.LinkInstance == null || !option.LinkInstance.IsValidObject)
                throw new InvalidOperationException("A instância do link não está mais disponível. Abra o comando novamente.");
            EnsureActiveHostDocument(app, option.LinkInstance);
            Document linkedDocument = option.LinkInstance.GetLinkDocument();
            if (linkedDocument == null)
                throw new InvalidOperationException("O modelo vinculado está descarregado ou inacessível.");
            if (!_categoryCache.TryGetValue(linkedDocument, out IReadOnlyList<LinkedCategoryOption> categories))
            {
                using (var progress = new ClashProgress("Carregando categorias do link"))
                {
                    categories = new LinkConflictAnalysisService().GetCategories(linkedDocument, progress.Report, progress.Token);
                    progress.Token.ThrowIfCancellationRequested();
                }
                _categoryCache[linkedDocument] = categories;
            }
            _setup?.CategoriesLoaded(option, categories);
        }

        private void Analyze(UIApplication app)
        {
            if (_linkInstance == null || _categories == null || _categories.Count == 0)
                return;
            if (!_linkInstance.IsValidObject)
                throw new InvalidOperationException("A instância do link não está mais disponível. Abra o comando novamente.");
            EnsureActiveHostDocument(app, _linkInstance);
            LinkConflictAnalysisService service = new LinkConflictAnalysisService();
            using (var progress = new ClashProgress("Conflitos em Links"))
            {
                _report = service.Analyze(app.ActiveUIDocument.Document, _linkInstance, _categories, progress.Report, progress.Token);
                progress.BeginFinishing();
                _results = new LinkConflictResultsWindow(app, _linkInstance, _report, this);
            }
            RevitWindowOwner.Attach(_results, app);
            _results.Show();
            _setup?.CloseForResults();
        }

        private static void EnsureActiveHostDocument(UIApplication app, RevitLinkInstance link)
        {
            Document activeDocument = app.ActiveUIDocument?.Document;
            if (activeDocument == null)
                throw new InvalidOperationException("Nenhum documento está ativo no Revit. Ative o projeto hospedeiro e tente novamente.");
            // Wrappers diferentes podem representar o mesmo documento nativo do Revit.
            if (!activeDocument.Equals(link.Document))
                throw new InvalidOperationException("O documento ativo mudou. Volte ao projeto que contém o link e tente novamente.");
        }

        private void CreateViewMarkers(UIApplication app)
        {
            if (_report == null)
                return;

            ViewPlan view = app.ActiveUIDocument.ActiveView as ViewPlan;
            if (view == null)
                throw new InvalidOperationException("Ative uma vista de planta de piso para criar os avisos visuais.");

            LinkConflictMarkerService service = new LinkConflictMarkerService();
            IReadOnlyList<LinkConflictItem> conflicts = string.IsNullOrWhiteSpace(_hostCategoryFilter)
                ? _report.Conflicts
                : _report.Conflicts
                    .Where(item => string.Equals(item.HostCategory, _hostCategoryFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            int created;
            ViewPlan markerView = service.CreateInCopiedView(
                app.ActiveUIDocument.Document,
                view,
                conflicts,
                out created);
            app.ActiveUIDocument.ActiveView = markerView;
            string categoryText = string.IsNullOrWhiteSpace(_hostCategoryFilter) ? "todas as categorias" : _hostCategoryFilter;
            _results?.SetMarkerStatus($"{created} aviso(s) visual(is) criado(s) para {categoryText} na cópia '{markerView.Name}'. A vista original não foi alterada.");
        }

        private void SelectHost(UIApplication app)
        {
            if (_item == null)
                return;
            app.ActiveUIDocument.Selection.SetElementIds(new[] { new ElementId(_item.HostElementId) });
        }

        private void ShowHost(UIApplication app)
        {
            if (_item == null)
                return;
            ElementId hostId = new ElementId(_item.HostElementId);
            app.ActiveUIDocument.Selection.SetElementIds(new[] { hostId });
            app.ActiveUIDocument.ShowElements(hostId);
        }

        private void SelectLinked(UIApplication app)
        {
            if (_item == null || _linkInstance == null)
                return;

            Document linkedDocument = _linkInstance.GetLinkDocument();
            Element linkedElement = linkedDocument?.GetElement(new ElementId(_item.LinkedElementId));
            if (linkedElement == null)
                throw new InvalidOperationException("O elemento do link nao esta disponivel.");

            Reference linkedReference = new Reference(linkedElement).CreateLinkReference(_linkInstance);
            app.ActiveUIDocument.Selection.SetReferences(new[] { linkedReference });
        }

        private void ShowLinked(UIApplication app)
        {
            if (_linkInstance == null)
                return;
            app.ActiveUIDocument.Selection.SetElementIds(new[] { _linkInstance.Id });
            app.ActiveUIDocument.ShowElements(_linkInstance.Id);
        }

        private void Raise(LinkConflictRequest request)
        {
            _request = request;
            ExternalEventRequest result = _externalEvent.Raise();
            if (result == ExternalEventRequest.Denied)
                throw new InvalidOperationException("O Revit nao aceitou a operacao neste momento. Tente novamente.");
        }
    }
}



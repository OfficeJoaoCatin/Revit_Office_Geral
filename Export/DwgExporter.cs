using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Services;

namespace RevitFamilyImporter.Export
{
    public class DwgExporter : IDisposable
    {
        private readonly ExportSettingsService _settingsService = new ExportSettingsService();
        private readonly Dictionary<Document, (string Name, string Error)> _copies = new Dictionary<Document, (string, string)>();
        private DWGExportOptions? _sourceOptions;
        private string? _sourceError;

        public string Export(ExportJob job, BatchExportRequest request)
        {
            Directory.CreateDirectory(job.OutputDirectory);

            using DWGExportOptions options = request.CopyDwgSetupToProjects
                ? GetCopiedOptions(job, request)
                : _settingsService.CreateDwgOptions(job.Document, request.DwgSettings, request.ForceGlobalDwgSettings);

            ICollection<ElementId> viewIds = job.Sheets
                .Select(sheet => sheet.Id ?? new ElementId((int)sheet.IdValue))
                .ToList();

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(job.OutputFileName);
            bool exported = job.Document.Export(job.OutputDirectory, fileNameWithoutExtension, viewIds, options);
            if (!exported)
                throw new InvalidOperationException("A API do Revit retornou falha ao exportar DWG.");

            return Path.Combine(job.OutputDirectory, job.OutputFileName);
        }

        private DWGExportOptions GetCopiedOptions(ExportJob job, BatchExportRequest request)
        {
            Document source = request.DwgSetupSourceDocument ?? throw new InvalidOperationException("Selecione o projeto de origem do setup DWG.");
            if (!source.IsValidObject) throw new InvalidOperationException("O projeto de origem do setup DWG não está disponível.");
            string sourceName = request.DwgSettings.Name;
            if (_sourceError != null) throw new InvalidOperationException("Não foi possível ler o setup DWG de origem: " + _sourceError);
            if (_sourceOptions == null)
            {
                try
                {
                    ExportDWGSettings sourceSettings = ExportDWGSettings.FindByName(source, sourceName)
                        ?? throw new InvalidOperationException($"O setup DWG '{sourceName}' não existe no projeto de origem.");
                    _sourceOptions = sourceSettings.GetDWGExportOptions()
                        ?? throw new InvalidOperationException("O Revit não retornou as opções do setup.");
                    if (!Enum.TryParse(request.DwgSettings.Version, out ACADVersion version) || !Enum.IsDefined(typeof(ACADVersion), version))
                        throw new InvalidOperationException("Selecione uma versão DWG válida.");
                    // GetDWGExportOptions devolve uma cópia: o setup original não é alterado.
                    _sourceOptions.FileVersion = version;
                    _sourceOptions.MergedViews = request.DwgSettings.MergeViews;
                }
                catch (Exception ex)
                {
                    _sourceOptions?.Dispose();
                    _sourceOptions = null;
                    _sourceError = ex.Message;
                    request.DwgSetupMessage?.Invoke($"Projeto de origem: não foi possível ler o setup DWG '{sourceName}': {ex.Message}");
                    throw new InvalidOperationException("Não foi possível ler o setup DWG de origem: " + ex.Message, ex);
                }
            }
            if (job.Document.Equals(source))
            {
                if (!_copies.ContainsKey(job.Document))
                {
                    _copies[job.Document] = (sourceName, "");
                    request.DwgSetupMessage?.Invoke($"{job.DocumentInfo.ProjectName}: usado setup de origem '{sourceName}' com versão e mesclagem escolhidas; setup original preservado.");
                }
                return new DWGExportOptions(_sourceOptions);
            }
            if (!_copies.TryGetValue(job.Document, out var result))
            {
                try
                {
                    string copiedName = DwgSetupNamePolicy.Choose(sourceName, ExportDWGSettings.ListNames(job.Document));
                    using (var transaction = new Transaction(job.Document, "Copiar setup DWG - Exportar Lote"))
                    {
                        if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Não foi possível iniciar a transação.");
                        using (var options = new DWGExportOptions(_sourceOptions))
                            ExportDWGSettings.Create(job.Document, copiedName, options);
                        if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("O Revit não confirmou a cópia do setup.");
                    }
                    result = (copiedName, "");
                    _copies.Add(job.Document, result);
                    request.DwgSetupMessage?.Invoke($"{job.DocumentInfo.ProjectName}: setup '{copiedName}' copiado; salve ou sincronize o projeto para preservá-lo.");
                }
                catch (Exception ex)
                {
                    result = ("", ex.Message);
                    _copies[job.Document] = result;
                    request.DwgSetupMessage?.Invoke($"{job.DocumentInfo.ProjectName}: não foi possível copiar o setup DWG: {ex.Message}");
                }
            }
            if (!string.IsNullOrEmpty(result.Error))
                throw new InvalidOperationException("Exportação DWG não executada porque a cópia do setup falhou: " + result.Error);
            return DWGExportOptions.GetPredefinedOptions(job.Document, result.Name);
        }

        public void Dispose() { _sourceOptions?.Dispose(); }
    }
}

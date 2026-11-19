using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Export;

static class Program
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Main()
    {
        Check(DwgSetupNamePolicy.Choose("Office", new[] { "office", "OFFICE - Exportar Lote" }) == "Office - Exportar Lote (2)", "Nomes existentes devem ser preservados sem depender de maiúsculas.");
        var source = new Document(); source.Setups["Office"] = new DWGExportOptions { FileVersion = ACADVersion.R2018, Layers = "originais", MergedViews = false };
        var target = new Document(); target.Setups["Office"] = new DWGExportOptions { FileVersion = ACADVersion.R2007, Layers = "existentes", MergedViews = true };
        var failed = new Document { FailCreate = true };
        var messages = new List<string>();
        var request = new BatchExportRequest { CopyDwgSetupToProjects = true, DwgSetupSourceDocument = source, DwgSettings = new Settings { Name = "Office", Version = "R2010", MergeViews = true }, DwgSetupMessage = messages.Add };
        string folder = Path.Combine(Path.GetTempPath(), "office-dwg-setup-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var exporter = new DwgExporter();
            exporter.Export(Job(source, folder), request);
            Check(source.CreateCount == 0 && source.LastOptions!.Layers == "originais", "O projeto de origem deve exportar o próprio setup sem modificá-lo.");
            Check(source.LastOptions.FileVersion == ACADVersion.R2010 && source.LastOptions.MergedViews, "A exportação de origem deve respeitar as opções escolhidas.");
            Check(source.Setups["Office"].FileVersion == ACADVersion.R2018 && !source.Setups["Office"].MergedViews, "O setup original não pode ser modificado.");
            source.Setups["Office"].Layers = "alterado durante o lote";
            exporter.Export(Job(target, folder), request);
            exporter.Export(Job(target, folder), request);
            Check(target.CreateCount == 1 && target.ExportCount == 2, "O setup deve ser copiado apenas uma vez por projeto no lote.");
            Check(target.Setups["Office"].Layers == "existentes", "O setup existente não pode ser substituído.");
            Check(target.Setups["Office - Exportar Lote"].Layers == "originais" && target.LastOptions!.FileVersion == ACADVersion.R2010 && target.LastOptions.MergedViews && target.Setups["Office - Exportar Lote"].FileVersion == ACADVersion.R2010 && target.Setups["Office - Exportar Lote"].MergedViews, "O setup copiado e a exportação devem usar as opções escolhidas.");
            try { exporter.Export(Job(failed, folder), request); throw new Exception("A falha deveria impedir a exportação."); }
            catch (InvalidOperationException) { }
            try { exporter.Export(Job(failed, folder), request); throw new Exception("A falha deveria impedir a segunda exportação."); }
            catch (InvalidOperationException) { }
            Check(failed.ExportCount == 0, "Não pode exportar silenciosamente com outro setup quando a cópia falhar.");
            Check(messages.Count == 3, "O relatório deve informar origem, cópia e falha por projeto.");
            var invalidTarget = new Document();
            var invalidRequest = new BatchExportRequest { CopyDwgSetupToProjects = true, DwgSetupSourceDocument = source, DwgSettings = new Settings { Name = "Office", Version = "invalida" } };
            using var invalidExporter = new DwgExporter();
            try { invalidExporter.Export(Job(invalidTarget, folder), invalidRequest); throw new Exception("Uma versão inválida deveria impedir a exportação."); }
            catch (InvalidOperationException) { }
            Check(invalidTarget.CreateCount == 0 && invalidTarget.ExportCount == 0, "Versão inválida não pode gerar setup ou exportação silenciosa.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("Cópia, preservação de setup existente e falha segura verificadas.");
    }
    static ExportJob Job(Document doc, string folder) => new ExportJob { Document = doc, OutputDirectory = folder, OutputFileName = "folha.dwg", DocumentInfo = new DocumentInfo { ProjectName = "Projeto" }, Sheets = new List<Sheet> { new Sheet { Id = new ElementId(1) } } };
}

namespace RevitFamilyImporter.Export
{
    public class Settings { public string Name { get; set; } = ""; public string Version { get; set; } = "Default"; public bool MergeViews { get; set; } }
    public class BatchExportRequest { public bool CopyDwgSetupToProjects; public Document? DwgSetupSourceDocument; public Settings DwgSettings = new(); public bool ForceGlobalDwgSettings; public Action<string>? DwgSetupMessage; }
    public class DocumentInfo { public string ProjectName { get; set; } = ""; }
    public class Sheet { public ElementId? Id; public long IdValue; }
    public class ExportJob { public Document Document = null!; public DocumentInfo DocumentInfo = new(); public List<Sheet> Sheets = new(); public string OutputDirectory = ""; public string OutputFileName = ""; }
}
namespace RevitFamilyImporter.Services
{
    public class ExportSettingsService { public DWGExportOptions CreateDwgOptions(Document doc, RevitFamilyImporter.Export.Settings settings, bool force) => new(); }
}
namespace Autodesk.Revit.DB
{
    public class ElementId { public ElementId(int id) {} }
    public enum ACADVersion { Default, R2007, R2010, R2018 }
    public enum TransactionStatus { Started, Committed }
    public class Transaction : IDisposable { public Transaction(Document document, string name) {} public TransactionStatus Start() => TransactionStatus.Started; public TransactionStatus Commit() => TransactionStatus.Committed; public void Dispose() {} }
    public class DWGExportOptions : IDisposable
    {
        public ACADVersion FileVersion; public string Layers = ""; public bool MergedViews;
        public DWGExportOptions() {} public DWGExportOptions(DWGExportOptions other) { FileVersion=other.FileVersion; Layers=other.Layers; MergedViews=other.MergedViews; }
        public static DWGExportOptions GetPredefinedOptions(Document doc, string name) => new(doc.Setups[name]);
        public void Dispose() {}
    }
    public class Document
    {
        public bool IsValidObject = true, FailCreate; public int CreateCount, ExportCount; public DWGExportOptions? LastOptions;
        public Dictionary<string, DWGExportOptions> Setups = new(StringComparer.OrdinalIgnoreCase);
        public bool Export(string folder, string name, ICollection<ElementId> views, DWGExportOptions options) { ExportCount++; LastOptions = new(options); return true; }
    }
    public class ExportDWGSettings
    {
        private readonly DWGExportOptions _options;
        private ExportDWGSettings(DWGExportOptions options) { _options = options; }
        public static ExportDWGSettings? FindByName(Document doc, string name) => doc.Setups.TryGetValue(name, out var options) ? new(options) : null;
        public static ICollection<string> ListNames(Document doc) => doc.Setups.Keys;
        public DWGExportOptions GetDWGExportOptions() => new(_options);
        public static ExportDWGSettings Create(Document doc, string name, DWGExportOptions options) { if (doc.FailCreate) throw new InvalidOperationException("cópia recusada"); doc.CreateCount++; doc.Setups.Add(name, new(options)); return new(options); }
    }
}

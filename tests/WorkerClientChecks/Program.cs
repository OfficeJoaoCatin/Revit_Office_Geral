using System.Diagnostics;
using System.Reflection;
using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Services.DetailImport;
namespace RevitFamilyImporter.Models.DetailImport
{
    public class DwgDocumentModel { public string FilePath { get; set; } }
    public class DetailRegion { public int Id { get; set; } public string Name { get; set; } public DwgBounds Bounds { get; set; } }
}
namespace RevitFamilyImporter.Services.DetailImport
{
    public interface ILogger { void Info(string message); void Warn(string message); }
    public class Logger : ILogger { public Logger(string scope) {} public void Info(string message) {} public void Warn(string message) {} }
}
class Program
{
    static void Main()
    {
        var region = new DetailRegion { Id=1, Name="Teste ç", Bounds=new DwgBounds(0,0,1,1) };
        using (var client = new GstarCadDetailExporter(new Logger("test")))
        {
            string path;
            if (!client.TryExport(new DwgDocumentModel { FilePath="ok" }, region, Path.GetTempPath(), "", "", 100, CancellationToken.None, out path) || !File.Exists(path))
                throw new Exception("Resultado de sucesso inválido.");
            File.Delete(path);
        }
        using (var client = new GstarCadDetailExporter(new Logger("test")))
        using (var cancel = new CancellationTokenSource(500))
        {
            var watch = Stopwatch.StartNew();
            try { string path; client.TryExport(new DwgDocumentModel { FilePath="hang" }, region, Path.GetTempPath(), "", "", 100, cancel.Token, out path); throw new Exception("Cancelamento não ocorreu."); }
            catch (OperationCanceledException) { }
            if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new Exception("Cancelamento demorou demais.");
        }
        using (var client = new GstarCadDetailExporter(new Logger("test")))
        {
            Task<bool> call = Task.Run(() =>
            {
                try { string path; client.TryExport(new DwgDocumentModel { FilePath="hang" }, region, Path.GetTempPath(), "", "", 100, CancellationToken.None, out path); return false; }
                catch (InvalidOperationException) { return true; }
            });
            var workerField = typeof(GstarCadDetailExporter).GetField("_worker", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Process worker = null;
            if (!SpinWait.SpinUntil(() => (worker = (Process)workerField.GetValue(client)) != null, 5000))
                throw new Exception("O auxiliar simulado não iniciou.");
            worker.Kill();
            if (!call.Wait(5000) || !call.Result) throw new Exception("Falha ao reagir ao encerramento do auxiliar.");
        }
        using (var client = new GstarCadDetailExporter(new Logger("test"), 400))
        {
            var watch = Stopwatch.StartNew();
            try { string path; client.TryExport(new DwgDocumentModel { FilePath="hang" }, region, Path.GetTempPath(), "", "", 100, CancellationToken.None, out path); throw new Exception("Tempo limite não ocorreu."); }
            catch (TimeoutException) { }
            if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new Exception("Tempo limite demorou demais.");
        }
        using (var client = new GstarCadDetailExporter(new Logger("test")))
        {
            try { string path; client.TryExport(new DwgDocumentModel { FilePath="error" }, region, Path.GetTempPath(), "", "", 100, CancellationToken.None, out path); throw new Exception("Erro não propagado."); }
            catch (InvalidOperationException ex) when (ex.Message == "erro simulado") { }
            string retryPath;
            if (!client.TryExport(new DwgDocumentModel { FilePath="ok" }, region, Path.GetTempPath(), "", "", 100, CancellationToken.None, out retryPath))
                throw new Exception("O auxiliar não foi reaproveitado após a falha de um detalhe.");
            File.Delete(retryPath);
        }
        Console.WriteLine("Sucesso, cancelamento, tempo limite, falha e encerramento do auxiliar verificados.");
    }
}

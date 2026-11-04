using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Services
{
    public class ExportSettingsService
    {
        public List<PdfExportSettingsInfo> LoadPdfSettings(Document document)
        {
            List<PdfExportSettingsInfo> result = new List<PdfExportSettingsInfo>();

            try
            {
                foreach (string name in ExportPDFSettings.ListNames(document).OrderBy(name => name))
                {
                    ExportPDFSettings? settings = ExportPDFSettings.FindByName(document, name);
                    PDFExportOptions options = settings?.GetOptions() ?? new PDFExportOptions();
                    result.Add(CreatePdfInfo(name, options));
                }

                if (result.Count == 0)
                {
                    ExportPDFSettings? active = ExportPDFSettings.GetActivePredefinedSettings(document);
                    if (active != null)
                        result.Add(CreatePdfInfo(active.Name, active.GetOptions()));
                }
            }
            catch
            {
                result.Clear();
            }

            if (result.Count == 0)
                result.Add(new PdfExportSettingsInfo { Name = "Padrao" });

            return result;
        }

        public List<DwgExportSettingsInfo> LoadDwgSettings(Document document)
        {
            List<DwgExportSettingsInfo> result = new List<DwgExportSettingsInfo>();

            try
            {
                foreach (string name in ExportDWGSettings.ListNames(document).OrderBy(name => name))
                {
                    DWGExportOptions options = DWGExportOptions.GetPredefinedOptions(document, name);
                    result.Add(CreateDwgInfo(name, options));
                }

                if (result.Count == 0)
                {
                    ExportDWGSettings? active = ExportDWGSettings.GetActivePredefinedSettings(document);
                    if (active != null)
                        result.Add(CreateDwgInfo(active.Name, active.GetDWGExportOptions()));
                }
            }
            catch
            {
                result.Clear();
            }

            if (result.Count == 0)
                result.Add(new DwgExportSettingsInfo { Name = "Padrao" });

            return result;
        }

        public PDFExportOptions CreatePdfOptions(
            Document document,
            PdfExportSettingsInfo settings,
            bool useDocumentSettings,
            string outputFileName,
            bool combine)
        {
            PDFExportOptions options = new PDFExportOptions();

            if (useDocumentSettings)
            {
                options = TryGetPdfOptions(document, settings.Name) ?? options;
            }

            options.FileName = System.IO.Path.GetFileNameWithoutExtension(outputFileName);
            options.Combine = combine;
            options.StopOnError = false;
            options.AlwaysUseRaster = !settings.VectorMode;
            options.ExportQuality = ToPdfQuality(settings.DPI);
            options.RasterQuality = RasterQualityType.High;
            options.PaperPlacement = settings.Center ? PaperPlacementType.Center : PaperPlacementType.LowerLeft;
            options.ZoomType = ZoomType.FitToPage;

            return options;
        }

        public DWGExportOptions CreateDwgOptions(
            Document document,
            DwgExportSettingsInfo settings,
            bool forceGlobalSettings)
        {
            DWGExportOptions options = null!;

            if (!string.IsNullOrWhiteSpace(settings.Name) && settings.Name != "Padrao")
            {
                try
                {
                    options = DWGExportOptions.GetPredefinedOptions(document, settings.Name);
                }
                catch
                {
                    options = null!;
                }
            }

            if (options == null && !forceGlobalSettings)
            {
                try
                {
                    ExportDWGSettings? active = ExportDWGSettings.GetActivePredefinedSettings(document);
                    options = active?.GetDWGExportOptions() ?? new DWGExportOptions();
                }
                catch
                {
                    options = new DWGExportOptions();
                }
            }

            options ??= new DWGExportOptions();

            if (Enum.TryParse(settings.Version, out ACADVersion version))
                options.FileVersion = version;

            options.MergedViews = settings.MergeViews;
            return options;
        }

        private static PDFExportOptions? TryGetPdfOptions(Document document, string setupName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(setupName) && setupName != "Padrao")
                {
                    ExportPDFSettings? settings = ExportPDFSettings.FindByName(document, setupName);
                    if (settings != null)
                        return settings.GetOptions();
                }

                ExportPDFSettings? active = ExportPDFSettings.GetActivePredefinedSettings(document);
                return active?.GetOptions();
            }
            catch
            {
                return null;
            }
        }

        private static PdfExportSettingsInfo CreatePdfInfo(string name, PDFExportOptions options)
        {
            return new PdfExportSettingsInfo
            {
                Name = name,
                Combine = options.Combine,
                DPI = FromPdfQuality(options.ExportQuality),
                Center = options.PaperPlacement == PaperPlacementType.Center,
                VectorMode = !options.AlwaysUseRaster
            };
        }

        private static DwgExportSettingsInfo CreateDwgInfo(string name, DWGExportOptions options)
        {
            return new DwgExportSettingsInfo
            {
                Name = name,
                Version = options.FileVersion.ToString(),
                MergeViews = options.MergedViews,
                ExportLinks = false
            };
        }

        private static PDFExportQualityType ToPdfQuality(int dpi)
        {
            if (dpi <= 72) return PDFExportQualityType.DPI72;
            if (dpi <= 144) return PDFExportQualityType.DPI144;
            if (dpi <= 300) return PDFExportQualityType.DPI300;
            if (dpi <= 600) return PDFExportQualityType.DPI600;
            if (dpi <= 1200) return PDFExportQualityType.DPI1200;
            if (dpi <= 2400) return PDFExportQualityType.DPI2400;
            if (dpi <= 3600) return PDFExportQualityType.DPI3600;
            return PDFExportQualityType.DPI4000;
        }

        private static int FromPdfQuality(PDFExportQualityType quality)
        {
            string raw = quality.ToString().Replace("DPI", string.Empty);
            return int.TryParse(raw, out int dpi) ? dpi : 300;
        }
    }
}

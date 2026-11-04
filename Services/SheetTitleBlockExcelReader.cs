using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace RevitFamilyImporter.Services
{
    internal class SheetTitleBlockExcelReader
    {
        private const int MaxInputColumns = 39;
        private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public List<List<string>> ReadWorksheet(string filePath, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("A Lista Mestra informada nao foi encontrada.", filePath);

            string extension = Path.GetExtension(filePath);
            if (string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
                return ReadLegacyWorkbookWithExcel(filePath, sheetName);

            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Use uma planilha .xlsx, .xlsm ou .xls.");

            using (ZipArchive archive = ZipFile.OpenRead(filePath))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                HashSet<int> dateStyles = ReadDateStyleIndexes(archive);
                string worksheetPath = FindWorksheetPath(archive, sheetName);
                ZipArchiveEntry? worksheetEntry = archive.GetEntry(worksheetPath);
                if (worksheetEntry == null)
                    throw new InvalidOperationException($"A aba '{sheetName}' nao foi encontrada na planilha.");

                return ReadRows(worksheetEntry, sharedStrings, dateStyles);
            }
        }

        private static List<List<string>> ReadLegacyWorkbookWithExcel(string filePath, string sheetName)
        {
            Type? excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
                throw new InvalidOperationException("Para ler arquivos .xls, o Microsoft Excel precisa estar instalado nesta maquina.");

            object? excel = null;
            object? workbooks = null;
            object? workbook = null;
            object? worksheets = null;
            object? worksheet = null;
            object? usedRange = null;
            object? readRange = null;

            try
            {
                excel = Activator.CreateInstance(excelType);
                dynamic xl = excel!;
                xl.Visible = false;
                xl.DisplayAlerts = false;
                TrySetExcelProperty(xl, "ScreenUpdating", false);
                TrySetExcelProperty(xl, "EnableEvents", false);
                TrySetExcelProperty(xl, "AskToUpdateLinks", false);

                workbooks = xl.Workbooks;
                dynamic books = workbooks;
                workbook = books.Open(filePath, 0, true);
                dynamic book = workbook;
                worksheets = book.Worksheets;
                dynamic sheets = worksheets;

                int sheetCount = sheets.Count;
                for (int i = 1; i <= sheetCount; i++)
                {
                    object? candidate = sheets.Item[i];
                    dynamic dynCandidate = candidate!;
                    string candidateName = Convert.ToString(dynCandidate.Name, CultureInfo.InvariantCulture) ?? string.Empty;
                    if (string.Equals(candidateName, sheetName, StringComparison.OrdinalIgnoreCase))
                    {
                        worksheet = candidate;
                        break;
                    }

                    ReleaseComObject(candidate);
                }

                if (worksheet == null)
                    throw new InvalidOperationException($"A aba '{sheetName}' nao foi encontrada na planilha.");

                dynamic ws = worksheet;
                usedRange = ws.UsedRange;
                dynamic range = usedRange;

                int rowCount = range.Rows.Count;
                int columnCount = Math.Min(range.Columns.Count, MaxInputColumns);
                readRange = range.Resize[rowCount, columnCount];
                dynamic dataRange = readRange;
                object? rawValues = dataRange.Value2;
                rowCount = TrimLegacyTrailingRows(rawValues, rowCount, columnCount);
                var rows = new List<List<string>>();

                for (int row = 1; row <= rowCount; row++)
                {
                    var values = new List<string>();
                    for (int column = 1; column <= columnCount; column++)
                    {
                        string value = column == 1
                            ? GetLegacyCellText(range, row, column)
                            : FormatLegacyValue(GetLegacyValue(rawValues, row, column));
                        values.Add(value.Trim());
                    }

                    while (values.Count > 0 && string.IsNullOrWhiteSpace(values[values.Count - 1]))
                        values.RemoveAt(values.Count - 1);

                    rows.Add(values);
                }

                return rows;
            }
            finally
            {
                try
                {
                    if (workbook != null)
                    {
                        dynamic book = workbook;
                        book.Close(false);
                    }
                }
                catch { }

                try
                {
                    if (excel != null)
                    {
                        dynamic xl = excel;
                        xl.Quit();
                    }
                }
                catch { }

                ReleaseComObject(usedRange);
                ReleaseComObject(readRange);
                ReleaseComObject(worksheet);
                ReleaseComObject(worksheets);
                ReleaseComObject(workbook);
                ReleaseComObject(workbooks);
                ReleaseComObject(excel);
            }
        }

        private static void TrySetExcelProperty(dynamic excel, string name, object value)
        {
            try
            {
                if (name == "ScreenUpdating") excel.ScreenUpdating = value;
                else if (name == "EnableEvents") excel.EnableEvents = value;
                else if (name == "AskToUpdateLinks") excel.AskToUpdateLinks = value;
            }
            catch { }
        }

        private static string GetLegacyCellText(dynamic range, int row, int column)
        {
            object? cell = null;
            try
            {
                cell = range.Cells[row, column];
                dynamic dynCell = cell!;
                return Convert.ToString(dynCell.Text, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            finally
            {
                ReleaseComObject(cell);
            }
        }

        private static object? GetLegacyValue(object? values, int row, int column)
        {
            if (values is object[,] matrix)
                return matrix[row, column];

            return row == 1 && column == 1 ? values : null;
        }

        private static string FormatLegacyValue(object? value)
        {
            if (value == null) return string.Empty;

            if (value is DateTime date)
                return date.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));

            if (value is double doubleValue)
            {
                if (doubleValue >= 20000 && doubleValue <= 60000)
                {
                    try { return DateTime.FromOADate(doubleValue).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")); }
                    catch { }
                }

                return doubleValue.ToString("0.############", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static int TrimLegacyTrailingRows(object? values, int rowCount, int columnCount)
        {
            if (values is not object[,] matrix)
                return rowCount;

            for (int row = rowCount; row >= 1; row--)
            {
                for (int column = 1; column <= columnCount; column++)
                {
                    if (!string.IsNullOrWhiteSpace(FormatLegacyValue(matrix[row, column])))
                        return row;
                }
            }

            return 0;
        }

        private static void ReleaseComObject(object? value)
        {
            if (value == null) return;

            try
            {
                if (Marshal.IsComObject(value))
                    Marshal.FinalReleaseComObject(value);
            }
            catch { }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();

            using (Stream stream = entry.Open())
            {
                XDocument doc = XDocument.Load(stream);
                return doc.Descendants(SpreadsheetNs + "si")
                    .Select(si => string.Concat(si.Descendants(SpreadsheetNs + "t").Select(t => t.Value)))
                    .ToList();
            }
        }

        private static HashSet<int> ReadDateStyleIndexes(ZipArchive archive)
        {
            var dateStyles = new HashSet<int>();
            ZipArchiveEntry? entry = archive.GetEntry("xl/styles.xml");
            if (entry == null) return dateStyles;

            using (Stream stream = entry.Open())
            {
                XDocument doc = XDocument.Load(stream);
                var customDateFormats = doc.Descendants(SpreadsheetNs + "numFmt")
                    .Where(n => LooksLikeDateFormat((string?)n.Attribute("formatCode")))
                    .Select(n => TryParseInt((string?)n.Attribute("numFmtId")))
                    .Where(id => id >= 0)
                    .ToHashSet();

                XElement? cellXfs = doc.Descendants(SpreadsheetNs + "cellXfs").FirstOrDefault();
                if (cellXfs == null) return dateStyles;

                int index = 0;
                foreach (XElement xf in cellXfs.Elements(SpreadsheetNs + "xf"))
                {
                    int numFmtId = TryParseInt((string?)xf.Attribute("numFmtId"));
                    if (IsBuiltInDateFormat(numFmtId) || customDateFormats.Contains(numFmtId))
                        dateStyles.Add(index);
                    index++;
                }
            }

            return dateStyles;
        }

        private static string FindWorksheetPath(ZipArchive archive, string sheetName)
        {
            ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry? relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relsEntry == null)
                throw new InvalidOperationException("A planilha nao contem a estrutura esperada do Excel.");

            using Stream workbookStream = workbookEntry.Open();
            using Stream relsStream = relsEntry.Open();

            XDocument workbook = XDocument.Load(workbookStream);
            XDocument rels = XDocument.Load(relsStream);

            XElement? sheet = workbook.Descendants(SpreadsheetNs + "sheet")
                .FirstOrDefault(s => string.Equals((string?)s.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet == null)
                throw new InvalidOperationException($"A aba '{sheetName}' nao foi encontrada na planilha.");

            string relationshipId = (string?)sheet.Attribute(RelationshipNs + "id") ?? string.Empty;
            XElement? rel = rels.Descendants(PackageRelationshipNs + "Relationship")
                .FirstOrDefault(r => string.Equals((string?)r.Attribute("Id"), relationshipId, StringComparison.Ordinal));
            string target = (string?)rel?.Attribute("Target") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException($"Nao foi possivel localizar o arquivo interno da aba '{sheetName}'.");

            target = target.Replace('\\', '/').TrimStart('/');
            return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? target
                : "xl/" + target;
        }

        private static List<List<string>> ReadRows(
            ZipArchiveEntry worksheetEntry,
            IList<string> sharedStrings,
            ISet<int> dateStyles)
        {
            using Stream stream = worksheetEntry.Open();
            XDocument doc = XDocument.Load(stream);
            var rows = new List<List<string>>();

            foreach (XElement row in doc.Descendants(SpreadsheetNs + "row"))
            {
                var values = new Dictionary<int, string>();
                int maxColumn = -1;

                foreach (XElement cell in row.Elements(SpreadsheetNs + "c"))
                {
                    int columnIndex = GetColumnIndex((string?)cell.Attribute("r"));
                    if (columnIndex < 0) continue;
                    if (columnIndex >= MaxInputColumns) continue;

                    values[columnIndex] = ReadCellValue(cell, sharedStrings, dateStyles);
                    maxColumn = Math.Max(maxColumn, columnIndex);
                }

                var list = new List<string>();
                for (int i = 0; i <= maxColumn; i++)
                    list.Add(values.TryGetValue(i, out string value) ? value : string.Empty);

                rows.Add(list);
            }

            return rows;
        }

        private static string ReadCellValue(XElement cell, IList<string> sharedStrings, ISet<int> dateStyles)
        {
            string type = (string?)cell.Attribute("t") ?? string.Empty;
            string raw = cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;

            if (type == "s")
            {
                int index = TryParseInt(raw);
                return index >= 0 && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;
            }

            if (type == "inlineStr")
                return string.Concat(cell.Descendants(SpreadsheetNs + "t").Select(t => t.Value));

            if (type == "b")
                return raw == "1" ? "TRUE" : "FALSE";

            int styleIndex = TryParseInt((string?)cell.Attribute("s"));
            if (dateStyles.Contains(styleIndex) &&
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
            {
                try { return DateTime.FromOADate(serial).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")); }
                catch { }
            }

            return raw;
        }

        private static int GetColumnIndex(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference)) return -1;

            int index = 0;
            bool foundLetter = false;
            foreach (char ch in cellReference)
            {
                if (!char.IsLetter(ch)) break;
                foundLetter = true;
                index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }

            return foundLetter ? index - 1 : -1;
        }

        private static bool LooksLikeDateFormat(string? format)
        {
            if (string.IsNullOrWhiteSpace(format)) return false;

            string lower = format.ToLowerInvariant();
            return lower.Contains("yy") || lower.Contains("dd") || lower.Contains("mm/") || lower.Contains("/mm");
        }

        private static bool IsBuiltInDateFormat(int numFmtId)
        {
            return numFmtId >= 14 && numFmtId <= 22;
        }

        private static int TryParseInt(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : -1;
        }
    }
}

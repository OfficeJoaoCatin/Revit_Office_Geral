using RevitFamilyImporter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitFamilyImporter.Services
{
    internal class SheetTitleBlockDataService
    {
        public const string WorksheetName = "LMD";
        public const string ElectricalDiscipline = "Eletrica";
        public const string HydraulicDiscipline = "Hidraulica";
        public const string OtherDiscipline = "Outra";

        private const int SheetNumberIndex = 0;
        private const int SheetNameIndex = 3;
        private const int AddressIndex = 40;
        private const int CurrentRevisionIndex = 41;
        private const int ProjectNameIndex = 42;
        private const int WorkNameIndex = 43;
        private const int ClientIndex = 44;
        private const int DisciplineNameIndex = 45;
        private const int ProjectPhaseIndex = 46;
        private const int DesignerIndex = 47;
        private const int SupervisorIndex = 48;
        private const int IssueDateIndex = 49;
        private const int ResponsibleIndex = 50;

        private static readonly int[] RevisionDateIndexes = { 12, 15, 18, 21, 24, 27, 30, 33 };
        private static readonly string[] RevisionLabels = { "R00", "R01", "R02", "R03", "R04", "R05", "R06", "R07" };
        private static readonly Regex OnlyXRegex = new Regex(@"^[xX\-\s_/\\.]+$", RegexOptions.Compiled);
        private static readonly Regex XxPrefixRegex = new Regex(@"^xx($|[^A-Za-z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly SheetTitleBlockExcelReader _reader = new SheetTitleBlockExcelReader();

        public int LastRawRowCount { get; private set; }

        public List<SheetTitleBlockRow> LoadRows(string filePath, string discipline)
        {
            List<List<string>> allRows = _reader.ReadWorksheet(filePath, WorksheetName);
            LastRawRowCount = allRows.Count;

            DisciplineBlock? block = FindDisciplineBlock(allRows, discipline);
            HeaderContext header = ExtractHeaderContext(allRows, block);
            List<List<string>> disciplineRows = block != null
                ? block.Rows
                : SelectByLegacyMarkers(allRows, discipline);
            List<SheetTitleBlockRow> result = new List<SheetTitleBlockRow>();

            foreach (List<string> source in disciplineRows)
            {
                if (!IsCandidateRow(source)) continue;

                List<string> values = BuildValues(source, header);
                ApplyCurrentRevision(values);

                string sheetNumber = Get(values, SheetNumberIndex);
                string sheetName = Get(values, SheetNameIndex);
                if (string.IsNullOrWhiteSpace(sheetNumber) || string.IsNullOrWhiteSpace(sheetName))
                    continue;

                var row = new SheetTitleBlockRow
                {
                    SheetNumber = sheetNumber,
                    SheetName = sheetName,
                    Discipline = ToDisplayDiscipline(discipline),
                    CurrentRevision = Get(values, CurrentRevisionIndex),
                    Values = values,
                    Headers = BuildHeaders(values.Count)
                };
                row.SearchText = string.Join(" ", new[] { row.SheetNumber, row.SheetName, row.Discipline, row.CurrentRevision }
                    .Concat(row.Values ?? Enumerable.Empty<string>()));
                result.Add(row);
            }

            return result
                .GroupBy(r => r.SheetNumber, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(r => r.SheetNumber, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public int CountRawRows(string filePath)
        {
            return _reader.ReadWorksheet(filePath, WorksheetName).Count;
        }

        private static List<List<string>> SelectByLegacyMarkers(IList<List<string>> allRows, string discipline)
        {
            List<int> markers = allRows
                .Select((row, index) => RowContains(row, "marcador") ? index : -1)
                .Where(index => index >= 0)
                .ToList();

            if (markers.Count < 6)
                return allRows.Select(row => new List<string>(row)).ToList();

            int pairStart = NormalizeDiscipline(discipline) switch
            {
                ElectricalDiscipline => 0,
                HydraulicDiscipline => 2,
                _ => 4
            };

            int start = markers[pairStart] + 1;
            int end = markers[pairStart + 1];
            if (start >= end) return new List<List<string>>();

            return allRows
                .Skip(start)
                .Take(end - start)
                .Select(row => new List<string>(row))
                .ToList();
        }

        private static DisciplineBlock? FindDisciplineBlock(IList<List<string>> allRows, string discipline)
        {
            List<int> headers = allRows
                .Select((row, index) => RowContains(row, "Disciplina:") ? index : -1)
                .Where(index => index >= 0)
                .ToList();

            if (headers.Count == 0)
                return null;

            string normalized = NormalizeDiscipline(discipline);
            var blocks = new List<DisciplineBlock>();
            for (int i = 0; i < headers.Count; i++)
            {
                int headerIndex = headers[i];
                int nextHeaderIndex = i + 1 < headers.Count ? headers[i + 1] : allRows.Count;
                int dataStart = FindDataStart(allRows, headerIndex, nextHeaderIndex);
                string displayName = Get(allRows.ElementAtOrDefault(headerIndex + 1) ?? new List<string>(), 0);

                blocks.Add(new DisciplineBlock
                {
                    HeaderIndex = headerIndex,
                    DataStart = dataStart,
                    DataEnd = nextHeaderIndex,
                    DisciplineName = displayName
                });
            }

            DisciplineBlock? selected = blocks.FirstOrDefault(b => MatchesDiscipline(b.DisciplineName, normalized));
            if (selected == null && normalized == OtherDiscipline)
                selected = blocks.FirstOrDefault(b => !MatchesDiscipline(b.DisciplineName, ElectricalDiscipline) &&
                                                      !MatchesDiscipline(b.DisciplineName, HydraulicDiscipline));

            if (selected == null)
                return null;

            selected.Rows = allRows
                .Skip(selected.DataStart)
                .Take(Math.Max(0, selected.DataEnd - selected.DataStart))
                .Select(row => new List<string>(row))
                .ToList();

            return selected;
        }

        private static int FindDataStart(IList<List<string>> allRows, int headerIndex, int endIndex)
        {
            for (int i = headerIndex + 1; i < endIndex; i++)
            {
                if (RowContains(allRows[i], "marcador"))
                    return i + 1;
            }

            return Math.Min(headerIndex + 4, endIndex);
        }

        private static bool IsCandidateRow(IList<string> row)
        {
            if (row == null || row.Count == 0) return false;
            if (row.All(IsEmpty)) return false;
            if (RowContains(row, "marcador") || RowContains(row, "Disciplina:")) return false;

            string sheetNumber = Get(row, SheetNumberIndex);
            string sheetName = Get(row, SheetNameIndex);
            if (string.IsNullOrWhiteSpace(sheetNumber) || string.IsNullOrWhiteSpace(sheetName))
                return false;

            if (sheetNumber.IndexOf("DESENHO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sheetName.IndexOf("ARQUIVO", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            bool placeholderCode = row.Count > 6 && IsPlaceholderCode(row[6]);
            bool col0Empty = IsEmpty(row[SheetNumberIndex]);
            bool col3Empty = IsEmpty(row[SheetNameIndex]);

            return !(placeholderCode && col0Empty && col3Empty);
        }

        private static List<string> BuildValues(IList<string> source, HeaderContext header)
        {
            List<string> values = source
                .Select(v => NormalizeCell(v))
                .ToList();

            EnsureLength(values, 59);

            values[ClientIndex] = header.Client;
            values[AddressIndex] = header.Address;
            values[ProjectNameIndex] = header.ProjectName;
            values[WorkNameIndex] = header.ProjectName;
            values[CurrentRevisionIndex] = header.MasterRevision;
            values[DisciplineNameIndex] = header.DisciplineName;
            values[ProjectPhaseIndex] = header.ProjectPhase;
            values[DesignerIndex] = header.Designers;
            values[SupervisorIndex] = header.Supervisor;
            values[ResponsibleIndex] = header.Responsible;
            values[IssueDateIndex] = string.IsNullOrWhiteSpace(Get(values, 12)) ? header.MasterDate : Get(values, 12);

            return values;
        }

        private static void ApplyCurrentRevision(IList<string> values)
        {
            string currentRevision = string.Empty;
            for (int i = 0; i < RevisionDateIndexes.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(Get(values, RevisionDateIndexes[i])))
                    currentRevision = RevisionLabels[i];
            }

            values[CurrentRevisionIndex] = currentRevision;
            int currentIndex = Array.IndexOf(RevisionLabels, currentRevision);
            for (int i = 0; i < RevisionLabels.Length; i++)
                values[51 + i] = currentIndex >= 0 && i <= currentIndex ? RevisionLabels[i] : string.Empty;
        }

        private static HeaderContext ExtractHeaderContext(IList<List<string>> allRows, DisciplineBlock? block)
        {
            var context = new HeaderContext
            {
                Client = FindValueBelowLabel(allRows, "Cliente:"),
                Address = FindValueBelowLabel(allRows, "Endereço:"),
                ProjectName = FindValueBelowLabel(allRows, "Obra:"),
                WorkOrder = FindValueBelowLabel(allRows, "OS:"),
                ProjectCode = FindValueBelowLabel(allRows, "Código da Obra:"),
                MasterRevision = FindValueBelowLabel(allRows, "Revisão:"),
                MasterDate = FindValueBelowLabel(allRows, "Data:")
            };

            if (block != null)
            {
                List<string> labelRow = allRows.ElementAtOrDefault(block.HeaderIndex) ?? new List<string>();
                List<string> valueRow = allRows.ElementAtOrDefault(block.HeaderIndex + 1) ?? new List<string>();

                context.DisciplineName = GetValueUnderLabel(labelRow, valueRow, "Disciplina:");
                context.ProjectPhase = GetValueUnderLabel(labelRow, valueRow, "Fase do Projeto:");
                context.Responsible = GetValueUnderLabel(labelRow, valueRow, "Responsável:");
                context.Supervisor = GetValueUnderLabel(labelRow, valueRow, "Supervisor:");
                context.Designers = GetValueUnderLabel(labelRow, valueRow, "Projetistas");
            }

            return context;
        }

        private static List<string> BuildHeaders(int length)
        {
            List<string> headers = Enumerable.Repeat(string.Empty, Math.Max(length, 59)).ToList();
            headers[1] = "PAVIMENTO";
            headers[2] = "DESCRIÇÃO INSTALAÇÕES";
            headers[3] = "ARQUIVO";
            headers[4] = "STATUS";
            headers[5] = "ESCALA";
            headers[6] = "CÓDIGO PLANTA";
            headers[7] = "REFERÊNCIA-1";
            headers[8] = "REFERÊNCIA-2";
            headers[9] = "REFERÊNCIA-3";
            headers[10] = "REFERÊNCIA-4";
            headers[11] = "REFERÊNCIA-5";
            headers[12] = "00-DATA";
            headers[13] = "00-DESCRIÇÃO";
            headers[14] = "00-RESPONSÁVEL";
            headers[15] = "01-DATA";
            headers[16] = "01-DESCRIÇÃO";
            headers[17] = "01-RESPONSÁVEL";
            headers[18] = "02-DATA";
            headers[19] = "02-DESCRIÇÃO";
            headers[20] = "02-RESPONSÁVEL";
            headers[21] = "03-DATA";
            headers[22] = "03-DESCRIÇÃO";
            headers[23] = "03-RESPONSÁVEL";
            headers[24] = "04-DATA";
            headers[25] = "04-DESCRIÇÃO";
            headers[26] = "04-RESPONSÁVEL";
            headers[27] = "05-DATA";
            headers[28] = "05-DESCRIÇÃO";
            headers[29] = "05-RESPONSÁVEL";
            headers[30] = "06-DATA";
            headers[31] = "06-DESCRIÇÃO";
            headers[32] = "06-RESPONSÁVEL";
            headers[33] = "07-DATA";
            headers[34] = "07-DESCRIÇÃO";
            headers[35] = "07-RESPONSÁVEL";
            headers[36] = "EMPTY-1";
            headers[37] = "ART";
            headers[38] = "CREA";
            headers[39] = "EMPTY-2";
            headers[AddressIndex] = "ENDEREÇO";
            headers[CurrentRevisionIndex] = "REVISÃO";
            headers[ProjectNameIndex] = "NOME DO PROJETO";
            headers[WorkNameIndex] = "OBRA";
            headers[ClientIndex] = "CLIENTE";
            headers[DisciplineNameIndex] = "DISCIPLINA";
            headers[ProjectPhaseIndex] = "FASE DO PROJETO";
            headers[DesignerIndex] = "PROJETISTA";
            headers[SupervisorIndex] = "SUPERVISOR";
            headers[IssueDateIndex] = "DATA EMISSÃO";
            headers[ResponsibleIndex] = "RESPONSÁVEL";
            headers[51] = "00-REVISÃO";
            headers[52] = "01-REVISÃO";
            headers[53] = "02-REVISÃO";
            headers[54] = "03-REVISÃO";
            headers[55] = "04-REVISÃO";
            headers[56] = "05-REVISÃO";
            headers[57] = "06-REVISÃO";
            headers[58] = "07-REVISÃO";
            return headers;
        }

        private static string FindValueBelowLabel(IList<List<string>> rows, string label)
        {
            for (int rowIndex = 0; rowIndex < rows.Count - 1; rowIndex++)
            {
                List<string> row = rows[rowIndex];
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    if (string.Equals(NormalizeCell(row[columnIndex]), label, StringComparison.OrdinalIgnoreCase))
                    {
                        for (int offset = 0; offset <= 2; offset++)
                        {
                            string value = Get(rows[rowIndex + 1], columnIndex + offset);
                            if (!string.IsNullOrWhiteSpace(value))
                                return value;
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static string GetValueUnderLabel(IList<string> labelRow, IList<string> valueRow, string label)
        {
            for (int i = 0; i < labelRow.Count; i++)
            {
                if (string.Equals(NormalizeCell(labelRow[i]), label, StringComparison.OrdinalIgnoreCase))
                    return Get(valueRow, i);
            }

            return string.Empty;
        }

        private static void EnsureLength(IList<string> values, int length)
        {
            while (values.Count < length)
                values.Add(string.Empty);
        }

        private static bool RowContains(IEnumerable<string> row, string value)
        {
            return row.Any(cell => string.Equals(NormalizeCell(cell), value, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPlaceholderCode(string value)
        {
            string text = NormalizeCell(value);
            return string.IsNullOrWhiteSpace(text) || OnlyXRegex.IsMatch(text) || XxPrefixRegex.IsMatch(text);
        }

        private static bool IsEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(NormalizeCell(value));
        }

        private static string NormalizeCell(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string Get(IList<string> values, int index)
        {
            return index >= 0 && index < values.Count ? values[index] ?? string.Empty : string.Empty;
        }

        public static string NormalizeDiscipline(string discipline)
        {
            string value = (discipline ?? string.Empty).Trim();
            value = value
                .Replace("é", "e")
                .Replace("É", "E")
                .Replace("á", "a")
                .Replace("Á", "A")
                .Replace("í", "i")
                .Replace("Í", "I")
                .Replace("ó", "o")
                .Replace("Ó", "O");

            if (value.StartsWith("Ele", StringComparison.OrdinalIgnoreCase) ||
                value.IndexOf("Eletr", StringComparison.OrdinalIgnoreCase) >= 0)
                return ElectricalDiscipline;

            if (value.StartsWith("Hid", StringComparison.OrdinalIgnoreCase) ||
                value.IndexOf("Hidr", StringComparison.OrdinalIgnoreCase) >= 0)
                return HydraulicDiscipline;

            return OtherDiscipline;
        }

        public static string ToDisplayDiscipline(string discipline)
        {
            return NormalizeDiscipline(discipline) switch
            {
                ElectricalDiscipline => "Eletrica",
                HydraulicDiscipline => "Hidraulica",
                _ => "Outra"
            };
        }

        private static bool MatchesDiscipline(string value, string normalizedDiscipline)
        {
            string normalizedValue = NormalizeDiscipline(value);
            return string.Equals(normalizedValue, normalizedDiscipline, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class DisciplineBlock
        {
            public int HeaderIndex { get; set; }
            public int DataStart { get; set; }
            public int DataEnd { get; set; }
            public string DisciplineName { get; set; } = string.Empty;
            public List<List<string>> Rows { get; set; } = new List<List<string>>();
        }

        private sealed class HeaderContext
        {
            public string Client { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string ProjectName { get; set; } = string.Empty;
            public string WorkOrder { get; set; } = string.Empty;
            public string ProjectCode { get; set; } = string.Empty;
            public string MasterRevision { get; set; } = string.Empty;
            public string MasterDate { get; set; } = string.Empty;
            public string DisciplineName { get; set; } = string.Empty;
            public string ProjectPhase { get; set; } = string.Empty;
            public string Responsible { get; set; } = string.Empty;
            public string Supervisor { get; set; } = string.Empty;
            public string Designers { get; set; } = string.Empty;
        }
    }
}

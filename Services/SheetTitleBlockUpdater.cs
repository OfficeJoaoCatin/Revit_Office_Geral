using Autodesk.Revit.DB;
using RevitFamilyImporter.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RevitFamilyImporter.Services
{
    internal class SheetTitleBlockUpdater
    {
        private const string OfficeTitleBlockFamilyName = "OFC-FOL";

        public List<FamilySymbol> GetTitleBlockTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .OfType<FamilySymbol>()
                .OrderBy(s => IsOfficeTitleBlock(s) ? 0 : 1)
                .ThenBy(s => s.FamilyName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static bool IsOfficeTitleBlock(FamilySymbol symbol)
        {
            return string.Equals(symbol.FamilyName, OfficeTitleBlockFamilyName, StringComparison.OrdinalIgnoreCase);
        }

        public SheetTitleBlockUpdateReport Apply(
            Document doc,
            IEnumerable<SheetTitleBlockRow> rows,
            FamilySymbol? titleBlockType,
            bool updateExistingOnly = false)
        {
            var report = new SheetTitleBlockUpdateReport();
            List<SheetTitleBlockRow> selectedRows = (rows ?? Enumerable.Empty<SheetTitleBlockRow>())
                .Where(r => r.Selected)
                .ToList();

            report.RowsSelected = selectedRows.Count;
            if (selectedRows.Count == 0) return report;

            Dictionary<string, ViewSheet> sheetsByNumber = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .GroupBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            if (!updateExistingOnly)
            {
                if (titleBlockType == null)
                    throw new InvalidOperationException("Selecione a familia/tipo do carimbo para criar folhas novas.");

                if (!titleBlockType.IsActive)
                    titleBlockType.Activate();
                doc.Regenerate();
            }

            foreach (SheetTitleBlockRow row in selectedRows)
            {
                try
                {
                    ViewSheet sheet;
                    bool created = false;
                    if (!sheetsByNumber.TryGetValue(row.SheetNumber, out sheet))
                    {
                        if (updateExistingOnly)
                        {
                            report.SheetsMissingSkipped++;
                            continue;
                        }

                        sheet = ViewSheet.Create(doc, titleBlockType!.Id);
                        sheet.SheetNumber = row.SheetNumber;
                        sheetsByNumber[row.SheetNumber] = sheet;
                        report.SheetsCreated++;
                        created = true;
                    }

                    if (!string.Equals(sheet.Name, row.SheetName, StringComparison.Ordinal))
                    {
                        sheet.Name = row.SheetName;
                        report.SheetsRenamed++;
                    }

                    if (!created)
                        report.SheetsUpdated++;

                    UpdateSheetAndTitleBlockParameters(doc, sheet, row, report);
                }
                catch (Exception ex)
                {
                    report.Errors++;
                    report.Messages.Add($"{row.SheetNumber} - {row.SheetName}: {ex.Message}");
                }
            }

            return report;
        }

        private static void UpdateSheetAndTitleBlockParameters(
            Document doc,
            ViewSheet sheet,
            SheetTitleBlockRow row,
            SheetTitleBlockUpdateReport report)
        {
            int before = report.ParametersUpdated;

            UpdateParameters(doc, sheet, row, report);

            List<Element> titleBlocks = GetTitleBlockInstances(doc, sheet);
            if (titleBlocks.Count == 0)
            {
                report.Messages.Add($"{row.SheetNumber} - nenhum carimbo encontrado na folha.");
                return;
            }

            foreach (Element titleBlock in titleBlocks)
                UpdateParameters(doc, titleBlock, row, report);

            if (report.ParametersUpdated == before)
                report.Messages.Add($"{row.SheetNumber} - nenhum parametro do carimbo/folha correspondeu aos nomes mapeados.");
        }

        private static List<Element> GetTitleBlockInstances(Document doc, ViewSheet sheet)
        {
            return new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        private static void UpdateParameters(
            Document doc,
            Element target,
            SheetTitleBlockRow row,
            SheetTitleBlockUpdateReport report)
        {
            var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < row.Values.Count; i++)
            {
                string value = row.Values[i] ?? string.Empty;
                bool empty = string.IsNullOrWhiteSpace(value);
                bool revisionField = (i >= 12 && i <= 35) || i == 41 || (i >= 51 && i <= 58);
                if (empty && !revisionField) continue;
                if (empty) value = string.Empty;

                bool foundAny = false;
                foreach (string name in SheetTitleBlockParameterMapper.GetCandidates(i, row.Headers))
                {
                    if (!touched.Add(name)) continue;

                    Parameter parameter = FindParameter(target, name);
                    if (parameter == null) continue;

                    foundAny = true;
                    if (parameter.IsReadOnly || (empty && parameter.StorageType != StorageType.String))
                    {
                        report.ParametersSkipped++;
                        if (empty)
                            report.Messages.Add($"{row.SheetNumber} - não foi possível limpar o parâmetro de revisão '{name}' (somente leitura ou não é texto).");
                        continue;
                    }

                    if (SetParameterValue(doc, parameter, value))
                        report.ParametersUpdated++;
                    else
                    {
                        report.ParametersSkipped++;
                        if (empty)
                            report.Messages.Add($"{row.SheetNumber} - não foi possível limpar o parâmetro de revisão '{name}'.");
                    }
                }

                if (!foundAny && !empty)
                    report.ParametersSkipped++;
            }
        }

        private static Parameter FindParameter(Element element, string name)
        {
            Parameter parameter = element.LookupParameter(name);
            if (parameter != null)
                return parameter;

            string normalizedName = NormalizeParameterName(name);
            foreach (Parameter candidate in element.Parameters)
            {
                Definition definition = candidate.Definition;
                if (definition == null) continue;

                if (string.Equals(NormalizeParameterName(definition.Name), normalizedName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return null;
        }

        private static bool SetParameterValue(Document doc, Parameter parameter, string value)
        {
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        return parameter.Set(value ?? string.Empty);

                    case StorageType.Integer:
                        if (int.TryParse(NormalizeNumber(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                            return parameter.Set(intValue);
                        return TrySetValueString(parameter, value);

                    case StorageType.Double:
                        if (!double.TryParse(NormalizeNumber(value), NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
                            return TrySetValueString(parameter, value);
                        return parameter.Set(ConvertDoubleValue(doc, parameter, doubleValue));

                    case StorageType.ElementId:
                        return TrySetValueString(parameter, value);

                    default:
                        return TrySetValueString(parameter, value);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetValueString(Parameter parameter, string value)
        {
            try { return parameter.SetValueString(value ?? string.Empty); }
            catch { return false; }
        }

        private static double ConvertDoubleValue(Document doc, Parameter parameter, double value)
        {
            try
            {
                ForgeTypeId dataType = parameter.Definition.GetDataType();
                if (dataType == SpecTypeId.Length)
                    return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

                ForgeTypeId unitTypeId = doc.GetUnits()
                    .GetFormatOptions(dataType)
                    .GetUnitTypeId();
                if (unitTypeId != null)
                    return UnitUtils.ConvertToInternalUnits(value, unitTypeId);
            }
            catch { }

            return value;
        }

        private static string NormalizeNumber(string value)
        {
            return (value ?? string.Empty).Trim().Replace(" ", string.Empty).Replace(",", ".");
        }

        private static string NormalizeParameterName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            string normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (char ch in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.NonSpacingMark) continue;

                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToUpperInvariant(ch));
            }

            return builder.ToString();
        }
    }
}

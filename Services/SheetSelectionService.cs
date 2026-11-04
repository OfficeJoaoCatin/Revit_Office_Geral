using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitFamilyImporter.Models;

namespace RevitFamilyImporter.Services
{
    public class SheetSelectionService
    {
        public List<SheetInfo> GetSheets(ExportDocumentInfo documentInfo)
        {
            Document document = documentInfo.Document
                ?? throw new InvalidOperationException("Documento Revit invalido.");

            return new FilteredElementCollector(document)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(sheet => !sheet.IsPlaceholder)
                .OrderBy(sheet => sheet.SheetNumber, StringComparer.OrdinalIgnoreCase)
                .Select(sheet => new SheetInfo
                {
                    Id = sheet.Id,
                    IdValue = sheet.Id.IntegerValue,
                    DocumentKey = documentInfo.StableKey,
                    ProjectName = documentInfo.ProjectName,
                    Number = sheet.SheetNumber,
                    Name = sheet.Name,
                    Discipline = GetDiscipline(sheet),
                    Issued = IsIssued(sheet),
                    CanBePrinted = sheet.CanBePrinted,
                    Selected = sheet.CanBePrinted
                })
                .ToList();
        }

        public void ApplyMode(IEnumerable<SheetInfo> sheets, SheetSelectionMode mode)
        {
            if (mode == SheetSelectionMode.Manual)
                return;

            foreach (SheetInfo sheet in sheets)
            {
                sheet.Selected = mode == SheetSelectionMode.AllSheets
                    ? sheet.CanBePrinted
                    : sheet.CanBePrinted && sheet.Issued;
            }
        }

        private static bool IsIssued(ViewSheet sheet)
        {
            Parameter? currentRevisionIssued = sheet.get_Parameter(BuiltInParameter.SHEET_CURRENT_REVISION_ISSUED);
            if (currentRevisionIssued != null && currentRevisionIssued.StorageType == StorageType.Integer)
                return currentRevisionIssued.AsInteger() == 1;

            Parameter? issueDate = sheet.get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE);
            string value = issueDate?.AsString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string GetDiscipline(ViewSheet sheet)
        {
            string? explicitDiscipline = ReadStringParameter(
                sheet,
                "Disciplina",
                "Discipline",
                "Subdisciplina",
                "Sub-Disciplina");

            if (!string.IsNullOrWhiteSpace(explicitDiscipline))
                return explicitDiscipline.Trim();

            return "Geral";
        }

        private static string? ReadStringParameter(Element element, params string[] parameterNames)
        {
            foreach (string parameterName in parameterNames)
            {
                try
                {
                    Parameter? parameter = element.LookupParameter(parameterName);
                    string? value = parameter?.AsString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                catch
                {
                    // Some shared/built-in parameters can be unavailable depending on the project template.
                }
            }

            return null;
        }
    }
}

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitFamilyImporter.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BatchTypeParameterUpdateCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            if (doc == null) return Result.Failed;

            try
            {
                List<Element> selectedElements = GetSelectedElements(uiDoc, doc);
                if (selectedElements.Count == 0)
                {
                    TaskDialog.Show(
                        "Editar Parâmetros",
                        "Nenhum elemento foi selecionado.");
                    return Result.Cancelled;
                }

                List<ElementType> types = CollectUniqueTypes(doc, selectedElements);
                List<string> parameterNames = CollectEditableParameterNames(selectedElements, types);
                if (parameterNames.Count == 0)
                {
                    TaskDialog.Show(
                        "Editar Parâmetros",
                        "Nenhum parâmetro editável foi encontrado nos elementos selecionados.");
                    return Result.Cancelled;
                }

                var dialog = new BatchTypeParameterDialog(commandData.Application, parameterNames);
                if (dialog.ShowDialog() != true)
                    return Result.Cancelled;

                string parameterName = dialog.ParameterName;
                string value = dialog.ParameterValue ?? string.Empty;

                var report = new UpdateReport();
                bool updateInstanceParameters = selectedElements.Any(e => HasParameter(e, parameterName));
                if (!updateInstanceParameters && types.Count == 0)
                {
                    TaskDialog.Show(
                        "Editar Parâmetros",
                        "Nenhum parâmetro de instância ou tipo editável foi encontrado nos elementos selecionados.");
                    return Result.Cancelled;
                }

                using (var tx = new Transaction(doc, updateInstanceParameters
                    ? "Alterar Parametro de Instancia em Lote"
                    : "Alterar Parametro de Tipo em Lote"))
                {
                    tx.Start();

                    if (updateInstanceParameters)
                    {
                        foreach (Element element in selectedElements)
                            UpdateElementParameter(doc, element, parameterName, value, report);
                    }
                    else
                    {
                        foreach (ElementType type in types)
                            UpdateTypeParameter(doc, type, parameterName, value, report);
                    }

                    tx.Commit();
                }

                TaskDialog.Show(
                    "Editar Parâmetros",
                    BuildSummary(parameterName, value, selectedElements.Count, types.Count, updateInstanceParameters, report));

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Erro - Editar Parâmetros", ex.Message);
                return Result.Failed;
            }
        }

        private static List<Element> GetSelectedElements(UIDocument uiDoc, Document doc)
        {
            List<Element> selected = uiDoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .Where(e => e != null)
                .ToList();

            if (selected.Count > 0)
                return selected;

            IList<Reference> picked = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                "Selecione as tags/famílias. Você pode usar TAB para alternar/pegar elementos conectados e depois clicar.");

            return picked
                .Select(r => doc.GetElement(r))
                .Where(e => e != null)
                .ToList();
        }


        private static List<string> CollectEditableParameterNames(
            IEnumerable<Element> elements,
            IEnumerable<ElementType> types)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Element element in elements ?? Enumerable.Empty<Element>())
                AddEditableParameterNames(element, names);

            foreach (ElementType type in types ?? Enumerable.Empty<ElementType>())
                AddEditableParameterNames(type, names);

            return names
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void AddEditableParameterNames(Element element, ISet<string> names)
        {
            if (element == null || names == null) return;

            try
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    string name = parameter?.Definition?.Name;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (parameter.IsReadOnly) continue;

                    names.Add(name.Trim());
                }
            }
            catch { }
        }
        private static List<ElementType> CollectUniqueTypes(Document doc, IEnumerable<Element> elements)
        {
            var result = new Dictionary<ElementId, ElementType>();

            foreach (Element element in elements ?? Enumerable.Empty<Element>())
            {
                ElementId typeId = ElementId.InvalidElementId;
                try { typeId = element.GetTypeId(); }
                catch { }

                if (typeId == ElementId.InvalidElementId)
                    continue;

                if (result.ContainsKey(typeId))
                    continue;

                if (doc.GetElement(typeId) is ElementType type)
                    result.Add(typeId, type);
            }

            return result.Values.OrderBy(t => t.FamilyName).ThenBy(t => t.Name).ToList();
        }

        private static void UpdateTypeParameter(
            Document doc,
            ElementType type,
            string parameterName,
            string value,
            UpdateReport report)
        {
            Parameter parameter = type.LookupParameter(parameterName);
            if (parameter == null)
            {
                report.NotFound.Add(GetTypeName(type));
                return;
            }

            if (parameter.IsReadOnly)
            {
                report.ReadOnly.Add(GetTypeName(type));
                return;
            }

            try
            {
                if (SetParameterValue(doc, parameter, value))
                    report.Updated++;
                else
                    report.InvalidStorage.Add(GetTypeName(type));
            }
            catch (Exception ex)
            {
                report.Errors.Add($"{GetTypeName(type)}: {ex.Message}");
            }
        }

        private static void UpdateElementParameter(
            Document doc,
            Element element,
            string parameterName,
            string value,
            UpdateReport report)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter == null)
            {
                report.NotFound.Add(GetElementName(element));
                return;
            }

            if (parameter.IsReadOnly)
            {
                report.ReadOnly.Add(GetElementName(element));
                return;
            }

            try
            {
                if (SetParameterValue(doc, parameter, value))
                    report.Updated++;
                else
                    report.InvalidStorage.Add(GetElementName(element));
            }
            catch (Exception ex)
            {
                report.Errors.Add($"{GetElementName(element)}: {ex.Message}");
            }
        }

        private static bool SetParameterValue(Document doc, Parameter parameter, string value)
        {
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    parameter.Set(value ?? string.Empty);
                    return true;

                case StorageType.Integer:
                    if (TryParseInteger(value, out int intValue))
                    {
                        parameter.Set(intValue);
                        return true;
                    }
                    if (TrySetValueString(parameter, value))
                        return true;
                    return false;

                case StorageType.Double:
                    if (!TryParseDouble(value, out double doubleValue))
                        return TrySetValueString(parameter, value);

                    parameter.Set(ConvertDoubleValue(doc, parameter, doubleValue));
                    return true;

                case StorageType.ElementId:
                    if (TryFindElementIdByName(doc, value, out ElementId elementId))
                    {
                        parameter.Set(elementId);
                        return true;
                    }
                    return TrySetValueString(parameter, value);

                default:
                    return TrySetValueString(parameter, value);
            }
        }

        private static bool TrySetValueString(Parameter parameter, string value)
        {
            try
            {
                return parameter.SetValueString(value ?? string.Empty);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryFindElementIdByName(Document doc, string value, out ElementId elementId)
        {
            elementId = ElementId.InvalidElementId;
            if (doc == null || string.IsNullOrWhiteSpace(value))
                return false;

            string target = value.Trim();
            try
            {
                List<Element> matches = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .Where(e => string.Equals(GetElementName(e), target, StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToList();

                if (matches.Count == 1)
                {
                    elementId = matches[0].Id;
                    return true;
                }

                matches = new FilteredElementCollector(doc)
                    .WhereElementIsElementType()
                    .Where(e => string.Equals(GetElementName(e), target, StringComparison.OrdinalIgnoreCase))
                    .Take(2)
                    .ToList();

                if (matches.Count == 1)
                {
                    elementId = matches[0].Id;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool HasParameter(Element element, string parameterName)
        {
            try { return element?.LookupParameter(parameterName)?.IsReadOnly == false; }
            catch { return false; }
        }

        private static double ConvertDoubleValue(Document doc, Parameter parameter, double value)
        {
            ForgeTypeId dataType = null;
            try { dataType = parameter.Definition.GetDataType(); }
            catch { }

            if (dataType != null && dataType == SpecTypeId.Length)
                return UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

            try
            {
                ForgeTypeId unitTypeId = doc.GetUnits()
                    .GetFormatOptions(dataType)
                    .GetUnitTypeId();

                if (unitTypeId != null)
                    return UnitUtils.ConvertToInternalUnits(value, unitTypeId);
            }
            catch { }

            return value;
        }

        private static bool TryParseInteger(string value, out int result)
        {
            value = NormalizeNumberText(value);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return true;

            result = 0;
            return false;
        }

        private static bool TryParseDouble(string value, out double result)
        {
            value = NormalizeNumberText(value);
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return true;

            result = 0.0;
            return false;
        }

        private static string NormalizeNumberText(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace(",", ".");
        }

        private static string BuildSummary(
            string parameterName,
            string value,
            int selectedCount,
            int typeCount,
            bool updatedInstances,
            UpdateReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Elementos selecionados: {selectedCount}");
            builder.AppendLine($"Tipos encontrados: {typeCount}");
            builder.AppendLine($"Modo: {(updatedInstances ? "Parâmetros de instância" : "Parâmetros de tipo")}");
            builder.AppendLine($"Parâmetro: {parameterName}");
            builder.AppendLine($"Valor aplicado: {value}");
            builder.AppendLine(updatedInstances
                ? $"Elementos alterados: {report.Updated}"
                : $"Tipos alterados: {report.Updated}");

            AppendList(builder, "Parâmetro não encontrado", report.NotFound);
            AppendList(builder, "Parâmetro somente leitura", report.ReadOnly);
            AppendList(builder, "Valor incompatível com o tipo do parâmetro", report.InvalidStorage);
            AppendList(builder, "Erros", report.Errors);

            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, string title, IList<string> items)
        {
            if (items == null || items.Count == 0)
                return;

            builder.AppendLine();
            builder.AppendLine(title + ":");
            foreach (string item in items.Distinct().Take(8))
                builder.AppendLine("- " + item);

            if (items.Count > 8)
                builder.AppendLine($"- Mais {items.Count - 8} item(ns).");
        }

        private static string GetTypeName(ElementType type)
        {
            if (type == null) return "(tipo nulo)";

            string family = string.Empty;
            try { family = type.FamilyName; }
            catch { }

            return string.IsNullOrWhiteSpace(family)
                ? type.Name
                : $"{family}: {type.Name}";
        }

        private static string GetElementName(Element element)
        {
            if (element == null) return "(elemento nulo)";

            string name = string.Empty;
            try { name = element.Name; }
            catch { }

            if (!string.IsNullOrWhiteSpace(name))
                return $"{element.Id} - {name}";

            try { return $"{element.Id} - {element.Category?.Name ?? element.GetType().Name}"; }
            catch { return "(elemento sem nome)"; }
        }

        private class UpdateReport
        {
            public int Updated { get; set; }
            public List<string> NotFound { get; } = new List<string>();
            public List<string> ReadOnly { get; } = new List<string>();
            public List<string> InvalidStorage { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();
        }
    }
}





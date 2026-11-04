using System;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.VisibilityInspector.Utils
{
    internal static class RevitText
    {
        public static string Id(ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId)
                return string.Empty;

            return id.IntegerValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ElementName(Element element)
        {
            if (element == null)
                return string.Empty;

            try
            {
                if (!string.IsNullOrWhiteSpace(element.Name))
                    return element.Name;
            }
            catch { }

            return element.GetType().Name;
        }

        public static string ElementTypeName(Document doc, Element element)
        {
            try
            {
                ElementId typeId = element.GetTypeId();
                if (typeId != ElementId.InvalidElementId && doc.GetElement(typeId) is ElementType type)
                    return type.Name;
            }
            catch { }

            return string.Empty;
        }

        public static string FamilyName(Document doc, Element element)
        {
            if (element is FamilyInstance instance)
                return instance.Symbol?.FamilyName ?? string.Empty;

            try
            {
                ElementId typeId = element.GetTypeId();
                if (typeId != ElementId.InvalidElementId && doc.GetElement(typeId) is ElementType type)
                    return type.FamilyName ?? string.Empty;
            }
            catch { }

            return string.Empty;
        }

        public static string ParameterValue(Document doc, Parameter parameter)
        {
            if (parameter == null)
                return string.Empty;

            try
            {
                string value = parameter.AsValueString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            catch { }

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.Double:
                        return parameter.AsDouble().ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
                    case StorageType.Integer:
                        return parameter.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    case StorageType.String:
                        return parameter.AsString() ?? string.Empty;
                    case StorageType.ElementId:
                        ElementId id = parameter.AsElementId();
                        Element named = doc.GetElement(id);
                        return named != null ? $"{ElementName(named)} ({Id(id)})" : Id(id);
                    default:
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string YesNo(bool value)
        {
            return value ? "Sim" : "Não";
        }
    }
}

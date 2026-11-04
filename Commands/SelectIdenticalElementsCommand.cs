using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SelectIdenticalElementsCommand : IExternalCommand
    {
        private const double DoubleTolerance = 1e-6;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            if (doc == null)
                return Result.Failed;

            try
            {
                Element source = ResolveSourceElement(uiDoc, doc);
                if (source == null)
                    return Result.Cancelled;

                SearchScope? scope = AskScope();
                if (!scope.HasValue)
                    return Result.Cancelled;

                SelectionProfile profile = SelectionProfile.Create(doc, source);
                List<ElementId> matches = FindMatches(doc, doc.ActiveView, profile, scope.Value);
                uiDoc.Selection.SetElementIds(matches);

                TaskDialog.Show(
                    "Selecionar Idênticos",
                    BuildResultText(profile, matches.Count, scope.Value));

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Erro - Selecionar Idênticos", ex.Message);
                return Result.Failed;
            }
        }

        private static Element ResolveSourceElement(UIDocument uiDoc, Document doc)
        {
            ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
            if (selectedIds.Count == 1)
            {
                Element selected = doc.GetElement(selectedIds.First());
                if (selected != null)
                    return selected;
            }

            Reference reference = uiDoc.Selection.PickObject(ObjectType.Element, "Selecione o elemento base para buscar idênticos.");
            return doc.GetElement(reference);
        }

        private static SearchScope? AskScope()
        {
            TaskDialog dialog = new TaskDialog("Selecionar Idênticos")
            {
                MainInstruction = "Onde procurar elementos idênticos?",
                MainContent =
                    "O comando compara categoria, família, tipo, parâmetros editáveis relevantes da instância " +
                    "e parâmetros geométricos de identidade como A e Ângulo. " +
                    "Parâmetros de localização, ambiente e documentação são ignorados.",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Vista ativa");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Projeto inteiro");

            TaskDialogResult result = dialog.Show();
            if (result == TaskDialogResult.CommandLink1)
                return SearchScope.ActiveView;
            if (result == TaskDialogResult.CommandLink2)
                return SearchScope.EntireProject;

            return null;
        }

        private static List<ElementId> FindMatches(Document doc, RevitView activeView, SelectionProfile profile, SearchScope scope)
        {
            FilteredElementCollector collector = CreateCollector(doc, activeView, scope);
            if (profile.CategoryId != ElementId.InvalidElementId)
                collector.WherePasses(new ElementCategoryFilter(profile.CategoryId));

            return collector
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent()
                .Where(element => profile.Matches(doc, element))
                .Select(element => element.Id)
                .OrderBy(id => id.IntegerValue)
                .ToList();
        }

        private static FilteredElementCollector CreateCollector(Document doc, RevitView activeView, SearchScope scope)
        {
            if (scope == SearchScope.ActiveView && activeView != null && !activeView.IsTemplate)
            {
                try { return new FilteredElementCollector(doc, activeView.Id); }
                catch { }
            }

            return new FilteredElementCollector(doc);
        }

        private static string BuildResultText(SelectionProfile profile, int count, SearchScope scope)
        {
            string scopeText = scope == SearchScope.ActiveView ? "vista ativa" : "projeto inteiro";
            string parameterText = profile.ComparedParameters.Count == 0
                ? "Nenhum parâmetro editável relevante foi comparado; a seleção usou categoria/família/tipo."
                : "Parâmetros comparados: " + string.Join(", ", profile.ComparedParameters.Take(18)) +
                  (profile.ComparedParameters.Count > 18 ? $" e mais {profile.ComparedParameters.Count - 18}." : ".");

            return
                $"Elementos selecionados: {count}\n" +
                $"Escopo: {scopeText}\n" +
                $"Categoria: {profile.CategoryName}\n" +
                $"Família/Tipo: {profile.FamilyName} / {profile.TypeName}\n\n" +
                parameterText;
        }

        private enum SearchScope
        {
            ActiveView,
            EntireProject
        }

        private class SelectionProfile
        {
            private readonly List<ParameterSignature> _parameters;

            private SelectionProfile(
                ElementId categoryId,
                ElementId typeId,
                string categoryName,
                string familyName,
                string typeName,
                List<ParameterSignature> parameters)
            {
                CategoryId = categoryId;
                TypeId = typeId;
                CategoryName = categoryName;
                FamilyName = familyName;
                TypeName = typeName;
                _parameters = parameters;
                ComparedParameters = parameters.Select(parameter => parameter.Name).ToList();
            }

            public ElementId CategoryId { get; }
            public ElementId TypeId { get; }
            public string CategoryName { get; }
            public string FamilyName { get; }
            public string TypeName { get; }
            public List<string> ComparedParameters { get; }

            public static SelectionProfile Create(Document doc, Element source)
            {
                ElementId typeId = source.GetTypeId();
                ElementType type = typeId == ElementId.InvalidElementId ? null : doc.GetElement(typeId) as ElementType;
                string familyName = BuildFamilyName(doc, source, type);
                string typeName = type?.Name ?? source.Name ?? string.Empty;

                List<ParameterSignature> parameters = source.Parameters
                    .Cast<Parameter>()
                    .Where(IsComparableInstanceParameter)
                    .Select(parameter => ParameterSignature.From(parameter))
                    .Where(signature => signature != null)
                    .GroupBy(signature => signature.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(signature => signature.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new SelectionProfile(
                    source.Category?.Id ?? ElementId.InvalidElementId,
                    typeId,
                    source.Category?.Name ?? "Sem categoria",
                    familyName,
                    typeName,
                    parameters);
            }

            public bool Matches(Document doc, Element candidate)
            {
                if (candidate == null)
                    return false;

                if (CategoryId != ElementId.InvalidElementId && candidate.Category?.Id != CategoryId)
                    return false;

                if (candidate.GetTypeId() != TypeId)
                    return false;

                foreach (ParameterSignature signature in _parameters)
                {
                    Parameter parameter = FindParameter(candidate, signature.Name);
                    if (parameter == null || !signature.Matches(parameter))
                        return false;
                }

                return true;
            }

            private static string BuildFamilyName(Document doc, Element element, ElementType type)
            {
                if (element is FamilyInstance instance)
                    return instance.Symbol?.FamilyName ?? string.Empty;

                return type?.FamilyName ?? string.Empty;
            }
        }

        private class ParameterSignature
        {
            private ParameterSignature(string name, StorageType storageType, object value)
            {
                Name = name;
                StorageType = storageType;
                Value = value;
            }

            public string Name { get; }
            public StorageType StorageType { get; }
            public object Value { get; }

            public static ParameterSignature From(Parameter parameter)
            {
                try
                {
                    string name = NormalizeName(parameter.Definition?.Name);
                    switch (parameter.StorageType)
                    {
                        case StorageType.Double:
                            return new ParameterSignature(name, parameter.StorageType, parameter.AsDouble());
                        case StorageType.Integer:
                            return new ParameterSignature(name, parameter.StorageType, parameter.AsInteger());
                        case StorageType.String:
                            return new ParameterSignature(name, parameter.StorageType, NormalizeText(parameter.AsString()));
                        case StorageType.ElementId:
                            return new ParameterSignature(name, parameter.StorageType, parameter.AsElementId().IntegerValue);
                        default:
                            return null;
                    }
                }
                catch
                {
                    return null;
                }
            }

            public bool Matches(Parameter parameter)
            {
                if (parameter.StorageType != StorageType)
                    return false;

                try
                {
                    switch (StorageType)
                    {
                        case StorageType.Double:
                            return Math.Abs(parameter.AsDouble() - (double)Value) <= DoubleTolerance;
                        case StorageType.Integer:
                            return parameter.AsInteger() == (int)Value;
                        case StorageType.String:
                            return string.Equals(NormalizeText(parameter.AsString()), (string)Value, StringComparison.OrdinalIgnoreCase);
                        case StorageType.ElementId:
                            return parameter.AsElementId().IntegerValue == (int)Value;
                        default:
                            return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        private static Parameter FindParameter(Element element, string normalizedName)
        {
            string searchName = NormalizeForSearch(normalizedName);
            foreach (Parameter parameter in element.Parameters)
            {
                if (string.Equals(NormalizeForSearch(parameter.Definition?.Name), searchName, StringComparison.OrdinalIgnoreCase))
                    return parameter;
            }

            return null;
        }

        private static bool IsComparableInstanceParameter(Parameter parameter)
        {
            if (parameter == null || parameter.StorageType == StorageType.None)
                return false;

            string name = NormalizeName(parameter.Definition?.Name);
            if (string.IsNullOrWhiteSpace(name) || IsIgnoredParameterName(name))
                return false;

            if (!parameter.IsReadOnly)
                return true;

            return IsShapeIdentityParameterName(name);
        }

        private static bool IsShapeIdentityParameterName(string name)
        {
            string normalized = NormalizeForSearch(name);

            string[] exact =
            {
                "a",
                "angulo",
                "angle",
                "angulo de curvatura",
                "curvatura",
                "bend angle",
                "angle of bend"
            };

            if (exact.Contains(normalized))
                return true;

            string[] contains =
            {
                "bend",
                "curv"
            };

            return contains.Any(normalized.Contains);
        }

        private static bool IsIgnoredParameterName(string name)
        {
            string normalized = NormalizeForSearch(name);

            string[] exact =
            {
                "id",
                "level",
                "nivel",
                "host",
                "workset",
                "comments",
                "comentarios",
                "comentario",
                "mark",
                "marca",
                "description",
                "descricao",
                "codigo",
                "codigo ofc",
                "ambiente",
                "room",
                "space",
                "fase",
                "phase",
                "system name",
                "system type",
                "system classification",
                "nome do sistema",
                "tipo de sistema",
                "classificacao do sistema"
            };

            if (exact.Contains(normalized))
                return true;

            string[] contains =
            {
                "elevation",
                "offset",
                "level",
                "nivel",
                "schedule",
                "tag",
                "room",
                "space",
                "area",
                "ambiente",
                "comment",
                "coment",
                "mark",
                "codigo",
                "code",
                "system",
                "sistema",
                "ifc",
                "omniclass",
                "assembly",
                "fase",
                "phase",
                "workset"
            };

            return contains.Any(normalized.Contains);
        }

        private static string NormalizeName(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeForSearch(string value)
        {
            string trimmed = NormalizeText(value).ToLowerInvariant();
            string decomposed = trimmed.Normalize(System.Text.NormalizationForm.FormD);
            char[] chars = decomposed
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray();
            return new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}

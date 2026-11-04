using System;
using Autodesk.Revit.DB;
using RevitFamilyImporter.VisibilityInspector.Models;
using RevitFamilyImporter.VisibilityInspector.Utils;
using RevitView = Autodesk.Revit.DB.View;

namespace RevitFamilyImporter.VisibilityInspector.Services
{
    public class GeometryVisibilityAnalysisService : IVisibilityAnalyzer
    {
        public string Name => "Geometria visível";
        public int Priority => 14;

        public VisibilityIssue Analyze(VisibilityAnalysisContext context)
        {
            VisibilityIssue issue = new VisibilityIssue
            {
                Key = "GeometryVisibility",
                Title = "Geometria visível na vista",
                Priority = Priority,
                Severity = IssueSeverity.Ok,
                Summary = "Geometria renderizável encontrada na vista."
            };

            GeometryStats visible = ReadGeometry(context, false, issue);
            GeometryStats includingHidden = ReadGeometry(context, true, issue);
            BoundingBoxXYZ viewBox = context.GetElementBoundingBoxInHostCoordinates();
            BoundingBoxXYZ modelBox = SafeBoundingBox(context.Element, null);

            issue.Details.Add($"Sólidos/malhas visíveis na vista: {visible.RenderableCount}");
            issue.Details.Add($"Curvas visíveis na vista: {visible.CurveCount}");
            issue.Details.Add($"Geometria incluindo itens não visíveis: {includingHidden.TotalCount}");
            issue.Details.Add($"BoundingBox na vista: {FormatBoundingBox(viewBox)}");
            issue.Details.Add($"BoundingBox no modelo: {FormatBoundingBox(modelBox)}");
            AddViewTemplateDetail(context, issue);
            AddFamilyHints(context, issue);

            if (visible.RenderableCount == 0 && includingHidden.RenderableCount > 0)
            {
                issue.Severity = IssueSeverity.Blocked;
                issue.Summary = "A família tem geometria 3D, mas ela não está sendo retornada como visível nesta vista.";
                issue.Details.Add("Causa provável: visibilidade interna da família, parâmetro de visibilidade do tipo/instância, subcategoria da família, nível de detalhe ou template da vista.");
                return issue;
            }

            if (visible.RenderableCount == 0 && visible.CurveCount > 0 && IsModelFamily(context.Element))
            {
                issue.Severity = IssueSeverity.Warning;
                issue.Summary = "A vista retorna apenas curvas/simbologia, sem sólido ou malha 3D para este elemento.";
                issue.Details.Add("Para famílias de modelo, isso costuma indicar representação 3D desligada na família ou controlada por nível de detalhe/parâmetro.");
                return issue;
            }

            if (visible.TotalCount == 0 && (viewBox != null || modelBox != null))
            {
                issue.Severity = IssueSeverity.Blocked;
                issue.Summary = "O elemento existe e tem extensão, mas nenhuma geometria visível foi retornada para a vista.";
                issue.Details.Add("Causa provável: regra interna de visibilidade da família/tipo ou configuração gráfica controlada pelo template da vista.");
                return issue;
            }

            return issue;
        }

        private static GeometryStats ReadGeometry(VisibilityAnalysisContext context, bool includeNonVisible, VisibilityIssue issue)
        {
            GeometryStats stats = new GeometryStats();
            try
            {
                Options options = new Options
                {
                    IncludeNonVisibleObjects = includeNonVisible,
                    ComputeReferences = false
                };

                if (context.IsLinkedElement)
                {
                    options.DetailLevel = context.View.DetailLevel;
                    issue.Details.Add(includeNonVisible
                        ? "Geometria do link lida no documento vinculado incluindo objetos não visíveis."
                        : "Geometria do link lida no documento vinculado; a visibilidade final ainda depende da instância do link no host.");
                }
                else
                {
                    options.View = context.View;
                }

                GeometryElement geometry = context.Element.get_Geometry(options);
                CountGeometry(geometry, stats);
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Leitura de geometria ({(includeNonVisible ? "inclui não visível" : "visível")}): {ex.Message}");
            }

            return stats;
        }

        private static void CountGeometry(GeometryElement geometry, GeometryStats stats)
        {
            if (geometry == null)
                return;

            foreach (GeometryObject item in geometry)
                CountObject(item, stats);
        }

        private static void CountObject(GeometryObject item, GeometryStats stats)
        {
            if (item == null)
                return;

            stats.TotalCount++;

            if (item is Solid solid)
            {
                if (solid.Faces.Size > 0 || solid.Edges.Size > 0)
                    stats.RenderableCount++;
                return;
            }

            if (item is Mesh mesh)
            {
                if (mesh.NumTriangles > 0)
                    stats.RenderableCount++;
                return;
            }

            if (item is Curve)
            {
                stats.CurveCount++;
                return;
            }

            if (item is GeometryInstance instance)
            {
                CountGeometry(instance.GetInstanceGeometry(), stats);
                return;
            }

            if (item is Autodesk.Revit.DB.Point)
                stats.PointCount++;
        }

        private static BoundingBoxXYZ SafeBoundingBox(Element element, RevitView view)
        {
            try { return element.get_BoundingBox(view); }
            catch { return null; }
        }

        private static string FormatBoundingBox(BoundingBoxXYZ box)
        {
            if (box == null)
                return "não disponível";

            return $"Min({Format(box.Min.X)}, {Format(box.Min.Y)}, {Format(box.Min.Z)}) Max({Format(box.Max.X)}, {Format(box.Max.Y)}, {Format(box.Max.Z)})";
        }

        private static string Format(double value)
        {
            return value.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void AddViewTemplateDetail(VisibilityAnalysisContext context, VisibilityIssue issue)
        {
            try
            {
                if (context.View.ViewTemplateId != ElementId.InvalidElementId && context.HostDocument.GetElement(context.View.ViewTemplateId) is RevitView template)
                    issue.Details.Add($"Template aplicado: {template.Name} ({RevitText.Id(template.Id)})");
                else
                    issue.Details.Add("Template aplicado: nenhum");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Template aplicado: não foi possível avaliar ({ex.Message}).");
            }
        }

        private static void AddFamilyHints(VisibilityAnalysisContext context, VisibilityIssue issue)
        {
            try
            {
                issue.Details.Add($"Família: {RevitText.FamilyName(context.ElementDocument, context.Element)}");
                issue.Details.Add($"Tipo: {RevitText.ElementTypeName(context.ElementDocument, context.Element)}");
                issue.Details.Add($"Nível de detalhe da vista: {context.View.DetailLevel}");

                int suspicious = 0;
                suspicious += AddVisibilityParameters(context.ElementDocument, context.Element, issue, "Instância");
                ElementId typeId = context.Element.GetTypeId();
                if (typeId != ElementId.InvalidElementId && context.ElementDocument.GetElement(typeId) is ElementType type)
                    suspicious += AddVisibilityParameters(context.ElementDocument, type, issue, "Tipo");

                if (suspicious == 0)
                    issue.Details.Add("Parâmetros com nome de visibilidade: nenhum encontrado na instância/tipo.");
            }
            catch (Exception ex)
            {
                issue.Details.Add($"Dicas de família/tipo: não foi possível avaliar ({ex.Message}).");
            }
        }

        private static int AddVisibilityParameters(Document document, Element element, VisibilityIssue issue, string source)
        {
            int count = 0;
            foreach (Parameter parameter in element.Parameters)
            {
                string name = parameter?.Definition?.Name ?? string.Empty;
                if (!LooksLikeVisibilityParameter(name))
                    continue;

                count++;
                issue.Details.Add($"{source} - {name}: {RevitText.ParameterValue(document, parameter)}");
            }

            return count;
        }

        private static bool LooksLikeVisibilityParameter(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.IndexOf("visib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("visible", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("mostrar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("show", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("3d", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsModelFamily(Element element)
        {
            Category category = element.Category;
            return category == null || category.CategoryType == CategoryType.Model;
        }

        private class GeometryStats
        {
            public int TotalCount { get; set; }
            public int RenderableCount { get; set; }
            public int CurveCount { get; set; }
            public int PointCount { get; set; }
        }
    }
}

using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Import;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace RevitFamilyImporter.Import.DetailImport
{
    public class DetailDwgWriter : IDisposable
    {
        private readonly ILogger _logger;
        private readonly GstarCadDetailExporter _gstarExporter;

        public DetailDwgWriter(ILogger logger)
        {
            _logger = logger ?? new Logger("DetailImport");
            _gstarExporter = new GstarCadDetailExporter(_logger);
        }

        public string WriteDetail(DwgDocumentModel source, DetailRegion region, string outputFolder, ImportSettings settings = null, CancellationToken cancellationToken = default)
        {
            if (source?.NativeDocument == null)
                throw new ArgumentNullException(nameof(source));

            if (region == null)
                throw new ArgumentNullException(nameof(region));

            settings = settings ?? new ImportSettings();
            settings.Normalize();

            Directory.CreateDirectory(outputFolder);

            if (region.Entities.Count > settings.MaxEntitiesPerDetail)
            {
                throw new InvalidOperationException(
                    $"O detalhe {region.Name} possui {region.Entities.Count} entidades, acima do limite configurado de {settings.MaxEntitiesPerDetail}. " +
                    "Reduza o recorte ou aumente o limite após revisar o DWG.");
            }

            if (settings.UseGstarCadHighFidelity)
            {
                if (_gstarExporter.TryExport(
                    source,
                    region,
                    outputFolder,
                    settings.BoundaryLayer,
                    settings.NameLayer,
                    settings.MaxEntitiesPerDetail,
                    cancellationToken,
                    out string highFidelityPath))
                {
                    return highFidelityPath;
                }

                throw new InvalidOperationException(
                    "Não foi possível exportar o detalhe com alta fidelidade no GstarCAD. " +
                    "O motor interno não foi usado. Confira o log, feche outras sessões do GstarCAD e tente novamente.");
            }

            _logger.Info($"Detalhe {region.Name}: motor interno selecionado pelo usuário.");

            string name = DetailNamingService.CleanName(region.Name);
            if (string.IsNullOrWhiteSpace(name))
                name = $"Detalhe_{region.Id:000}";

            string filePath = Path.Combine(outputFolder, $"{region.Id:000}_{name}.dwg");
            CadDocument document = new CadDocument(GetWritableVersion(source.NativeDocument.Header.Version));
            document.CreateDefaults();

            foreach (Layer layer in source.NativeDocument.Layers)
                EnsureLayer(document, layer);

            foreach (LineType lineType in source.NativeDocument.LineTypes)
                EnsureLineType(document, lineType);

            foreach (TextStyle style in source.NativeDocument.TextStyles)
                EnsureTextStyle(document, style);

            HashSet<string> visitingBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DwgEntityInfo info in region.Entities)
            {
                try
                {
                    Entity clone = CloneEntity(info.NativeEntity, document, visitingBlocks);
                    if (clone != null)
                    {
                        document.Entities.Add(clone);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Entidade {info.Handle} ({info.Kind}) do detalhe {region.Name} nao foi clonada pelo motor interno: {ex.Message}");
                }
            }

            document.UpdateCollections(true, true);

            using (var writer = new DwgWriter(filePath, document))
            {
                writer.OnNotification += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args?.Message))
                        _logger.Warn(args.Message);
                };
                writer.Write();
            }

            return filePath;
        }

        public void Dispose()
        {
            _gstarExporter?.Dispose();
        }

        private Entity CloneEntity(Entity sourceEntity, CadDocument target, HashSet<string> visitingBlocks)
        {
            if (sourceEntity == null)
                return null;

            if (sourceEntity is Insert sourceInsert)
            {
                BlockRecord block = EnsureBlockRecord(target, sourceInsert.Block, visitingBlocks);
                if (block == null)
                    return null;

                var insert = new Insert(block)
                {
                    InsertPoint = sourceInsert.InsertPoint,
                    Normal = sourceInsert.Normal,
                    Rotation = sourceInsert.Rotation,
                    XScale = sourceInsert.XScale,
                    YScale = sourceInsert.YScale,
                    ZScale = sourceInsert.ZScale,
                    RowCount = sourceInsert.RowCount,
                    RowSpacing = sourceInsert.RowSpacing,
                    ColumnCount = sourceInsert.ColumnCount,
                    ColumnSpacing = sourceInsert.ColumnSpacing
                };

                AssignCommonReferences(insert, sourceEntity, target, visitingBlocks);

                foreach (AttributeEntity attribute in sourceInsert.Attributes)
                {
                    var clonedAttribute = (AttributeEntity)attribute.Clone();
                    AssignCommonReferences(clonedAttribute, attribute, target, visitingBlocks);
                    insert.Attributes.Add(clonedAttribute);
                }

                return insert;
            }

            try
            {
                Entity clone = (Entity)sourceEntity.Clone();
                AssignCommonReferences(clone, sourceEntity, target, visitingBlocks);
                return clone;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Falha ao clonar entidade CAD {sourceEntity.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private void AssignCommonReferences(
            Entity clone,
            Entity source,
            CadDocument target,
            HashSet<string> visitingBlocks)
        {
            clone.Layer = EnsureLayer(target, source.Layer);

            if (source.LineType != null)
                clone.LineType = EnsureLineType(target, source.LineType);

            CadGraphicStyleCopier.CopyEntityOverrides(clone, source);

            if (clone is TextEntity cloneText && source is TextEntity sourceText)
                cloneText.Style = EnsureTextStyle(target, sourceText.Style);

            if (clone is MText cloneMText && source is MText sourceMText)
                cloneMText.Style = EnsureTextStyle(target, sourceMText.Style);

            if (clone is Insert && source is Insert)
                return;
        }

        private Layer EnsureLayer(CadDocument target, Layer sourceLayer)
        {
            string name = sourceLayer?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = "0";

            if (target.Layers.TryGetValue(name, out Layer existing))
                return existing;

            Layer layer;
            try
            {
                layer = sourceLayer != null
                    ? (Layer)sourceLayer.Clone()
                    : new Layer(name);
            }
            catch
            {
                layer = new Layer(name);
            }

            CadGraphicStyleCopier.CopyLayerGraphics(layer, sourceLayer, lineType => EnsureLineType(target, lineType));
            target.Layers.Add(layer);
            return layer;
        }

        private LineType EnsureLineType(CadDocument target, LineType sourceLineType)
        {
            string name = sourceLineType?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = "Continuous";

            if (target.LineTypes.TryGetValue(name, out LineType existing))
                return existing;

            try
            {
                var clone = (LineType)sourceLineType.Clone();
                target.LineTypes.Add(clone);
                return clone;
            }
            catch
            {
                target.LineTypes.TryGetValue("Continuous", out existing);
                return existing;
            }
        }

        private TextStyle EnsureTextStyle(CadDocument target, TextStyle sourceStyle)
        {
            string name = sourceStyle?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = "Standard";

            if (target.TextStyles.TryGetValue(name, out TextStyle existing))
                return existing;

            try
            {
                var clone = (TextStyle)sourceStyle.Clone();
                target.TextStyles.Add(clone);
                return clone;
            }
            catch
            {
                target.TextStyles.TryGetValue("Standard", out existing);
                return existing;
            }
        }

        private BlockRecord EnsureBlockRecord(
            CadDocument target,
            BlockRecord sourceBlock,
            HashSet<string> visitingBlocks)
        {
            if (sourceBlock == null || string.IsNullOrWhiteSpace(sourceBlock.Name))
                return null;

            if (target.BlockRecords.TryGetValue(sourceBlock.Name, out BlockRecord existing))
                return existing;

            if (!visitingBlocks.Add(sourceBlock.Name))
                return null;

            var block = new BlockRecord(sourceBlock.Name)
            {
                Units = sourceBlock.Units
            };
            target.BlockRecords.Add(block);

            foreach (Entity sourceEntity in sourceBlock.Entities ?? Enumerable.Empty<Entity>())
            {
                Entity clonedEntity = CloneEntity(sourceEntity, target, visitingBlocks);
                if (clonedEntity != null)
                    block.Entities.Add(clonedEntity);
            }

            visitingBlocks.Remove(sourceBlock.Name);
            return block;
        }

        private static ACadVersion GetWritableVersion(ACadVersion sourceVersion)
        {
            switch (sourceVersion)
            {
                case ACadVersion.AC1014:
                case ACadVersion.AC1015:
                case ACadVersion.AC1018:
                case ACadVersion.AC1024:
                case ACadVersion.AC1027:
                case ACadVersion.AC1032:
                    return sourceVersion;
                default:
                    return ACadVersion.AC1032;
            }
        }
    }
}





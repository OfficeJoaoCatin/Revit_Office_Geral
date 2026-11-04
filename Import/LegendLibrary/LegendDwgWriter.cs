using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using RevitFamilyImporter.Import;
using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using RevitFamilyImporter.Services.DetailImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendDwgWriter
    {
        private readonly ILogger _logger;

        public LegendDwgWriter(ILogger logger)
        {
            _logger = logger ?? new Logger("LegendLibrary");
        }

        public string Write(DwgDocumentModel source, LegendComposition composition, string outputFolder)
        {
            if (source?.NativeDocument == null)
                throw new ArgumentNullException(nameof(source));

            if (composition == null || composition.PlacedItems.Count == 0)
                throw new InvalidOperationException("Nenhum item foi selecionado para gerar a legenda.");

            Directory.CreateDirectory(outputFolder);
            string filePath = Path.Combine(outputFolder, $"Legenda_CAD_{DateTime.Now:yyyyMMdd_HHmmss}.dwg");

            CadDocument document = new CadDocument(GetWritableVersion(source.NativeDocument.Header.Version));
            document.CreateDefaults();

            foreach (Layer layer in source.NativeDocument.Layers)
                EnsureLayer(document, layer);

            foreach (LineType lineType in source.NativeDocument.LineTypes)
                EnsureLineType(document, lineType);

            foreach (TextStyle style in source.NativeDocument.TextStyles)
                EnsureTextStyle(document, style);

            foreach (KeyValuePair<string, DwgPoint2> title in composition.SectionTitlePositions)
                document.Entities.Add(CreateTitleText(title.Key, title.Value, document));

            HashSet<string> visitingBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LegendPlacedItem placed in composition.PlacedItems)
            {
                DwgPoint2 offset = new DwgPoint2(
                    placed.TargetOrigin.X - placed.Item.Bounds.MinX,
                    placed.TargetOrigin.Y - placed.Item.Bounds.MinY);

                foreach (DwgEntityInfo info in placed.Item.Entities)
                {
                    try
                    {
                        Entity clone = CloneEntity(info.NativeEntity, document, visitingBlocks);
                        if (clone == null)
                            continue;

                        clone.ApplyTransform(Transform.CreateTranslation(new XYZ(offset.X, offset.Y, 0)));
                        document.Entities.Add(clone);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Entidade {info.Handle} ({info.Kind}) da legenda {placed.Item.Name} nao foi clonada: {ex.Message}");
                    }
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

        private TextEntity CreateTitleText(string sectionTitle, DwgPoint2 point, CadDocument document)
        {
            var text = new TextEntity
            {
                Value = $"DESCRICAO DO SIMBOLO - {sectionTitle}".ToUpperInvariant(),
                Height = 3.0,
                InsertPoint = new XYZ(point.X, point.Y, 0)
            };

            if (document.TextStyles.TryGetValue("Standard", out TextStyle style))
                text.Style = style;

            return text;
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

            Entity clone = (Entity)sourceEntity.Clone();
            AssignCommonReferences(clone, sourceEntity, target, visitingBlocks);
            return clone;
        }

        private void AssignCommonReferences(Entity clone, Entity source, CadDocument target, HashSet<string> visitingBlocks)
        {
            clone.Layer = EnsureLayer(target, source.Layer);

            if (source.LineType != null)
                clone.LineType = EnsureLineType(target, source.LineType);

            CadGraphicStyleCopier.CopyEntityOverrides(clone, source);

            if (clone is TextEntity cloneText && source is TextEntity sourceText)
                cloneText.Style = EnsureTextStyle(target, sourceText.Style);

            if (clone is MText cloneMText && source is MText sourceMText)
                cloneMText.Style = EnsureTextStyle(target, sourceMText.Style);
        }

        private Layer EnsureLayer(CadDocument target, Layer sourceLayer)
        {
            string name = sourceLayer?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = "0";

            if (target.Layers.TryGetValue(name, out Layer existing))
                return existing;

            Layer layer;
            try { layer = sourceLayer != null ? (Layer)sourceLayer.Clone() : new Layer(name); }
            catch { layer = new Layer(name); }

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

        private BlockRecord EnsureBlockRecord(CadDocument target, BlockRecord sourceBlock, HashSet<string> visitingBlocks)
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



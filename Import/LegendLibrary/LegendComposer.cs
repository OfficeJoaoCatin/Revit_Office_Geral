using RevitFamilyImporter.Models.DetailImport;
using RevitFamilyImporter.Models.LegendLibrary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitFamilyImporter.Import.LegendLibrary
{
    public class LegendComposer
    {
        public LegendComposition Compose(IEnumerable<LegendSection> sections, LegendSettings settings)
        {
            settings = settings ?? new LegendSettings();
            settings.Normalize();

            var composition = new LegendComposition();
            List<LegendSection> sourceSections = (sections ?? Enumerable.Empty<LegendSection>())
                .Where(section => section != null && section.Items.Count > 0)
                .ToList();

            foreach (LegendSection section in sourceSections)
                composition.Sections.Add(section);

            double y = 0;
            double maxX = 0;
            double minY = 0;
            bool hasAny = false;

            foreach (LegendSection section in sourceSections)
            {
                y -= Math.Max(0, section.SpacingBefore);
                string title = $"DESCRICAO DO SIMBOLO - {section.Title}".ToUpperInvariant();
                composition.SectionTitlePositions[section.Title] = new DwgPoint2(0, y);
                y -= settings.TitleHeight + settings.TitleItemSpacing;

                if (settings.LayoutMode == LegendLayoutMode.Grid)
                {
                    LayoutGrid(section, settings, composition, ref y, ref maxX, ref minY, ref hasAny);
                }
                else
                {
                    LayoutVertical(section, settings, composition, ref y, ref maxX, ref minY, ref hasAny);
                }

                y -= settings.GroupSpacing + Math.Max(0, section.SpacingAfter);
                minY = Math.Min(minY, y);
            }

            composition.Bounds = hasAny
                ? new DwgBounds(0, minY, Math.Max(maxX, 1), settings.TitleHeight)
                : new DwgBounds(0, 0, 1, 1);

            return composition;
        }

        private static void LayoutVertical(
            LegendSection section,
            LegendSettings settings,
            LegendComposition composition,
            ref double y,
            ref double maxX,
            ref double minY,
            ref bool hasAny)
        {
            double symbolTextGap = Math.Max(3, settings.HorizontalSpacing * 0.35);
            double maxSymbolWidth = section.Items.Select(i => Math.Max(i.Width, 1)).DefaultIfEmpty(1).Max();

            foreach (LegendItem item in section.Items)
            {
                double height = Math.Max(item.Height, settings.TitleHeight * 2.0);
                y -= height;

                AddPlacement(section, item, composition, 0, y, maxSymbolWidth + symbolTextGap, y + height * 0.5);

                maxX = Math.Max(maxX, maxSymbolWidth + symbolTextGap + Math.Max(item.Name?.Length ?? 0, 6) * settings.TitleHeight * 0.65);
                minY = Math.Min(minY, y);
                y -= settings.VerticalSpacing;
                hasAny = true;
            }
        }

        private static void LayoutGrid(
            LegendSection section,
            LegendSettings settings,
            LegendComposition composition,
            ref double y,
            ref double maxX,
            ref double minY,
            ref bool hasAny)
        {
            int perColumn = Math.Max(1, settings.ItemsPerColumn);
            double cellWidth = section.Items.Select(i => Math.Max(i.Width, 1)).DefaultIfEmpty(1).Max() + settings.HorizontalSpacing;
            double cellHeight = section.Items.Select(i => Math.Max(i.Height, 1)).DefaultIfEmpty(1).Max() + settings.VerticalSpacing;
            int rows = Math.Min(perColumn, section.Items.Count);

            for (int index = 0; index < section.Items.Count; index++)
            {
                LegendItem item = section.Items[index];
                int column = index / perColumn;
                int row = index % perColumn;
                double x = column * cellWidth;
                double itemY = y - row * cellHeight - Math.Max(item.Height, 1);
                double textX = x + Math.Max(item.Width, 1) + Math.Max(3, settings.HorizontalSpacing * 0.35);
                double textY = itemY + Math.Max(item.Height, 1) * 0.5;

                AddPlacement(section, item, composition, x, itemY, textX, textY);

                maxX = Math.Max(maxX, textX + Math.Max(item.Name?.Length ?? 0, 6) * settings.TitleHeight * 0.65);
                minY = Math.Min(minY, itemY);
                hasAny = true;
            }

            y -= rows * cellHeight;
        }

        private static void AddPlacement(
            LegendSection section,
            LegendItem item,
            LegendComposition composition,
            double x,
            double y,
            double textX,
            double textY)
        {
            composition.PlacedItems.Add(new LegendPlacedItem
            {
                Section = section,
                Item = item,
                TargetOrigin = new DwgPoint2(x, y),
                TextX = textX,
                TextY = textY
            });
        }
    }
}



using System;
using System.Collections.Generic;
using System.Text;

namespace RevitFamilyImporter.LinkConflict.Services
{
    // Não mistura itens não verificados com conflitos confirmados.
    internal sealed class UnverifiedCoverage
    {
        private const int MaxListedEntries = 10000;
        private readonly HashSet<string> _entries = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _orderedEntries = new List<string>();
        private bool _truncated;
        public long LinkedWithoutSolids { get; private set; }
        public long PairsWithoutLinkedSolids { get; private set; }
        public long PairsWithoutHostSolids { get; private set; }
        public long PairsWithIncompleteGeometry { get; private set; }
        public long PairsWithBooleanFailure { get; private set; }
        public long LinksWithoutReliableBounds { get; private set; }
        public long HostPairsWithoutReliableBounds { get; private set; }
        public bool HasIssues => LinkedWithoutSolids > 0 || PairsWithoutLinkedSolids > 0 || PairsWithoutHostSolids > 0 ||
            PairsWithIncompleteGeometry > 0 || PairsWithBooleanFailure > 0 || LinksWithoutReliableBounds > 0 || HostPairsWithoutReliableBounds > 0;
        public bool CanAddDiagnosticDetail => _orderedEntries.Count < 20;

        public void RecordLinkedWithoutSolids(int id, string category)
        {
            LinkedWithoutSolids++;
            Add($"Link #{id} ({category}): sem sólidos utilizáveis; o elemento não foi verificado integralmente.");
        }
        public void RecordLinkedBounds(int id, string category)
        {
            LinksWithoutReliableBounds++;
            Add($"Link #{id} ({category}): sem caixa confiável; a área pode não ter sido totalmente pesquisada.");
        }
        public void RecordHostBounds(int id, string category)
        {
            HostPairsWithoutReliableBounds++;
            Add($"Hospedeiro #{id} ({category}): sem caixa confiável em uma comparação.");
        }
        public void RecordPair(PhysicalPairOutcome outcome, int linkedId, string linkedCategory, int hostId, string hostCategory, string details = null)
        {
            string suffix = string.IsNullOrEmpty(details) ? string.Empty : $" Detalhes: {details}";
            switch (outcome)
            {
                case PhysicalPairOutcome.MissingLinkedSolid:
                    PairsWithoutLinkedSolids++;
                    return; // O elemento do link é listado separadamente.
                case PhysicalPairOutcome.MissingHostSolid:
                    PairsWithoutHostSolids++;
                    Add($"Link #{linkedId} ({linkedCategory}) × hospedeiro #{hostId} ({hostCategory}): hospedeiro sem sólidos utilizáveis.{suffix}");
                    return;
                case PhysicalPairOutcome.IncompleteGeometry:
                    PairsWithIncompleteGeometry++;
                    Add($"Link #{linkedId} ({linkedCategory}) × hospedeiro #{hostId} ({hostCategory}): geometria incompleta.{suffix}");
                    return;
                case PhysicalPairOutcome.BooleanFailure:
                    PairsWithBooleanFailure++;
                    Add($"Link #{linkedId} ({linkedCategory}) × hospedeiro #{hostId} ({hostCategory}): falha na operação geométrica.{suffix}");
                    return;
            }
        }
        private void Add(string value)
        {
            if (_entries.Contains(value)) return;
            if (_orderedEntries.Count >= MaxListedEntries) { _truncated = true; return; }
            _entries.Add(value);
            _orderedEntries.Add(value);
        }
        public string Format(int maxDisplayEntries = MaxListedEntries)
        {
            var text = new StringBuilder();
            text.AppendLine("ITENS NÃO VERIFICADOS — NÃO SÃO CONFLITOS CONFIRMADOS");
            text.AppendLine("Os conflitos físicos destes itens não puderam ser descartados. Revise a geometria ou o modelo.");
            text.AppendLine($"Elementos do link sem sólidos: {LinkedWithoutSolids:N0}.");
            text.AppendLine($"Pares com link sem sólidos: {PairsWithoutLinkedSolids:N0}; pares com hospedeiro sem sólidos: {PairsWithoutHostSolids:N0}.");
            text.AppendLine($"Pares com geometria incompleta: {PairsWithIncompleteGeometry:N0}; com falha booleana: {PairsWithBooleanFailure:N0}.");
            text.AppendLine($"Elementos do link sem caixa confiável: {LinksWithoutReliableBounds:N0}; pares com hospedeiro sem caixa confiável: {HostPairsWithoutReliableBounds:N0}.");
            text.AppendLine("Os contadores podem se sobrepor; pares são comparações, não necessariamente elementos distintos.");
            text.AppendLine("Detalhes de geometria são limitados às primeiras 20 entradas para preservar desempenho.");
            if (_truncated) text.AppendLine($"A lista está limitada a {MaxListedEntries:N0} entradas; outros itens podem não estar listados.");
            text.AppendLine();
            int displayed = Math.Min(Math.Max(0, maxDisplayEntries), _orderedEntries.Count);
            if (displayed < _orderedEntries.Count) text.AppendLine($"Exibindo {displayed:N0} de {_orderedEntries.Count:N0} entradas registradas.");
            for (int index = 0; index < displayed; index++) text.AppendLine(_orderedEntries[index]);
            return text.ToString();
        }
    }
}

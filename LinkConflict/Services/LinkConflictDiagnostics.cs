using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RevitFamilyImporter.LinkConflict.Services
{
    // Dados mantidos apenas durante a amostra; IDs e categorias só são exibidos localmente.
    internal sealed class LinkConflictDiagnostics
    {
        public const int MaxLinkedElements = 200;
        public const int MaxPairs = 20000;
        public static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(60);
        private const int MaxExamples = 20;
        private readonly HashSet<int> _hosts = new HashSet<int>();
        private readonly HashSet<int> _extractedHosts = new HashSet<int>();
        private readonly Dictionary<string, int> _missingCategories = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _examples = new List<string>();
        private readonly Dictionary<string, int> _linkedMissingElements = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _unboundedLinkedExamples = new List<string>();
        private readonly List<string> _unboundedHostExamples = new List<string>();
        private readonly HashSet<int> _unboundedLinkedIds = new HashSet<int>();
        private readonly HashSet<int> _unboundedHostIds = new HashSet<int>();
        private long _emptyLinkSolidOccurrences, _emptyHostSolidOccurrences, _unboundedHostPairs;
        private readonly Dictionary<int, string> _linkedBounds = new Dictionary<int, string>();
        public long HostRequests { get; private set; }
        public long HostExtractions { get; private set; }
        public long RepeatedExtractions { get; private set; }
        public long OutlineCacheHits { get; private set; }
        public long OnlyLinkMissing { get; private set; }
        public long OnlyHostMissing { get; private set; }
        public long BothMissing { get; private set; }
        public int UniqueHosts => _hosts.Count;
        public long ReportedConflicts { get; set; }

        public void RecordHostRequest(int id, bool cacheMiss, bool outlineCacheHit = false)
        {
            HostRequests++;
            if (outlineCacheHit) OutlineCacheHits++;
            _hosts.Add(id);
            if (!cacheMiss) return;
            HostExtractions++;
            if (!_extractedHosts.Add(id)) RepeatedExtractions++;
        }

        public void RecordEmptySolids(int occurrences, bool linked)
        {
            if (linked) _emptyLinkSolidOccurrences += occurrences;
            else _emptyHostSolidOccurrences += occurrences;
        }
        public void RecordUnboundedLinked(int id, string category)
        {
            if (_unboundedLinkedIds.Add(id) && _unboundedLinkedExamples.Count < MaxExamples)
                _unboundedLinkedExamples.Add($"Link #{id} ({category})");
        }
        public void RecordUnboundedHost(int id, string category)
        {
            _unboundedHostPairs++;
            if (_unboundedHostIds.Add(id) && _unboundedHostExamples.Count < MaxExamples)
                _unboundedHostExamples.Add($"Hospedeiro #{id} ({category})");
        }

        public void RecordLinkedBounds(int id, string family, string type, double xFeet, double yFeet, double zFeet, int parts)
        {
            _linkedBounds[id] = $"família: {family}; tipo: {type}; caixa combinada: {xFeet * 0.3048:N2} × {yFeet * 0.3048:N2} × {zFeet * 0.3048:N2} m; partes: {parts:N0}";
        }

        public void RecordPair(int linkedId, string linkedCategory, string linkedType, int hostId, string hostCategory, int hostCategoryId, string hostType,
            bool linkedMissing, bool hostMissing)
        {
            if (!linkedMissing && !hostMissing) return;
            if (linkedMissing && hostMissing) BothMissing++;
            else if (linkedMissing) OnlyLinkMissing++;
            else OnlyHostMissing++;
            string side = linkedMissing && hostMissing ? "ambos" : linkedMissing ? "link" : "hospedeiro";
            string category = $"Link: {linkedCategory} ({linkedType}) | Hospedeiro: {hostCategory} (categoria #{hostCategoryId}; {hostType}) | Sem sólidos: {side}";
            if (linkedMissing)
            {
                _linkedBounds.TryGetValue(linkedId, out string bounds);
                string linkedKey = $"Link #{linkedId} — {linkedCategory} ({linkedType})" + (string.IsNullOrEmpty(bounds) ? "" : " | " + bounds);
                _linkedMissingElements.TryGetValue(linkedKey, out int linkedCount);
                _linkedMissingElements[linkedKey] = linkedCount + 1;
            }
            _missingCategories.TryGetValue(category, out int count);
            _missingCategories[category] = count + 1;
            if (_examples.Count < MaxExamples)
                _examples.Add($"Link #{linkedId} ({linkedCategory}; {linkedType}) × Hospedeiro #{hostId} ({hostCategory}; categoria #{hostCategoryId}; {hostType}) — sem sólidos: {side}");
        }

        public static (int Start, int End) GetSampleRange(int available, int startPosition)
        {
            if (available < 0) throw new ArgumentOutOfRangeException(nameof(available));
            if (startPosition < 1) throw new ArgumentOutOfRangeException(nameof(startPosition), "A posição inicial deve ser pelo menos 1.");
            if (available == 0)
            {
                if (startPosition != 1) throw new ArgumentOutOfRangeException(nameof(startPosition), "Não há elementos coletados para essa posição.");
                return (0, 0);
            }
            int start = startPosition - 1;
            if (start >= available)
                throw new ArgumentOutOfRangeException(nameof(startPosition), $"A posição inicial excede os {available:N0} elementos coletados.");
            return (start, start + Math.Min(available - start, MaxLinkedElements));
        }

        public static bool ShouldStop(long pairs, TimeSpan elapsed, out string reason)
        {
            if (pairs >= MaxPairs) { reason = $"Limite de {MaxPairs:N0} pares atingido."; return true; }
            if (elapsed >= MaxDuration) { reason = $"Limite de {MaxDuration.TotalSeconds:N0} segundos atingido."; return true; }
            reason = string.Empty;
            return false;
        }

        public string Format(string link, string categories, int available, int examined, int startPosition, long pairs,
            TimeSpan total, TimeSpan search, TimeSpan geometry, TimeSpan comparison, string stopReason,
            int regionQueries = 0, int emptyRegionReuses = 0, int emptyRegionSkips = 0,
            int excludedCurves = 0, bool curveScanPartial = false, TimeSpan curvePreparation = default, bool curveScanFailed = false)
        {
            var text = new StringBuilder();
            text.AppendLine("DIAGNÓSTICO PARCIAL — NÃO É UM RELATÓRIO COMPLETO DE CONFLITOS");
            text.AppendLine("Amostra parcial na ordem de coleta; não representa necessariamente o modelo inteiro. A ordem pode variar entre execuções.");
            text.AppendLine($"Limites: {MaxLinkedElements:N0} elementos do link, {MaxPairs:N0} pares ou {MaxDuration.TotalSeconds:N0} s; uma chamada individual do Revit pode ultrapassar o tempo.");
            text.AppendLine($"Motivo da parada: {stopReason}");
            text.AppendLine($"Posição inicial na coleta: {startPosition:N0} (base 1).");
            text.AppendLine("DWGs identificados, linhas/curvas e câmeras internas de vista identificadas no hospedeiro são ignorados. Definições PipeSegment e sistemas MEP lógicos são ignorados em ambos os modelos.");
            text.AppendLine($"Link: {link}");
            text.AppendLine($"Categorias selecionadas: {categories}");
            text.AppendLine($"Elementos do link iniciados: {examined:N0} de {available:N0} coletados; o último pode estar incompleto.");
            text.AppendLine($"Pares verificados: {pairs:N0}; conflitos físicos confirmados na amostra: {ReportedConflicts:N0}.");
            text.AppendLine($"Tempo total: {total.TotalSeconds:N1} s; busca: {search.TotalSeconds:N1} s; geometria: {geometry.TotalSeconds:N1} s; comparação: {comparison.TotalSeconds:N1} s.");
            text.AppendLine();
            text.AppendLine("LIMITES DE GEOMETRIA:");
            text.AppendLine($"Ocorrências de sólidos vazios ignoradas: link {_emptyLinkSolidOccurrences:N0}; extrações do hospedeiro {_emptyHostSolidOccurrences:N0} (podem incluir releituras).");
            text.AppendLine($"Elementos do link sem caixa confiável: {_unboundedLinkedIds.Count:N0}; pares com hospedeiro sem caixa confiável: {_unboundedHostPairs:N0} ({_unboundedHostIds.Count:N0} hospedeiros distintos).");
            text.AppendLine(_unboundedLinkedIds.Count > 0 || _unboundedHostPairs > 0
                ? "Esses elementos podem não ter sido totalmente comparados e exigem verificação."
                : "Nenhum limite de geometria não resolvido foi encontrado nesta amostra.");
            foreach (string example in _unboundedLinkedExamples) text.AppendLine(example);
            foreach (string example in _unboundedHostExamples) text.AppendLine(example);
            text.AppendLine();
            text.AppendLine("PARES ANALISADOS SEM SÓLIDOS UTILIZÁVEIS (não são necessariamente avisos):");
            text.AppendLine($"Somente no link: {OnlyLinkMissing:N0}; somente no hospedeiro: {OnlyHostMissing:N0}; em ambos: {BothMissing:N0}.");
            text.AppendLine();
            text.AppendLine("BUSCA ESPACIAL:");
            string curveStatus = curveScanFailed ? "indisponível; verificação por tipo mantida" : curveScanPartial ? "parcial" : "concluída";
            text.AppendLine($"Preparação: {excludedCurves:N0} linhas/curvas verificadas para exclusão antecipada; leitura {curveStatus}; {curvePreparation.TotalSeconds:N1} s.");

            text.AppendLine($"Consultas de regiões ampliadas: {regionQueries:N0}; dispensas por regiões vazias: {emptyRegionSkips:N0}; dessas, reutilizações: {emptyRegionReuses:N0}.");
            text.AppendLine("Somente regiões confirmadas vazias dispensam uma busca; regiões com candidatos seguem pela análise habitual.");
            text.AppendLine();
            text.AppendLine("GEOMETRIA DO HOSPEDEIRO:");
            text.AppendLine($"Solicitações: {HostRequests:N0}; elementos distintos: {UniqueHosts:N0}; extrações: {HostExtractions:N0}; extrações repetidas do mesmo elemento: {RepeatedExtractions:N0}.");
            text.AppendLine($"Reutilizações da cache de geometria: {HostRequests - HostExtractions:N0}. Os números referem-se somente à amostra.");
            text.AppendLine();
            text.AppendLine("CATEGORIAS DOS PARES SEM SÓLIDOS — até 10 combinações mais frequentes:");
            foreach (var entry in _missingCategories.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(10))
                text.AppendLine($"{entry.Value:N0} pares — {entry.Key}");
            if (_missingCategories.Count == 0) text.AppendLine("Nenhuma na amostra.");
            text.AppendLine();
            text.AppendLine("ELEMENTOS DO LINK SEM SÓLIDOS — até 10 com mais pares:");
            foreach (var entry in _linkedMissingElements.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(10))
                text.AppendLine($"{entry.Value:N0} pares — {entry.Key}");
            if (_linkedMissingElements.Count == 0) text.AppendLine("Nenhum na amostra.");
            text.AppendLine();
            text.AppendLine("EXEMPLOS DE PARES SEM SÓLIDOS — até 20:");
            foreach (string example in _examples) text.AppendLine(example);
            if (_examples.Count == 0) text.AppendLine("Nenhum na amostra.");
            return text.ToString();
        }
    }
}

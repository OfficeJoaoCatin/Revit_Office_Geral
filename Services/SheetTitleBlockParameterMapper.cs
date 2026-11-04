using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RevitFamilyImporter.Services
{
    internal static class SheetTitleBlockParameterMapper
    {
        private static readonly Dictionary<int, string[]> AliasesByIndex = new Dictionary<int, string[]>
        {
            { 0, new[] { "Nº Desenho", "N° Desenho", "Numero Desenho", "Número Desenho" } },
            { 1, new[] { "PAVIMENTO" } },
            { 2, new[] { "DESCRIÇÃO INSTALAÇÕES", "DESCRIÇÃO DAS INSTALAÇÕES" } },
            { 3, new[] { "ARQUIVO", "NOME DO ARQUIVO" } },
            { 4, new[] { "STATUS", "ASSUNTO / SETOR" } },
            { 5, new[] { "ESCALA" } },
            { 6, new[] { "CÓDIGO PLANTA", "CÓDIGO DA PLANTA" } },
            { 7, ReferenceAliases(1) },
            { 8, ReferenceAliases(2) },
            { 9, ReferenceAliases(3) },
            { 10, ReferenceAliases(4) },
            { 11, ReferenceAliases(5) },
            { 12, RevisionAliases("R00", "DATA") },
            { 13, RevisionAliases("R00", "DESCRICAO") },
            { 14, RevisionAliases("R00", "RESPONSAVEL") },
            { 15, RevisionAliases("R01", "DATA") },
            { 16, RevisionAliases("R01", "DESCRICAO") },
            { 17, RevisionAliases("R01", "RESPONSAVEL") },
            { 18, RevisionAliases("R02", "DATA") },
            { 19, RevisionAliases("R02", "DESCRICAO") },
            { 20, RevisionAliases("R02", "RESPONSAVEL") },
            { 21, RevisionAliases("R03", "DATA") },
            { 22, RevisionAliases("R03", "DESCRICAO") },
            { 23, RevisionAliases("R03", "RESPONSAVEL") },
            { 24, RevisionAliases("R04", "DATA") },
            { 25, RevisionAliases("R04", "DESCRICAO") },
            { 26, RevisionAliases("R04", "RESPONSAVEL") },
            { 27, RevisionAliases("R05", "DATA") },
            { 28, RevisionAliases("R05", "DESCRICAO") },
            { 29, RevisionAliases("R05", "RESPONSAVEL") },
            { 30, RevisionAliases("R06", "DATA") },
            { 31, RevisionAliases("R06", "DESCRICAO") },
            { 32, RevisionAliases("R06", "RESPONSAVEL") },
            { 33, RevisionAliases("R07", "DATA") },
            { 34, RevisionAliases("R07", "DESCRICAO") },
            { 35, RevisionAliases("R07", "RESPONSAVEL") },
            { 37, new[] { "ART", "NÚMERO DA ART" } },
            { 38, new[] { "CREA", "NÚMERO DO CREA" } },
            { 40, new[] { "ENDEREÇO" } },
            { 41, new[] { "REVISÃO", "REVISÃO ATUAL" } },
            { 42, new[] { "NOME DO PROJETO" } },
            { 43, new[] { "OBRA" } },
            { 44, new[] { "CLIENTE" } },
            { 45, new[] { "DISCIPLINA" } },
            { 46, new[] { "FASE DO PROJETO" } },
            { 47, new[] { "PROJETISTA" } },
            { 48, new[] { "SUPERVISOR" } },
            { 49, new[] { "DATA EMISSÃO", "DATA DE EMISSÃO" } },
            { 50, new[] { "RESPONSÁVEL" } },
            { 51, RevisionMarkerAliases("00", "R00") },
            { 52, RevisionMarkerAliases("01", "R01") },
            { 53, RevisionMarkerAliases("02", "R02") },
            { 54, RevisionMarkerAliases("03", "R03") },
            { 55, RevisionMarkerAliases("04", "R04") },
            { 56, RevisionMarkerAliases("05", "R05") },
            { 57, RevisionMarkerAliases("06", "R06") },
            { 58, RevisionMarkerAliases("07", "R07") }
        };

        public static IReadOnlyList<string> GetCandidates(int index, IReadOnlyList<string>? headers)
        {
            var names = new List<string>();
            if (headers != null && index < headers.Count && !string.IsNullOrWhiteSpace(headers[index]))
                names.Add(headers[index]);

            if (AliasesByIndex.TryGetValue(index, out string[] aliases))
                names.AddRange(aliases);

            return names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .SelectMany(ExpandAccentVariants)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string[] ReferenceAliases(int number)
        {
            return new[] { $"REFERÊNCIA-{number}", $"REFERENCIA-{number}", $"REF {number}", $"REF{number}", $"REFERÊNCIA {number}", $"REFERENCIA {number}" };
        }

        private static string[] RevisionAliases(string revision, string field)
        {
            string revisionNumber = revision.Substring(1);
            string twoDigitNumber = revisionNumber.PadLeft(2, '0');
            return new[]
            {
                $"{twoDigitNumber}-{field}",
                $"{field}-{twoDigitNumber}",
                $"{field} {revision}",
                $"{field}_{revision}",
                $"{revision} {field}",
                $"{revision}_{field}",
                $"{field} REVISÃO {revisionNumber}",
                $"REVISÃO {revisionNumber} {field}",
                $"{field} REVISAO {revisionNumber}",
                $"REVISAO {revisionNumber} {field}"
            };
        }

        private static string[] RevisionMarkerAliases(string twoDigitNumber, string revision)
        {
            return new[]
            {
                $"{twoDigitNumber}-REVISÃO",
                $"{twoDigitNumber}-REVISAO",
                $"REVISÃO-{twoDigitNumber}",
                $"REVISAO-{twoDigitNumber}",
                revision,
                $"REVISAO {revision}",
                $"REV_{revision}"
            };
        }

        private static IEnumerable<string> ExpandAccentVariants(string name)
        {
            yield return name;

            string withoutAccents = RemoveDiacritics(name);
            if (!string.Equals(withoutAccents, name, StringComparison.Ordinal))
                yield return withoutAccents;

            string accented = withoutAccents
                .Replace("NUMERO", "NÚMERO")
                .Replace("CODIGO", "CÓDIGO")
                .Replace("TITULO", "TÍTULO")
                .Replace("SUBTITULO", "SUBTÍTULO")
                .Replace("REVISAO", "REVISÃO")
                .Replace("EMISSAO", "EMISSÃO")
                .Replace("DESCRICAO", "DESCRIÇÃO")
                .Replace("OBSERVACAO", "OBSERVAÇÃO")
                .Replace("OBSERVACOES", "OBSERVAÇÕES");

            if (!string.Equals(accented, name, StringComparison.Ordinal))
                yield return accented;
        }

        private static string RemoveDiacritics(string value)
        {
            string normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (char ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    builder.Append(ch);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}

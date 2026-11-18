using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RevitFamilyImporter.LinkConflict.Services
{
    // As marcações são pessoais e ficam fora do modelo e dos arquivos compartilhados do Revit.
    internal sealed class LinkConflictCompletionStore
    {
        private readonly string _folder;
        private readonly HashSet<string> _completed;

        public LinkConflictCompletionStore(string hostIdentity, string linkedIdentity, string linkInstanceUniqueId, string? root = null)
        {
            if (string.IsNullOrWhiteSpace(hostIdentity) || string.IsNullOrWhiteSpace(linkedIdentity) || string.IsNullOrWhiteSpace(linkInstanceUniqueId))
                throw new ArgumentException("Não foi possível identificar o projeto e o vínculo para salvar as marcações.");
            string baseFolder = root ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OfficeGeral", "ConflitosEmLinks", "v1");
            _folder = Path.Combine(baseFolder, HashParts(hostIdentity, linkedIdentity, linkInstanceUniqueId));
            Directory.CreateDirectory(_folder);
            _completed = new HashSet<string>(
                Directory.EnumerateFiles(_folder, "*.done").Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool IsCompleted(string conflictKey)
        {
            return _completed.Contains(KeyHash(conflictKey));
        }

        public void SetCompleted(string conflictKey, bool completed)
        {
            string key = KeyHash(conflictKey);
            string path = Path.Combine(_folder, key + ".done");
            if (completed)
            {
                using (new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { }
                _completed.Add(key);
            }
            else
            {
                File.Delete(path);
                _completed.Remove(key);
            }
        }

        private static string KeyHash(string conflictKey)
        {
            if (string.IsNullOrWhiteSpace(conflictKey))
                throw new ArgumentException("Não foi possível identificar o conflito para salvar a marcação.");
            return HashParts(conflictKey);
        }

        private static string HashParts(params string[] parts)
        {
            var content = new StringBuilder();
            foreach (string part in parts)
            {
                string value = part ?? string.Empty;
                content.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
            }
            using (SHA256 sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(content.ToString())).Select(b => b.ToString("x2")));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RevitFamilyImporter.Utils;

namespace RevitFamilyImporter.Profiles
{
    public class ProfileService
    {
        public ProfileService()
        {
            ProfilesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIM Tools",
                "RevitFamilyImporter",
                "BatchExportProfiles");

            Directory.CreateDirectory(ProfilesFolder);
        }

        public string ProfilesFolder { get; }

        public IReadOnlyList<BatchExportProfile> List()
        {
            Directory.CreateDirectory(ProfilesFolder);

            return Directory.GetFiles(ProfilesFolder, "*.json")
                .Select(LoadFromFile)
                .Where(profile => profile != null)
                .Cast<BatchExportProfile>()
                .OrderBy(profile => profile.Name)
                .ToList();
        }

        public BatchExportProfile? Load(string name)
        {
            string path = GetProfilePath(name);
            return File.Exists(path) ? LoadFromFile(path) : null;
        }

        public void Save(BatchExportProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
                throw new ArgumentException("Informe um nome para o perfil.", nameof(profile));

            Directory.CreateDirectory(ProfilesFolder);
            string path = GetProfilePath(profile.Name);
            JsonStorage.Save(path, profile);
        }

        public void Delete(string name)
        {
            string path = GetProfilePath(name);
            if (File.Exists(path))
                File.Delete(path);
        }

        private BatchExportProfile? LoadFromFile(string path)
        {
            try
            {
                return JsonStorage.Load<BatchExportProfile>(path);
            }
            catch
            {
                return null;
            }
        }

        private string GetProfilePath(string name)
        {
            return Path.Combine(ProfilesFolder, SanitizeFileName(name) + ".json");
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "perfil" : value.Trim();
        }
    }
}

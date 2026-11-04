using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RevitFamilyImporter.Utils
{
    public static class JsonStorage
    {
        public static void Save<T>(string path, T value)
        {
            using FileStream stream = File.Create(path);
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            serializer.WriteObject(stream, value);
        }

        public static T? Load<T>(string path) where T : class
        {
            using FileStream stream = File.OpenRead(path);
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            return serializer.ReadObject(stream) as T;
        }

        public static string Serialize<T>(T value)
        {
            using MemoryStream stream = new MemoryStream();
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
            serializer.WriteObject(stream, value);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}

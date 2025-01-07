using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;

#pragma warning disable SYSLIB0011

namespace _86BoxManager.Core
{
    internal static class Serializer
    {
        public static T Read<T>(byte[] array)
        {
            JsonSerializerOptions options = new()
            {
                IncludeFields = true
            };

            using var ms = new MemoryStream(array);
            var res = (T) JsonSerializer.Deserialize<T>(ms, options);
            ms.Close();
            return res;
        }

        public static byte[] Write(object obj)
        {
            JsonSerializerOptions options = new()
            {
                IncludeFields = true
            };

            using var ms = new MemoryStream();
            JsonSerializer.Serialize(ms, obj, options);
            var data = ms.ToArray();
            return data;
        }
    }
}
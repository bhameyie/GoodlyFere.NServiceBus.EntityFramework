using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace GoodlyFere.NServiceBus.EntityFramework
{
    class DefaultSerializer
    {
        public static string Serialize<T>(T instance)
        {
            var serializer = BuildSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static T DeSerialize<T>(string data)
        {
            var serializer = BuildSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        static DataContractJsonSerializer BuildSerializer(Type objectType)
        {
            var settings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,

            };
            return new DataContractJsonSerializer(objectType, settings);
        }
    }
}

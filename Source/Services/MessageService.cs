using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;

namespace CMMT.Services
{
    public class MessageService
    {
        public int Code { get; }
        public string Message { get; }

        // Static cache for all mapping types
        private static readonly Lazy<Dictionary<string, Dictionary<int, string>>> _allMappings =
            new Lazy<Dictionary<string, Dictionary<int, string>>>(LoadAllMappings);

        private static Dictionary<string, Dictionary<int, string>> LoadAllMappings()
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "Message.json");
            if (!File.Exists(filePath))
                return new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mappingType in doc.RootElement.EnumerateObject())
            {
                var dict = new Dictionary<int, string>();
                foreach (var entry in mappingType.Value.EnumerateArray())
                {
                    if (entry.TryGetProperty("ReturnCode", out var codeProp))
                    {
                        int code = codeProp.GetInt32();
                        string msg = entry.TryGetProperty("Message", out var msgProp)
                            ? msgProp.GetString() ?? "Unknown error."
                            : "Unknown error.";
                        dict[code] = msg;
                    }
                }
                result[mappingType.Name] = dict;
            }
            return result;
        }

        public static string GetMessage(int code, string mappingType)
        {
            if (_allMappings.Value.TryGetValue(mappingType, out var mapping) && mapping.TryGetValue(code, out var msg))
                return msg;
            if (!_allMappings.Value.ContainsKey(mappingType))
                return $"Unknown mapping type '{mappingType}'.";
            return $"Unknown error for code {code} in mapping '{mappingType}'.";
        }

        public MessageService(int code, string mappingType)
        {
            Code = code;
            Message = GetMessage(code, mappingType);
        }
    }
}
using System.IO;
using System.Text.Json;

namespace CMMT.Helpers
{
    public static class ConfigFileHelper
    {
        public static async Task<T?> LoadAsync<T>(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }

        public static async Task SaveAsync<T>(T data, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file '{filePath}' does not exist.");

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public static string GetConfigFilePath(string fileDirectory, string fileName)
        {
            if (string.IsNullOrEmpty(fileDirectory) && string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File directory and File Name cannot be null or empty.");

            var exePath = AppContext.BaseDirectory; // More robust for both dev & published
            var configPath = Path.Combine(exePath, fileDirectory, fileName);

            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Config file not found at: {configPath}");

            return configPath;
        }

    }
}

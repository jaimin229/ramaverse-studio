using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using RamaverseStudio.Models;

namespace RamaverseStudio.Services
{
    public class SceneCollectionManifest
    {
        public string Version { get; set; } = "1.2.0";
        public string ExportDate { get; set; } = DateTime.UtcNow.ToString("o");
        public string CollectionName { get; set; } = "Ramaverse Collection";
        public StudioProfile Profile { get; set; } = new StudioProfile();
        public List<Scene> Scenes { get; set; } = new List<Scene>();
        public AudioFilterSettings AudioFilters { get; set; } = new AudioFilterSettings();
        public Dictionary<string, string> AssetFileMapping { get; set; } = new Dictionary<string, string>();
    }

    public static class SceneCollectionExporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Packages the given studio profile, scenes, audio filter settings, and any
        /// referenced local image/media files into a single compressed .rama file.
        /// </summary>
        public static async Task<bool> ExportCollectionAsync(string targetFilePath, string collectionName,
            StudioProfile profile, IEnumerable<Scene> scenes, AudioFilterSettings audioSettings)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "Ramaverse_Export_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string assetsDir = Path.Combine(tempDir, "assets");
                Directory.CreateDirectory(assetsDir);

                var manifest = new SceneCollectionManifest
                {
                    CollectionName = collectionName,
                    Profile = profile,
                    AudioFilters = audioSettings
                };

                int assetCounter = 1;
                foreach (var scene in scenes)
                {
                    var clonedScene = scene.Clone();
                    clonedScene.Name = scene.Name;
                    foreach (var src in clonedScene.Sources)
                    {
                        if ((src.Type == SourceType.ImageOverlay || src.Type == SourceType.MediaFile) &&
                            !string.IsNullOrWhiteSpace(src.FilePath) && File.Exists(src.FilePath))
                        {
                            string ext = Path.GetExtension(src.FilePath);
                            string assetFileName = $"asset_{assetCounter++}{ext}";
                            string assetDest = Path.Combine(assetsDir, assetFileName);
                            File.Copy(src.FilePath, assetDest, true);

                            manifest.AssetFileMapping[src.FilePath] = $"assets/{assetFileName}";
                            src.FilePath = $"assets/{assetFileName}"; // relative in archive
                        }
                    }
                    manifest.Scenes.Add(clonedScene);
                }

                string manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
                await File.WriteAllTextAsync(Path.Combine(tempDir, "manifest.json"), manifestJson);

                if (File.Exists(targetFilePath))
                {
                    File.Delete(targetFilePath);
                }

                string? dir = Path.GetDirectoryName(targetFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                ZipFile.CreateFromDirectory(tempDir, targetFilePath, CompressionLevel.Optimal, false);

                try { Directory.Delete(tempDir, true); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExportCollectionAsync failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads and extracts a .rama collection archive, recreating any referenced media assets
        /// in a persistent local directory and returning the deserialized profile, scenes, and audio filters.
        /// </summary>
        public static async Task<(bool Success, StudioProfile? Profile, List<Scene>? Scenes, AudioFilterSettings? AudioFilters, string Error)>
            ImportCollectionAsync(string ramaFilePath)
        {
            if (!File.Exists(ramaFilePath))
            {
                return (false, null, null, null, "File not found.");
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "Ramaverse_Import_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(ramaFilePath, tempDir, true);

                string manifestPath = Path.Combine(tempDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    Directory.Delete(tempDir, true);
                    return (false, null, null, null, "Invalid archive: manifest.json is missing.");
                }

                string manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<SceneCollectionManifest>(manifestJson, JsonOptions);
                if (manifest == null)
                {
                    Directory.Delete(tempDir, true);
                    return (false, null, null, null, "Failed to parse collection manifest.");
                }

                // Relink media assets to persistent local AppData
                string persistentAssetsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RamaverseStudio", "ImportedAssets", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(persistentAssetsDir);

                string extractedAssetsDir = Path.Combine(tempDir, "assets");
                if (Directory.Exists(extractedAssetsDir))
                {
                    foreach (var file in Directory.GetFiles(extractedAssetsDir))
                    {
                        string fileName = Path.GetFileName(file);
                        string target = Path.Combine(persistentAssetsDir, fileName);
                        File.Copy(file, target, true);
                    }
                }

                foreach (var scene in manifest.Scenes)
                {
                    foreach (var src in scene.Sources)
                    {
                        if ((src.Type == SourceType.ImageOverlay || src.Type == SourceType.MediaFile) &&
                            !string.IsNullOrWhiteSpace(src.FilePath))
                        {
                            string fileName = Path.GetFileName(src.FilePath);
                            string candidate = Path.Combine(persistentAssetsDir, fileName);
                            if (File.Exists(candidate))
                            {
                                src.FilePath = candidate;
                            }
                        }
                    }
                }

                try { Directory.Delete(tempDir, true); } catch { }
                return (true, manifest.Profile, manifest.Scenes, manifest.AudioFilters, "");
            }
            catch (Exception ex)
            {
                return (false, null, null, null, $"Import failed: {ex.Message}");
            }
        }
    }
}

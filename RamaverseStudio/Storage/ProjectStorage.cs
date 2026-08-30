using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using RamaverseStudio.Models;

namespace RamaverseStudio.Storage
{
    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? hex = reader.GetString();
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Transparent;
            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                return converted is Color c ? c : Colors.White;
            }
            catch
            {
                return Colors.White;
            }
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
        }
    }

    public class StudioProjectData
    {
        public StudioProfile Profile { get; set; } = new StudioProfile();
        public List<Scene> Scenes { get; set; } = new List<Scene>();
        public AudioFilterSettings AudioFilters { get; set; } = new AudioFilterSettings();
        public int ActiveSceneIndex { get; set; } = 0;
    }

    public static class ProjectStorage
    {
        private static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamaverseStudio");
        private static readonly string ConfigPath = Path.Combine(AppDataDir, "project_state.json");
        private static readonly string TempConfigPath = Path.Combine(AppDataDir, "project_state.json.tmp");

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new ColorJsonConverter() }
        };

        public static void SaveProject(StudioProfile profile, ObservableCollection<Scene> scenes, AudioFilterSettings filters, int activeSceneIndex)
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);

                var data = new StudioProjectData
                {
                    Profile = profile,
                    Scenes = new List<Scene>(scenes),
                    AudioFilters = filters,
                    ActiveSceneIndex = Math.Max(0, activeSceneIndex)
                };

                string json = JsonSerializer.Serialize(data, JsonOpts);
                
                // Atomic save via temp file
                File.WriteAllText(TempConfigPath, json);
                File.Move(TempConfigPath, ConfigPath, true);
            }
            catch (Exception) { }
        }

        public static StudioProjectData? LoadProject()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    if (string.IsNullOrWhiteSpace(json)) return null;

                    var data = JsonSerializer.Deserialize<StudioProjectData>(json, JsonOpts);
                    return data;
                }
            }
            catch (Exception)
            {
                try
                {
                    // Backup corrupted file for user recovery
                    string backupPath = Path.Combine(AppDataDir, $"project_state.corrupted_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    if (File.Exists(ConfigPath))
                    {
                        File.Copy(ConfigPath, backupPath, true);
                    }
                }
                catch { }
            }

            return null;
        }
    }
}

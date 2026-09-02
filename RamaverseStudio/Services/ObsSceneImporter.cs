using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using RamaverseStudio.Models;

namespace RamaverseStudio.Services
{
    public class ObsImportResult
    {
        public bool Success { get; set; }
        public string CollectionName { get; set; } = string.Empty;
        public ObservableCollection<Scene> Scenes { get; set; } = new ObservableCollection<Scene>();
        public int TotalSourcesCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public static class ObsSceneImporter
    {
        public static string GetDefaultObsScenesDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "obs-studio", "basic", "scenes");
        }

        public static List<string> DetectObsSceneCollections()
        {
            var collections = new List<string>();
            try
            {
                string dir = GetDefaultObsScenesDirectory();
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.json");
                    collections.AddRange(files);
                }
            }
            catch
            {
                // Directory inaccessible or OBS not installed
            }
            return collections;
        }

        public static ObsImportResult ImportFromObsJson(string jsonFilePath)
        {
            var result = new ObsImportResult();
            try
            {
                if (!File.Exists(jsonFilePath))
                {
                    result.Success = false;
                    result.Message = $"File not found: {jsonFilePath}";
                    return result;
                }

                string json = File.ReadAllText(jsonFilePath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string collectionName = Path.GetFileNameWithoutExtension(jsonFilePath);
                if (root.TryGetProperty("name", out var nameProp))
                {
                    collectionName = nameProp.GetString() ?? collectionName;
                }
                result.CollectionName = collectionName;

                // Dictionary of all sources in the OBS JSON by name
                var sourceSettingsMap = new Dictionary<string, (string Id, JsonElement Settings, JsonElement Filters)>();
                if (root.TryGetProperty("sources", out var sourcesProp) && sourcesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var srcEl in sourcesProp.EnumerateArray())
                    {
                        string name = srcEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        string id = srcEl.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                        var settings = srcEl.TryGetProperty("settings", out var s) ? s : default;
                        var filters = srcEl.TryGetProperty("filters", out var f) ? f : default;
                        if (!string.IsNullOrEmpty(name))
                        {
                            sourceSettingsMap[name] = (id, settings, filters);
                        }
                    }
                }

                // Parse scene order
                var scenesList = new List<Scene>();
                if (root.TryGetProperty("scene_order", out var sceneOrderProp) && sceneOrderProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var scEl in sceneOrderProp.EnumerateArray())
                    {
                        string sceneName = scEl.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(sceneName)) continue;

                        var scene = new Scene { Name = sceneName };

                        // Find corresponding scene definition
                        if (sourceSettingsMap.TryGetValue(sceneName, out var sceneSrc) && sceneSrc.Settings.ValueKind == JsonValueKind.Object)
                        {
                            if (sceneSrc.Settings.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                            {
                                int z = 0;
                                foreach (var itemEl in itemsProp.EnumerateArray())
                                {
                                    string itemName = itemEl.TryGetProperty("name", out var iname) ? iname.GetString() ?? "" : "";
                                    var item = ConvertObsItemToSource(itemName, itemEl, sourceSettingsMap);
                                    if (item != null)
                                    {
                                        item.ZIndex = z++;
                                        scene.Sources.Add(item);
                                        result.TotalSourcesCount++;
                                    }
                                }
                            }
                        }
                        scenesList.Add(scene);
                    }
                }

                // If scene_order was empty, scan sources directly for "scene" id
                if (scenesList.Count == 0)
                {
                    foreach (var kvp in sourceSettingsMap)
                    {
                        if (kvp.Value.Id == "scene" && kvp.Value.Settings.ValueKind == JsonValueKind.Object)
                        {
                            var scene = new Scene { Name = kvp.Key };
                            if (kvp.Value.Settings.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                            {
                                int z = 0;
                                foreach (var itemEl in itemsProp.EnumerateArray())
                                {
                                    string itemName = itemEl.TryGetProperty("name", out var iname) ? iname.GetString() ?? "" : "";
                                    var item = ConvertObsItemToSource(itemName, itemEl, sourceSettingsMap);
                                    if (item != null)
                                    {
                                        item.ZIndex = z++;
                                        scene.Sources.Add(item);
                                        result.TotalSourcesCount++;
                                    }
                                }
                            }
                            scenesList.Add(scene);
                        }
                    }
                }

                if (scenesList.Count > 0)
                {
                    scenesList[0].IsActive = true;
                    foreach (var sc in scenesList)
                    {
                        result.Scenes.Add(sc);
                    }
                    result.Success = true;
                    result.Message = $"Successfully imported {result.Scenes.Count} scenes ({result.TotalSourcesCount} sources) from '{collectionName}'.";
                }
                else
                {
                    result.Success = false;
                    result.Message = "No compatible scenes found in the specified OBS JSON file.";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"OBS Import failed: {ex.Message}";
            }

            return result;
        }

        private static SourceItem? ConvertObsItemToSource(string name, JsonElement itemEl, Dictionary<string, (string Id, JsonElement Settings, JsonElement Filters)> sourceMap)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var sourceItem = new SourceItem
            {
                Name = name
            };

            // Read item transform properties
            if (itemEl.TryGetProperty("visible", out var visProp)) sourceItem.IsVisible = visProp.GetBoolean();
            if (itemEl.TryGetProperty("locked", out var lockProp)) sourceItem.IsLocked = lockProp.GetBoolean();

            if (itemEl.TryGetProperty("pos", out var posProp))
            {
                if (posProp.TryGetProperty("x", out var xProp)) sourceItem.X = xProp.GetDouble();
                if (posProp.TryGetProperty("y", out var yProp)) sourceItem.Y = yProp.GetDouble();
            }

            if (itemEl.TryGetProperty("rot", out var rotProp)) sourceItem.Rotation = rotProp.GetDouble();

            if (itemEl.TryGetProperty("crop_left", out var cl)) sourceItem.CropLeft = cl.GetDouble();
            if (itemEl.TryGetProperty("crop_top", out var ct)) sourceItem.CropTop = ct.GetDouble();
            if (itemEl.TryGetProperty("crop_right", out var cr)) sourceItem.CropRight = cr.GetDouble();
            if (itemEl.TryGetProperty("crop_bottom", out var cb)) sourceItem.CropBottom = cb.GetDouble();

            // Map Source Type and Specific Settings
            if (sourceMap.TryGetValue(name, out var srcData))
            {
                string obsId = srcData.Id.ToLowerInvariant();
                var s = srcData.Settings;

                switch (obsId)
                {
                    case "monitor_capture":
                    case "display_capture":
                        sourceItem.Type = SourceType.DisplayCapture;
                        break;

                    case "window_capture":
                        sourceItem.Type = SourceType.WindowCapture;
                        if (s.TryGetProperty("window", out var winProp))
                        {
                            sourceItem.WindowTitle = winProp.GetString() ?? "";
                        }
                        break;

                    case "dshow_input":
                        sourceItem.Type = SourceType.VideoCaptureDevice;
                        if (s.TryGetProperty("video_device_id", out var devProp))
                        {
                            sourceItem.CameraDeviceId = devProp.GetString() ?? "";
                        }
                        break;

                    case "image_source":
                        sourceItem.Type = SourceType.ImageOverlay;
                        if (s.TryGetProperty("file", out var fileProp))
                        {
                            sourceItem.FilePath = fileProp.GetString() ?? "";
                        }
                        break;

                    case "ffmpeg_source":
                    case "media_source":
                        sourceItem.Type = SourceType.MediaFile;
                        if (s.TryGetProperty("local_file", out var mfProp))
                        {
                            sourceItem.FilePath = mfProp.GetString() ?? "";
                        }
                        break;

                    case "text_gdiplus_v2":
                    case "text_ft2_source_v2":
                        sourceItem.Type = SourceType.TextOverlay;
                        if (s.TryGetProperty("text", out var textProp))
                        {
                            sourceItem.TextContent = textProp.GetString() ?? "";
                        }
                        break;

                    case "browser_source":
                        sourceItem.Type = SourceType.BrowserSource;
                        if (s.TryGetProperty("url", out var urlProp))
                        {
                            sourceItem.BrowserUrl = urlProp.GetString() ?? "";
                        }
                        break;

                    case "wasapi_input_capture":
                        sourceItem.Type = SourceType.AudioInputCapture;
                        break;

                    case "wasapi_output_capture":
                        sourceItem.Type = SourceType.AudioOutputCapture;
                        break;

                    case "color_source":
                    case "color_source_v3":
                        sourceItem.Type = SourceType.ColorSource;
                        break;

                    default:
                        sourceItem.Type = SourceType.DisplayCapture;
                        break;
                }

                // Check for Chroma Key Filter in OBS
                if (srcData.Filters.ValueKind == JsonValueKind.Array)
                {
                    foreach (var filterEl in srcData.Filters.EnumerateArray())
                    {
                        string fId = filterEl.TryGetProperty("id", out var fid) ? fid.GetString() ?? "" : "";
                        if (fId.Contains("chroma_key") || fId.Contains("color_key"))
                        {
                            sourceItem.ChromaKeyEnabled = true;
                            if (filterEl.TryGetProperty("settings", out var fSet))
                            {
                                if (fSet.TryGetProperty("similarity", out var sim))
                                    sourceItem.KeySimilarity = sim.GetDouble() / 1000.0;
                                if (fSet.TryGetProperty("smoothness", out var sm))
                                    sourceItem.KeySmoothness = sm.GetDouble() / 1000.0;
                            }
                        }
                    }
                }
            }

            return sourceItem;
        }
    }
}

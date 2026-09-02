using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace RamaverseStudio.Services
{
    public enum SupportedLanguage
    {
        English,
        Hindi,
        Spanish,
        German,
        Japanese,
        French
    }

    /// <summary>
    /// Zero-overhead, hot-swappable multilingual localization service for Ramaverse Studio.
    /// Provides instantaneous runtime language switching across English, Hindi (हिन्दी),
    /// Spanish (Español), German (Deutsch), Japanese (日本語), and French (Français).
    /// </summary>
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        private static readonly Lazy<LocalizationService> _lazyInstance = new(() => new LocalizationService());
        public static LocalizationService Instance => _lazyInstance.Value;

        private SupportedLanguage _currentLanguage = SupportedLanguage.English;
        private readonly ConcurrentDictionary<string, string> _activeDictionary = new(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? LanguageChanged;

        public SupportedLanguage CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    LoadDictionaryForLanguage(value);
                    OnPropertyChanged(nameof(CurrentLanguage));
                    OnPropertyChanged("Item[]");
                    LanguageChanged?.Invoke();
                }
            }
        }

        public string this[string key] => GetString(key);

        public static string T(string key) => Instance.GetString(key);
        public static string T(string key, string arg0)
        {
            string template = Instance.GetString(key);
            if (template.Contains("{0}"))
            {
                try { return string.Format(CultureInfo.InvariantCulture, template, arg0); }
                catch { return template; }
            }
            return template;
        }
        public static string T(string key, params object[] args) => Instance.Format(key, args);

        public static void SetLanguage(string code) => Instance.SetLanguageByCode(code);

        public static IReadOnlyList<(string Code, string DisplayName)> SupportedLanguages { get; } = new List<(string, string)>
        {
            ("en", "English"),
            ("hi", "हिन्दी (Hindi)"),
            ("es", "Español (Spanish)"),
            ("de", "Deutsch (German)"),
            ("ja", "日本語 (Japanese)"),
            ("fr", "Français (French)")
        };

        public LocalizationService()
        {
            LoadDictionaryForLanguage(SupportedLanguage.English);
        }

        public string GetString(string key, string? defaultText = null)
        {
            if (string.IsNullOrEmpty(key)) return defaultText ?? string.Empty;
            if (_activeDictionary.TryGetValue(key, out var val)) return val;
            return defaultText ?? key;
        }

        public string Format(string key, params object[] args)
        {
            string template = GetString(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch
            {
                return template;
            }
        }

        public void SetLanguageByCode(string code)
        {
            CurrentLanguage = code?.ToLowerInvariant() switch
            {
                "hi" or "hindi" => SupportedLanguage.Hindi,
                "es" or "spanish" or "espanol" => SupportedLanguage.Spanish,
                "de" or "german" or "deutsch" => SupportedLanguage.German,
                "ja" or "japanese" => SupportedLanguage.Japanese,
                "fr" or "french" or "francais" => SupportedLanguage.French,
                _ => SupportedLanguage.English
            };
        }

        private void LoadDictionaryForLanguage(SupportedLanguage lang)
        {
            _activeDictionary.Clear();
            foreach (var kvp in EnglishDictionary)
            {
                _activeDictionary[kvp.Key] = kvp.Value;
            }

            var overlay = lang switch
            {
                SupportedLanguage.Hindi => HindiDictionary,
                SupportedLanguage.Spanish => SpanishDictionary,
                SupportedLanguage.German => GermanDictionary,
                SupportedLanguage.Japanese => JapaneseDictionary,
                SupportedLanguage.French => FrenchDictionary,
                _ => null
            };

            if (overlay != null)
            {
                foreach (var kvp in overlay)
                {
                    _activeDictionary[kvp.Key] = kvp.Value;
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        #region Dictionaries

        private static readonly Dictionary<string, string> EnglishDictionary = new()
        {
            ["BtnRecord"] = "● RECORD",
            ["BtnRecordStop"] = "■ STOP REC",
            ["StatusLive"] = "LIVE",
            ["SettingsLanguage"] = "INTERFACE LANGUAGE",
            ["ToastSnapshotSaved"] = "Snapshot saved: {0}",

            ["App.Title"] = "RAMAVERSE STUDIO",
            ["Nav.Studio"] = "STUDIO",
            ["Nav.Scenes"] = "SCENES",
            ["Nav.Mixer"] = "AUDIO MIXER",
            ["Nav.Settings"] = "SETTINGS",
            ["Nav.Chat"] = "LIVE CHAT",
            ["Nav.TouchDeck"] = "TOUCH DECK",

            ["Action.GoLive"] = "GO LIVE",
            ["Action.EndStream"] = "END STREAM",
            ["Action.Record"] = "RECORD",
            ["Action.StopRecord"] = "STOP",
            ["Action.ClipReplay"] = "SAVE CLIP",
            ["Action.TakeSnapshot"] = "SNAPSHOT",
            ["Action.StudioMode"] = "STUDIO MODE",
            ["Action.VirtualCam"] = "VIRTUAL CAM",
            ["Action.AddSource"] = "ADD SOURCE",
            ["Action.AddScene"] = "ADD SCENE",

            ["Status.Live"] = "LIVE",
            ["Status.Offline"] = "OFFLINE",
            ["Status.Recording"] = "REC",
            ["Status.Standby"] = "STANDBY",
            ["Status.Fps"] = "FPS",
            ["Status.Cpu"] = "CPU",
            ["Status.DroppedFrames"] = "DROPPED",
            ["Status.Bitrate"] = "BITRATE",

            ["Audio.Mic"] = "MICROPHONE",
            ["Audio.Desktop"] = "DESKTOP AUDIO",
            ["Audio.Soundboard"] = "SOUNDBOARD",
            ["Audio.Mute"] = "MUTE",
            ["Audio.Unmute"] = "UNMUTE",
            ["Audio.NoiseFilter"] = "AI NOISE REDUCTION",
            ["Audio.ClickSuppressor"] = "KEYBOARD CLICK SUPPRESSION",

            ["Settings.General"] = "GENERAL",
            ["Settings.Video"] = "VIDEO & RESOLUTION",
            ["Settings.Audio"] = "AUDIO & DSP",
            ["Settings.Stream"] = "STREAM DESTINATION",
            ["Settings.Hotkeys"] = "GLOBAL HOTKEYS",
            ["Settings.Themes"] = "THEMES & ACCESSIBILITY",
            ["Settings.Language"] = "LANGUAGE",
            ["Settings.Update"] = "CHECK FOR UPDATES"
        };

        private static readonly Dictionary<string, string> HindiDictionary = new()
        {
            ["BtnRecord"] = "● रिकॉर्ड",
            ["BtnRecordStop"] = "■ रिकॉर्ड बंद",
            ["StatusLive"] = "लाइव",
            ["SettingsLanguage"] = "इंटरफ़ेस भाषा",
            ["ToastSnapshotSaved"] = "स्नैपशॉट सेव: {0}",

            ["App.Title"] = "रामावर्स स्टूडियो",
            ["Nav.Studio"] = "स्टूडियो",
            ["Nav.Scenes"] = "सीन्स",
            ["Nav.Mixer"] = "ऑडियो मिक्सर",
            ["Nav.Settings"] = "सेटिंग्स",
            ["Nav.Chat"] = "लाइव चैट",
            ["Nav.TouchDeck"] = "टच डेक",

            ["Action.GoLive"] = "लाइव जाएं",
            ["Action.EndStream"] = "स्ट्रीम बंद करें",
            ["Action.Record"] = "रिकॉर्ड करें",
            ["Action.StopRecord"] = "रोकें",
            ["Action.ClipReplay"] = "क्लिप सहेजें",
            ["Action.TakeSnapshot"] = "स्नैपशॉट",
            ["Action.StudioMode"] = "स्टूडियो मोड",
            ["Action.VirtualCam"] = "वर्चुअल कैमरा",
            ["Action.AddSource"] = "स्रोत जोड़ें",
            ["Action.AddScene"] = "सीन जोड़ें",

            ["Status.Live"] = "लाइव",
            ["Status.Offline"] = "ऑफ़लाइन",
            ["Status.Recording"] = "रिकॉर्डिंग",
            ["Status.Standby"] = "स्टैंडबाय",
            ["Status.Fps"] = "एफपीएस",
            ["Status.Cpu"] = "सीपीयू",
            ["Status.DroppedFrames"] = "ड्रॉप फ्रेम",
            ["Status.Bitrate"] = "बिटरेट",

            ["Audio.Mic"] = "माइक्रोफ़ोन",
            ["Audio.Desktop"] = "डेस्कटॉप ऑडियो",
            ["Audio.Soundboard"] = "साउंडबोर्ड",
            ["Audio.Mute"] = "म्यूट करें",
            ["Audio.Unmute"] = "अनम्यूट करें",
            ["Audio.NoiseFilter"] = "एआई नॉइज़ रिडक्शन",
            ["Audio.ClickSuppressor"] = "कीबोर्ड क्लिक सप्रेशन",

            ["Settings.General"] = "सामान्य",
            ["Settings.Video"] = "वीडियो और रिज़ॉल्यूशन",
            ["Settings.Audio"] = "ऑडियो और डीएसपी",
            ["Settings.Stream"] = "स्ट्रीम गंतव्य",
            ["Settings.Hotkeys"] = "ग्लोबल हॉटकीज़",
            ["Settings.Themes"] = "थीम्स और एक्सेसिबिलिटी",
            ["Settings.Language"] = "भाषा",
            ["Settings.Update"] = "अपडेट जांचें"
        };

        private static readonly Dictionary<string, string> SpanishDictionary = new()
        {
            ["App.Title"] = "RAMAVERSE STUDIO",
            ["Nav.Studio"] = "ESTUDIO",
            ["Nav.Scenes"] = "ESCENAS",
            ["Nav.Mixer"] = "MEZCLADOR DE AUDIO",
            ["Nav.Settings"] = "AJUSTES",
            ["Nav.Chat"] = "CHAT EN VIVO",
            ["Nav.TouchDeck"] = "TOUCH DECK",

            ["Action.GoLive"] = "INICIAR TRANSMISIÓN",
            ["Action.EndStream"] = "FINALIZAR",
            ["Action.Record"] = "GRABAR",
            ["Action.StopRecord"] = "DETENER",
            ["Action.ClipReplay"] = "GUARDAR CLIP",
            ["Action.TakeSnapshot"] = "CAPTURAR",
            ["Action.StudioMode"] = "MODO ESTUDIO",
            ["Action.VirtualCam"] = "CÁMARA VIRTUAL",
            ["Action.AddSource"] = "AÑADIR FUENTE",
            ["Action.AddScene"] = "AÑADIR ESCENA",

            ["Status.Live"] = "EN VIVO",
            ["Status.Offline"] = "DESCONECTADO",
            ["Status.Recording"] = "GRABANDO",
            ["Status.Standby"] = "EN ESPERA",
            ["Status.Fps"] = "FPS",
            ["Status.Cpu"] = "CPU",
            ["Status.DroppedFrames"] = "PERDIDOS",
            ["Status.Bitrate"] = "TASA DE BITS",

            ["Audio.Mic"] = "MICRÓFONO",
            ["Audio.Desktop"] = "AUDIO DE ESCRITORIO",
            ["Audio.Soundboard"] = "TABLERO DE SONIDOS",
            ["Audio.Mute"] = "SILENCIAR",
            ["Audio.Unmute"] = "ACTIVAR SONIDO",
            ["Audio.NoiseFilter"] = "REDUCCIÓN DE RUIDO IA",
            ["Audio.ClickSuppressor"] = "SUPRESIÓN DE CLICS DE TECLADO",

            ["Settings.General"] = "GENERAL",
            ["Settings.Video"] = "VÍDEO Y RESOLUCIÓN",
            ["Settings.Audio"] = "AUDIO Y DSP",
            ["Settings.Stream"] = "DESTINO DE EMISIÓN",
            ["Settings.Hotkeys"] = "ATAJOS GLOBALES",
            ["Settings.Themes"] = "TEMAS Y ACCESIBILIDAD",
            ["Settings.Language"] = "IDIOMA",
            ["Settings.Update"] = "BUSCAR ACTUALIZACIONES"
        };

        private static readonly Dictionary<string, string> GermanDictionary = new()
        {
            ["App.Title"] = "RAMAVERSE STUDIO",
            ["Nav.Studio"] = "STUDIO",
            ["Nav.Scenes"] = "SZENEN",
            ["Nav.Mixer"] = "AUDIOMISCHPULT",
            ["Nav.Settings"] = "EINSTELLUNGEN",
            ["Nav.Chat"] = "LIVE-CHAT",
            ["Nav.TouchDeck"] = "TOUCH DECK",

            ["Action.GoLive"] = "STREAM STARTEN",
            ["Action.EndStream"] = "BEENDEN",
            ["Action.Record"] = "AUFNEHMEN",
            ["Action.StopRecord"] = "STOPP",
            ["Action.ClipReplay"] = "CLIP SPEICHERN",
            ["Action.TakeSnapshot"] = "SCHNAPPSCHUSS",
            ["Action.StudioMode"] = "STUDIO-MODUS",
            ["Action.VirtualCam"] = "VIRTUELLE KAMERA",
            ["Action.AddSource"] = "QUELLE HINZUFÜGEN",
            ["Action.AddScene"] = "SZENE HINZUFÜGEN",

            ["Status.Live"] = "LIVE",
            ["Status.Offline"] = "OFFLINE",
            ["Status.Recording"] = "AUFNAHME",
            ["Status.Standby"] = "STANDBY",
            ["Status.Fps"] = "FPS",
            ["Status.Cpu"] = "CPU",
            ["Status.DroppedFrames"] = "VERLUST",
            ["Status.Bitrate"] = "BITRATE",

            ["Audio.Mic"] = "MIKROFON",
            ["Audio.Desktop"] = "DESKTOP-AUDIO",
            ["Audio.Soundboard"] = "SOUNDBOARD",
            ["Audio.Mute"] = "STUMM",
            ["Audio.Unmute"] = "STUMMSCHALTUNG AUFHEBEN",
            ["Audio.NoiseFilter"] = "KI-RAUSCHUNTERDRÜCKUNG",
            ["Audio.ClickSuppressor"] = "TASTATUR-KLICK-UNTERDRÜCKUNG",

            ["Settings.General"] = "ALLGEMEIN",
            ["Settings.Video"] = "VIDEO & AUFLÖSUNG",
            ["Settings.Audio"] = "AUDIO & DSP",
            ["Settings.Stream"] = "STREAM-ZIEL",
            ["Settings.Hotkeys"] = "GLOBALE HOTKEYS",
            ["Settings.Themes"] = "THEMEN & BARRIEREFREIHEIT",
            ["Settings.Language"] = "SPRACHE",
            ["Settings.Update"] = "NACH UPDATES SUCHEN"
        };

        private static readonly Dictionary<string, string> JapaneseDictionary = new()
        {
            ["App.Title"] = "ラマバース スタジオ",
            ["Nav.Studio"] = "スタジオ",
            ["Nav.Scenes"] = "シーン",
            ["Nav.Mixer"] = "オーディオミキサー",
            ["Nav.Settings"] = "設定",
            ["Nav.Chat"] = "ライブチャット",
            ["Nav.TouchDeck"] = "タッチデッキ",

            ["Action.GoLive"] = "配信開始",
            ["Action.EndStream"] = "配信終了",
            ["Action.Record"] = "録画開始",
            ["Action.StopRecord"] = "停止",
            ["Action.ClipReplay"] = "クリップ保存",
            ["Action.TakeSnapshot"] = "スナップショット",
            ["Action.StudioMode"] = "スタジオモード",
            ["Action.VirtualCam"] = "仮想カメラ",
            ["Action.AddSource"] = "ソース追加",
            ["Action.AddScene"] = "シーン追加",

            ["Status.Live"] = "配信中",
            ["Status.Offline"] = "オフライン",
            ["Status.Recording"] = "録画中",
            ["Status.Standby"] = "スタンバイ",
            ["Status.Fps"] = "FPS",
            ["Status.Cpu"] = "CPU",
            ["Status.DroppedFrames"] = "ドロップ",
            ["Status.Bitrate"] = "ビットレート",

            ["Audio.Mic"] = "マイク",
            ["Audio.Desktop"] = "デスクトップ音声",
            ["Audio.Soundboard"] = "サウンドボード",
            ["Audio.Mute"] = "ミュート",
            ["Audio.Unmute"] = "ミュート解除",
            ["Audio.NoiseFilter"] = "AIノイズ抑制",
            ["Audio.ClickSuppressor"] = "キークリック音除去",

            ["Settings.General"] = "一般",
            ["Settings.Video"] = "映像と解像度",
            ["Settings.Audio"] = "音声とDSP",
            ["Settings.Stream"] = "配信先設定",
            ["Settings.Hotkeys"] = "グローバルホットキー",
            ["Settings.Themes"] = "テーマとアクセシビリティ",
            ["Settings.Language"] = "言語",
            ["Settings.Update"] = "更新を確認"
        };

        private static readonly Dictionary<string, string> FrenchDictionary = new()
        {
            ["App.Title"] = "RAMAVERSE STUDIO",
            ["Nav.Studio"] = "STUDIO",
            ["Nav.Scenes"] = "SCÈNES",
            ["Nav.Mixer"] = "MIXEUR AUDIO",
            ["Nav.Settings"] = "PARAMÈTRES",
            ["Nav.Chat"] = "CHAT EN DIRECT",
            ["Nav.TouchDeck"] = "TOUCH DECK",

            ["Action.GoLive"] = "DÉMARRER LE LIVE",
            ["Action.EndStream"] = "ARRÊTER",
            ["Action.Record"] = "ENREGISTRER",
            ["Action.StopRecord"] = "STOP",
            ["Action.ClipReplay"] = "SAUVEGARDER CLIP",
            ["Action.TakeSnapshot"] = "CAPTURE",
            ["Action.StudioMode"] = "MODE STUDIO",
            ["Action.VirtualCam"] = "CAMÉRA VIRTUELLE",
            ["Action.AddSource"] = "AJOUTER SOURCE",
            ["Action.AddScene"] = "AJOUTER SCÈNE",

            ["Status.Live"] = "EN DIRECT",
            ["Status.Offline"] = "HORS LIGNE",
            ["Status.Recording"] = "ENREGISTREMENT",
            ["Status.Standby"] = "VEILLE",
            ["Status.Fps"] = "FPS",
            ["Status.Cpu"] = "CPU",
            ["Status.DroppedFrames"] = "PERDUS",
            ["Status.Bitrate"] = "DÉBIT",

            ["Audio.Mic"] = "MICROPHONE",
            ["Audio.Desktop"] = "AUDIO DU BUREAU",
            ["Audio.Soundboard"] = "TABLE DE SONS",
            ["Audio.Mute"] = "COUPER LE SON",
            ["Audio.Unmute"] = "RÉACTIVER",
            ["Audio.NoiseFilter"] = "RÉDUCTION DU BRUIT IA",
            ["Audio.ClickSuppressor"] = "SUPPRESSION DES CLICS CLAVIER",

            ["Settings.General"] = "GÉNÉRAL",
            ["Settings.Video"] = "VIDÉO & RÉSOLUTION",
            ["Settings.Audio"] = "AUDIO & DSP",
            ["Settings.Stream"] = "DESTINATION DU STREAM",
            ["Settings.Hotkeys"] = "RACCOURCIS GLOBAUX",
            ["Settings.Themes"] = "THÈMES & ACCESSIBILITÉ",
            ["Settings.Language"] = "LANGUE",
            ["Settings.Update"] = "VÉRIFIER LES MISES À JOUR"
        };

        #endregion
    }
}

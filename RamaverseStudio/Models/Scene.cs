using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RamaverseStudio.Models
{
    public enum TransitionType
    {
        Cut,
        CrossFade,
        SlideLeft,
        SlideRight,
        WipeLeft,
        WipeRight,
        LumaWipe
    }

    public class Scene : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = "Scene";
        private bool _isActive = false;
        private string _transition = "Fade (300ms)";
        private TransitionType _transitionType = TransitionType.CrossFade;
        private int _transitionDurationMs = 300;

        public string Id { get => _id; set => SetField(ref _id, value); }
        public string Name { get => _name; set => SetField(ref _name, value); }
        public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
        public string Transition
        {
            get => _transition;
            set
            {
                if (SetField(ref _transition, value))
                {
                    var (t, dur) = ParseTransitionString(value);
                    _transitionType = t;
                    _transitionDurationMs = dur;
                }
            }
        }

        public TransitionType TransitionEffect { get => _transitionType; set => SetField(ref _transitionType, value); }
        public int TransitionDurationMs { get => _transitionDurationMs; set => SetField(ref _transitionDurationMs, Math.Clamp(value, 0, 5000)); }

        public static (TransitionType Type, int DurationMs) ParseTransitionString(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return (TransitionType.CrossFade, 300);
            string s = str.ToLowerInvariant();
            TransitionType t;
            int dur = 300;

            if (s.Contains("cut")) t = TransitionType.Cut;
            else if (s.Contains("slide left")) t = TransitionType.SlideLeft;
            else if (s.Contains("slide right")) t = TransitionType.SlideRight;
            else if (s.Contains("wipe left")) t = TransitionType.WipeLeft;
            else if (s.Contains("wipe right")) t = TransitionType.WipeRight;
            else if (s.Contains("luma")) t = TransitionType.LumaWipe;
            else t = TransitionType.CrossFade;

            // Extract milliseconds if present
            var match = System.Text.RegularExpressions.Regex.Match(str, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int ms))
            {
                dur = Math.Clamp(ms, 0, 5000);
            }

            return (t, dur);
        }

        public ObservableCollection<SourceItem> Sources { get; set; } = new ObservableCollection<SourceItem>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public Scene Clone()
        {
            var cloned = new Scene
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"{this.Name} (Copy)",
                Transition = this.Transition
            };
            foreach (var src in this.Sources)
            {
                cloned.Sources.Add(src.Clone());
            }
            return cloned;
        }
    }
}

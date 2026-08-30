using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RamaverseStudio.Models
{
    public class Scene : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = "Scene";
        private bool _isActive = false;
        private string _transition = "Fade (300ms)";

        public string Id { get => _id; set => SetField(ref _id, value); }
        public string Name { get => _name; set => SetField(ref _name, value); }
        public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }
        public string Transition { get => _transition; set => SetField(ref _transition, value); }

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

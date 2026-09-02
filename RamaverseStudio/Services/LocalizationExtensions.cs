using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace RamaverseStudio.Services
{
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;
        public string? DefaultText { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return DefaultText ?? string.Empty;

            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationService.Instance,
                Mode = BindingMode.OneWay,
                FallbackValue = DefaultText ?? Key
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}

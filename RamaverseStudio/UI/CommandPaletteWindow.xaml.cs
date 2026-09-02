using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RamaverseStudio.UI
{
    public class CommandPaletteItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Shortcut { get; set; } = "";
        public string Icon { get; set; } = "⚡";
        public Action? Action { get; set; }
    }

    public partial class CommandPaletteWindow : Window
    {
        private readonly List<CommandPaletteItem> _allCommands = new();
        public ObservableCollection<CommandPaletteItem> FilteredCommands { get; } = new();

        public CommandPaletteWindow(IEnumerable<CommandPaletteItem> commands)
        {
            InitializeComponent();
            _allCommands.AddRange(commands);
            ActionListBox.ItemsSource = FilteredCommands;

            Loaded += (s, e) =>
            {
                SearchBox.Focus();
                RefreshFilter("");
            };
        }

        private void RefreshFilter(string query)
        {
            FilteredCommands.Clear();
            var matches = string.IsNullOrWhiteSpace(query)
                ? _allCommands
                : _allCommands.Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                          c.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                          c.Shortcut.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var item in matches)
            {
                FilteredCommands.Add(item);
            }

            if (FilteredCommands.Count > 0)
            {
                ActionListBox.SelectedIndex = 0;
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshFilter(SearchBox.Text);
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSelectedCommand();
            }
            else if (e.Key == Key.Down)
            {
                if (ActionListBox.SelectedIndex < FilteredCommands.Count - 1)
                {
                    ActionListBox.SelectedIndex++;
                    ActionListBox.ScrollIntoView(ActionListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (ActionListBox.SelectedIndex > 0)
                {
                    ActionListBox.SelectedIndex--;
                    ActionListBox.ScrollIntoView(ActionListBox.SelectedItem);
                }
                e.Handled = true;
            }
        }

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectedCommand();
        }

        private void ExecuteSelectedCommand()
        {
            if (ActionListBox.SelectedItem is CommandPaletteItem item)
            {
                Close();
                item.Action?.Invoke();
            }
        }
    }
}

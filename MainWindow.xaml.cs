using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HostsTool;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
{
    private HostEntry? _selectedEntry;
    private readonly string _hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "drivers", "etc", "hosts");

    public ObservableCollection<HostEntry> Entries { get; } = new ObservableCollection<HostEntry>();

    public HostEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!SetField(ref _selectedEntry, value)) return;

            // If the selected entry is the first (Local) and is read-only, reload hosts file content
            if (_selectedEntry != null && _selectedEntry.IsReadOnly)
            {
                // ensure it's the Local entry (first item)
                if (Entries.Count > 0 && Entries[0] == _selectedEntry)
                {
                    _selectedEntry.Content = LoadHostsContent();
                }
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        // First item: local hosts (read-only)
        string localContent = LoadHostsContent();

        var localEntry = new HostEntry
        {
            Title = "Local",
            IsActive = true,
            Content = localContent,
            IsReadOnly = true
        };

        Entries.Add(localEntry);

        // Sample other data
        Entries.Add(new HostEntry { Title = "Localhost (active)", IsActive = true, Content = "127.0.0.1 localhost" });
        Entries.Add(new HostEntry { Title = "Block ads (inactive)", IsActive = false, Content = "0.0.0.0 ad.example.com" });
        Entries.Add(new HostEntry { Title = "Dev server", IsActive = true, Content = "192.168.1.10 dev.local" });

        SelectedEntry = Entries.FirstOrDefault();

        DataContext = this;
    }

    private string LoadHostsContent()
    {
        try
        {
            if (File.Exists(_hostsPath))
            {
                return File.ReadAllText(_hostsPath);
            }
            else
            {
                return "(hosts file not found: " + _hostsPath + ")";
            }
        }
        catch (Exception ex)
        {
            return "(error reading hosts file: " + ex.Message + ")";
        }
    }

    private void EntriesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Entries.Count == 0) return;

        // Hit test to find the item being clicked
        var fe = e.OriginalSource as DependencyObject;
        while (fe != null && fe is not System.Windows.Controls.ListViewItem)
        {
            fe = VisualTreeHelper.GetParent(fe);
        }

        if (fe is System.Windows.Controls.ListViewItem lvi)
        {
            var item = lvi.DataContext as HostEntry;
            if (item != null && item == Entries[0])
            {
                // Refresh Local content even if already selected
                item.Content = LoadHostsContent();
            }
        }
    }

    private void RenameEntry_Click(object sender, RoutedEventArgs e)
    {
        HostEntry? entry = null;
        if (sender is FrameworkElement fe && fe.DataContext is HostEntry dc)
            entry = dc;
        else
            entry = SelectedEntry;
        if (entry == null) return;
        if (!entry.IsEditable) return; // Local can't be renamed

        var dlg = new RenameDialog(entry.Title) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var input = dlg.NewName;
            if (!string.IsNullOrWhiteSpace(input) && !string.Equals(input, entry.Title))
            {
                entry.Title = input.Trim();
            }
        }
    }

    private void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        HostEntry? entry = null;
        if (sender is FrameworkElement fe && fe.DataContext is HostEntry dc)
        {
            entry = dc;
        }
        else
        {
            entry = SelectedEntry;
        }

        if (entry == null) return;
        if (!entry.IsEditable)
        {
            System.Windows.MessageBox.Show(this, "Cannot delete the Local entry.", "Delete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(this, $"Delete '{entry.Title}'?", "Confirm", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        var wasSelected = entry == SelectedEntry;
        var idx = Entries.IndexOf(entry);
        Entries.Remove(entry);

        if (wasSelected)
        {
            if (Entries.Count > 0)
            {
                var newIndex = Math.Max(0, idx - 1);
                SelectedEntry = Entries[newIndex];
            }
            else
            {
                SelectedEntry = null;
            }
        }
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        var newEntry = new HostEntry
        {
            Title = "New entry",
            IsActive = true,
            Content = "",
            IsReadOnly = false
        };

        Entries.Add(newEntry);
        SelectedEntry = newEntry;
    }

    private void RowContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu cm) return;
        if (cm.PlacementTarget is not FrameworkElement fe) return;
        cm.DataContext = fe.DataContext;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

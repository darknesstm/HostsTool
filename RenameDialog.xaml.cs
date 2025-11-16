using System.Windows;

namespace HostsTool;

public partial class RenameDialog : Window
{
    public string NewName { get; private set; } = string.Empty;

    public RenameDialog(string current)
    {
        InitializeComponent();
        NameBox.Text = current;
        NameBox.SelectAll();
        NameBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        NewName = NameBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}


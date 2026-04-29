using System.IO;
using Forms = System.Windows.Forms;

namespace CombolistTools.Presentation.Wpf.Services;

public interface IPathPickerService
{
    string? PickInputFile(string? initialPath = null, string filter = "All files (*.*)|*.*");
    string? PickOutputFile(string? initialPath = null, string filter = "CSV files (*.csv)|*.csv|GZip files (*.gz)|*.gz|All files (*.*)|*.*");
    string? PickFolder(string? initialPath = null);
}

public sealed class PathPickerService : IPathPickerService
{
    public string? PickInputFile(string? initialPath = null, string filter = "All files (*.*)|*.*")
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            TrySetInitialDirectory(dialog, initialPath);
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOutputFile(string? initialPath = null, string filter = "CSV files (*.csv)|*.csv|GZip files (*.gz)|*.gz|All files (*.*)|*.*")
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            if (Path.HasExtension(initialPath))
            {
                dialog.FileName = Path.GetFileName(initialPath);
            }
            TrySetInitialDirectory(dialog, initialPath);
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialPath = null)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description = "Choose folder"
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath!;
        }

        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static void TrySetInitialDirectory(Microsoft.Win32.FileDialog dialog, string initialPath)
    {
        var directory = Directory.Exists(initialPath) ? initialPath : Path.GetDirectoryName(initialPath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            dialog.InitialDirectory = directory;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace MetaRemovig;

public partial class MainWindow : Window
{
    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".heif" };

    private readonly List<CleanResult> _results = new();
    private string? _lastSavedFolder;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        ShowState(StartPanel);
    }

    // ---------- Navegación entre pantallas ----------

    private void ShowState(UIElement panel)
    {
        StartPanel.Visibility = Visibility.Collapsed;
        WorkingPanel.Visibility = Visibility.Collapsed;
        DonePanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
    }

    // ---------- Selección de imágenes ----------

    private void PickButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Selecciona tus imágenes",
            Multiselect = true,
            Filter = "Imágenes|*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.heic;*.heif|Todos los archivos|*.*"
        };

        if (dlg.ShowDialog(this) == true && dlg.FileNames.Length > 0)
            _ = ProcessAsync(dlg.FileNames);
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && !_busy
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = ((string[])e.Data.GetData(DataFormats.FileDrop))
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        if (files.Length > 0)
            _ = ProcessAsync(files);
    }

    // ---------- Procesado ----------

    private async Task ProcessAsync(string[] files)
    {
        _busy = true;
        _results.Clear();
        _lastSavedFolder = null;

        Progress.Maximum = files.Length;
        Progress.Value = 0;
        CounterText.Text = $"0 / {files.Length}";
        CurrentFileText.Text = "";
        ShowState(WorkingPanel);

        for (int i = 0; i < files.Length; i++)
        {
            var path = files[i];
            CurrentFileText.Text = Path.GetFileName(path);
            CounterText.Text = $"{i + 1} / {files.Length}";

            var result = await Task.Run(() => ImageCleaner.Clean(path));
            _results.Add(result);
            Progress.Value = i + 1;
        }

        ShowDone();
        _busy = false;
    }

    private void ShowDone()
    {
        int ok = _results.Count(r => r.Success);
        int failed = _results.Count - ok;
        int withMarkers = _results.Count(r => r.Success && r.HadAiMarkers);

        if (ok == 0)
        {
            DoneTitle.Text = "No se pudo procesar ninguna imagen";
            DoneSubtitle.Text = "Revisa que los archivos sean imágenes válidas.";
            SaveButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            DoneTitle.Text = ok == 1 ? "1 imagen limpia" : $"{ok} imágenes limpias";

            var parts = new List<string>
            {
                "Metadatos eliminados y píxeles copiados a un archivo nuevo."
            };
            if (withMarkers > 0)
                parts.Add($"{withMarkers} contenían marcas de IA (C2PA/IPTC) que ya no están.");
            if (failed > 0)
                parts.Add($"{failed} no se pudieron procesar.");

            DoneSubtitle.Text = string.Join(" ", parts);
            SaveButton.Visibility = Visibility.Visible;
        }

        SavedText.Visibility = Visibility.Collapsed;
        OpenFolderButton.Visibility = Visibility.Collapsed;
        ResultsList.ItemsSource = _results.ToList();
        ShowState(DonePanel);
    }

    // ---------- Guardado ----------

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "¿Dónde quieres guardar las imágenes limpias?",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var folder = dlg.FolderName;
            int saved = 0;

            foreach (var r in _results.Where(r => r.Success))
            {
                var target = UniquePath(folder, r.OutputName);
                File.WriteAllBytes(target, r.Data);
                saved++;
            }

            _lastSavedFolder = folder;
            SavedText.Text = $"✓ {saved} {(saved == 1 ? "imagen guardada" : "imágenes guardadas")} en {folder}";
            SavedText.Visibility = Visibility.Visible;
            OpenFolderButton.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo guardar: " + ex.Message, "Meta Removig",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string UniquePath(string folder, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var path = Path.Combine(folder, fileName);
        int n = 2;
        while (File.Exists(path))
            path = Path.Combine(folder, $"{name} ({n++}){ext}");
        return path;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSavedFolder != null)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_lastSavedFolder}\"") { UseShellExecute = true });
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        _results.Clear();
        ResultsList.ItemsSource = null;
        ShowState(StartPanel);
    }
}

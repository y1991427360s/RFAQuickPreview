using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RFAQuickPreview.Desktop.Models;
using RFAQuickPreview.Desktop.Services;
using WinForms = System.Windows.Forms;

namespace RFAQuickPreview.Desktop;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<RfaFileInfo> _files = new();
    private readonly ICollectionView _view;
    private readonly ThumbnailCache _cache = new();
    private readonly ShellThumbnailProvider _thumbnailProvider = new();
    private readonly RevitAutomationService _revitAutomation = new();
    private System.Windows.Point? _dragStartPoint;
    private RfaFileInfo? _dragStartFile;
    private bool _isPlacementRequestInProgress;

    public MainWindow(string? initialFolder = null)
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(_files);
        _view.Filter = Filter;
        FileListBox.ItemsSource = _view;

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            FolderTextBox.Text = initialFolder;
            Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(async () => await ScanAsync()));
        }
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Select folder containing Revit family files",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        await ScanAsync();
    }

    private async Task ScanAsync()
    {
        var folderPath = FolderTextBox.Text;
        AppLog.Write("Scan start folder=" + folderPath);
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            System.Windows.MessageBox.Show(this, "Select a valid folder first.", "RFAQuickPreview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ScanButton.IsEnabled = false;
        _files.Clear();
        LogTextBox.Clear();
        ProgressBar.Value = 0;
        ProgressTextBlock.Text = "Scanning...";

        try
        {
            var paths = await Task.Run(() => Directory.EnumerateFiles(folderPath, "*.rfa", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList());

            AppendLog($"Found {paths.Count} RFA files.");
            var pathsMissingRevitPreviews = paths
                .Where(path => !_cache.IsFresh(path, _cache.GetPath(path)) || !_cache.HasFreshDimensions(path))
                .ToList();
            if (pathsMissingRevitPreviews.Count > 0)
            {
                ProgressTextBlock.Text = "Generating Revit previews...";
                AppendLog($"Generating {pathsMissingRevitPreviews.Count} changed Revit previews and dimensions. Revit will open and close automatically.");
                var automationResult = await _revitAutomation.GenerateFilePreviewsAsync(
                    pathsMissingRevitPreviews,
                    new Progress<string>(AppendLog),
                    CancellationToken.None);
                AppendLog(automationResult);
            }

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var info = RfaFileInfo.FromPath(path);
                var thumbnailPath = _cache.GetPath(path);
                info.ThumbnailPath = thumbnailPath;

                if (_cache.IsFresh(path, thumbnailPath))
                {
                    info.Status = "Revit preview";
                }
                else
                {
                    var ok = await Task.Run(() => _thumbnailProvider.TryCreateThumbnail(path, thumbnailPath));
                    if (ok)
                    {
                        info.Status = "Preview";
                    }
                    else
                    {
                        await Task.Run(() => _thumbnailProvider.CreatePlaceholder(thumbnailPath, "RFA"));
                        info.Status = "No shell preview";
                    }
                }

                _files.Add(info);
                ProgressBar.Value = paths.Count == 0 ? 100 : (i + 1) * 100d / paths.Count;
                ProgressTextBlock.Text = $"{i + 1} / {paths.Count}";
                AppendLog($"{info.Status}: {info.FileName}");
                await Task.Yield();
            }

            ProgressTextBlock.Text = $"Completed: {paths.Count} files";
        }
        catch (Exception ex)
        {
            AppLog.Write("Scan failed: " + ex);
            AppendLog("Scan failed: " + ex.Message);
            System.Windows.MessageBox.Show(this, ex.Message, "RFAQuickPreview", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            AppLog.Write("Scan finally");
            ScanButton.IsEnabled = true;
        }
    }

    private bool Filter(object obj)
    {
        if (obj is not RfaFileInfo info)
        {
            return false;
        }

        var query = SearchTextBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return info.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || info.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
    }

    private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileListBox.SelectedItem is not RfaFileInfo info)
        {
            DetailTitleTextBlock.Text = "No file selected";
            DetailImage.Source = null;
            DetailPathTextBlock.Text = string.Empty;
            DetailSizeTextBlock.Text = string.Empty;
            DetailModifiedTextBlock.Text = string.Empty;
            DetailFrontWidthTextBlock.Text = string.Empty;
            DetailFrontHeightTextBlock.Text = string.Empty;
            DetailLeftWidthTextBlock.Text = string.Empty;
            PlaceInRevitButton.IsEnabled = false;
            return;
        }

        DetailTitleTextBlock.Text = info.FileName;
        DetailPathTextBlock.Text = info.FullPath;
        DetailSizeTextBlock.Text = "Size: " + info.FileSizeText;
        DetailModifiedTextBlock.Text = "Modified: " + info.ModifiedTime.ToString("yyyy-MM-dd HH:mm:ss");
        SetDimensionDetails(info);
        DetailImage.Source = LoadBitmap(info.ThumbnailPath);
        PlaceInRevitButton.IsEnabled = true;
    }

    private void SetDimensionDetails(RfaFileInfo info)
    {
        if (_cache.TryReadDimensions(info.FullPath, out var frontWidthFeet, out var frontHeightFeet, out var leftWidthFeet))
        {
            DetailFrontWidthTextBlock.Text = "Front width: " + FormatFeetAsMillimeters(frontWidthFeet);
            DetailFrontHeightTextBlock.Text = "Front height: " + FormatFeetAsMillimeters(frontHeightFeet);
            DetailLeftWidthTextBlock.Text = "Left width: " + FormatFeetAsMillimeters(leftWidthFeet);
            return;
        }

        DetailFrontWidthTextBlock.Text = "Front width: not generated";
        DetailFrontHeightTextBlock.Text = "Front height: not generated";
        DetailLeftWidthTextBlock.Text = "Left width: not generated";
    }

    private static string FormatFeetAsMillimeters(double feet)
    {
        var millimeters = feet * 304.8;
        return $"{millimeters:0} mm";
    }

    private void FileListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(FileListBox);
        _dragStartFile = GetRfaFileInfoFromMouseEvent(e);
    }

    private async void FileListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPlacementRequestInProgress
            || e.LeftButton != MouseButtonState.Pressed
            || _dragStartPoint is not { } startPoint
            || _dragStartFile is not { } info)
        {
            return;
        }

        var currentPoint = e.GetPosition(FileListBox);
        var movedFarEnough =
            Math.Abs(currentPoint.X - startPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(currentPoint.Y - startPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough || !File.Exists(info.FullPath))
        {
            return;
        }

        _dragStartPoint = null;
        _dragStartFile = null;
        await RequestPlaceInRevitAsync(info);
    }

    private async void PlaceInRevitButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListBox.SelectedItem is RfaFileInfo info)
        {
            await RequestPlaceInRevitAsync(info);
        }
    }

    private void ReplaceFileInfo(RfaFileInfo oldInfo, RfaFileInfo newInfo)
    {
        var index = _files.IndexOf(oldInfo);
        if (index < 0)
        {
            return;
        }

        _files[index] = newInfo;
        FileListBox.SelectedItem = newInfo;
    }

    private async Task RequestPlaceInRevitAsync(RfaFileInfo info)
    {
        if (_isPlacementRequestInProgress)
        {
            return;
        }

        _isPlacementRequestInProgress = true;
        PlaceInRevitButton.IsEnabled = false;
        ProgressTextBlock.Text = "Sending family to Revit...";

        try
        {
            AppendLog("Place in Revit: " + info.FileName);
            var result = await _revitAutomation.RequestPlaceFamilyAsync(info.FullPath, CancellationToken.None);
            AppendLog(result);
            ProgressTextBlock.Text = result;
        }
        catch (Exception ex)
        {
            AppLog.Write("Place in Revit failed: " + ex);
            AppendLog("Place in Revit failed: " + ex.Message);
            ProgressTextBlock.Text = "Place in Revit failed.";
            System.Windows.MessageBox.Show(this, ex.Message, "RFAQuickPreview", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isPlacementRequestInProgress = false;
            var hasSelection = FileListBox.SelectedItem is RfaFileInfo;
            PlaceInRevitButton.IsEnabled = hasSelection;
        }
    }

    private RfaFileInfo? GetRfaFileInfoFromMouseEvent(System.Windows.Input.MouseEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return null;
        }

        return FindAncestor<ListBoxItem>(source)?.DataContext as RfaFileInfo;
    }

    private static T? FindAncestor<T>(DependencyObject current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void FileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileListBox.SelectedItem is RfaFileInfo info)
        {
            Process.Start(new ProcessStartInfo(info.FullPath) { UseShellExecute = true });
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(FolderTextBox.Text) && Directory.Exists(FolderTextBox.Text))
        {
            Process.Start(new ProcessStartInfo(FolderTextBox.Text) { UseShellExecute = true });
        }
    }

    private void AppendLog(string message)
    {
        LogTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

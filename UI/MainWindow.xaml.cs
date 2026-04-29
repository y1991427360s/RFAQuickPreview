using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.UI;
using RFAQuickPreview.App;
using RFAQuickPreview.Models;
using WinForms = System.Windows.Forms;

namespace RFAQuickPreview.UI
{
    public partial class MainWindow : Window
    {
        private readonly ExternalEvent _scanEvent;
        private readonly ScanExternalEventHandler _scanHandler;
        private readonly ObservableCollection<FamilyPreviewInfo> _families = new ObservableCollection<FamilyPreviewInfo>();
        private readonly ICollectionView _familyView;

        public MainWindow(ExternalEvent scanEvent, ScanExternalEventHandler scanHandler)
        {
            InitializeComponent();
            _scanEvent = scanEvent;
            _scanHandler = scanHandler;
            _familyView = CollectionViewSource.GetDefaultView(_families);
            _familyView.Filter = FilterFamily;
            FamilyListBox.ItemsSource = _familyView;
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select folder containing Revit family files";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    FolderTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FolderTextBox.Text))
            {
                MessageBox.Show(this, "Select a folder first.", "RFAQuickPreview", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _families.Clear();
            LogTextBox.Clear();
            ScanProgressBar.Value = 0;
            ProgressTextBlock.Text = "Scanning...";

            SelectFolderButton.IsEnabled = false;
            ScanButton.IsEnabled = false;

            _scanHandler.RequestScan(FolderTextBox.Text);
            var result = _scanEvent.Raise();
            if (result != ExternalEventRequest.Accepted)
            {
                ReportScanFailed(new InvalidOperationException("Revit rejected the scan request: " + result));
            }
        }

        public void ReportScanProgress(ScanProgressInfo info)
        {
            if (info.Total > 0)
            {
                ScanProgressBar.Value = info.Current * 100d / info.Total;
                ProgressTextBlock.Text = info.Current + " / " + info.Total;
            }

            AppendLog(info.Message);
        }

        public void ReportFamilyScanned(FamilyPreviewInfo info)
        {
            _families.Add(info);
        }

        public void ReportScanCompleted(System.Collections.Generic.IList<FamilyPreviewInfo> results)
        {
            ProgressTextBlock.Text = "Completed: " + results.Count + " files";
            AppendLog("Completed. Files: " + results.Count);
            SelectFolderButton.IsEnabled = true;
            ScanButton.IsEnabled = true;
        }

        public void ReportScanFailed(Exception ex)
        {
            ProgressTextBlock.Text = "Scan failed";
            AppendLog("Scan failed: " + ex.Message);
            SelectFolderButton.IsEnabled = true;
            ScanButton.IsEnabled = true;
            MessageBox.Show(this, ex.Message, "RFAQuickPreview", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ScheduleNextScanStep()
        {
            Dispatcher.BeginInvoke(new Action(() => _scanHandler.ContinueScan()));
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _familyView.Refresh();
        }

        private bool FilterFamily(object obj)
        {
            var info = obj as FamilyPreviewInfo;
            if (info == null)
            {
                return false;
            }

            var query = SearchTextBox == null ? string.Empty : SearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return Contains(info.FileName, query)
                || Contains(info.FamilyName, query)
                || Contains(info.FamilyCategory, query);
        }

        private static bool Contains(string value, string query)
        {
            return value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FamilyListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var info = FamilyListBox.SelectedItem as FamilyPreviewInfo;
            if (info == null)
            {
                DetailTitleTextBlock.Text = "No family selected";
                DetailPathTextBlock.Text = string.Empty;
                DetailCategoryTextBlock.Text = string.Empty;
                DetailSizeTextBlock.Text = string.Empty;
                DetailModifiedTextBlock.Text = string.Empty;
                ParameterGrid.ItemsSource = null;
                TypeListBox.ItemsSource = null;
                return;
            }

            DetailTitleTextBlock.Text = info.FamilyName;
            DetailPathTextBlock.Text = info.FullPath;
            DetailCategoryTextBlock.Text = "Category: " + info.FamilyCategory;
            DetailSizeTextBlock.Text = "Size: " + info.FileSizeText;
            DetailModifiedTextBlock.Text = "Modified: " + info.ModifiedTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            ParameterGrid.ItemsSource = info.Parameters.OrderBy(p => p.Name).ToList();
            TypeListBox.ItemsSource = info.TypeNames;
        }

        private void FamilyListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var info = FamilyListBox.SelectedItem as FamilyPreviewInfo;
            if (info == null || string.IsNullOrWhiteSpace(info.FullPath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(info.FullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog("Open failed: " + ex.Message);
            }
        }

        private void AppendLog(string message)
        {
            LogTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            LogTextBox.ScrollToEnd();
        }

    }
}

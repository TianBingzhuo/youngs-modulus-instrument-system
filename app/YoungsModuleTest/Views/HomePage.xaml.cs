using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using YoungsModuleTest.Maintenance;




// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace YoungsModuleTest.Views
{
    public sealed partial class HomePage : Page
    {
        public ObservableCollection<QuickAccessItem> QuickAccessItems { get; private set; } = new();
        public ObservableCollection<ExperimentRecord> ExperimentRecords { get; private set; } = new();

        public HomePage()
        {
            this.InitializeComponent();
            InitializeQuickAccess();
            InitializeExperimentRecords();
            LoadExperimentRecords();
        }

        private void InitializeQuickAccess()
        {
            QuickAccessItems = new ObservableCollection<QuickAccessItem>
            {
                new QuickAccessItem("原理介绍", "\uE736", "Theory"),
                new QuickAccessItem("实验中心", "\uE7C5", "Experiment"),
                new QuickAccessItem("仪器校准", "\uE99A", "Calibration"),
                new QuickAccessItem("系统体检", "\uE9D9", "SystemCheck"),
                new QuickAccessItem("设置", "\uE713", "Settings")
            };

            QuickAccessGrid.ItemsSource = QuickAccessItems;
        }

        private void InitializeExperimentRecords()
        {
            ExperimentRecords = new ObservableCollection<ExperimentRecord>();
            ExperimentRecordsList.ItemsSource = ExperimentRecords;
        }

        private async void LoadExperimentRecords()
        {
            await RecordFile.Gate.WaitAsync();
            try
            {
                var records = await ReadRecords();
                ReplaceRecords(records);
            }
            catch (Exception ex)
            {
                RecordStatus.Text = $"记录读取失败，原文件未改动: {ex.Message}";
            }
            finally { RecordFile.Gate.Release(); }
        }

        private static string RecordsPath => System.IO.Path.Combine(
            ApplicationData.Current.LocalFolder.Path, "experiment_records.json");

        private static async Task<List<ExperimentRecord>> ReadRecords()
        {
            string? json = await RecordFile.ReadAsync(RecordsPath);
            if (json == null) return new List<ExperimentRecord>();
            var records = JsonSerializer.Deserialize<List<ExperimentRecord>>(json)
                ?? throw new InvalidDataException("Experiment records must be a JSON array.");
            if (records.Any(r => r == null)) throw new InvalidDataException("Null experiment record.");
            return records;
        }

        private void ReplaceRecords(List<ExperimentRecord> records)
        {
            ExperimentRecords.Clear();
            foreach (var record in records) ExperimentRecords.Add(record);
            RecordStatus.Text = records.Count == 0 ? "暂无实验记录" : "";
        }

        private async Task DeleteSavedRecords(string? name)
        {
            await RecordFile.Gate.WaitAsync();
            try
            {
                var records = await ReadRecords();
                if (name == null) records.Clear();
                else records.RemoveAll(r => r.Name == name);
                await RecordFile.WriteAsync(RecordsPath, JsonSerializer.Serialize(records,
                    new JsonSerializerOptions { WriteIndented = true }));
                ReplaceRecords(records);
            }
            catch (Exception ex)
            {
                RecordStatus.Text = $"删除未保存，原记录保留: {ex.Message}";
            }
            finally { RecordFile.Gate.Release(); }
        }

        private void QuickAccessGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QuickAccessItem item)
            {
                // 修复：获取主窗口并正确访问控件
                if (App.Current.MainWindow is MainWindow mainWindow)
                {
                    // 修复：通过主窗口的Content获取NavigationView
                    if (mainWindow.Content is Grid rootGrid)
                    {
                        // 查找NavigationView（在Grid的第二行）
                        if (rootGrid.Children.Count > 1 && rootGrid.Children[1] is NavigationView navigationView)
                        {
                            // 查找匹配的导航项
                            foreach (var navItem in navigationView.MenuItems)
                            {
                                if (navItem is NavigationViewItem navViewItem &&
                                    navViewItem.Tag?.ToString() == item.NavigationTag)
                                {
                                    navigationView.SelectedItem = navViewItem;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void ClearAllRecordsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog()
            {
                Title = "确认删除",
                Content = "确定要删除所有实验记录吗？此操作无法撤销。",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await DeleteSavedRecords(null);
            }
        }

        private void OpenRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ExperimentRecord record)
            {
                // 修复：正确的导航方式
                if (App.Current.MainWindow is MainWindow mainWindow)
                {
                    if (mainWindow.Content is Grid rootGrid && rootGrid.Children.Count > 1)
                    {
                        if (rootGrid.Children[1] is NavigationView navigationView)
                        {
                            // 获取ContentFrame
                            var contentFrame = navigationView.Content as Frame;
                            if (contentFrame != null)
                            {
                                // 导航到实验页面并传递记录数据
                                contentFrame.Navigate(typeof(ExperimentPage), record);

                                // 更新导航选中状态
                                foreach (var navItem in navigationView.MenuItems)
                                {
                                    if (navItem is NavigationViewItem navViewItem &&
                                        navViewItem.Tag?.ToString() == "Experiment")
                                    {
                                        navigationView.SelectedItem = navViewItem;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void DeleteRecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ExperimentRecord record)
            {
                var dialog = new ContentDialog()
                {
                    Title = "确认删除",
                    Content = $"确定要删除实验记录 \"{record.Name}\" 吗？",
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await DeleteSavedRecords(record.Name);
                }
            }
        }
    }

    public class QuickAccessItem
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("iconGlyph")]
        public string IconGlyph { get; set; } = "";

        [JsonPropertyName("navigationTag")]
        public string NavigationTag { get; set; } = "";

        public QuickAccessItem() { }

        public QuickAccessItem(string title, string iconGlyph, string navigationTag)
        {
            Title = title;
            IconGlyph = iconGlyph;
            NavigationTag = navigationTag;
        }
    }

    public class ExperimentRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("dateTime")]
        public string DateTime { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = "";

        [JsonPropertyName("youngsModulus")]
        public double? YoungsModulus { get; set; }

        [JsonPropertyName("measurementCount")]
        public int MeasurementCount { get; set; }

        [JsonPropertyName("materialType")]
        public string MaterialType { get; set; } = "";

        [JsonPropertyName("measurementData")]
        public string MeasurementData { get; set; } = "";

        // 新增字段：CCD数据
        [JsonPropertyName("ccdData")]
        public string CCDData { get; set; } = "";

        // 新增字段：CCD边界信息
        [JsonPropertyName("leftBoundary")]
        public int LeftBoundary { get; set; }

        [JsonPropertyName("rightBoundary")]
        public int RightBoundary { get; set; }

        // 新增字段：照片数据（Base64编码的图片列表）
        [JsonPropertyName("capturedPhotos")]
        public List<string> CapturedPhotos { get; set; } = new List<string>();

        // 新增字段：实验参数设置
        [JsonPropertyName("integrationTime")]
        public string IntegrationTime { get; set; } = "";

        [JsonPropertyName("serialPort")]
        public string SerialPort { get; set; } = "";

        [JsonPropertyName("cameraDevice")]
        public string CameraDevice { get; set; } = "";

        // 新增字段：统计数据
        [JsonPropertyName("maxValue")]
        public int MaxValue { get; set; }

        [JsonPropertyName("minValue")]
        public int MinValue { get; set; }

        [JsonPropertyName("avgValue")]
        public double AvgValue { get; set; }

        [JsonPropertyName("contrast")]
        public double Contrast { get; set; }

        [JsonPropertyName("currentIlluminatedLength")]
        public double CurrentIlluminatedLength { get; set; }
    }

}

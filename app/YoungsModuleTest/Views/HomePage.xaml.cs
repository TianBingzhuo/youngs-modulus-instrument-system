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




// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace YoungsModuleTest.Views
{
    public sealed partial class HomePage : Page
    {
        public ObservableCollection<QuickAccessItem> QuickAccessItems { get; private set; }
        public ObservableCollection<ExperimentRecord> ExperimentRecords { get; private set; }

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
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var recordsFile = await localFolder.TryGetItemAsync("experiment_records.json");

                if (recordsFile is StorageFile file)
                {
                    var json = await FileIO.ReadTextAsync(file);
                    var records = JsonSerializer.Deserialize<ExperimentRecord[]>(json);

                    ExperimentRecords.Clear();
                    if (records != null)
                    {
                        foreach (var record in records)
                        {
                            ExperimentRecords.Add(record);
                        }
                    }
                }
                else
                {
                    // 添加示例数据
                    ExperimentRecords.Add(new ExperimentRecord
                    {
                        Name = "示例实验 1",
                        DateTime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"),
                        Description = "材料：铜丝，长度：100cm"
                    });
                }
            }
            catch (Exception)
            {
                // 记录加载失败，使用默认的示例数据
                ExperimentRecords.Clear();
                ExperimentRecords.Add(new ExperimentRecord
                {
                    Name = "示例实验 1",
                    DateTime = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"),
                    Description = "材料：铜丝，长度：100cm"
                });
            }
        }

        private async Task SaveExperimentRecords()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var recordsFile = await localFolder.CreateFileAsync("experiment_records.json",
                    CreationCollisionOption.ReplaceExisting);

                var recordsArray = new ExperimentRecord[ExperimentRecords.Count];
                ExperimentRecords.CopyTo(recordsArray, 0);

                var json = JsonSerializer.Serialize(recordsArray,
                    new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(recordsFile, json);
            }
            catch (Exception)
            {
                // TODO: 显示保存失败的消息
            }
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
                ExperimentRecords.Clear();
                await SaveExperimentRecords();
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
                    ExperimentRecords.Remove(record);
                    await SaveExperimentRecords();
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

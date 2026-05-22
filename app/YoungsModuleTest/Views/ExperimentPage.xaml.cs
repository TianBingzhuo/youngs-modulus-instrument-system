using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;

namespace YoungsModuleTest.Views
{
    public sealed partial class ExperimentPage : Page
    {
        private List<CCDDataPoint> _ccdData = new List<CCDDataPoint>();
        private ExperimentRecord? _currentRecord;
        private List<MeasurementRecord> _measurementRecords = new List<MeasurementRecord>();

        // 摄像头相关字段
        private MediaFrameSourceGroup? mediaFrameSourceGroup;
        private MediaCapture? mediaCapture;
        private bool _isCameraInitialized = false;
        private List<MediaFrameSourceGroup> _availableCameraGroups = new List<MediaFrameSourceGroup>();

        // 照片容器相关字段
        private List<BitmapImage> _capturedPhotos = new List<BitmapImage>();
        private const int PHOTO_WIDTH = 80;
        private const int PHOTO_HEIGHT = 60;
        private const int PHOTO_MARGIN = 4;

        // CCD串口相关字段
        private SerialPort? _serialPort;
        private bool _isCCDConnected = false;
        private bool _isCollecting = false;
        private Timer? _dataCollectionTimer;
        private byte _currentIntegrationTime = 0xB1; // 默认10μs

        // 数据处理相关
        private List<byte> _dataBuffer = new List<byte>();
        private bool _receiveDataFlag = false;
        private CancellationTokenSource? _cancellationTokenSource;

        // 绘图相关
        private const int CHART_WIDTH = 350;
        private const int CHART_HEIGHT = 300;
        private const int CHART_MARGIN = 40;

        // CCD分析相关
        private const double PIXEL_SIZE_UM = 8.0; // 根据PDF文档，像素大小为8微米
        private int _leftBoundary = 0;
        private int _rightBoundary = 0;
        private double _currentIlluminatedLength = 0.0; // 当前光照长度（毫米）

        public ExperimentPage()
        {
            this.InitializeComponent();
            _ = InitializePageAsync();
            _ = LoadWireParameters(); // 这会加载保存的参数，如果没有则使用代码中的默认值

            // 监听急停状态
            this.Loaded += ExperimentPage_Loaded;
            this.SizeChanged += ExperimentPage_SizeChanged;

            // 监听施加力变化
            AppliedForceBox.ValueChanged += AppliedForceBox_ValueChanged;

            // 修复施加力控件设置
            AppliedForceBox.Value = double.NaN;
            AppliedForceBox.SmallChange = 1.0;
            AppliedForceBox.LargeChange = 5.0;
            AppliedForceBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;
            AppliedForceBox.NumberFormatter = new Windows.Globalization.NumberFormatting.DecimalFormatter()
            {
                FractionDigits = 1,
                IntegerDigits = 1
            };
        }

        private void ExperimentPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshPhotoLayout();
        }

        private void ExperimentPage_Loaded(object sender, RoutedEventArgs e)
        {
            CheckEmergencyStopStatus();
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                RefreshPhotoLayout();
            });
        }

        private void AppliedForceBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!double.IsNaN(args.NewValue))
            {
                CurrentForceText.Text = $"{args.NewValue:F1} KG";
                CalculateYoungsModulus();
            }
            else
            {
                CurrentForceText.Text = "0.0 KG";
            }
        }

        private void CheckEmergencyStopStatus()
        {
            if (App.Current.MainWindow is MainWindow mainWindow)
            {
                if (mainWindow.IsEmergencyStopped)
                {
                    if (_isCollecting)
                    {
                        _ = StopCCDCollection();
                    }

                    if (ExperimentResultText != null)
                    {
                        ExperimentResultText.Text = "急停状态 - 请解除急停";
                        ExperimentResultText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    }
                }
                else
                {
                    if (ExperimentResultText != null && ExperimentResultText.Text.Contains("急停"))
                    {
                        ExperimentResultText.Text = "请施加载荷并开始CCD数据采集";
                        var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as Microsoft.UI.Xaml.Media.SolidColorBrush;
                        ExperimentResultText.Foreground = accentBrush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Blue);
                    }
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            CheckEmergencyStopStatus();

            if (e.Parameter is ExperimentRecord record)
            {
                _currentRecord = record;
                await LoadExperimentRecord(record);
            }

            await LoadAvailableCameras();
            await LoadAvailableSerialPorts();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            await CleanupCamera();
            await CleanupSerialPort();
        }

        private async Task InitializePageAsync()
        {
            await LoadAvailableCameras();
            await LoadAvailableSerialPorts();
        }

        #region 摄像头相关方法
        private async Task LoadAvailableCameras()
        {
            try
            {
                var groups = await MediaFrameSourceGroup.FindAllAsync();
                _availableCameraGroups.Clear();
                _availableCameraGroups.AddRange(groups);

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    CameraSelectionComboBox.Items.Clear();

                    if (groups.Count == 0)
                    {
                        frameSourceName.Text = "实验画面";
                        CameraStatusText.Text = "未找到摄像头设备";
                        return;
                    }

                    foreach (var group in groups)
                    {
                        var cameraDevice = new CameraDevice
                        {
                            Name = group.DisplayName,
                            Id = group.Id,
                            SourceGroup = group
                        };
                        CameraSelectionComboBox.Items.Add(cameraDevice);
                    }

                    if (CameraSelectionComboBox.Items.Count > 0)
                    {
                        CameraSelectionComboBox.SelectedIndex = 0;
                        frameSourceName.Text = "实验画面";
                        CameraStatusText.Text = "点击启动摄像头";
                    }
                });
            }
            catch (Exception ex)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    frameSourceName.Text = "实验画面";
                    CameraStatusText.Text = $"摄像头枚举失败: {ex.Message}";
                });
            }
        }

        private async Task InitializeCamera()
        {
            try
            {
                await CleanupCamera();

                if (CameraSelectionComboBox.SelectedItem is not CameraDevice selectedDevice)
                {
                    CameraStatusText.Text = "请选择摄像头设备";
                    return;
                }

                mediaFrameSourceGroup = selectedDevice.SourceGroup;
                frameSourceName.Text = "实验画面";
                CameraStatusText.Text = "正在初始化摄像头...";

                mediaCapture = new MediaCapture();
                var mediaCaptureInitializationSettings = new MediaCaptureInitializationSettings()
                {
                    SourceGroup = mediaFrameSourceGroup,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };

                await mediaCapture.InitializeAsync(mediaCaptureInitializationSettings);

                if (mediaFrameSourceGroup.SourceInfos.Count > 0)
                {
                    var frameSource = mediaCapture.FrameSources[mediaFrameSourceGroup.SourceInfos[0].Id];
                    captureElement.Source = Windows.Media.Core.MediaSource.CreateFromMediaFrameSource(frameSource);
                }

                _isCameraInitialized = true;
                frameSourceName.Text = "实验画面";
                CameraStatusText.Visibility = Visibility.Collapsed;
                CapturePhotoButton.IsEnabled = true;
                StartCameraButton.Content = "停止摄像头";
            }
            catch (Exception ex)
            {
                _isCameraInitialized = false;
                CameraStatusText.Text = $"摄像头初始化失败: {ex.Message}";
                CameraStatusText.Visibility = Visibility.Visible;
                CapturePhotoButton.IsEnabled = false;
                StartCameraButton.Content = "启动摄像头";
            }
        }

        private async Task CleanupCamera()
        {
            _isCameraInitialized = false;
            CapturePhotoButton.IsEnabled = false;

            if (captureElement?.Source != null)
            {
                captureElement.Source = null;
            }

            if (mediaCapture != null)
            {
                mediaCapture.Dispose();
                mediaCapture = null;
            }

            mediaFrameSourceGroup = null;
            StartCameraButton.Content = "启动摄像头";

            await Task.CompletedTask;
        }

        private async void StartCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCameraInitialized)
            {
                await CleanupCamera();
                CameraStatusText.Text = "摄像头已停止";
                CameraStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                await InitializeCamera();
            }
        }

        private async void CapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            if (mediaCapture == null || !_isCameraInitialized)
                return;

            try
            {
                var imgFormat = ImageEncodingProperties.CreateJpeg();
                var stream = new InMemoryRandomAccessStream();
                await mediaCapture.CapturePhotoToStreamAsync(imgFormat, stream);
                stream.Seek(0);

                BitmapImage bmpImage = new BitmapImage();
                await bmpImage.SetSourceAsync(stream);
                _capturedPhotos.Insert(0, bmpImage);

                // 保存照片到设备相册的"杨氏模量测试仪"文件夹
                await SavePhotoToDevice(stream);

                RefreshPhotoLayout();
                capturedText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"拍照失败: {ex.Message}");
            }
        }

        private async Task SavePhotoToDevice(InMemoryRandomAccessStream stream)
        {
            try
            {
                // 获取相册库
                var picturesLibrary = Windows.Storage.KnownFolders.PicturesLibrary;

                // 创建或获取"杨氏模量测试仪"文件夹
                var appFolder = await picturesLibrary.CreateFolderAsync("杨氏模量测试仪",
                    CreationCollisionOption.OpenIfExists);

                // 生成文件名
                var fileName = $"实验照片_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";

                // 创建文件
                var photoFile = await appFolder.CreateFileAsync(fileName,
                    CreationCollisionOption.GenerateUniqueName);

                // 保存图片
                stream.Seek(0);
                using var fileStream = await photoFile.OpenAsync(FileAccessMode.ReadWrite);
                await RandomAccessStream.CopyAsync(stream, fileStream);

                Debug.WriteLine($"照片已保存到: {photoFile.Path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存照片失败: {ex.Message}");
            }
        }

        private void RefreshPhotoLayout()
        {
            if (_capturedPhotos.Count == 0)
                return;

            snapshots.Children.Clear();

            double containerWidth = snapshotsContainer.ActualWidth;
            if (containerWidth <= 0)
                containerWidth = 320;

            int photosPerRow = Math.Max(1, (int)(containerWidth / (PHOTO_WIDTH + PHOTO_MARGIN * 2)));

            StackPanel currentRow = null;
            int currentRowCount = 0;

            foreach (var photo in _capturedPhotos)
            {
                if (currentRow == null || currentRowCount >= photosPerRow)
                {
                    currentRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    snapshots.Children.Add(currentRow);
                    currentRowCount = 0;
                }

                var imageControl = new Image()
                {
                    Source = photo,
                    Width = PHOTO_WIDTH,
                    Height = PHOTO_HEIGHT,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
                    Margin = new Thickness(PHOTO_MARGIN)
                };

                currentRow.Children.Add(imageControl);
                currentRowCount++;
            }
        }

        private async void CameraSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isCameraInitialized)
            {
                await CleanupCamera();
            }
        }
        #endregion

        #region CCD串口相关方法
        private async Task LoadAvailableSerialPorts()
        {
            await Task.Run(() =>
            {
                try
                {
                    var portNames = SerialPort.GetPortNames();

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        SerialPortComboBox.Items.Clear();

                        if (portNames.Length == 0)
                        {
                            SerialPortComboBox.PlaceholderText = "未找到串口设备";
                            CCDStatusText.Text = "未找到串口设备";
                            return;
                        }

                        foreach (var portName in portNames.OrderBy(p => p))
                        {
                            SerialPortComboBox.Items.Add(portName);
                        }

                        SerialPortComboBox.PlaceholderText = "选择串口";
                        CCDStatusText.Text = "请选择串口";
                    });
                }
                catch (Exception ex)
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        SerialPortComboBox.PlaceholderText = "串口枚举失败";
                        CCDStatusText.Text = $"串口枚举失败: {ex.Message}";
                    });
                }
            });
        }

        private async void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortsButton.IsEnabled = false;
            await LoadAvailableSerialPorts();
            RefreshPortsButton.IsEnabled = true;
        }

        private async Task InitializeSerialPort(string portName)
        {
            try
            {
                await CleanupSerialPort();

                await Task.Run(() =>
                {
                    _serialPort = new SerialPort(portName, 921600, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 5000,
                        WriteTimeout = 1000
                    };

                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.Open();
                });

                await SendSerialCommand(_currentIntegrationTime);

                _isCCDConnected = true;

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    CCDStatusText.Text = "CCD已连接";
                    CCDStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
                });
            }
            catch (Exception ex)
            {
                _isCCDConnected = false;

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    CCDStatusText.Text = "串口连接失败";
                    CCDStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                });

                throw new InvalidOperationException($"串口连接失败: {ex.Message}");
            }
        }

        private async Task CleanupSerialPort()
        {
            _isCCDConnected = false;

            if (_dataCollectionTimer != null)
            {
                _dataCollectionTimer.Dispose();
                _dataCollectionTimer = null;
            }

            _cancellationTokenSource?.Cancel();

            if (_serialPort != null)
            {
                try
                {
                    while (_receiveDataFlag)
                    {
                        await Task.Delay(10);
                    }

                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    _serialPort.Dispose();
                }
                catch { }
                _serialPort = null;
            }

            await Task.CompletedTask;
        }

        private async Task<bool> SendSerialCommand(byte command)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                    return false;

                await Task.Run(() =>
                {
                    _serialPort.Write(new byte[] { command }, 0, 1);
                });

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"发送串口命令失败: {ex.Message}");
                return false;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                _receiveDataFlag = true;
                int num = _serialPort?.BytesToRead ?? 0;
                if (num > 0 && _serialPort != null)
                {
                    byte[] receivedBuffer = new byte[num];
                    _serialPort.Read(receivedBuffer, 0, num);

                    lock (_dataBuffer)
                    {
                        _dataBuffer.AddRange(receivedBuffer);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"串口数据接收失败: {ex.Message}");
            }
            finally
            {
                _receiveDataFlag = false;
            }
        }

        private async void SerialPortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SerialPortComboBox.SelectedItem is string portName)
            {
                CCDStatusText.Text = "正在连接...";
                CCDStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);

                try
                {
                    await InitializeSerialPort(portName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"串口初始化失败: {ex.Message}");
                }
            }
        }

        private async void IntegrationTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntegrationTimeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            {
                if (byte.TryParse(tagStr.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out byte command))
                {
                    _currentIntegrationTime = command;
                    if (_isCCDConnected)
                    {
                        await SendSerialCommand(command);
                    }
                }
            }
        }

        private async void StartCCDButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCCDConnected)
            {
                CCDStatusText.Text = "请先选择并连接串口";
                return;
            }

            await StartCCDCollection();
        }

        private async void StopCCDButton_Click(object sender, RoutedEventArgs e)
        {
            await StopCCDCollection();
        }

        private async Task StartCCDCollection()
        {
            if (_isCollecting) return;

            _isCollecting = true;
            StartCCDButton.IsEnabled = false;
            StopCCDButton.IsEnabled = true;
            ChartPlaceholderText.Visibility = Visibility.Collapsed;

            _cancellationTokenSource = new CancellationTokenSource();

            _ = Task.Run(() => ProcessCCDDataLoop(_cancellationTokenSource.Token));

            ExperimentResultText.Text = "CCD数据采集中...";
        }

        private async Task StopCCDCollection()
        {
            if (!_isCollecting) return;

            _isCollecting = false;
            StartCCDButton.IsEnabled = true;
            StopCCDButton.IsEnabled = false;

            _cancellationTokenSource?.Cancel();

            ExperimentResultText.Text = "CCD数据采集已停止";
            await Task.CompletedTask;
        }

        private async Task ProcessCCDDataLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (_isCollecting && !cancellationToken.IsCancellationRequested)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        await SendSerialCommand(0xA2);
                        await Task.Delay(100, cancellationToken);
                        await ProcessBufferedData();
                        await Task.Delay(200, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不处理
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CCD数据处理循环失败: {ex.Message}");
            }
        }

        private async Task ProcessBufferedData()
        {
            List<byte> currentBuffer;

            lock (_dataBuffer)
            {
                if (_dataBuffer.Count < 3648)
                    return;

                currentBuffer = new List<byte>(_dataBuffer.Take(3648));
                _dataBuffer.RemoveRange(0, 3648);
            }

            var dataPoints = new List<CCDDataPoint>();
            for (int i = 0; i < currentBuffer.Count; i++)
            {
                dataPoints.Add(new CCDDataPoint
                {
                    PixelIndex = i,
                    Intensity = currentBuffer[i]
                });
            }

            this.DispatcherQueue.TryEnqueue(() =>
            {
                _ccdData = dataPoints;
                AnalyzeBoundaryPoints();
                UpdateCCDChart();
                UpdateStatistics();
            });

            await Task.CompletedTask;
        }

        // 修正的边界检测算法 - 检测中间低谷区域
        // 改进的边界检测算法 - 替换ExperimentPage.xaml.cs中的AnalyzeBoundaryPoints方法
        // 改进的边界检测算法 - 替换ExperimentPage.xaml.cs中的AnalyzeBoundaryPoints方法
        private void AnalyzeBoundaryPoints()
        {
            if (_ccdData.Count == 0) return;

            var intensities = _ccdData.Select(d => d.Intensity).ToArray();

            // 使用移动平均平滑数据
            var smoothedData = SmoothData(intensities, 10);

            // 第一步：计算所有数据的平均值
            var firstAverage = smoothedData.Average();

            // 第二步：剔除所有大于平均值的数值，只保留小于等于平均值的数值
            var belowAverageValues = smoothedData.Where(x => x <= firstAverage).ToArray();

            // 第三步：计算剔除后数据的平均值
            var secondAverage = belowAverageValues.Average();

            // 第四步：确定阈值为 secondAverage + 10
            var threshold = secondAverage + 5;

            // 第五步：找到所有小于等于阈值的点的索引
            var lowValleyIndices = new List<int>();
            for (int i = 0; i < smoothedData.Length; i++)
            {
                if (smoothedData[i] <= threshold)
                {
                    lowValleyIndices.Add(i);
                }
            }

            if (lowValleyIndices.Count >= 4) // 确保至少有4个点，才能取第二个和倒数第二个
            {
                // 取第二个和倒数第二个满足条件的点作为边界
                _leftBoundary = lowValleyIndices[1]; // 第二个点
                _rightBoundary = lowValleyIndices[lowValleyIndices.Count - 2]; // 倒数第二个点
            }
            else if (lowValleyIndices.Count >= 2)
            {
                // 如果只有2-3个点，取第一个和最后一个
                _leftBoundary = lowValleyIndices[0];
                _rightBoundary = lowValleyIndices[lowValleyIndices.Count - 1];
            }
            else
            {
                // 备用方案：如果没有足够的低谷点，使用最小值点扩展
                var minIntensity = smoothedData.Min();
                var minIndex = Array.IndexOf(smoothedData, minIntensity);
                _leftBoundary = Math.Max(0, minIndex - 100);
                _rightBoundary = Math.Min(intensities.Length - 1, minIndex + 100);
            }

            // 确保边界有效性
            if (_leftBoundary >= _rightBoundary)
            {
                // 如果边界无效，使用中心点扩展
                var centerIndex = intensities.Length / 2;
                _leftBoundary = Math.Max(0, centerIndex - 50);
                _rightBoundary = Math.Min(intensities.Length - 1, centerIndex + 50);
            }

            // 计算低谷区域长度
            var illuminatedPixels = Math.Abs(_rightBoundary - _leftBoundary);
            _currentIlluminatedLength = illuminatedPixels * PIXEL_SIZE_UM / 1000.0;

            // 调试输出
            Debug.WriteLine($"第一次平均值: {firstAverage:F2}");
            Debug.WriteLine($"剔除高值后的数据量: {belowAverageValues.Length}/{smoothedData.Length}");
            Debug.WriteLine($"第二次平均值: {secondAverage:F2}");
            Debug.WriteLine($"最终阈值: {threshold:F2}");
            Debug.WriteLine($"低谷点数量: {lowValleyIndices.Count}");
            Debug.WriteLine($"检测到低谷区域: 左边界={_leftBoundary}, 右边界={_rightBoundary}, 宽度={illuminatedPixels}像素, 长度={_currentIlluminatedLength:F3}mm");
        }

        private double[] SmoothData(byte[] data, int windowSize)
        {
            var smoothed = new double[data.Length];
            int halfWindow = windowSize / 2;

            for (int i = 0; i < data.Length; i++)
            {
                double sum = 0;
                int count = 0;

                for (int j = Math.Max(0, i - halfWindow); j <= Math.Min(data.Length - 1, i + halfWindow); j++)
                {
                    sum += data[j];
                    count++;
                }

                smoothed[i] = sum / count;
            }

            return smoothed;
        }

        private List<(int start, int end)> FindContinuousLowRegions(double[] data, double threshold)
        {
            var regions = new List<(int start, int end)>();
            int regionStart = -1;
            int minContinuousLength = 20; // 最小连续长度

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] <= threshold)
                {
                    if (regionStart == -1)
                    {
                        regionStart = i;
                    }
                }
                else
                {
                    if (regionStart != -1)
                    {
                        var regionLength = i - regionStart;
                        if (regionLength >= minContinuousLength)
                        {
                            regions.Add((regionStart, i - 1));
                        }
                        regionStart = -1;
                    }
                }
            }

            // 处理最后一个区域
            if (regionStart != -1)
            {
                var regionLength = data.Length - regionStart;
                if (regionLength >= minContinuousLength)
                {
                    regions.Add((regionStart, data.Length - 1));
                }
            }

            return regions;
        }

        private int ExtendBoundaryLeft(double[] data, int startIndex, double threshold)
        {
            // 向左扩展边界，直到强度显著上升
            var extendedThreshold = threshold * 1.1; // 稍微放宽阈值

            for (int i = startIndex; i >= 0; i--)
            {
                if (data[i] > extendedThreshold)
                {
                    return Math.Max(0, i + 1);
                }
            }

            return 0;
        }

        private int ExtendBoundaryRight(double[] data, int endIndex, double threshold)
        {
            // 向右扩展边界，直到强度显著上升
            var extendedThreshold = threshold * 1.1; // 稍微放宽阈值

            for (int i = endIndex; i < data.Length; i++)
            {
                if (data[i] > extendedThreshold)
                {
                    return Math.Min(data.Length - 1, i - 1);
                }
            }

            return data.Length - 1;
        }

        // 重写绘图功能
        private void UpdateCCDChart()
        {
            CCDChart.Children.Clear();

            if (_ccdData.Count == 0) return;

            double chartWidth = CCDChart.ActualWidth > 0 ? CCDChart.ActualWidth - 2 * CHART_MARGIN : CHART_WIDTH - 2 * CHART_MARGIN;
            double chartHeight = CCDChart.ActualHeight > 0 ? CCDChart.ActualHeight - 2 * CHART_MARGIN : CHART_HEIGHT - 2 * CHART_MARGIN;

            var maxIntensity = _ccdData.Max(d => d.Intensity);
            var minIntensity = _ccdData.Min(d => d.Intensity);
            var intensityRange = maxIntensity - minIntensity;

            if (intensityRange == 0) intensityRange = 1;

            DrawGridLines(chartWidth, chartHeight);
            DrawAxes(chartWidth, chartHeight, maxIntensity, minIntensity);
            DrawDataCurve(chartWidth, chartHeight, maxIntensity, minIntensity, intensityRange);
            DrawBoundaryMarkers(chartWidth, chartHeight, minIntensity, intensityRange);
        }

        private void DrawGridLines(double chartWidth, double chartHeight)
        {
            var gridColor = new SolidColorBrush(Microsoft.UI.Colors.LightGray);

            for (int i = 1; i < 10; i++)
            {
                double x = CHART_MARGIN + (chartWidth / 10) * i;
                var gridLine = new Line()
                {
                    X1 = x,
                    Y1 = CHART_MARGIN,
                    X2 = x,
                    Y2 = CHART_MARGIN + chartHeight,
                    Stroke = gridColor,
                    StrokeThickness = 0.5
                };
                CCDChart.Children.Add(gridLine);
            }

            for (int i = 1; i < 8; i++)
            {
                double y = CHART_MARGIN + (chartHeight / 8) * i;
                var gridLine = new Line()
                {
                    X1 = CHART_MARGIN,
                    Y1 = y,
                    X2 = CHART_MARGIN + chartWidth,
                    Y2 = y,
                    Stroke = gridColor,
                    StrokeThickness = 0.5
                };
                CCDChart.Children.Add(gridLine);
            }
        }

        private void DrawAxes(double chartWidth, double chartHeight, double maxIntensity, double minIntensity)
        {
            var axisColor = new SolidColorBrush(Microsoft.UI.Colors.Black);
            var textBrush = new SolidColorBrush(Microsoft.UI.Colors.Black);

            // X轴
            var xAxis = new Line()
            {
                X1 = CHART_MARGIN,
                Y1 = CHART_MARGIN + chartHeight,
                X2 = CHART_MARGIN + chartWidth,
                Y2 = CHART_MARGIN + chartHeight,
                Stroke = axisColor,
                StrokeThickness = 2
            };
            CCDChart.Children.Add(xAxis);

            // Y轴
            var yAxis = new Line()
            {
                X1 = CHART_MARGIN,
                Y1 = CHART_MARGIN,
                X2 = CHART_MARGIN,
                Y2 = CHART_MARGIN + chartHeight,
                Stroke = axisColor,
                StrokeThickness = 2
            };
            CCDChart.Children.Add(yAxis);

            // 添加标签
            for (int i = 0; i <= 10; i++)
            {
                double x = CHART_MARGIN + (chartWidth / 10) * i;
                int pixelValue = (int)((3648.0 / 10) * i);

                var label = new TextBlock()
                {
                    Text = pixelValue.ToString(),
                    FontSize = 10,
                    Foreground = textBrush
                };

                Canvas.SetLeft(label, x - 15);
                Canvas.SetTop(label, CHART_MARGIN + chartHeight + 5);
                CCDChart.Children.Add(label);
            }

            for (int i = 0; i <= 8; i++)
            {
                double y = CHART_MARGIN + chartHeight - (chartHeight / 8) * i;
                int intensityValue = (int)(minIntensity + (maxIntensity - minIntensity) / 8 * i);

                var label = new TextBlock()
                {
                    Text = intensityValue.ToString(),
                    FontSize = 10,
                    Foreground = textBrush
                };

                Canvas.SetLeft(label, 5);
                Canvas.SetTop(label, y - 8);
                CCDChart.Children.Add(label);
            }
        }

        private void DrawDataCurve(double chartWidth, double chartHeight, double maxIntensity, double minIntensity, double intensityRange)
        {
            if (_ccdData.Count < 2) return;

            var dataColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure();

            double firstX = CHART_MARGIN;
            double firstY = CHART_MARGIN + chartHeight - (_ccdData[0].Intensity - minIntensity) / intensityRange * chartHeight;
            pathFigure.StartPoint = new Windows.Foundation.Point(firstX, firstY);

            for (int i = 1; i < _ccdData.Count; i++)
            {
                double x = CHART_MARGIN + (double)i / (_ccdData.Count - 1) * chartWidth;
                double y = CHART_MARGIN + chartHeight - (_ccdData[i].Intensity - minIntensity) / intensityRange * chartHeight;

                var lineSegment = new LineSegment()
                {
                    Point = new Windows.Foundation.Point(x, y)
                };
                pathFigure.Segments.Add(lineSegment);
            }

            pathGeometry.Figures.Add(pathFigure);

            var path = new Microsoft.UI.Xaml.Shapes.Path()
            {
                Data = pathGeometry,
                Stroke = dataColor,
                StrokeThickness = 1.5
            };

            CCDChart.Children.Add(path);
        }

        private void DrawBoundaryMarkers(double chartWidth, double chartHeight, double minIntensity, double intensityRange)
        {
            if (_leftBoundary == 0 && _rightBoundary == 0) return;

            var boundaryColor = new SolidColorBrush(Microsoft.UI.Colors.Green);

            // 左边界线
            double leftX = CHART_MARGIN + (double)_leftBoundary / (_ccdData.Count - 1) * chartWidth;
            var leftLine = new Line()
            {
                X1 = leftX,
                Y1 = CHART_MARGIN,
                X2 = leftX,
                Y2 = CHART_MARGIN + chartHeight,
                Stroke = boundaryColor,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };
            CCDChart.Children.Add(leftLine);

            // 右边界线
            double rightX = CHART_MARGIN + (double)_rightBoundary / (_ccdData.Count - 1) * chartWidth;
            var rightLine = new Line()
            {
                X1 = rightX,
                Y1 = CHART_MARGIN,
                X2 = rightX,
                Y2 = CHART_MARGIN + chartHeight,
                Stroke = boundaryColor,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 }
            };
            CCDChart.Children.Add(rightLine);

            // 修正：低谷区域标注（使用正确的颜色API）
            var illuminatedRegion = new Rectangle()
            {
                Width = Math.Abs(rightX - leftX),
                Height = chartHeight,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)) // 半透明红色表示低谷区域
            };
            Canvas.SetLeft(illuminatedRegion, Math.Min(leftX, rightX));
            Canvas.SetTop(illuminatedRegion, CHART_MARGIN);
            CCDChart.Children.Add(illuminatedRegion);
        }

        private void UpdateStatistics()
        {
            if (_ccdData.Count == 0) return;

            var max = _ccdData.Max(d => d.Intensity);
            var min = _ccdData.Min(d => d.Intensity);
            var avg = _ccdData.Average(d => d.Intensity);

            MaxValueText.Text = max.ToString();
            MinValueText.Text = min.ToString();
            AvgValueText.Text = avg.ToString("F1");

            // 更新边界和光照信息
            BoundaryPositionText.Text = $"{_leftBoundary}-{_rightBoundary}";

            // 计算低谷区域的平均光强
            if (_rightBoundary > _leftBoundary)
            {
                var lowRegion = _ccdData.Where(d => d.PixelIndex >= _leftBoundary && d.PixelIndex <= _rightBoundary).ToList();
                if (lowRegion.Any())
                {
                    LaserRegionAvgText.Text = lowRegion.Average(d => d.Intensity).ToString("F1");
                }
            }

            // 对比度计算
            double contrast = (max - min) / (max + min) * 100;
            ContrastText.Text = contrast.ToString("F1") + "%";

            // 显示低谷长度
            IlluminatedLengthText.Text = $"{_currentIlluminatedLength:F3} mm";
        }
        #endregion

        #region 数据记录功能
        private async void RecordDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.IsNaN(AppliedForceBox.Value))
            {
                ExperimentResultText.Text = "请先输入施加力值";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                return;
            }

            if (_currentIlluminatedLength <= 0)
            {
                ExperimentResultText.Text = "请先采集CCD数据";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                return;
            }

            var measurement = new MeasurementRecord
            {
                SerialNumber = _measurementRecords.Count + 1,
                AppliedForce = AppliedForceBox.Value,
                IlluminatedLength = _currentIlluminatedLength,
                Timestamp = DateTime.Now,
                MaterialType = MaterialTypeTextBox.Text,
                // 新增：保存边界信息
                LeftBoundary = _leftBoundary,
                RightBoundary = _rightBoundary,
                PixelLength = Math.Abs(_rightBoundary - _leftBoundary),
                ActualLength = _currentIlluminatedLength
            };

            _measurementRecords.Add(measurement);
            UpdateMeasurementsList();

            // 如果是第一个数据且当前没有实验记录，创建新的实验记录
            if (_measurementRecords.Count == 1 && _currentRecord == null)
            {
                await CreateNewExperimentRecord();
            }
            else if (_currentRecord != null)
            {
                // 更新现有实验记录
                await UpdateExperimentRecord();
            }

            // 保留杨氏模量计算逻辑但不显示结果（逻辑保留供后续使用）
            CalculateYoungsModulus();

            ExperimentResultText.Text = $"已记录第{measurement.SerialNumber}组数据";
            var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
            ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
        }


        private void UpdateMeasurementsList()
        {
            MeasurementsList.ItemsSource = null;
            MeasurementsList.ItemsSource = _measurementRecords;
        }

        private async Task CreateNewExperimentRecord()
        {
            var newRecord = new ExperimentRecord
            {
                Name = $"杨氏模量实验_{DateTime.Now:yyyyMMdd_HHmmss}",
                DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Description = $"材料：{MaterialTypeTextBox.Text}，数据点：{_measurementRecords.Count}",
                MaterialType = MaterialTypeTextBox.Text,
                MeasurementCount = _measurementRecords.Count,
                MeasurementData = JsonSerializer.Serialize(_measurementRecords),
                YoungsModulus = null
            };

            _currentRecord = newRecord;

            // 保存到首页实验记录
            await SaveExperimentRecord(newRecord);
        }

        private async Task UpdateExperimentRecord()
        {
            if (_currentRecord == null) return;

            _currentRecord.Description = $"材料：{MaterialTypeTextBox.Text}，数据点：{_measurementRecords.Count}";
            _currentRecord.MeasurementCount = _measurementRecords.Count;
            _currentRecord.MeasurementData = JsonSerializer.Serialize(_measurementRecords);
            _currentRecord.MaterialType = MaterialTypeTextBox.Text;

            // 计算并保存杨氏模量
            var youngsModulus = CalculateByLeastSquares();
            if (youngsModulus.HasValue)
            {
                _currentRecord.YoungsModulus = youngsModulus.Value;
            }

            await SaveExperimentRecord(_currentRecord, true);
        }

        private async Task SaveExperimentRecord(ExperimentRecord record, bool isUpdate = false)
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var recordsFile = await localFolder.TryGetItemAsync("experiment_records.json");

                List<ExperimentRecord> records = new List<ExperimentRecord>();

                if (recordsFile is StorageFile file)
                {
                    var json = await FileIO.ReadTextAsync(file);
                    var existingRecords = JsonSerializer.Deserialize<ExperimentRecord[]>(json);
                    if (existingRecords != null)
                    {
                        records.AddRange(existingRecords);
                    }
                }

                if (isUpdate)
                {
                    // 更新现有记录
                    var existingIndex = records.FindIndex(r => r.Name == record.Name);
                    if (existingIndex >= 0)
                    {
                        records[existingIndex] = record;
                    }
                    else
                    {
                        records.Add(record);
                    }
                }
                else
                {
                    records.Add(record);
                }

                var newRecordsFile = await localFolder.CreateFileAsync("experiment_records.json",
                    CreationCollisionOption.ReplaceExisting);

                var newJson = JsonSerializer.Serialize(records.ToArray(),
                    new JsonSerializerOptions { WriteIndented = true });
                await FileIO.WriteTextAsync(newRecordsFile, newJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存实验记录失败: {ex.Message}");
            }
        }

        private void ClearAllMeasurementsButton_Click(object sender, RoutedEventArgs e)
        {
            _measurementRecords.Clear();
            UpdateMeasurementsList();
            CalculateYoungsModulus();
        }

        private void DeleteMeasurementButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MeasurementRecord record)
            {
                _measurementRecords.Remove(record);

                // 重新编号
                for (int i = 0; i < _measurementRecords.Count; i++)
                {
                    _measurementRecords[i].SerialNumber = i + 1;
                }

                UpdateMeasurementsList();
                CalculateYoungsModulus();
            }
        }
        #endregion

        #region 计算和数据处理
        private void CalculateYoungsModulus()
        {
            try
            {
                if (_measurementRecords.Count < 2)
                {
                    // 不再更新UI显示，但可以在内部保留计算逻辑
                    // YoungsModulusText.Text = "需要至少2组数据";
                    // FitQualityText.Text = "--";
                    return;
                }

                // 使用最小二乘法计算杨氏模量（带异常值剔除）
                var result = CalculateByLeastSquares();

                if (result.HasValue)
                {
                    // 保留计算结果但不显示在UI上
                    var youngsModulus = result.Value;
                    var correlation = CalculateCorrelationCoefficient();

                    // 可以将结果保存到内部变量或记录中，但不显示在UI
                    // YoungsModulusText.Text = $"{result.Value:F2} GPa";
                    // FitQualityText.Text = $"R² = {correlation:F4}";
                }
                else
                {
                    // 计算失败的情况也不在UI上显示
                    // YoungsModulusText.Text = "计算失败";
                    // FitQualityText.Text = "--";
                }
            }
            catch (Exception ex)
            {
                // 错误处理，但不在UI上显示
                // YoungsModulusText.Text = "计算错误";
                // FitQualityText.Text = "--";
                Debug.WriteLine($"杨氏模量计算失败: {ex.Message}");
            }
        }

        // 异常值剔除方法（使用IQR方法）
        private List<MeasurementRecord> RemoveOutliers(List<MeasurementRecord> data)
        {
            if (data.Count < 4) return data; // 数据太少，不进行剔除

            // 按照光照长度进行异常值检测
            var lengths = data.Select(r => r.IlluminatedLength).OrderBy(x => x).ToArray();

            int q1Index = lengths.Length / 4;
            int q3Index = 3 * lengths.Length / 4;

            double q1 = lengths[q1Index];
            double q3 = lengths[q3Index];
            double iqr = q3 - q1;

            double lowerBound = q1 - 1.5 * iqr;
            double upperBound = q3 + 1.5 * iqr;

            // 过滤掉异常值
            var filteredData = data.Where(r =>
                r.IlluminatedLength >= lowerBound &&
                r.IlluminatedLength <= upperBound).ToList();

            // 如果剔除了异常值，显示提示
            if (filteredData.Count < data.Count)
            {
                var removedCount = data.Count - filteredData.Count;
                ExperimentResultText.Text = $"已剔除{removedCount}个异常数据点";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange);
            }

            return filteredData;
        }

        // 修正的杨氏模量计算方法 - 替换ExperimentPage.xaml.cs中的相关方法

        // 修正后的杨氏模量计算方法
        // 修正后的杨氏模量计算方法
        // 基于ΔF-ΔL关系的杨氏模量计算
        private double? CalculateByLeastSquares()
        {
            if (_measurementRecords.Count < 2) return null;

            var cleanedData = RemoveOutliers(_measurementRecords);
            if (cleanedData.Count < 2) return null;

            var sortedData = cleanedData.OrderBy(r => r.AppliedForce).ToList();
            var baseLength = sortedData[0].IlluminatedLength;
            var baseForce = sortedData[0].AppliedForce;

            // 钢丝参数
            var wireLengthM = 0.530; // 实际受力长度：530mm
            var wireDiameterM = _wireDiameter / 1000.0;
            var wireArea = Math.PI * Math.Pow(wireDiameterM / 2, 2);

            var deltaForces = new List<double>(); // ΔF
            var deltaLengths = new List<double>(); // ΔL

            for (int i = 0; i < sortedData.Count; i++)
            {
                // CCD测量的阴影长度变化 (mm)
                var shadowLengthChange = sortedData[i].IlluminatedLength - baseLength;

                // 转换为实际形变长度 (m)
                var actualDeformation = shadowLengthChange * _ccdToWireRatio / 1000.0;
                deltaLengths.Add(actualDeformation);

                // 力的变化 (N)
                var forceChange = (sortedData[i].AppliedForce - baseForce) * 9.8;
                deltaForces.Add(forceChange);
            }

            // 最小二乘法计算 ΔF/ΔL
            var n = deltaForces.Count;
            var sumX = deltaLengths.Sum();
            var sumY = deltaForces.Sum();
            var sumXY = deltaLengths.Zip(deltaForces, (x, y) => x * y).Sum();
            var sumX2 = deltaLengths.Sum(x => x * x);

            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10) return null;

            var slope = (n * sumXY - sumX * sumY) / denominator; // ΔF/ΔL (N/m)

            // 杨氏模量 = (ΔF/ΔL) × (L₀/S)
            var youngsModulus = slope * wireLengthM / wireArea;
            var youngsModulusGPa = youngsModulus / 1e9;

            Debug.WriteLine($"ΔF/ΔL斜率: {slope:F2} N/m");
            Debug.WriteLine($"L₀/S: {wireLengthM / wireArea:E2} m⁻¹");
            Debug.WriteLine($"杨氏模量: {youngsModulusGPa:F2} GPa");

            return Math.Abs(youngsModulusGPa);
        }

        // 添加实验参数设置方法
        private void SetExperimentParameters()
        {
            // 这些参数应该通过UI界面设置，或者从配置文件读取
            WireDiameter = 0.001; // 1mm，需要实际测量
            WireLength = 1.0;     // 1m，需要实际测量
            CcdToWireRatio = 106.80 / 317.0; // 根据你的测量结果
        }

        // 添加字段存储实验参数
        private double WireDiameter = 0.001; // 钢丝直径，米
        private double WireLength = 1.0;     // 钢丝原长，米  
        private double CcdToWireRatio = 106.80 / 317.0; // CCD测量到实际形变的比例

        private double CalculateCorrelationCoefficient()
        {
            if (_measurementRecords.Count < 2) return 0;

            var cleanedData = RemoveOutliers(_measurementRecords);
            if (cleanedData.Count < 2) return 0;

            var sortedData = cleanedData.OrderBy(r => r.AppliedForce).ToList();
            var baseLength = sortedData[0].IlluminatedLength;
            var displacements = sortedData.Select(r => r.IlluminatedLength - baseLength).ToArray();
            var forces = sortedData.Select(r => r.AppliedForce * 9.8).ToArray();

            var n = forces.Length;
            var sumX = displacements.Sum();
            var sumY = forces.Sum();
            var sumXY = displacements.Zip(forces, (x, y) => x * y).Sum();
            var sumX2 = displacements.Sum(x => x * x);
            var sumY2 = forces.Sum(y => y * y);

            var numerator = n * sumXY - sumX * sumY;
            var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));

            if (Math.Abs(denominator) < 1e-10) return 0;

            var r = numerator / denominator;
            return r * r; // 返回R²
        }
        #endregion

        #region 文件导入导出
        private async void ExportImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("CSV文件", new List<string>() { ".csv" });
                picker.SuggestedFileName = $"CCD数据_{DateTime.Now:yyyyMMdd_HHmmss}";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await ExportCCDDataToCSV(file);
                }
            }
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"导出失败: {ex.Message}";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async Task ExportCCDDataToCSV(StorageFile file)
        {
            var csvContent = "像素索引,光强度\n";
            foreach (var dataPoint in _ccdData)
            {
                csvContent += $"{dataPoint.PixelIndex},{dataPoint.Intensity}\n";
            }

            await FileIO.WriteTextAsync(file, csvContent);
            ExperimentResultText.Text = "CCD数据导出成功";
            var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
            ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
        }

        private async void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("实验报告", new List<string>() { ".txt" });
                picker.SuggestedFileName = $"杨氏模量实验报告_{DateTime.Now:yyyyMMdd_HHmmss}";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await ExportExperimentReport(file);
                }
            }
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"报告导出失败: {ex.Message}";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async Task ExportExperimentReport(StorageFile file)
        {
            var forceValue = double.IsNaN(AppliedForceBox.Value) ? 0.0 : AppliedForceBox.Value;

            var report = $@"杨氏模量测试实验报告
生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}

实验参数：
实验材料：{MaterialTypeTextBox.Text}
杨氏模量：{YoungsModulusText.Text}
拟合质量：{FitQualityText.Text}
CCD积分时间：{(IntegrationTimeComboBox.SelectedItem as ComboBoxItem)?.Content}

测量数据：
";

            foreach (var measurement in _measurementRecords)
            {
                report += $"第{measurement.SerialNumber}组：施加力 {measurement.AppliedForce:F1} KG，阴影长度 {measurement.IlluminatedLength:F3} mm\n";
            }

            report += $@"
CCD数据统计：
最大值：{MaxValueText.Text}
最小值：{MinValueText.Text}
平均值：{AvgValueText.Text}
边界位置：{BoundaryPositionText.Text}
低谷区平均：{LaserRegionAvgText.Text}
对比度：{ContrastText.Text}
阴影长度：{IlluminatedLengthText.Text}
数据点数量：{_ccdData.Count}

分析结果：
{ExperimentResultText.Text}
";

            await FileIO.WriteTextAsync(file, report);
            ExperimentResultText.Text = "实验报告导出成功";
            var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
            ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
        }

        private async void ImportDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".csv");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    await ImportCCDDataFromCSV(file);
                }
            }
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"数据导入失败: {ex.Message}";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }

        private async Task ImportCCDDataFromCSV(StorageFile file)
        {
            try
            {
                var content = await FileIO.ReadTextAsync(file);
                var lines = content.Split('\n');
                _ccdData.Clear();

                for (int i = 1; i < lines.Length; i++)
                {
                    var parts = lines[i].Trim().Split(',');
                    if (parts.Length >= 2)
                    {
                        if (int.TryParse(parts[0], out int pixelIndex) &&
                            byte.TryParse(parts[1], out byte intensity))
                        {
                            _ccdData.Add(new CCDDataPoint
                            {
                                PixelIndex = pixelIndex,
                                Intensity = intensity
                            });
                        }
                    }
                }

                AnalyzeBoundaryPoints();
                UpdateCCDChart();
                UpdateStatistics();
                ExperimentResultText.Text = "历史数据导入成功";
                var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
                ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
            }
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"数据导入失败: {ex.Message}";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        }
        #endregion

        #region 其他方法
        private async Task LoadExperimentRecord(ExperimentRecord record)
        {
            try
            {
                MaterialTypeTextBox.Text = record.MaterialType ?? "";

                // 恢复测量数据
                if (!string.IsNullOrEmpty(record.MeasurementData))
                {
                    var measurements = JsonSerializer.Deserialize<List<MeasurementRecord>>(record.MeasurementData);
                    if (measurements != null)
                    {
                        _measurementRecords = measurements;
                        UpdateMeasurementsList();
                        CalculateYoungsModulus();
                    }
                }

                // 恢复CCD数据
                if (!string.IsNullOrEmpty(record.CCDData))
                {
                    var ccdData = JsonSerializer.Deserialize<List<CCDDataPoint>>(record.CCDData);
                    if (ccdData != null)
                    {
                        _ccdData = ccdData;
                        _leftBoundary = record.LeftBoundary;
                        _rightBoundary = record.RightBoundary;
                        _currentIlluminatedLength = record.CurrentIlluminatedLength;

                        UpdateCCDChart();
                        UpdateStatistics();
                    }
                }

                // 恢复照片数据
                _capturedPhotos.Clear();
                foreach (var base64Photo in record.CapturedPhotos)
                {
                    var bitmap = await ConvertBase64ToBitmap(base64Photo);
                    if (bitmap != null)
                    {
                        _capturedPhotos.Add(bitmap);
                    }
                }
                RefreshPhotoLayout();
                if (_capturedPhotos.Count > 0)
                {
                    capturedText.Visibility = Visibility.Visible;
                }

                // 恢复实验参数设置
                if (!string.IsNullOrEmpty(record.IntegrationTime))
                {
                    for (int i = 0; i < IntegrationTimeComboBox.Items.Count; i++)
                    {
                        if ((IntegrationTimeComboBox.Items[i] as ComboBoxItem)?.Content?.ToString() == record.IntegrationTime)
                        {
                            IntegrationTimeComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }

                ExperimentResultText.Text = $"已恢复实验记录：{record.Name}";
                var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
                ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载实验记录失败: {ex.Message}");
                ExperimentResultText.Text = "实验记录加载失败";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }

            await Task.CompletedTask;
        }

        private async Task<BitmapImage?> ConvertBase64ToBitmap(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return null;

                var bytes = Convert.FromBase64String(base64);
                using var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream.GetOutputStreamAt(0));
                writer.WriteBytes(bytes);
                await writer.StoreAsync();

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }


        public void HandleEmergencyStop(bool isEmergencyStopped)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (isEmergencyStopped)
                {
                    if (_isCollecting)
                    {
                        _ = StopCCDCollection();
                    }

                    ExperimentResultText.Text = "急停状态 - 请解除急停";
                    ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
                else
                {
                    if (ExperimentResultText.Text.Contains("急停"))
                    {
                        ExperimentResultText.Text = "请施加载荷并开始CCD数据采集";
                        var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
                        ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
                    }
                }
            });
        }

        private async Task SaveCompleteExperimentData()
        {
            if (_currentRecord == null) return;

            try
            {
                // 保存CCD数据
                _currentRecord.CCDData = JsonSerializer.Serialize(_ccdData);

                // 保存边界信息
                _currentRecord.LeftBoundary = _leftBoundary;
                _currentRecord.RightBoundary = _rightBoundary;

                // 保存照片数据（转换为Base64）
                _currentRecord.CapturedPhotos.Clear();
                foreach (var photo in _capturedPhotos)
                {
                    var base64 = await ConvertBitmapToBase64(photo);
                    if (!string.IsNullOrEmpty(base64))
                    {
                        _currentRecord.CapturedPhotos.Add(base64);
                    }
                }

                // 保存实验参数
                _currentRecord.IntegrationTime = (IntegrationTimeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                _currentRecord.SerialPort = SerialPortComboBox.SelectedItem?.ToString() ?? "";
                _currentRecord.CameraDevice = (CameraSelectionComboBox.SelectedItem as CameraDevice)?.Name ?? "";

                // 保存统计数据
                if (_ccdData.Count > 0)
                {
                    _currentRecord.MaxValue = _ccdData.Max(d => d.Intensity);
                    _currentRecord.MinValue = _ccdData.Min(d => d.Intensity);
                    _currentRecord.AvgValue = _ccdData.Average(d => d.Intensity);

                    // 计算对比度
                    var max = _currentRecord.MaxValue;
                    var min = _currentRecord.MinValue;
                    _currentRecord.Contrast = (max - min) / (double)(max + min) * 100;
                }

                _currentRecord.CurrentIlluminatedLength = _currentIlluminatedLength;

                // 更新实验记录文件
                await SaveExperimentRecord(_currentRecord, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存完整实验数据失败: {ex.Message}");
            }
        }

        private async Task<string> ConvertBitmapToBase64(BitmapImage bitmap)
        {
            try
            {
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                // 这里需要从BitmapImage获取像素数据，实现可能比较复杂
                // 简化版本：返回空字符串，实际使用时需要完整实现
                return "";
            }
            catch
            {
                return "";
            }
        }
        private double _wireDiameter = 0.5; // 钢丝直径，毫米
        private double _wireLength = 530.0; // 钢丝原长，毫米
        private double _ccdToWireRatio = 0.343; // CCD测量到实际形变的比例

        // 钢丝参数设置按钮点击事件
        private async void WireParametersButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowWireParametersDialog();
        }

        // 显示钢丝参数设置对话框
        private async Task ShowWireParametersDialog()
        {
            var dialog = new ContentDialog()
            {
                Title = "钢丝参数设置",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            // 创建对话框内容
            var stackPanel = new StackPanel() { Spacing = 16 };

            // 钢丝直径设置
            var diameterPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 8 };
            diameterPanel.Children.Add(new TextBlock()
            {
                Text = "钢丝直径:",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 80
            });
            var diameterBox = new NumberBox()
            {
                Value = _wireDiameter,
                Minimum = 0.1,
                Maximum = 10.0,
                SmallChange = 0.1,
                LargeChange = 0.5,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width = 160 // 从120改为160
            };
            diameterPanel.Children.Add(diameterBox);
            diameterPanel.Children.Add(new TextBlock()
            {
                Text = "mm",
                VerticalAlignment = VerticalAlignment.Center
            });
            stackPanel.Children.Add(diameterPanel);

            // 钢丝原长设置
            var lengthPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 8 };
            lengthPanel.Children.Add(new TextBlock()
            {
                Text = "钢丝原长:",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 80
            });
            var lengthBox = new NumberBox()
            {
                Value = _wireLength,
                Minimum = 100,
                Maximum = 3000,
                SmallChange = 10,
                LargeChange = 100,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width = 160 // 从120改为160
            };
            lengthPanel.Children.Add(lengthBox);
            lengthPanel.Children.Add(new TextBlock()
            {
                Text = "mm",
                VerticalAlignment = VerticalAlignment.Center
            });
            stackPanel.Children.Add(lengthPanel);

            // CCD校准比例设置
            var ratioPanel = new StackPanel() { Spacing = 8 };
            ratioPanel.Children.Add(new TextBlock()
            {
                Text = "CCD校准比例 (实际形变/CCD测量):",
                FontWeight = FontWeights.SemiBold
            });

            var ratioSubPanel = new StackPanel() { Orientation = Orientation.Horizontal, Spacing = 8 };
            ratioSubPanel.Children.Add(new TextBlock()
            {
                Text = "比例系数:",
                VerticalAlignment = VerticalAlignment.Center,
                Width = 80
            });
            var ratioBox = new NumberBox()
            {
                Value = _ccdToWireRatio,
                Minimum = 0.01,
                Maximum = 2.0,
                SmallChange = 0.001, // 从0.01改为0.001，更精确
                LargeChange = 0.01,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                Width = 160 // 从120改为160
            };
            ratioSubPanel.Children.Add(ratioBox);
            ratioPanel.Children.Add(ratioSubPanel);

            // 添加说明文字
            var helpText = new TextBlock()
            {
                Text = "说明：比例系数 = 实际钢丝形变长度 / CCD测量的阴影长度变化\n" +
                       "当前设备默认值：0.343\n" +
                       "钢丝直径默认值：0.5mm\n" +
                       "钢丝长度默认值：530mm",
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            };
            ratioPanel.Children.Add(helpText);
            stackPanel.Children.Add(ratioPanel);

            // 添加重置按钮
            var resetButton = new Button()
            {
                Content = "恢复默认值",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0)
            };
            resetButton.Click += (s, e) =>
            {
                diameterBox.Value = 0.5;    // 默认直径0.6mm
                lengthBox.Value = 530.0;   // 默认长度170cm
                ratioBox.Value = 0.343;     // 默认比例0.303
            };
            stackPanel.Children.Add(resetButton);

            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // 用户点击确定，更新参数
                if (!double.IsNaN(diameterBox.Value))
                    _wireDiameter = diameterBox.Value;

                if (!double.IsNaN(lengthBox.Value))
                    _wireLength = lengthBox.Value;

                if (!double.IsNaN(ratioBox.Value))
                    _ccdToWireRatio = ratioBox.Value;

                // 重新计算杨氏模量
                CalculateYoungsModulus();

                // 保存参数到设置
                await SaveWireParameters();

                ExperimentResultText.Text = "钢丝参数已更新，杨氏模量已重新计算";
                var accentBrush = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as SolidColorBrush;
                ExperimentResultText.Foreground = accentBrush ?? new SolidColorBrush(Microsoft.UI.Colors.Blue);
            }
        }

        private async Task SaveWireParameters()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values["WireDiameter"] = _wireDiameter;
                localSettings.Values["WireLength"] = _wireLength;
                localSettings.Values["CcdToWireRatio"] = _ccdToWireRatio;

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存钢丝参数失败: {ex.Message}");
            }
        }

        // 加载钢丝参数
        private async Task LoadWireParameters()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;

                if (localSettings.Values.TryGetValue("WireDiameter", out var diameter))
                    _wireDiameter = (double)diameter;

                if (localSettings.Values.TryGetValue("WireLength", out var length))
                    _wireLength = (double)length;

                if (localSettings.Values.TryGetValue("CcdToWireRatio", out var ratio))
                    _ccdToWireRatio = (double)ratio;

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载钢丝参数失败: {ex.Message}");
            }
        }

        #endregion
    }

    public class CameraDevice
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public MediaFrameSourceGroup? SourceGroup { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class CCDDataPoint
    {
        [JsonPropertyName("pixelIndex")]
        public int PixelIndex { get; set; }

        [JsonPropertyName("intensity")]
        public byte Intensity { get; set; }
    }

    // 在ExperimentPage.xaml.cs文件的末尾，修改MeasurementRecord类
    public class MeasurementRecord
    {
        [JsonPropertyName("serialNumber")]
        public int SerialNumber { get; set; }

        [JsonPropertyName("appliedForce")]
        public double AppliedForce { get; set; }

        [JsonPropertyName("illuminatedLength")]
        public double IlluminatedLength { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("materialType")]
        public string MaterialType { get; set; } = "";

        // 新增字段：边界信息
        [JsonPropertyName("leftBoundary")]
        public int LeftBoundary { get; set; }

        [JsonPropertyName("rightBoundary")]
        public int RightBoundary { get; set; }

        [JsonPropertyName("pixelLength")]
        public int PixelLength { get; set; }

        [JsonPropertyName("actualLength")]
        public double ActualLength { get; set; }
    }
}

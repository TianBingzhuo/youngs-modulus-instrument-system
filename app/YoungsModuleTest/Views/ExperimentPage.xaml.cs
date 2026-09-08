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
using YoungsModuleTest.Maintenance;

namespace YoungsModuleTest.Views
{
    public sealed partial class ExperimentPage : Page
    {
        private List<CCDDataPoint> _ccdData = new List<CCDDataPoint>();
        private ExperimentRecord? _currentRecord;
        private List<MeasurementRecord> _measurementRecords = new List<MeasurementRecord>();

        // 摄像头相关字段
        private readonly SemaphoreSlim _cameraGate = new(1, 1);
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
        private bool _isActive;
        private bool _inputFault;
        private bool _isSavingRecord;
        private readonly object _portGate = new();
        private readonly SemaphoreSlim _connectionGate = new(1, 1);
        private readonly SemaphoreSlim _collectionGate = new(1, 1);
        private Task _collectionTask = Task.CompletedTask;
        private int _collectionGeneration;
        private int _frameUpdatePending;
        private byte _currentIntegrationTime = 0xB1; // 默认10μs

        // 数据处理相关
        private readonly CcdFrameBuffer _frames = new();
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
            // Device enumeration runs once in OnNavigatedTo.
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

        private bool IsSoftwareStopped => App.Current.MainWindow is MainWindow window && window.IsEmergencyStopped;

        private void UpdateAcquisitionControls()
        {
            bool available = _isActive && !IsSoftwareStopped && !_inputFault;
            StartCCDButton.IsEnabled = available && _isCCDConnected && !_isCollecting;
            StopCCDButton.IsEnabled = _isCollecting;
            IntegrationTimeComboBox.IsEnabled = available && !_isCollecting;
            RecordDataButton.IsEnabled = available && !_isSavingRecord;
            ImportDataButton.IsEnabled = available && !_isCollecting;
        }

        private void CheckEmergencyStopStatus()
        {
            UpdateAcquisitionControls();
            if (IsSoftwareStopped)
            {
                _ = StopCCDCollection();
                ExperimentResultText.Text = "软件采集已锁定；设备断电和卸载请使用实体控制";
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isActive = true;
            CheckEmergencyStopStatus();

            if (e.Parameter is ExperimentRecord record)
            {
                await LoadExperimentRecord(record);
            }

            await LoadAvailableCameras();
            await LoadAvailableSerialPorts();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            await ShutdownAsync();
        }

        public async Task ShutdownAsync()
        {
            _isActive = false;
            UpdateAcquisitionControls();
            await CleanupSerialPort();
            await CleanupCamera();
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
            await _cameraGate.WaitAsync();
            try
            {
                await CleanupCameraCore();
                if (!_isActive) return;

                if (CameraSelectionComboBox.SelectedItem is not CameraDevice selectedDevice || selectedDevice.SourceGroup == null)
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
                if (!_isActive)
                {
                    await CleanupCameraCore();
                    return;
                }

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
                await CleanupCameraCore();
            }
            finally { _cameraGate.Release(); }
        }

        private async Task CleanupCamera()
        {
            await _cameraGate.WaitAsync();
            try { await CleanupCameraCore(); }
            finally { _cameraGate.Release(); }
        }

        private async Task CleanupCameraCore()
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
            await _cameraGate.WaitAsync();
            try
            {
                if (!_isActive || mediaCapture == null || !_isCameraInitialized) return;
                using var stream = new InMemoryRandomAccessStream();
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                stream.Seek(0);
                var image = new BitmapImage { DecodePixelWidth = 1600 };
                await image.SetSourceAsync(stream);
                await SavePhotoToDevice(stream);
                if (!_isActive) return;
                _capturedPhotos.Insert(0, image);
                if (_capturedPhotos.Count > 16) _capturedPhotos.RemoveAt(16);
                RefreshPhotoLayout();
                capturedText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CameraStatusText.Text = $"照片未保存: {ex.Message}";
                CameraStatusText.Visibility = Visibility.Visible;
            }
            finally { _cameraGate.Release(); }
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
                throw new IOException("照片写入图片库失败。", ex);
            }
        }

        private void RefreshPhotoLayout()
        {
            snapshots.Children.Clear();
            if (_capturedPhotos.Count == 0) return;

            double containerWidth = snapshotsContainer.ActualWidth;
            if (containerWidth <= 0)
                containerWidth = 320;

            int photosPerRow = Math.Max(1, (int)(containerWidth / (PHOTO_WIDTH + PHOTO_MARGIN * 2)));

            StackPanel? currentRow = null;
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
            await _connectionGate.WaitAsync();
            SerialPort? opened = null;
            try
            {
                await CleanupSerialPortCore();
                if (!_isActive) return;
                opened = new SerialPort(portName, 921600, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                await Task.Run(opened.Open);
                if (!_isActive) return;
                lock (_portGate)
                {
                    _serialPort = opened;
                    _serialPort.DataReceived += SerialPort_DataReceived;
                    opened = null;
                    _inputFault = false;
                }
                _isCCDConnected = true;
                CCDStatusText.Text = "CCD串口已连接";
                CCDStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            catch (Exception ex)
            {
                await CleanupSerialPortCore();
                CCDStatusText.Text = $"串口连接失败: {ex.Message}";
                CCDStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            finally
            {
                opened?.Dispose();
                _connectionGate.Release();
                UpdateAcquisitionControls();
            }
        }

        private async Task CleanupSerialPort()
        {
            await _connectionGate.WaitAsync();
            try { await CleanupSerialPortCore(); }
            finally { _connectionGate.Release(); }
        }

        private async Task CleanupSerialPortCore()
        {
            _isCCDConnected = false;
            await StopCCDCollection();
            SerialPort? port;
            lock (_portGate)
            {
                port = _serialPort;
                _serialPort = null;
                if (port != null) port.DataReceived -= SerialPort_DataReceived;
                _frames.Clear();
            }
            // SerialPort.Dispose may wait for its callback; do not hold the receive lock.
            if (port != null)
            {
                try { await Task.Run(port.Dispose); }
                catch (Exception ex) { CCDStatusText.Text = $"串口关闭失败: {ex.Message}"; }
            }
        }

        private Task SendSerialCommand(byte command, CancellationToken token) => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            lock (_portGate)
            {
                token.ThrowIfCancellationRequested();
                if (_serialPort == null || !_serialPort.IsOpen)
                    throw new IOException("CCD serial port is not open.");
                _serialPort.Write(new byte[] { command }, 0, 1);
            }
        }, token);

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (_portGate)
            {
                if (!ReferenceEquals(sender, _serialPort) || !_isCollecting || _inputFault) return;
                int generation = _collectionGeneration;
                try
                {
                    int count = _serialPort!.BytesToRead;
                    if (count > CcdFrameBuffer.Capacity)
                        throw new InvalidDataException("CCD receive buffer overflow; reconnect before collecting.");
                    if (count == 0) return;
                    var bytes = new byte[count];
                    int read = _serialPort.Read(bytes, 0, count);
                    if (!_frames.Append(bytes.AsSpan(0, read)))
                        throw new InvalidDataException("CCD receive buffer overflow; reconnect before collecting.");
                }
                catch (Exception ex)
                {
                    _inputFault = true;
                    _cancellationTokenSource?.Cancel();
                    PostAcquisitionFault(ex.Message, generation);
                }
            }
        }

        private void PostAcquisitionFault(string message, int generation)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                await _connectionGate.WaitAsync();
                try
                {
                    // Ignore faults queued by an older acquisition.
                    if (generation != _collectionGeneration) return;
                    _inputFault = true;
                    await CleanupSerialPortCore();
                    CCDStatusText.Text = $"采集已停止，请重新连接: {message}";
                    CCDStatusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }
                finally
                {
                    _connectionGate.Release();
                    UpdateAcquisitionControls();
                }
            });
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

        private void IntegrationTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntegrationTimeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag &&
                byte.TryParse(tag.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out byte command))
            {
                // Applied at the next explicit start, never while software-locked.
                _currentIntegrationTime = command;
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
            await _collectionGate.WaitAsync();
            try
            {
                if (!_isActive || IsSoftwareStopped || _inputFault || !_isCCDConnected || _isCollecting) return;
                lock (_portGate)
                {
                    if (_serialPort == null || !_serialPort.IsOpen) throw new IOException("CCD serial port is not open.");
                    _serialPort.DiscardInBuffer();
                    _frames.Clear();
                    _isCollecting = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    int generation = Interlocked.Increment(ref _collectionGeneration);
                    var token = _cancellationTokenSource.Token;
                    byte integrationTime = _currentIntegrationTime;
                    _collectionTask = Task.Run(() => ProcessCCDDataLoop(token, generation, integrationTime));
                }
                ChartPlaceholderText.Visibility = Visibility.Collapsed;
                ExperimentResultText.Text = "CCD数据采集中...";
            }
            catch (Exception ex)
            {
                _inputFault = true;
                PostAcquisitionFault(ex.Message, _collectionGeneration);
            }
            finally
            {
                _collectionGate.Release();
                UpdateAcquisitionControls();
            }
        }

        private async Task StopCCDCollection()
        {
            await _collectionGate.WaitAsync();
            try
            {
                lock (_portGate)
                {
                    _isCollecting = false;
                    Interlocked.Increment(ref _collectionGeneration);
                    _cancellationTokenSource?.Cancel();
                }
                await _collectionTask;
                lock (_portGate)
                {
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                    _frames.Clear();
                }
            }
            finally
            {
                _collectionGate.Release();
                UpdateAcquisitionControls();
            }
        }

        private async Task ProcessCCDDataLoop(CancellationToken token, int generation, byte integrationTime)
        {
            try
            {
                await SendSerialCommand(integrationTime, token);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    await SendSerialCommand(0xA2, token);
                    await Task.Delay(100, token);
                    ProcessBufferedData(generation);
                    await Task.Delay(200, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested) PostAcquisitionFault(ex.Message, generation);
            }
        }

        private void ProcessBufferedData(int generation)
        {
            var frame = _frames.TakeFrame();
            if (frame == null || Interlocked.CompareExchange(ref _frameUpdatePending, 1, 0) != 0) return;
            if (!DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (!_isActive || IsSoftwareStopped || !_isCollecting || generation != _collectionGeneration) return;
                    _ccdData = frame.Select((value, index) => new CCDDataPoint { PixelIndex = index, Intensity = value }).ToList();
                    AnalyzeBoundaryPoints();
                    UpdateCCDChart();
                    UpdateStatistics();
                }
                finally { Interlocked.Exchange(ref _frameUpdatePending, 0); }
            })) Interlocked.Exchange(ref _frameUpdatePending, 0);
        }

        // Original low-valley rule, retained for comparison with the competition software.
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

// 第四步：确定阈值为 secondAverage + 5
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
            double contrast = max + min == 0 ? 0 : (double)(max - min) / (max + min) * 100;
            ContrastText.Text = contrast.ToString("F1") + "%";

            // 显示低谷长度
            IlluminatedLengthText.Text = $"{_currentIlluminatedLength:F3} mm";
        }
        #endregion

        #region 数据记录功能
        private async void RecordDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isActive || IsSoftwareStopped || _inputFault || _isSavingRecord) return;

            if (!double.IsFinite(AppliedForceBox.Value) || AppliedForceBox.Value < 0)
            {
                ExperimentResultText.Text = "请先输入施加力值";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                return;
            }

            if (!double.IsFinite(_currentIlluminatedLength) || _currentIlluminatedLength <= 0)
            {
                ExperimentResultText.Text = "请先采集CCD数据";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                return;
            }

            if (_ccdData.Count != CcdFrameBuffer.FrameSize ||
                _ccdData.Max(p => p.Intensity) == _ccdData.Min(p => p.Intensity))
            {
                ExperimentResultText.Text = "当前波形没有可辨识的明暗变化，不能记录为有效测量。";
                return;
            }
            _isSavingRecord = true;
            UpdateAcquisitionControls();
            try
            {
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
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"记录未保存，当前数据仍在内存中: {ex.Message}";
                ExperimentResultText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            finally
            {
                _isSavingRecord = false;
                UpdateAcquisitionControls();
            }
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
Name = $"杨氏模量实验_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}",
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
            _currentRecord.YoungsModulus = youngsModulus.HasValue && double.IsFinite(youngsModulus.Value)
                ? youngsModulus.Value : null;

            await SaveExperimentRecord(_currentRecord, true);
        }

        private async Task SaveExperimentRecord(ExperimentRecord record, bool isUpdate = false)
        {
            await RecordFile.Gate.WaitAsync();
            try
            {
                string path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "experiment_records.json");
                string? json = await RecordFile.ReadAsync(path);
                var records = json == null ? new List<ExperimentRecord>() :
                    JsonSerializer.Deserialize<List<ExperimentRecord>>(json)
                        ?? throw new InvalidDataException("Experiment records must be a JSON array.");
                if (records.Any(r => r == null)) throw new InvalidDataException("Null experiment record.");
                int index = records.FindIndex(r => r.Name == record.Name);
                if (index >= 0) records[index] = record;
                else records.Add(record);
                await RecordFile.WriteAsync(path, JsonSerializer.Serialize(records,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            finally { RecordFile.Gate.Release(); }
        }

        private async void ClearAllMeasurementsButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteMeasurement(null);
        }

        private async void DeleteMeasurementButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MeasurementRecord record)
                await DeleteMeasurement(record);
        }

        private async Task DeleteMeasurement(MeasurementRecord? record)
        {
            if (_isSavingRecord) return;
            _isSavingRecord = true;
            UpdateAcquisitionControls();
            try
            {
                if (record == null) _measurementRecords.Clear();
                else _measurementRecords.Remove(record);
                for (int i = 0; i < _measurementRecords.Count; i++) _measurementRecords[i].SerialNumber = i + 1;
                UpdateMeasurementsList();
                CalculateYoungsModulus();
                await UpdateExperimentRecord();
                ExperimentResultText.Text = "测量记录已更新";
            }
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"修改未保存，当前数据仍在内存中: {ex.Message}";
            }
            finally
            {
                _isSavingRecord = false;
                UpdateAcquisitionControls();
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

        // Original least-squares rule for the change in load versus estimated wire elongation.
        private double? CalculateByLeastSquares()
        {
            if (_measurementRecords.Count < 2) return null;

            var cleanedData = RemoveOutliers(_measurementRecords);
            if (cleanedData.Count < 2) return null;

            var sortedData = cleanedData.OrderBy(r => r.AppliedForce).ToList();
            var baseLength = sortedData[0].IlluminatedLength;
            var baseForce = sortedData[0].AppliedForce;

            // 钢丝参数
            var wireLengthM = _wireLength / 1000.0; // Configured length: mm to m
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
            if (!_isActive || IsSoftwareStopped || _isCollecting || _inputFault)
                throw new InvalidOperationException("请先停止采集并解除软件锁定，再导入CCD CSV。");
            using var stream = await file.OpenStreamForReadAsync();
            using var buffer = new MemoryStream();
            var bytes = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(bytes.AsMemory())) != 0)
            {
                if (buffer.Length + read > CcdCsv.MaxFileBytes)
                    throw new InvalidDataException("CCD CSV不能超过128 KiB。");
                await buffer.WriteAsync(bytes.AsMemory(0, read));
            }
            var frame = CcdCsv.Parse(new System.Text.UTF8Encoding(false, true).GetString(buffer.ToArray()));
            if (!_isActive || IsSoftwareStopped || _isCollecting || _inputFault) return;
            // Replace the displayed frame only after the entire CSV has passed validation.
            _ccdData = frame.Select((value, index) => new CCDDataPoint { PixelIndex = index, Intensity = value }).ToList();
            AnalyzeBoundaryPoints();
            UpdateCCDChart();
            UpdateStatistics();
            ChartPlaceholderText.Visibility = Visibility.Collapsed;
            ExperimentResultText.Text = "已导入CCD CSV（离线数据）";
        }
        #endregion

        #region 其他方法
        private async Task LoadExperimentRecord(ExperimentRecord record)
        {
            try
            {
                var measurements = string.IsNullOrEmpty(record.MeasurementData)
                    ? new List<MeasurementRecord>()
                    : JsonSerializer.Deserialize<List<MeasurementRecord>>(record.MeasurementData)
                        ?? throw new InvalidDataException("Measurement data must be an array.");
                if (measurements.Count > 10000 || measurements.Any(m => m == null ||
                    !double.IsFinite(m.AppliedForce) || m.AppliedForce < 0 ||
                    !double.IsFinite(m.IlluminatedLength) || m.IlluminatedLength <= 0))
                    throw new InvalidDataException("Invalid measurement record.");

                var frame = string.IsNullOrEmpty(record.CCDData)
                    ? new List<CCDDataPoint>()
                    : JsonSerializer.Deserialize<List<CCDDataPoint>>(record.CCDData)
                        ?? throw new InvalidDataException("CCD data must be an array.");
                if (frame.Count != 0 && (frame.Count != CcdFrameBuffer.FrameSize ||
                    frame.Where((p, i) => p == null || p.PixelIndex != i).Any()))
                    throw new InvalidDataException("CCD record must contain one ordered 3648-sample frame.");

                var photos = new List<BitmapImage>();
                foreach (var encoded in (record.CapturedPhotos ?? new List<string>()).Take(16))
                {
                    var image = await ConvertBase64ToBitmap(encoded);
                    if (image != null) photos.Add(image);
                    if (!_isActive) return;
                }
                if (!_isActive) return;

                // Apply a record only after its numeric payload has passed validation.
                _currentRecord = record;
                MaterialTypeTextBox.Text = record.MaterialType ?? "";
                _measurementRecords = measurements;
                UpdateMeasurementsList();
                _ccdData = frame;
                _currentIlluminatedLength = 0;
                if (_ccdData.Count != 0)
                {
                    AnalyzeBoundaryPoints();
                    UpdateCCDChart();
                    UpdateStatistics();
                }
                _capturedPhotos = photos;
                RefreshPhotoLayout();
                capturedText.Visibility = photos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                foreach (var item in IntegrationTimeComboBox.Items.OfType<ComboBoxItem>())
                {
                    if (item.Content?.ToString() == record.IntegrationTime)
                    {
                        IntegrationTimeComboBox.SelectedItem = item;
                        break;
                    }
                }
                CalculateYoungsModulus();
                ExperimentResultText.Text = $"已恢复实验记录：{record.Name}；请核对当前钢丝参数";
            }
            catch (Exception ex)
            {
                ExperimentResultText.Text = $"记录读取失败，当前数据未替换: {ex.Message}";
            }
        }

        private async Task<BitmapImage?> ConvertBase64ToBitmap(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64) || base64.Length > 4 * 1024 * 1024) return null;

                var bytes = Convert.FromBase64String(base64);
                using var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream.GetOutputStreamAt(0));
                writer.WriteBytes(bytes);
                await writer.StoreAsync();

                stream.Seek(0);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                if ((ulong)decoder.PixelWidth * decoder.PixelHeight > 16_000_000) return null;
                stream.Seek(0);
                var bitmap = new BitmapImage { DecodePixelWidth = 1600 };
                await bitmap.SetSourceAsync(stream);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }


        public async Task HandleEmergencyStopAsync()
        {
            UpdateAcquisitionControls();
            if (IsSoftwareStopped) await StopCCDCollection();
            ExperimentResultText.Text = IsSoftwareStopped
                ? "软件采集已锁定；设备断电和卸载请使用实体控制"
                : "软件锁定已解除；需要手动开始采集，仪器未执行硬件复位";
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
                    _currentRecord.Contrast = max + min == 0 ? 0 : (double)(max - min) / (max + min) * 100;
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
if (double.IsFinite(diameterBox.Value) && diameterBox.Value > 0)
                    _wireDiameter = diameterBox.Value;

if (double.IsFinite(lengthBox.Value) && lengthBox.Value > 0)
                    _wireLength = lengthBox.Value;

if (double.IsFinite(ratioBox.Value) && ratioBox.Value > 0)
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

if (localSettings.Values.TryGetValue("WireDiameter", out var diameter) && diameter is double d && double.IsFinite(d) && d > 0)
                    _wireDiameter = d;

if (localSettings.Values.TryGetValue("WireLength", out var length) && length is double l && double.IsFinite(l) && l > 0)
                    _wireLength = l;

if (localSettings.Values.TryGetValue("CcdToWireRatio", out var ratio) && ratio is double r && double.IsFinite(r) && r > 0)
                    _ccdToWireRatio = r;

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

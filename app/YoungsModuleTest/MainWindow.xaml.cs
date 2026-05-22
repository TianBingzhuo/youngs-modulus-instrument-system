using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using YoungsModuleTest.Views;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.


namespace YoungsModuleTest
{
    public sealed partial class MainWindow : Window
    {
        private bool isEmergencyStopped = false;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            // 默认导航到首页
            ContentFrame.Navigate(typeof(HomePage));
            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                string tag = selectedItem.Tag.ToString();
                Type pageType = tag switch
                {
                    "Home" => typeof(HomePage),
                    "Theory" => typeof(TheoryPage),
                    "Experiment" => typeof(ExperimentPage),
                    "Calibration" => typeof(CalibrationPage),
                    "SystemCheck" => typeof(SystemCheckPage),
                    "Settings" => typeof(SettingsPage),
                    _ => typeof(HomePage)
                };

                ContentFrame.Navigate(pageType);
            }
        }

        private void EmergencyStopButton_Click(object sender, RoutedEventArgs e)
        {
            isEmergencyStopped = !isEmergencyStopped;

            if (isEmergencyStopped)
            {
                // 强制停机状态
                PowerIcon.Foreground = new SolidColorBrush(Colors.White);
                EmergencyStopButton.Background = new SolidColorBrush(Colors.Red);
                EmergencyStopButton.BorderBrush = new SolidColorBrush(Colors.DarkRed);

                // 实现程序停止接口
                StopAllOperations();

                // 通知实验页面处理急停状态
                NotifyExperimentPageEmergencyStop(true);
            }
            else
            {
                // 恢复正常状态
                var systemBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
                PowerIcon.Foreground = systemBrush ?? new SolidColorBrush(Colors.Black);
                EmergencyStopButton.Background = new SolidColorBrush(Colors.Transparent);

                var borderBrush = Application.Current.Resources["SystemControlForegroundBaseMediumLowBrush"] as SolidColorBrush;
                EmergencyStopButton.BorderBrush = borderBrush ?? new SolidColorBrush(Colors.Gray);

                // 实现仪器复位接口
                ResetInstrument();

                // 通知实验页面处理急停状态
                NotifyExperimentPageEmergencyStop(false);
            }
        }

        private void StopAllOperations()
        {
            // TODO: 实现停止所有正在运行的程序的接口
            // 例如：停止摄像头、停止数据采集等
        }

        private void ResetInstrument()
        {
            // TODO: 实现仪器复位接口
            // 例如：重新初始化传感器、重置摄像头等
        }

        private void NotifyExperimentPageEmergencyStop(bool isEmergencyStopped)
        {
            // 如果当前页面是实验页面，直接通知
            if (ContentFrame.Content is ExperimentPage experimentPage)
            {
                experimentPage.HandleEmergencyStop(isEmergencyStopped);
            }

            // 注意：当用户切换到其他页面再回到实验页面时，
            // 实验页面会在OnNavigatedTo中检查急停状态
        }

        public bool IsEmergencyStopped => isEmergencyStopped;
    }
}


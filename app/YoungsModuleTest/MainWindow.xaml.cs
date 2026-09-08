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
        private bool _closing;
        private bool _readyToClose;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            // 默认导航到首页
            ContentFrame.Navigate(typeof(HomePage));
            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];
            AppWindow.Closing += async (_, args) =>
            {
                if (_readyToClose) return;
                args.Cancel = true;
                if (_closing) return;
                _closing = true;
                MainNavigationView.IsEnabled = false;
                EmergencyStopButton.IsEnabled = false;
                try
                {
                    if (ContentFrame.Content is ExperimentPage page) await page.ShutdownAsync();
                }
                finally
                {
                    _readyToClose = true;
                    Close();
                }
            };
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem != null)
            {
                string tag = selectedItem.Tag?.ToString() ?? "Home";
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

                if (ContentFrame.CurrentSourcePageType != pageType) ContentFrame.Navigate(pageType);
            }
        }

        private async void EmergencyStopButton_Click(object sender, RoutedEventArgs e)
        {
            EmergencyStopButton.IsEnabled = false;
            isEmergencyStopped = !isEmergencyStopped;
            try
            {
                PowerIcon.Foreground = new SolidColorBrush(isEmergencyStopped ? Colors.White : Colors.Black);
                EmergencyStopButton.Background = new SolidColorBrush(isEmergencyStopped ? Colors.Red : Colors.Transparent);
                ToolTipService.SetToolTip(EmergencyStopButton, isEmergencyStopped
                    ? "解除软件采集锁定（不执行硬件复位）"
                    : "停止并锁定软件采集（不替代实体急停）");
                if (isEmergencyStopped) StopAllOperations();
                else ResetInstrument();
                if (ContentFrame.Content is ExperimentPage page) await page.HandleEmergencyStopAsync();
            }
            finally { EmergencyStopButton.IsEnabled = !_closing; }
        }

        private void StopAllOperations()
        {
            // Reserved for a verified hardware stop protocol. No actuator command is implemented.
        }

        private void ResetInstrument()
        {
            // Reserved for a verified hardware reset protocol. Unlocking does not reset hardware.
        }

        public bool IsEmergencyStopped => isEmergencyStopped;
    }
}


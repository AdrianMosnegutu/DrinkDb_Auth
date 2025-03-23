using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Windows;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrinkDb_Auth
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private AppWindow m_appWindow;

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "DrinkDb - Sign In";

            // Get AppWindow
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            m_appWindow = AppWindow.GetFromWindowId(wndId);

            // Set window to full screen
            var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var size = new SizeInt32
                {
                    Width = displayArea.WorkArea.Width,
                    Height = displayArea.WorkArea.Height
                };
                m_appWindow.Resize(size);
                m_appWindow.Move(new PointInt32(0, 0));
            }
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            // Basic validation
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                // Navigate to success page
                MainFrame.Navigate(typeof(SuccessPage));
            }
            else
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Validation Error",
                    Content = "Please enter both username and password.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };

                _ = errorDialog.ShowAsync();
            }
        }
    }
}

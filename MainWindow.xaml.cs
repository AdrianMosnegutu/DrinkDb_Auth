using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System;
using System.Threading.Tasks;
using DrinkDb_Auth.OAuthProviders;

namespace DrinkDb_Auth
{
    public sealed partial class MainWindow : Window
    {
        private FacebookLocalOAuthServer _localServer;

        public MainWindow()
        {
            this.InitializeComponent();

            StartLocalServer();

            Title = "DrinkDb - Sign In";

            this.AppWindow.Resize(new SizeInt32
            {
                Width = DisplayArea.Primary.WorkArea.Width,
                Height = DisplayArea.Primary.WorkArea.Height
            });
            this.AppWindow.Move(new PointInt32(0, 0));
        }

        private void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
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

        private async void StartLocalServer()
        {
            _localServer = new FacebookLocalOAuthServer("http://localhost:8888/");
            await Task.Run(() => _localServer.StartAsync());
        }

        /// <summary>
        /// Initiates the Facebook sign-in process.
        /// 
        /// This method creates an instance of <see cref="FacebookOAuthHelper"/>, which is responsible for starting the Facebook OAuth2 
        /// authentication flow. The helper opens the default browser with the Facebook login URL (using a local redirect URI) and awaits 
        /// the access token via the local OAuth server. Once the token is received and processed (including fetching user details and 
        /// updating/inserting the user in the database), an <see cref="AuthResponse"/> is returned.
        /// 
        /// If authentication is successful (<c>AuthSuccessful == true</c>), the application navigates to the <c>SuccessPage</c>. 
        /// Otherwise, an error dialog is displayed to the user.
        /// 
        /// Any exceptions that occur during the process are caught and displayed in a ContentDialog.
        /// </summary>
        /// <param name="sender">The event sender, typically the Facebook sign-in button.</param>
        /// <param name="e">The event arguments.</param>
        private async void FacebookSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fbHelper = new FacebookOAuthHelper();
                var authResponse = await fbHelper.AuthenticateAsync();

                if (authResponse.AuthSuccessful)
                {
                    MainFrame.Navigate(typeof(SuccessPage));
                }
                else
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Facebook Authentication Failed",
                        Content = "Authentication was not successful. Please try again.",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };

                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Authentication Error",
                    Content = ex.ToString(),
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }
    }
}

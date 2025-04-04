using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using DrinkDb_Auth.OAuthProviders;
using DrinkDb_Auth.Service;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using DrinkDb_Auth.Adapter;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrinkDb_Auth
{
    public sealed partial class MainWindow : Window
    {
        private AuthenticationService _authenticationService = new();

        public MainWindow()
        {
            this.InitializeComponent();

            Title = "DrinkDb - Sign In";

            this.AppWindow.Resize(new SizeInt32
            {
                Width = DisplayArea.Primary.WorkArea.Width,
                Height = DisplayArea.Primary.WorkArea.Height
            });
            this.AppWindow.Move(new PointInt32(0, 0));
        }

        private void AuthenticationComplete(AuthResponse res)
        {
            if (res.AuthSuccessful)
            {
                MainFrame.Navigate(typeof(SuccessPage));
            }
            else
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Authentication Failed",
                    Content = "Authentication was not successful. Please try again.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
        }

        public void SignInButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            AuthResponse res = AuthenticationService.AuthWithUserPass(username, password);
            AuthenticationComplete(res);
        }

        public async void GithubSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var authResponse = await _authenticationService.AuthenticateWithGitHubAsync();
                AuthenticationComplete(authResponse);
            }
            catch (Exception ex)
            {
                await ShowError("Authentication Error", ex.ToString());
            }
        }

        public async void GoogleSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GoogleSignInButton.IsEnabled = false;
                var authResponse = await _authenticationService.AuthenticateWithGoogleAsync(this);
                AuthenticationComplete(authResponse);
            }
            catch (Exception ex)
            {
                await ShowError("Error", ex.Message);
            }
            finally
            {
                GoogleSignInButton.IsEnabled = true;
            }
        }

        public async void FacebookSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var authResponse = await _authenticationService.AuthenticateWithFacebookAsync();
                AuthenticationComplete(authResponse);
            }
            catch (Exception ex)
            {
                await ShowError("Authentication Error", ex.ToString());
            }
        }

        public async void XSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                XSignInButton.IsEnabled = false;
                var authResponse = await _authenticationService.AuthenticateWithTwitterAsync(this);
                AuthenticationComplete(authResponse);
            }
            catch (Exception ex)
            {
                await ShowError("Error", ex.Message);
            }
            finally
            {
                XSignInButton.IsEnabled = true;
            }
        }

        public async void LinkedInSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var authResponse = await _authenticationService.AuthenticateWithLinkedInAsync();
                AuthenticationComplete(authResponse);
            }
            catch (Exception ex)
            {
                await ShowError("Authentication Error", ex.ToString());
            }
        }

        private async Task ShowError(string title, string content)
        {
            var errorDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }
}

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
using DrinkDb_Auth.View;
using DrinkDb_Auth.Model;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrinkDb_Auth
{
    public sealed partial class MainWindow : Window
    {
        private AuthenticationService _authenticationService = new();
        private ITwoFactorAuthenticationService _twoFactorAuthService = new TwoFactorAuthenticationService();

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
            App.CurrentSessionId = res.SessionId;
            if (res.AuthSuccessful)
            {
                var user = _authenticationService.GetUser(res.SessionId);
                if (user.TwoFASecret != null)
                {
                    MainFrame.Navigate(typeof(TwoFactorAuthCheckView));
                }
                else
                {
                    _twoFactorAuthService.Setup2FA(user.UserId);
                }
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
                var authResponse = await _authenticationService.AuthWithOAuth(this, OAuthService.GitHub);
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
                var authResponse = await _authenticationService.AuthWithOAuth(this, OAuthService.Google);
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
                var authResponse = await _authenticationService.AuthWithOAuth(this, OAuthService.Facebook);
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
                var authResponse = await _authenticationService.AuthWithOAuth(this, OAuthService.Google);
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
                var authResponse = await _authenticationService.AuthWithOAuth(this, OAuthService.LinkedIn);
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

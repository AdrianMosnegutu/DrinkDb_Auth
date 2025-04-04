using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using DrinkDb_Auth.OAuthProviders;
using System.Threading.Tasks;
using System;
using DrinkDb_Auth.Service;
using System.Text.Json;
using System.Net.Http;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrinkDb_Auth
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private LinkedInLocalOAuthServer _linkedinLocalServer;

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "DrinkDb - Sign In";

            StartLinkedInLocalServer();

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

        private async void StartLinkedInLocalServer()
        {
            _linkedinLocalServer = new LinkedInLocalOAuthServer("http://localhost:8891/");
            await _linkedinLocalServer.StartAsync();
        }

        private async Task<string> GetLinkedInIdAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                // Use the userinfo endpoint for OpenID Connect
                var response = await client.GetAsync("https://api.linkedin.com/v2/userinfo");
                if (!response.IsSuccessStatusCode)
                    return string.Empty;
                string json = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    // Retrieve "sub" as the unique identifier
                    if (root.TryGetProperty("sub", out var idProp))
                    {
                        return idProp.GetString();
                    }
                }
            }
            return string.Empty;
        }


        private async void LinkedInSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var lnHelper = new LinkedInOAuthHelper(
                    clientId: "86j0ikb93jm78x",
                    clientSecret: "WPL_AP1.pg2Bd1XhCi821VTG.+hatTA==",
                    redirectUri: "http://localhost:8891/auth",
                    scope: "openid profile email"                     // Using OpenID Connect scopes
                );
                var authResponse = await lnHelper.AuthenticateAsync();

                if (authResponse.AuthSuccessful)
                {
                    var lnProvider = new LinkedInOAuth2Provider();
                    var finalAuth = lnProvider.Authenticate(null, authResponse.SessionToken);

                    if (finalAuth.AuthSuccessful)
                    {
                        // Retrieve LinkedIn ID from the token.
                        string lnId = await GetLinkedInIdAsync(authResponse.SessionToken);
                        var userService = new UserService();
                        var user = userService.GetUserByUsername(lnId);
                        if (user != null)
                        {
                            App.CurrentUserId = user.UserId;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("User not found for LinkedIn ID: " + lnId);
                        }
                        MainFrame.Navigate(typeof(SuccessPage));
                    }
                    else
                    {
                        await ShowError("LinkedIn Authentication Failed", "Unable to verify token in DB.");
                    }
                }
                else
                {
                    await ShowError("LinkedIn Authentication Failed", "Authentication was not successful. Please try again.");
                }
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
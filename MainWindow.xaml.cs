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
        private LinkedInLocalOAuthServer _linkedinLocalServer;
        private GitHubLocalOAuthServer _githubLocalServer;
        private FacebookLocalOAuthServer _facebookLocalServer;
        private AuthenticationService _authenticationService = new();
        public MainWindow()
        {
            this.InitializeComponent();

            Title = "DrinkDb - Sign In";

            _githubLocalServer = new GitHubLocalOAuthServer("http://localhost:8890/");
            _ = _githubLocalServer.StartAsync();

            _facebookLocalServer = new FacebookLocalOAuthServer("http://localhost:8888/");
            _ = _facebookLocalServer.StartAsync();

            _linkedinLocalServer = new LinkedInLocalOAuthServer("http://localhost:8891/");
            _ = _linkedinLocalServer.StartAsync();

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

        private void SignInButton_Click(object sender, RoutedEventArgs e)
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
                // Launch the OAuth flow and wait for the token.
                var ghHelper = new GitHubOAuthHelper();
                var authResponse = await ghHelper.AuthenticateAsync();

                if (authResponse.AuthSuccessful)
                {
                    // Verify the token
                    var provider = new GitHubOAuth2Provider();
                    if (authResponse.OAuthToken == null || authResponse.OAuthToken == string.Empty) throw new Exception("OAuth token is null.");
                    var finalAuth = provider.Authenticate(null, authResponse.OAuthToken);

                    if (finalAuth.AuthSuccessful)
                    {
                        // Retrieve the GitHub username using the token.
                        string? githubUsername = await GitHubOAuth2Provider.GetGitHubUsernameAsync(authResponse.OAuthToken);
                        if (!string.IsNullOrWhiteSpace(githubUsername))
                        {
                            // Lookup the user by the dynamic GitHub username.
                            var userService = new UserService();
                            var user = userService.GetUserByUsername(githubUsername);

                            if (user != null)
                            {
                                App.CurrentUserId = user.UserId;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("User not found for username: " + githubUsername);
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Failed to retrieve GitHub username.");
                        }

                        AuthenticationComplete(authResponse);
                    }
                    else
                    {
                        await ShowError("GitHub Authentication Failed", "Unable to verify GitHub token in DB.");
                    }
                }
                else
                {
                    await ShowError("GitHub Authentication Failed", "Authentication was not successful. Please try again.");
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

        public async void GoogleSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button while authentication is in progress
                GoogleSignInButton.IsEnabled = false;

                var googleProvider = new GoogleOAuth2Provider();
                var authResponse = await googleProvider.SignInWithGoogleAsync(this);

                if (authResponse.AuthSuccessful)
                {
                    // Navigate to success page
                    AuthenticationComplete(authResponse);
                }
                else
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Authentication Failed",
                        Content = "Google authentication was not successful. Please try again.",
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
                    Title = "Error",
                    Content = $"An error occurred: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
            finally
            {
                // Re-enable button
                GoogleSignInButton.IsEnabled = true;
            }
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
        public async void FacebookSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fbHelper = new FacebookOAuthHelper();
                var authResponse = await fbHelper.AuthenticateAsync();

                if (authResponse.AuthSuccessful)
                {
                    AuthenticationComplete(authResponse);
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


        public async void XSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the button to prevent multiple clicks.
                XSignInButton.IsEnabled = false;

                // Optionally, show a loading indicator here.

                // Create an instance of the Twitter provider and call the sign-in method.
                var twitterProvider = new TwitterOAuth2Provider();
                var authResponse = await twitterProvider.SignInWithTwitterAsync(this);

                if (authResponse.AuthSuccessful)
                {
                    AuthenticationComplete(authResponse);
                }
                else
                {
                    // Sign in failed: Show an error dialog.
                    ContentDialog failureDialog = new ContentDialog
                    {
                        Title = "Sign In Failed",
                        Content = "Unable to sign in with X. Please try again.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot  // Set the XamlRoot
                    };
                    await failureDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                // Log the error if needed.
                System.Diagnostics.Debug.WriteLine("Error during X sign-in: " + ex.Message);

                // Display an error message to the user.
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "An error occurred during sign in: " + ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot  // Set the XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                // Re-enable the button after processing is complete.
                XSignInButton.IsEnabled = true;
            }
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
                        return idProp.GetString() ?? throw new Exception("LinkedIn ID not found in response.");
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
                    if (authResponse.OAuthToken == null || authResponse.OAuthToken == string.Empty) throw new Exception("OAuth token is null.");
                    var finalAuth = lnProvider.Authenticate(string.Empty, authResponse.OAuthToken);

                    if (finalAuth.AuthSuccessful)
                    {
                        // Retrieve LinkedIn ID from the token.
                        string lnId = await GetLinkedInIdAsync(authResponse.OAuthToken);
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


    }
}

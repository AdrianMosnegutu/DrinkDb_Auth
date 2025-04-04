using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using DrinkDb_Auth.OAuthProviders;
using DrinkDb_Auth.Service;
using System;
using System.Threading.Tasks;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrinkDb_Auth
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private GitHubLocalOAuthServer _githubLocalServer;

        public MainWindow()
        {
            this.InitializeComponent();
            Title = "DrinkDb - Sign In";

            StartGitHubLocalServer();

            this.AppWindow.Resize(new SizeInt32 {
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
    private async void StartGitHubLocalServer()
    {
        _githubLocalServer = new GitHubLocalOAuthServer("http://localhost:8890/");
        await _githubLocalServer.StartAsync();
    }

    private async void GithubSignInButton_Click(object sender, RoutedEventArgs e)
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
                var finalAuth = provider.Authenticate(null, authResponse.SessionToken);

                if (finalAuth.AuthSuccessful)
                {
                    // Retrieve the GitHub username using the token.
                    string githubUsername = await GitHubOAuth2Provider.GetGitHubUsernameAsync(authResponse.SessionToken);
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

                    MainFrame.Navigate(typeof(SuccessPage));
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

    private async void GoogleSignInButton_Click(object sender, RoutedEventArgs e)
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
                MainFrame.Navigate(typeof(SuccessPage));
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
    
    }
}

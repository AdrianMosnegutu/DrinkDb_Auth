using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using DrinkDb_Auth.OAuthProviders;
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
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "DrinkDb - Sign In";

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

        private async void XSignInButton_Click(object sender, RoutedEventArgs e)
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
                    MainFrame.Navigate(typeof(SuccessPage));
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



    }
}

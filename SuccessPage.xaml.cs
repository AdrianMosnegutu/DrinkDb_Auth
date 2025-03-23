using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DrinkDb_Auth
{
    public sealed partial class SuccessPage : Page
    {
        public SuccessPage()
        {
            this.InitializeComponent();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Navigate to the main app page
            // For now, we'll just show a message
            var dialog = new ContentDialog
            {
                Title = "Coming Soon",
                Content = "Main application is under development",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }
    }
} 
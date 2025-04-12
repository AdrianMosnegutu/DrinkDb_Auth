using DrinkDb_Auth.ViewModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace DrinkDb_Auth.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TwoFactorAuthCheckView : Page
    {
        public TwoFactorAuthCheckView(AuthenticationQRCodeAndTextBoxDigits authentificationHandler)
        {
            this.InitializeComponent();
            this.DataContext = authentificationHandler;
        }
        public void TextBox_KeyUp(object sender, KeyRoutedEventArgs @event)
        {
            AuthenticationTextBox.TextBoxKeyEvent(sender, @event, this);
        }
    }
}

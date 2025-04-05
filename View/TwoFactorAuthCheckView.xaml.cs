using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using DrinkDb_Auth.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DrinkDb_Auth.View
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TwoFactorAuthCheckView : Page
    {
        public TwoFactorAuthCheckView(TwoFactorAuthCheckViewModel twoFactorAuthCheckViewModel)
        {
            this.InitializeComponent();
            DataContext = twoFactorAuthCheckViewModel;
        }
        public void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (e.Key == Windows.System.VirtualKey.Back)
            {
                if (textBox.Text.Length == 0)
                {
                    MoveFocusToPreviousTextBox(textBox);
                }
            }
            else
            {
                if (textBox.Text.Length == 1)
                {
                    MoveFocusToNextTextBox(textBox);
                }
            }
        }

        private void MoveFocusToNextTextBox(TextBox textBox)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(textBox);
            var provider = peer.GetPattern(PatternInterface.Text) as ITextProvider;

            var options = new FindNextElementOptions
            {
                SearchRoot = this // Assuming 'this' is a loaded DependencyObject
            };

            FocusManager.TryMoveFocus(FocusNavigationDirection.Right, options);
        }

        private void MoveFocusToPreviousTextBox(TextBox textBox)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(textBox);
            var provider = peer.GetPattern(PatternInterface.Text) as ITextProvider;
            var options = new FindNextElementOptions
            {
                SearchRoot = this // Assuming 'this' is a loaded DependencyObject
            };
            FocusManager.TryMoveFocus(FocusNavigationDirection.Left, options);
        }
    }

}

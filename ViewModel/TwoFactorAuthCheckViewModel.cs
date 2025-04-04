using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QRCoder;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Service;
using Microsoft.UI.Xaml.Controls;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.ViewModel
{
    public class TwoFactorAuthCheckViewModel : INotifyPropertyChanged
    {
        private static readonly UserAdapter _userAdapter = new();
        private static readonly ITwoFactorAuthenticationService _twoFactorAuthService = new TwoFactorAuthenticationService();
        private static readonly SessionAdapter _sessionAdapter = new();
        private string _codeDigit1;
        private string _codeDigit2;
        private string _codeDigit3;
        private string _codeDigit4;
        private string _codeDigit5;
        private string _codeDigit6;

        public event EventHandler<bool>? DialogResult;
        public string CodeDigit1
        {
            get => _codeDigit1;
            set
            {
                _codeDigit1 = value;
                OnPropertyChanged();
            }
        }

        public string CodeDigit2
        {
            get => _codeDigit2;
            set
            {
                _codeDigit2 = value;
                OnPropertyChanged();
            }
        }

        public string CodeDigit3
        {
            get => _codeDigit3;
            set
            {
                _codeDigit3 = value;
                OnPropertyChanged();
            }
        }

        public string CodeDigit4
        {
            get => _codeDigit4;
            set
            {
                _codeDigit4 = value;
                OnPropertyChanged();
            }
        }

        public string CodeDigit5
        {
            get => _codeDigit5;
            set
            {
                _codeDigit5 = value;
                OnPropertyChanged();
            }
        }

        public string CodeDigit6
        {
            get => _codeDigit6;
            set
            {
                _codeDigit6 = value;
                OnPropertyChanged();
            }
        }

        public ICommand CancelCommand { get; }
        public ICommand SubmitCommand { get; }

        private readonly Guid _userId;
        public TwoFactorAuthCheckViewModel()
        {
            Session session = _sessionAdapter.GetSession(App.CurrentSessionId);
            _userId = session.userId;
            CancelCommand = new RelayCommand(Cancel);
            SubmitCommand = new RelayCommand(Submit);

            _codeDigit1 = string.Empty;
            _codeDigit2 = string.Empty;
            _codeDigit3 = string.Empty;
            _codeDigit4 = string.Empty;
            _codeDigit5 = string.Empty;
            _codeDigit6 = string.Empty;
        }

        private void Cancel()
        {
            DialogResult?.Invoke(this, false);
        }

        private void Submit()
        {
            string code = CodeDigit1 + CodeDigit2 + CodeDigit3 + CodeDigit4 + CodeDigit5 + CodeDigit6;
            // Verify the 2FA code
            if (_twoFactorAuthService.Verify2FACode(_userId, code))
            {
                DialogResult?.Invoke(this, true);
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Invalid 2FA code. Please try again.",
                    CloseButtonText = "OK"
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}


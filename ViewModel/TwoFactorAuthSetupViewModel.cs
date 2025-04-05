using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QRCoder;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Service;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Drawing;

namespace DrinkDb_Auth.ViewModel
{
    public class TwoFactorAuthSetupViewModel : INotifyPropertyChanged
    {
        private static readonly UserAdapter _userAdapter = new();
        private static readonly ITwoFactorAuthenticationService _twoFactorAuthService = new TwoFactorAuthenticationService();
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
                if (_codeDigit1 != value)
                {
                    _codeDigit1 = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CodeDigit2
        {
            get => _codeDigit2;
            set
            {
                if (_codeDigit2 != value)
                {
                    _codeDigit2 = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CodeDigit3
        {
            get => _codeDigit3;
            set
            {
                if (_codeDigit3 != value)
                {
                    _codeDigit3 = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CodeDigit4
        {
            get => _codeDigit4;
            set
            {
                if (_codeDigit4 != value)
                {
                    _codeDigit4 = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CodeDigit5
        {
            get => _codeDigit5;
            set
            {
                if (_codeDigit5 != value)
                {
                    _codeDigit5 = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CodeDigit6
        {
            get => _codeDigit6;
            set
            {
                if (_codeDigit6 != value)
                {
                    _codeDigit6 = value;
                    OnPropertyChanged();
                }
            }
        }

        private BitmapImage _qrCodeImage;
        public BitmapImage QrCodeImage
        {
            get => _qrCodeImage;
            set
            {
                _qrCodeImage = value;
                OnPropertyChanged();
            }
        }

        public TwoFactorAuthSetupViewModel(string keyCode)
        {
            QRCodeData qRCodeData = new QRCodeGenerator().CreateQrCode(keyCode, QRCodeGenerator.ECCLevel.Q);
            BitmapByteQRCode qrCode = new(qRCodeData);
            byte[] qrCodeImageBytes = qrCode.GetGraphic(20);
            Bitmap qrCodeBitmap = new Bitmap(new MemoryStream(qrCodeImageBytes));
            BitmapImage qrCodeBitmapImage = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                qrCodeBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                qrCodeBitmapImage.SetSource(stream.AsRandomAccessStream());
            }
            _qrCodeImage = qrCodeBitmapImage;

            _codeDigit1 = string.Empty;
            _codeDigit2 = string.Empty;
            _codeDigit3 = string.Empty;
            _codeDigit4 = string.Empty;
            _codeDigit5 = string.Empty;
            _codeDigit6 = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
}

}

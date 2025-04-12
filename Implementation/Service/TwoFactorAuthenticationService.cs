using System;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.View;
using DrinkDb_Auth.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OtpNet;

namespace DrinkDb_Auth.Service
{
    internal class TwoFactorAuthenticationService : ITwoFactorAuthenticationService
    {
        private static readonly UserAdapter UserDatabaseAdapter = new ();

        public async Task<bool> SetupOrVerifyTwoFactor(Window window, Guid userId, bool isFirstTimeSetup)
        {
            User? currentUser = UserDatabaseAdapter.GetUserById(userId) ?? throw new ArgumentException("User not found.");

            byte[] twoFactorSecret;
            TaskCompletionSource<bool> authentificationTask = new TaskCompletionSource<bool>();
            AuthentificationQRCodeAndTextBoxDigits authentificationHandler;
            RelayCommand submitRellayCommand;
            ContentDialog authentificationSubWindow;
            switch (isFirstTimeSetup)
            {
                case true:
                    int keyLength = 42;
                    twoFactorSecret = OtpNet.KeyGeneration.GenerateRandomKey(keyLength) ?? throw new InvalidOperationException("Failed to generate 2FA secret.");
                    currentUser.TwoFASecret = Convert.ToBase64String(twoFactorSecret);
                    string? uniformResourceIdentifier = new OtpUri(OtpType.Totp, twoFactorSecret, currentUser.Username, "DrinkDB").ToString();
                    authentificationHandler = new AuthentificationQRCodeAndTextBoxDigits(uniformResourceIdentifier);
                    TwoFactorAuthSetupView twoFactorAuthSetupView = new TwoFactorAuthSetupView(authentificationHandler);
                    submitRellayCommand = this.CreateRelay(authentificationHandler, currentUser, twoFactorSecret, authentificationTask, isFirstTimeSetup);
                    authentificationSubWindow = this.CreateAuthentificationSubWindow(window, twoFactorAuthSetupView, submitRellayCommand);
                    break;
                case false:
                    twoFactorSecret = Convert.FromBase64String(currentUser.TwoFASecret ?? string.Empty);
                    Totp? timeBasedOneTimePassword = new OtpNet.Totp(twoFactorSecret);
                    authentificationHandler = new AuthentificationQRCodeAndTextBoxDigits();
                    TwoFactorAuthCheckView twoFactorAuthCheckView = new TwoFactorAuthCheckView(authentificationHandler);
                    submitRellayCommand = this.CreateRelay(authentificationHandler, currentUser, twoFactorSecret, authentificationTask, isFirstTimeSetup);
                    authentificationSubWindow = this.CreateAuthentificationSubWindow(window, twoFactorAuthCheckView, submitRellayCommand);
                    break;
            }
            await authentificationSubWindow.ShowAsync();
            bool authentificationResult = await authentificationTask.Task;

            TaskCompletionSource<bool> authentificationCompleteTask = new TaskCompletionSource<bool>();
            authentificationSubWindow.Hide();

            this.ShowResults(window, authentificationCompleteTask, authentificationResult);
            return await authentificationCompleteTask.Task;
        }

        private bool Verify2FAForSecret(byte[] twoFactorSecret, string token)
        {
            Totp? timeBasedOneTimePassword = new OtpNet.Totp(twoFactorSecret);

            return timeBasedOneTimePassword.VerifyTotp(token, out long _, new OtpNet.VerificationWindow(1, 1));
        }

        private ContentDialog CreateAuthentificationSubWindow(Window window, object view, RelayCommand command)
        {
            return new ContentDialog
            {
                Title = "Set up two factor auth",
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Submit",
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonCommand = command,
                XamlRoot = window.Content.XamlRoot,
                Content = view
            };
        }

        private RelayCommand CreateRelay(AuthentificationQRCodeAndTextBoxDigits authentificationHandler, User user, byte[] twoFactorSecret, TaskCompletionSource<bool> codeSetupTask, bool updateDatabase)
        {
            return new RelayCommand(() =>
            {
                string providedCode = authentificationHandler.FirstDigit
                            + authentificationHandler.SecondDigit
                            + authentificationHandler.ThirdDigit
                            + authentificationHandler.FourthDigit
                            + authentificationHandler.FifthDigit
                            + authentificationHandler.SixthDigit;
                switch (Verify2FAForSecret(twoFactorSecret, providedCode))
                {
                    case true:
                        switch (updateDatabase)
                        {
                            case true:
                                bool result = UserDatabaseAdapter.UpdateUser(user);
                                if (!result)
                                {
                                    throw new InvalidOperationException("Failed to update user with 2FA secret.");
                                }
                                break;
                            case false:
                                break;
                        }
                        codeSetupTask.SetResult(true);
                        break;
                    case false:
                        codeSetupTask.SetResult(false);
                        break;
                }
            });
        }

        private async void ShowResults(Window window, TaskCompletionSource<bool> authCompletionStatus, bool codeSetupResult)
        {
            if (codeSetupResult)
            {
                authCompletionStatus.SetResult(true);
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Invalid 2FA code. Please try again.",
                    CloseButtonText = "OK",
                    XamlRoot = window.Content.XamlRoot,
                    CloseButtonCommand = new RelayCommand(() =>
                    {
                        authCompletionStatus.SetResult(false);
                    })
                };
                await dialog.ShowAsync();
            }
        }
    }
}
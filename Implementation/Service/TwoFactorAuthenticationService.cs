using System;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.View;
using DrinkDb_Auth.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OtpNet;
using System.Windows.Input;

namespace DrinkDb_Auth.Service
{
    internal class TwoFactorAuthenticationService : ITwoFactorAuthenticationService
    {
        private static readonly UserAdapter _userAdapter = new();

        public async Task<bool> Setup2FA(Window window, Guid userId)
        {
            var user = _userAdapter.GetUserById(userId);

            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }

            // Generate a new 2FA secret
            var secret = OtpNet.KeyGeneration.GenerateRandomKey(42);
            if (secret == null)
            {
                throw new InvalidOperationException("Failed to generate 2FA secret.");
            }
            user.TwoFASecret = Convert.ToBase64String(secret);
            var uriString = new OtpUri(OtpType.Totp, secret, user.Username, "DrinkDB").ToString();
            // set client up by opening the two factor setup view
            TwoFactorAuthSetupViewModel twoFactorAuthSetupViewModel = new(uriString);

            // Watch dialogResult and return its value as this function's result
            var codeSetupTask = new TaskCompletionSource<bool>();

            TwoFactorAuthSetupView twoFactorAuthSetupView = new(twoFactorAuthSetupViewModel);

            ContentDialog setupDialog = new()
            {
                Title = "Set up two factor auth",
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Submit",
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonCommand = new RelayCommand(() =>
                {
                    string code = twoFactorAuthSetupViewModel.CodeDigit1
                                + twoFactorAuthSetupViewModel.CodeDigit2
                                + twoFactorAuthSetupViewModel.CodeDigit3
                                + twoFactorAuthSetupViewModel.CodeDigit4
                                + twoFactorAuthSetupViewModel.CodeDigit5
                                + twoFactorAuthSetupViewModel.CodeDigit6;
                    // Verify the 2FA code
                    if (Verify2FAForSecret(secret, code))
                    {
                        // Save the secret to the database
                        var result = _userAdapter.UpdateUser(user);

                        if (!result)
                        {
                            throw new InvalidOperationException("Failed to update user with 2FA secret.");
                        }
                        codeSetupTask.SetResult(true);
                    }
                    else
                    {
                        codeSetupTask.SetResult(false);
                    }
                }),
                XamlRoot = window.Content.XamlRoot,
                Content = twoFactorAuthSetupView
            };
            var authCompletionStatus = new TaskCompletionSource<bool>();

            await setupDialog.ShowAsync();
            var codeSetupResult = await codeSetupTask.Task;
            setupDialog.Hide();
            if(codeSetupResult)
            {
                authCompletionStatus.SetResult(true);
            } else
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
            return await authCompletionStatus.Task;
        }
        public async Task<bool> Verify2FAForUser(Window window, Guid userId)
        {
            var user = _userAdapter.GetUserById(userId) ?? throw new ArgumentException("User not found.");
            // Decode the 2FA secret
            var secret = Convert.FromBase64String(user.TwoFASecret);
            // Create a new OTP generator
            var totp = new OtpNet.Totp(secret);
            TwoFactorAuthCheckViewModel twoFactorAuthCheckViewModel = new();

            // Watch dialogResult and return its value as this function's result
            var codeCheckTask = new TaskCompletionSource<bool>();
            TwoFactorAuthCheckView twoFactorAuthCheckView = new(twoFactorAuthCheckViewModel);

            ContentDialog checkDialog = new()
            {
                Title = "Verify two factor auth",
                CloseButtonText = "Cancel",
                PrimaryButtonText = "Submit",
                DefaultButton = ContentDialogButton.Primary,
                PrimaryButtonCommand = new RelayCommand(() =>
                {
                    string code = twoFactorAuthCheckViewModel.CodeDigit1
                                + twoFactorAuthCheckViewModel.CodeDigit2
                                + twoFactorAuthCheckViewModel.CodeDigit3
                                + twoFactorAuthCheckViewModel.CodeDigit4
                                + twoFactorAuthCheckViewModel.CodeDigit5
                                + twoFactorAuthCheckViewModel.CodeDigit6;
                    // Verify the 2FA code
                    if (Verify2FAForSecret(secret, code))
                    {
                        codeCheckTask.SetResult(true);
                    }
                    else
                    {
                        codeCheckTask.SetResult(false);
                    }
                }),
                XamlRoot = window.Content.XamlRoot,
                Content = twoFactorAuthCheckView
            };

            var authCompletionStatus = new TaskCompletionSource<bool>();
            await checkDialog.ShowAsync();
            var codeCheckResult = await codeCheckTask.Task;
            checkDialog.Hide();
            if (codeCheckResult)
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
            return await authCompletionStatus.Task;
        }

        private bool Verify2FAForSecret(byte[] secret, string token)
        {
            // Create a new OTP generator
            var totp = new OtpNet.Totp(secret);
            // Verify the token
            return totp.VerifyTotp(token, out long _, new OtpNet.VerificationWindow(1, 1));
        }
    }

}

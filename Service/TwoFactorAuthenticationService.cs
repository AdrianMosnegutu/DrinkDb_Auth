using System;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.View;
using DrinkDb_Auth.ViewModel;

namespace DrinkDb_Auth.Service
{
    internal class TwoFactorAuthenticationService : ITwoFactorAuthenticationService
    {
        private static readonly UserAdapter _userAdapter = new();

        public async Task<bool> Setup2FA(Guid userId)
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

            // Save the secret to the database
            var result = _userAdapter.UpdateUser(user);

            if (!result)
            {
                throw new InvalidOperationException("Failed to update user with 2FA secret.");
            }

            // set client up by opening the two factor setup view
            TwoFactorAuthSetupViewModel twoFactorAuthSetupViewModel = new(user.TwoFASecret, userId);

            // Watch dialogResult and return its value as this function's result
            var tcs = new TaskCompletionSource<bool>();
            twoFactorAuthSetupViewModel.DialogResult += (sender, dialogResult) =>
            {
                tcs.SetResult(dialogResult);
            };

            TwoFactorAuthSetupView twoFactorAuthSetupView = new(twoFactorAuthSetupViewModel);
            return await tcs.Task;
        }

        public bool Verify2FACode(Guid userId, string token)
        {
            var user = _userAdapter.GetUserById(userId);
            if (user == null)
            {
                throw new ArgumentException("User not found.");
            }
            // Decode the 2FA secret
            var secret = Convert.FromBase64String(user.TwoFASecret);
            // Create a new OTP generator
            var totp = new OtpNet.Totp(secret);
            // Verify the token
            return totp.VerifyTotp(token, out long _, new OtpNet.VerificationWindow(1, 1));
        }
    }
}

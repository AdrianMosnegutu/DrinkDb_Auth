using OtpNet;

namespace DrinkDb_Auth.Service
{
    public class Verify2FactorAuthenticationSecret : IVerify
    {
        public bool Verify2FAForSecret(byte[] twoFactorSecret, string token)
        {
            Totp? oneTimePassword = new OtpNet.Totp(twoFactorSecret);
            int previous = 1, future = 1;
            return oneTimePassword.VerifyTotp(token, out long _, new OtpNet.VerificationWindow(previous, future));
        }
    }
}

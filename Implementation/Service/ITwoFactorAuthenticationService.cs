using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DrinkDb_Auth.Service
{
    internal interface ITwoFactorAuthenticationService
    {
        Task<bool> SetupOrVerifyTwoFactor(Window window, Guid userId, bool isFirstTimeSetup);
    }
}

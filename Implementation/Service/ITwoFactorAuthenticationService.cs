using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DrinkDb_Auth.Service
{
    internal interface ITwoFactorAuthenticationService
    {
        Task<bool> Setup2FA(Window parentWindow, Guid userId);

        Task<bool> Verify2FAForUser(Window window, Guid userId);
    }
}

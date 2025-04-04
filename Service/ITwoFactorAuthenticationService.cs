using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrinkDb_Auth.Service
{
    internal interface ITwoFactorAuthenticationService
    {
        Task<bool> Setup2FA(Guid userId);

        bool Verify2FACode(Guid userId, string token);
    }
}

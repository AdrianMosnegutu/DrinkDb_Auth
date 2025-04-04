using System.Collections.Generic;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Service
{
    public abstract class AuthenticationService
    {
        public abstract string authWithUserPass(string username, string password);
        public abstract string authWithOAuth(string userId, string token);
        public abstract void logout(string sessionId);
        public abstract User getUser(string sessionId);
        public abstract void createAccount(User user);
    }
}

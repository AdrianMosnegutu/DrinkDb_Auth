using System;
using System.Collections.Generic;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Service
{
    public class AuthenticationService
    {
        public string authWithOAuth(string userId, string token)
        {
            throw new NotImplementedException("AuthWithOAuth not implemented");
        }
        public void logout(string sessionId)
        {
            throw new NotImplementedException("Logout not implemented");
        }
        public User getUser(string sessionId)
        {
            throw new NotImplementedException("GetUser not implemented");
        }
        public void createAccount(User user)
        {
            throw new NotImplementedException("CreateAccount not implemented");
        }
        public string authWithUserPass(string username, string password)
        {
            throw new NotImplementedException("AuthWithUserPass not implemented");
        }

    }
}

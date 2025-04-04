using System;
using System.Collections.Generic;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.AuthProviders;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.OAuthProviders;

namespace DrinkDb_Auth.Service
{
    public class AuthenticationService
    {
        private static readonly SessionAdapter _sessionAdapter = new();
        private static readonly UserAdapter _userAdapter = new();

        public AuthResponse authWithOAuth(string userId, string token)
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
        public AuthResponse authWithUserPass(string username, string password)
        {
            // if not existent, do account creation
            // if existent, do login
            // if password is correct, return AuthResponse
            // if password is incorrect, return AuthResponse with error
            try
            {
                if(BasicAuthenticationProvider.Authenticate(username, password))
                {
                    User user = _userAdapter.GetUserByUsername(username);
                    Session sess = _sessionAdapter.CreateSession(user.UserId);
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        NewAccount = false,
                        OAuthToken = string.Empty,
                        SessionId = sess.sessionId,
                    };
                }
                else
                {
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        NewAccount = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                    };
                }

            }
            catch (UserNotFoundException)
            {
                //create user
                User user = new()
                {
                    Username = username,
                    PasswordHash = password,
                    UserId = Guid.NewGuid()
                };
                _userAdapter.CreateUser(user);
            }
            throw new Exception("Unexpected error during authentication");

        }

    }
}

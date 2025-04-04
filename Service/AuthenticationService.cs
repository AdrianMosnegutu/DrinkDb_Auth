using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
        public void Logout()
        {
            _sessionAdapter.EndSession(App.CurrentSessionId);
            App.CurrentSessionId = Guid.Empty;
            App.CurrentUserId = Guid.Empty;
        }
        public User GetUser(string sessionId)
        {
            Session sess = _sessionAdapter.GetSession(App.CurrentUserId);
            return _userAdapter.GetUserById(sess.userId) ?? throw new UserNotFoundException("User not found");
        }
        public static AuthResponse AuthWithUserPass(string username, string password)
        {
            try
            {
                if(BasicAuthenticationProvider.Authenticate(username, password))
                {
                    User user = _userAdapter.GetUserByUsername(username) ?? throw new UserNotFoundException("User not found");
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
                    PasswordHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)).ToString() ?? throw new Exception("Hashing failed"),
                    UserId = Guid.NewGuid()
                };
                _userAdapter.CreateUser(user);
                Session sess = _sessionAdapter.CreateSession(user.UserId);
                return new AuthResponse
                {
                    AuthSuccessful = true,
                    NewAccount = true,
                    OAuthToken = string.Empty,
                    SessionId = sess.sessionId,
                };
            }
            throw new Exception("Unexpected error during authentication");

        }

    }
}

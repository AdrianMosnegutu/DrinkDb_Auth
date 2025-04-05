using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DrinkDb_Auth.Adapter;

namespace DrinkDb_Auth.AuthProviders
{

    public class UserNotFoundException : Exception
    {
        public UserNotFoundException(string message) : base(message) { }
    }
    internal class BasicAuthenticationProvider
    {
        private static readonly IUserAdapter userAdapter = new UserAdapter();

        public static bool Authenticate(string username, string password)
        {
            var user = userAdapter.GetUserByUsername(username) ?? throw new UserNotFoundException("User not found");
            byte[] passwordBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            string passwordHash = Convert.ToBase64String(passwordBytes);
            return user.PasswordHash.SequenceEqual(passwordHash);
        }
    }
}

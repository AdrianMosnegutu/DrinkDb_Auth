using System;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.Service;

namespace DrinkDb_Auth.Service
{
    public class UserService
    {
        private readonly UserAdapter _userAdapter;
        private readonly AuthenticationService _authenticationService;

        public UserService()
        {
            _userAdapter = new UserAdapter();
            _authenticationService = new AuthenticationService();
        }

        public User GetUserById(Guid userId)
        {
            return _userAdapter.GetUserById(userId) ?? throw new ArgumentException("User not found", nameof(userId));
        }

        public User GetUserByUsername(string username)
        {
            return _userAdapter.GetUserByUsername(username) ?? throw new ArgumentException("User not found", nameof(username));
        }

        public bool ValidateAction(Guid userId, string resource, string action)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentException("Role cannot be null or empty.", nameof(resource));
            }
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentException("Action cannot be null or empty.", nameof(action));
            }
            return _userAdapter.ValidateAction(userId, resource, action);
        }

        public void LogoutUser()
        {
            _authenticationService.Logout();
        }
    }
}

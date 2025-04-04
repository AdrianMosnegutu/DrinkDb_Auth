using System;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Service
{
    public class UserService
    {
        private readonly UserAdapter _userAdapter;

        public UserService()
        {
            _userAdapter = new UserAdapter();
        }

        public User GetUserById(Guid userId)
        {
            return _userAdapter.GetUserById(userId);
        }

        public User GetUserByUsername(string username)
        {
            return _userAdapter.GetUserByUsername(username);
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
    }
}

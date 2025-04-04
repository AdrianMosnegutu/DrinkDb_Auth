using System;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using Windows.System;

namespace DrinkDb_Auth.Service
{
    public class UserService
    {
        private readonly UserAdapter _userAdapter;

        public UserService()
        {
            _userAdapter = new UserAdapter();
        }

        public Users GetUserById(Guid userId)
        {
            return _userAdapter.GetUserById(userId);
        }

        public Users GetUserByUsername(string username)
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

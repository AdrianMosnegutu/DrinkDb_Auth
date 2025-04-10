using System;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.Service;
using Microsoft.UI.Xaml.Controls;

namespace DrinkDb_Auth.Service
{
    public class UserService
    {
        private readonly UserAdapter userRepository;
        private readonly AuthenticationService authenticationService;

        private const string UserNotFoundMessage = "User not found";
        private const string NoUserLoggedInMessage = "No user is currently logged in.";
        private const string NullResourceError = "Resource cannot be null or empty.";
        private const string NullActionError = "Action cannot be null or empty.";

        public UserService()
        {
            userRepository = new UserAdapter();
            authenticationService = new AuthenticationService();
        }

        public User GetUserById(Guid userId)
        {
            var user = userRepository.GetUserById(userId);
            return user ?? throw new ArgumentException(UserNotFoundMessage, nameof(userId));
        }

        public User GetUserByUsername(string username)
        {
            var user = userRepository.GetUserByUsername(username);
            return user ?? throw new ArgumentException(UserNotFoundMessage, nameof(username));
        }

        public User GetCurrentUser()
        {
            Guid currentSessionId = App.CurrentSessionId;
            if (currentSessionId == Guid.Empty)
            {
                throw new InvalidOperationException(NoUserLoggedInMessage);
            }
            return authenticationService.GetUser(currentSessionId);
        }

        public bool IsUserAuthorized(Guid userId, string resource, string action)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                throw new ArgumentException(NullResourceError, nameof(resource));
            }
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException(NullActionError, nameof(action));
            }
            return userRepository.ValidateAction(userId, resource, action);
        }

        public void LogoutCurrentUser()
        {
            authenticationService.Logout();
        }
    }
}

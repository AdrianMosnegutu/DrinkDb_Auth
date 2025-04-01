﻿using System;
using DrinkDb_Auth.DataAccess;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Service
{
    public class UserService
    {
        private readonly UserRepository _userRepository;

        public UserService()
        {
            _userRepository = new UserRepository();
        }

        public User GetUserById(Guid userId)
        {
            return _userRepository.GetUserById(userId);
        }

        public User GetUserByUsername(string username)
        {
            return _userRepository.GetUserByUsername(username);
        }

        public bool ValidateAction(Guid userId, string resource, string action)
        {
            return _userRepository.ValidateAction(userId, resource, action);
        }

        // Additional business logic can go here,
        // e.g. hashing passwords, validating user input, etc.
    }
}

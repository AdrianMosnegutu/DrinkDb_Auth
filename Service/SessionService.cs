using System;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Service
{
    public class SessionService
    {
        private readonly SessionAdapter _sessionRepository;

        public SessionService()
        {
            _sessionRepository = new SessionAdapter();
        }

        public Session CreateSession(Guid userId)
        {
            return _sessionRepository.CreateSession(userId);
        }

        public bool EndSession(Guid sessionId, Guid userId)
        {
            return _sessionRepository.EndSession(sessionId, userId);
        }

        public Session GetSession(Guid sessionId)
        {
            return _sessionRepository.GetSession(sessionId);
        }

        public bool ValidateSession(Guid sessionId)
        {
            var session = GetSession(sessionId);
            return session != null && session.IsActive;
        }

        public bool AuthorizeAction(Guid sessionId, string resource, string action)
        {
            var session = GetSession(sessionId);
            if (session == null || !session.IsActive)
            {
                return false;
            }

            var userService = new UserService();
            return userService.ValidateAction(session.userId, resource, action);
        }
    }
} 
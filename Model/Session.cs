using System;

namespace DrinkDb_Auth.Model
{
    public class Session
    {
        public Guid sessionId { get; private set; }
        public Guid userId { get; private set; }
        public bool IsActive => userId != Guid.Empty;

        private Session()
        {
            sessionId = Guid.NewGuid();
        }

        private Session(Guid sessionId, Guid userId)
        {
            this.sessionId = sessionId;
            this.userId = userId;
        }

        public static Session createSessionForUser(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            var session = new Session();
            session.userId = userId;
            return session;
        }

        public static Session createSessionWithIds(Guid sessionId, Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("Session ID cannot be empty", nameof(sessionId));
            }

            return new Session(sessionId, userId);
        }

        public void endSessionForUser(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty", nameof(userId));
            }

            if (this.userId != userId)
            {
                throw new InvalidOperationException("Session does not belong to specified user");
            }

            this.userId = Guid.Empty;
        }

        public override string ToString()
        {
            return $"Session[ID: {sessionId}, UserID: {userId}, Active: {IsActive}]";
        }

        public override bool Equals(object obj)
        {
            if (obj is Session other)
            {
                return sessionId == other.sessionId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return sessionId.GetHashCode();
        }
    }
} 
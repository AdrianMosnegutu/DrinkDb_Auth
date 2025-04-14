using DrinkDb_Auth.Model;
using System;

namespace DrinkDb_Auth.Adapter
{
    public interface ISessionAdapter
    {
        Session CreateSession(Guid userId);
        bool EndSession(Guid sessionId);
        Session GetSession(Guid sessionId);
    }
}
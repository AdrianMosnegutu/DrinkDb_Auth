using DrinkDb_Auth.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    public interface ISessionAdapter
    {
        public Session CreateSession(Guid userId);
        public bool EndSession(Guid sessionId);
        public Session GetSession(Guid sessionId);
    }
}
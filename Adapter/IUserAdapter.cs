using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    internal interface IUserAdapter
    {
        public bool ValidateAction(Guid userId, string resource, string action);
        public Users GetUserByUsername(string username);
        public Users GetUserById(Guid userId);

    }
}

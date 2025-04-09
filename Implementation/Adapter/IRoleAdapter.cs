using System.Collections.Generic;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Database
{
    public interface IRoleAdapter
    {
        void CreateResource(Roles resource);
        void DeleteResource(Roles resource);
        Roles GetResourceById(int id);
        List<Roles> GetResources();
        void UpdateResource(Roles resource);
    }
}
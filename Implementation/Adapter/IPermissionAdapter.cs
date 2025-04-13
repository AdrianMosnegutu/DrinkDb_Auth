using System.Collections.Generic;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    internal interface IPermissionAdapter
    {
        public void CreatePermission(Permission permission);
        public void UpdatePermission(Permission permission);
        public void DeletePermission(Permission permission);
        public Permission GetPermissionById(int id);
        public List<Permission> GetPermissions();
    }
}

using System;

namespace DrinkDb_Auth.Model
{
    public class Permission
    {
        public int Id { get; set; }
        public string PermissionName { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; }
    }
}

using System;
namespace DrinkDb_Auth.Models
{
    public class Permission
    {
        public Guid PermissionId { get; set; }
        public string PermissionName { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; }
    }
}


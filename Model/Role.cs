using System;
namespace DrinkDb_Auth.Models
{
    public class Role
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; }
        // Each Role has one Permission (referenced by its ID)
        public Guid PermissionId { get; set; }
    }
}


using System;
namespace DrinkDb_Auth.Model
{
    public class Role
    {
        public required Guid RoleId { get; set; }
        public required string RoleName { get; set; }
        // Each Role has one Permission (referenced by its ID)
        public required Guid PermissionId { get; set; }
    }
}


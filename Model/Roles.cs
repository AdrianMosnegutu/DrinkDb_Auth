﻿using System;

namespace DrinkDb_Auth.Model
{
    public class Roles
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
    }
}

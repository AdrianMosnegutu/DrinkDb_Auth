﻿IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Permissions' AND xtype='U')
BEGIN
    CREATE TABLE Permissions (
        permissionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        permissionName NVARCHAR(50) NOT NULL,
        resource NVARCHAR(100) NOT NULL,
        action NVARCHAR(50) NOT NULL
    );
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Roles' AND xtype='U')
BEGIN
    CREATE TABLE Roles (
        roleId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        roleName NVARCHAR(50) NOT NULL UNIQUE,
        permissionId UNIQUEIDENTIFIER NOT NULL,
        FOREIGN KEY (permissionId) REFERENCES Permissions(permissionId)
    );
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UserRoles' AND xtype='U')
BEGIN
    CREATE TABLE UserRoles (
        userId UNIQUEIDENTIFIER NOT NULL,
        roleId UNIQUEIDENTIFIER NOT NULL,
        PRIMARY KEY (userId, roleId),
    );
END

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
BEGIN
    CREATE TABLE Users (
        userId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        userName NVARCHAR(50) NOT NULL UNIQUE,
        passwordHash NVARCHAR(255) NOT NULL,
        twoFASecret NVARCHAR(255),
        roleId UNIQUEIDENTIFIER NOT NULL,
        FOREIGN KEY (roleId) REFERENCES Roles(roleId)
    );
END
-- Create the following functions in the database
-- Get user by id
CREATE OR ALTER FUNCTION fnGetUserById
(
    @userId UNIQUEIDENTIFIER
)
RETURNS TABLE
AS
RETURN
    SELECT *
    FROM Users
    WHERE userId = @userId;
GO


--Get user by username
CREATE OR ALTER FUNCTION fnGetUserByUsername
(
    @username NVARCHAR(50)
)
RETURNS TABLE
AS
RETURN
    SELECT *
    FROM Users
    WHERE userName = @username;
GO

--To validate the user
CREATE OR ALTER FUNCTION fnValidateAction
(
    @userId UNIQUEIDENTIFIER,
    @resource NVARCHAR(100),
    @action   NVARCHAR(50)
)
RETURNS BIT
AS
BEGIN
    DECLARE @result BIT = 0;

    -- If we find a matching permission, set @result = 1
    IF EXISTS (
        SELECT 1
        FROM Users u
        JOIN Roles r ON u.roleId = r.roleId
        JOIN Permissions p ON r.permissionId = p.permissionId
        WHERE u.userId = @userId
          AND p.resource = @resource
          AND p.action   = @action
    )
    BEGIN
        SET @result = 1;
    END

    RETURN @result;
END
GO

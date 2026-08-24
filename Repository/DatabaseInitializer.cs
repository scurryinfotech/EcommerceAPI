using Microsoft.Data.SqlClient;
using System.Data;

namespace EcommerceAPI.Repository
{
    public static class DatabaseInitializer
    {
        public static void InitializeDatabase(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("EcommerceDb");
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                string sqlScript = @"
                -- 1. Users Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
                BEGIN
                    CREATE TABLE Users (
                        UserId INT IDENTITY(1,1) PRIMARY KEY,
                        MobileNumber VARCHAR(20) NOT NULL UNIQUE,
                        Email VARCHAR(150) NULL,
                        FullName NVARCHAR(150) NOT NULL,
                        CompanyName NVARCHAR(150) NULL,
                        GSTIN VARCHAR(20) NULL,
                        Role VARCHAR(20) NOT NULL DEFAULT 'Customer', -- Customer, Admin, SuperAdmin
                        IsActive BIT NOT NULL DEFAULT 1,
                        IsApproved BIT NOT NULL DEFAULT 1,
                        IsMobileVerified BIT NOT NULL DEFAULT 0,
                        PasswordHash NVARCHAR(256) NULL,
                        PasswordSalt NVARCHAR(256) NULL,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_Users_Mobile ON Users(MobileNumber);
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsMobileVerified')
                BEGIN
                    ALTER TABLE Users ADD IsMobileVerified BIT NOT NULL DEFAULT 0;
                END;

                -- 2. UserProfiles Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserProfiles')
                BEGIN
                    CREATE TABLE UserProfiles (
                        ProfileId INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Users(UserId) ON DELETE CASCADE,
                        ProfilePhoto NVARCHAR(500) NULL,
                        BusinessAddress NVARCHAR(500) NULL,
                        City NVARCHAR(100) NULL,
                        State NVARCHAR(100) NULL,
                        Country NVARCHAR(100) DEFAULT 'India',
                        Pincode VARCHAR(20) NULL,
                        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                -- 3. UserAddresses Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserAddresses')
                BEGIN
                    CREATE TABLE UserAddresses (
                        AddressId INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NOT NULL FOREIGN KEY REFERENCES Users(UserId) ON DELETE CASCADE,
                        FullName NVARCHAR(150) NOT NULL,
                        Mobile VARCHAR(20) NOT NULL,
                        AddressLine1 NVARCHAR(255) NOT NULL,
                        AddressLine2 NVARCHAR(255) NULL,
                        Landmark NVARCHAR(150) NULL,
                        City NVARCHAR(100) NOT NULL,
                        State NVARCHAR(100) NOT NULL,
                        Pincode VARCHAR(20) NOT NULL,
                        Country NVARCHAR(100) NOT NULL DEFAULT 'India',
                        IsDefault BIT NOT NULL DEFAULT 0,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                -- 4. OtpRequests Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OtpRequests')
                BEGIN
                    CREATE TABLE OtpRequests (
                        OtpId INT IDENTITY(1,1) PRIMARY KEY,
                        MobileNumber VARCHAR(20) NOT NULL,
                        OtpHash NVARCHAR(256) NOT NULL,
                        ExpiryTime DATETIME NOT NULL,
                        AttemptsCount INT NOT NULL DEFAULT 0,
                        MaxAttempts INT NOT NULL DEFAULT 5,
                        ResendCooldown DATETIME NULL,
                        IsVerified BIT NOT NULL DEFAULT 0,
                        Purpose VARCHAR(50) NOT NULL DEFAULT 'Login', -- Login, Register, ProfileUpdate
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_OtpRequests_Mobile ON OtpRequests(MobileNumber, IsVerified);
                END;

                -- 5. SubCategories Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SubCategories')
                BEGIN
                    CREATE TABLE SubCategories (
                        subcategory_id INT IDENTITY(1,1) PRIMARY KEY,
                        category_id INT NOT NULL,
                        Name NVARCHAR(150) NOT NULL,
                        Slug VARCHAR(150) NULL,
                        ImagePath NVARCHAR(500) NULL,
                        Description NVARCHAR(MAX) NULL,
                        DisplayOrder INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                -- 6. Collections Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Collections')
                BEGIN
                    CREATE TABLE Collections (
                        CollectionId INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(150) NOT NULL,
                        Slug VARCHAR(150) NOT NULL UNIQUE,
                        Description NVARCHAR(MAX) NULL,
                        BannerImage NVARCHAR(500) NULL,
                        ImagePath NVARCHAR(500) NULL,
                        SeoTitle NVARCHAR(200) NULL,
                        SeoDescription NVARCHAR(500) NULL,
                        DisplayOrder INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                -- 7. CollectionProducts Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CollectionProducts')
                BEGIN
                    CREATE TABLE CollectionProducts (
                        CollectionId INT NOT NULL FOREIGN KEY REFERENCES Collections(CollectionId) ON DELETE CASCADE,
                        ProductId INT NOT NULL,
                        DisplayOrder INT NOT NULL DEFAULT 0,
                        PRIMARY KEY (CollectionId, ProductId)
                    );
                END;

                -- 8. Brands Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Brands')
                BEGIN
                    CREATE TABLE Brands (
                        BrandId INT IDENTITY(1,1) PRIMARY KEY,
                        BrandName NVARCHAR(150) NOT NULL,
                        Logo NVARCHAR(500) NULL,
                        Description NVARCHAR(MAX) NULL,
                        DisplayOrder INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1
                    );
                END;

                -- 9. Cart & CartItems Tables
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Cart')
                BEGIN
                    CREATE TABLE Cart (
                        CartId INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NULL FOREIGN KEY REFERENCES Users(UserId) ON DELETE CASCADE,
                        SessionId VARCHAR(100) NULL,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CartItems')
                BEGIN
                    CREATE TABLE CartItems (
                        CartItemId INT IDENTITY(1,1) PRIMARY KEY,
                        CartId INT NOT NULL FOREIGN KEY REFERENCES Cart(CartId) ON DELETE CASCADE,
                        ProductId INT NOT NULL,
                        VariantId INT NULL,
                        Quantity INT NOT NULL DEFAULT 1,
                        PackQuantity INT NOT NULL DEFAULT 1,
                        UnitPrice DECIMAL(18,2) NOT NULL,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;

                -- 10. OrderStatusHistory Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderStatusHistory')
                BEGIN
                    CREATE TABLE OrderStatusHistory (
                        HistoryId INT IDENTITY(1,1) PRIMARY KEY,
                        OrderId INT NOT NULL,
                        Status VARCHAR(50) NOT NULL,
                        Notes NVARCHAR(500) NULL,
                        ChangedBy NVARCHAR(100) NOT NULL DEFAULT 'System',
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_OrderStatusHistory_OrderId ON OrderStatusHistory(OrderId);
                END;

                -- 11. HomepageSections Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HomepageSections')
                BEGIN
                    CREATE TABLE HomepageSections (
                        SectionId INT IDENTITY(1,1) PRIMARY KEY,
                        SectionKey VARCHAR(100) NOT NULL UNIQUE,
                        Title NVARCHAR(200) NULL,
                        Subtitle NVARCHAR(300) NULL,
                        ContentJson NVARCHAR(MAX) NULL,
                        DisplayOrder INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1
                    );
                END;

                -- 12. Menus & MenuItems Tables
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Menus')
                BEGIN
                    CREATE TABLE Menus (
                        MenuId INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        Location VARCHAR(50) NOT NULL UNIQUE -- Header, Footer, Mobile
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MenuItems')
                BEGIN
                    CREATE TABLE MenuItems (
                        MenuItemId INT IDENTITY(1,1) PRIMARY KEY,
                        MenuId INT NOT NULL FOREIGN KEY REFERENCES Menus(MenuId) ON DELETE CASCADE,
                        ParentId INT NULL,
                        Title NVARCHAR(150) NOT NULL,
                        Url NVARCHAR(500) NOT NULL,
                        Type VARCHAR(50) NOT NULL DEFAULT 'Custom',
                        TargetId INT NULL,
                        DisplayOrder INT NOT NULL DEFAULT 0
                    );
                END;

                -- 13. AuditLogs Table
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
                BEGIN
                    CREATE TABLE AuditLogs (
                        LogId INT IDENTITY(1,1) PRIMARY KEY,
                        AdminUserId INT NULL,
                        AdminName NVARCHAR(150) NOT NULL,
                        Action NVARCHAR(150) NOT NULL,
                        TargetEntity NVARCHAR(150) NOT NULL,
                        EntityId VARCHAR(100) NULL,
                        OldValue NVARCHAR(MAX) NULL,
                        NewValue NVARCHAR(MAX) NULL,
                        IpAddress VARCHAR(50) NULL,
                        Timestamp DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END;
                ";

                using var cmd = new SqlCommand(sqlScript, conn);
                cmd.ExecuteNonQuery();

                // Seed Default SuperAdmin if not exists
                string adminSeed = @"
                IF NOT EXISTS (SELECT * FROM Users WHERE Role = 'SuperAdmin')
                BEGIN
                    INSERT INTO Users (MobileNumber, Email, FullName, CompanyName, Role, IsActive, IsApproved)
                    VALUES ('9999999999', 'admin@euphoriacreation.com', 'Super Admin', 'Euphoria Creation', 'SuperAdmin', 1, 1);
                END;
                ";
                using var adminCmd = new SqlCommand(adminSeed, conn);
                adminCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database Initializer Notice: " + ex.Message);
            }
        }
    }
}

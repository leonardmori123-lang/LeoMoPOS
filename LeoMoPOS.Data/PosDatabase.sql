CREATE TABLE Roles
(
    RoleId      INTEGER PRIMARY KEY AUTOINCREMENT,
    RoleName    NVARCHAR(50) NOT NULL UNIQUE COLLATE NOCASE,
    Description NVARCHAR(200) NULL
);

CREATE TABLE Users
(
    UserId       INTEGER PRIMARY KEY AUTOINCREMENT,
    Username     NVARCHAR(50) NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash VARCHAR(64) NOT NULL,
    Salt         VARCHAR(32) NOT NULL,
    FullName     NVARCHAR(100) NOT NULL,
    RoleId       INT NOT NULL REFERENCES Roles(RoleId),
    IsActive     BIT NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAt    DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    LastLoginAt  DATETIME NULL
);

CREATE TABLE Categories
(
    CategoryId  INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        NVARCHAR(100) NOT NULL UNIQUE COLLATE NOCASE,
    Description NVARCHAR(200) NULL
);

CREATE TABLE Products
(
    ProductId    INTEGER PRIMARY KEY AUTOINCREMENT,
    Sku          NVARCHAR(30) NOT NULL UNIQUE COLLATE NOCASE,
    Barcode      NVARCHAR(50) NULL COLLATE NOCASE,
    Name         NVARCHAR(150) NOT NULL,
    CategoryId   INT NOT NULL REFERENCES Categories(CategoryId),
    Price        DECIMAL(18,2) NOT NULL CHECK (Price >= 0),
    Cost         DECIMAL(18,2) NOT NULL CHECK (Cost >= 0),
    StockQty     INT NOT NULL DEFAULT 0 CHECK (StockQty >= 0),
    ReorderLevel INT NOT NULL DEFAULT 5 CHECK (ReorderLevel >= 0),
    Image        BLOB NULL,
    IsActive     BIT NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    CreatedAt    DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    UpdatedAt    DATETIME NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE Customers
(
    CustomerId INTEGER PRIMARY KEY AUTOINCREMENT,
    FullName   NVARCHAR(100) NOT NULL,
    Phone      NVARCHAR(30) NULL,
    Email      NVARCHAR(120) NULL,
    Address    NVARCHAR(250) NULL,
    CreatedAt  DATETIME NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE Orders
(
    OrderId        INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderNumber    NVARCHAR(30) NOT NULL UNIQUE COLLATE NOCASE,
    CustomerId     INT NULL REFERENCES Customers(CustomerId),
    UserId         INT NOT NULL REFERENCES Users(UserId),
    OrderDate      DATETIME NOT NULL DEFAULT (datetime('now', 'localtime')),
    Subtotal       DECIMAL(18,2) NOT NULL,
    TaxAmount      DECIMAL(18,2) NOT NULL,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    Total          DECIMAL(18,2) NOT NULL,
    PaymentMethod  NVARCHAR(20) NOT NULL CHECK (PaymentMethod IN ('Cash', 'Card', 'Mobile')),
    AmountPaid     DECIMAL(18,2) NOT NULL,
    ChangeDue      DECIMAL(18,2) NOT NULL,
    Status         NVARCHAR(20) NOT NULL DEFAULT 'Completed' CHECK (Status IN ('Completed', 'Voided'))
);

CREATE TABLE OrderDetails
(
    OrderDetailId INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId       INT NOT NULL REFERENCES Orders(OrderId) ON DELETE CASCADE,
    ProductId     INT NOT NULL REFERENCES Products(ProductId),
    Quantity      INT NOT NULL CHECK (Quantity > 0),
    UnitPrice     DECIMAL(18,2) NOT NULL,
    LineTotal     DECIMAL(18,2) NOT NULL
);

CREATE TABLE InventoryLog
(
    LogId     INTEGER PRIMARY KEY AUTOINCREMENT,
    ProductId INT NOT NULL REFERENCES Products(ProductId),
    ChangeQty INT NOT NULL,
    Reason    NVARCHAR(200) NOT NULL,
    UserId    INT NOT NULL REFERENCES Users(UserId),
    CreatedAt DATETIME NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE UNIQUE INDEX UX_Products_Barcode ON Products(Barcode) WHERE Barcode IS NOT NULL;
CREATE INDEX IX_Products_Name ON Products(Name);
CREATE INDEX IX_Products_CategoryId ON Products(CategoryId);
CREATE INDEX IX_Users_RoleId ON Users(RoleId);
CREATE INDEX IX_Orders_OrderDate ON Orders(OrderDate);
CREATE INDEX IX_Orders_UserId ON Orders(UserId);
CREATE INDEX IX_Orders_CustomerId ON Orders(CustomerId);
CREATE INDEX IX_OrderDetails_OrderId ON OrderDetails(OrderId);
CREATE INDEX IX_OrderDetails_ProductId ON OrderDetails(ProductId);
CREATE INDEX IX_InventoryLog_Product ON InventoryLog(ProductId, CreatedAt DESC);

INSERT INTO Roles (RoleName, Description) VALUES
    ('Admin',   'Full access including user management'),
    ('Manager', 'POS, products, inventory and analytics'),
    ('Cashier', 'Point of sale only');

INSERT INTO Users (Username, PasswordHash, Salt, FullName, RoleId) VALUES
    ('admin',   '38ED4BB0A8ABDC131DC6AC277C31EB724200AEE12C2A6FD216EB85890FA04CE9', 'A1B2C3D4E5F60718293A4B5C6D7E8F90', 'System Administrator', (SELECT RoleId FROM Roles WHERE RoleName = 'Admin')),
    ('manager', '9557537202533101B35F505B52D0900384EEFC5C43B55B8CCCC0785BF5582D0F', '0F1E2D3C4B5A69788796A5B4C3D2E1F0', 'Store Manager',        (SELECT RoleId FROM Roles WHERE RoleName = 'Manager')),
    ('cashier', '77F6BD8D7AD793FB02D55089FF2FB1FA94B5F72B6C40E949441764F21D2C36E0', '1122334455667788AABBCCDDEEFF0011', 'Front Desk Cashier',   (SELECT RoleId FROM Roles WHERE RoleName = 'Cashier'));

INSERT INTO Categories (Name, Description) VALUES
    ('Beverages',     'Drinks'),
    ('Snacks',        'Chips, biscuits and sweets'),
    ('Groceries',     'Pantry staples'),
    ('Household',     'Cleaning and home'),
    ('Personal Care', 'Hygiene and beauty');

INSERT INTO Products (Sku, Barcode, Name, CategoryId, Price, Cost, StockQty, ReorderLevel) VALUES
    ('BEV-001', '8901000000011', 'Mineral Water 500ml', (SELECT CategoryId FROM Categories WHERE Name = 'Beverages'),     1.00, 0.45, 200, 40),
    ('BEV-002', '8901000000028', 'Cola 330ml Can',      (SELECT CategoryId FROM Categories WHERE Name = 'Beverages'),     1.50, 0.80, 150, 30),
    ('BEV-003', '8901000000035', 'Orange Juice 1L',     (SELECT CategoryId FROM Categories WHERE Name = 'Beverages'),     3.20, 1.90,  60, 15),
    ('SNK-001', '8902000000014', 'Potato Chips 150g',   (SELECT CategoryId FROM Categories WHERE Name = 'Snacks'),        2.50, 1.30,  90, 20),
    ('SNK-002', '8902000000021', 'Chocolate Bar 50g',   (SELECT CategoryId FROM Categories WHERE Name = 'Snacks'),        1.80, 0.95, 120, 25),
    ('SNK-003', '8902000000038', 'Salted Peanuts 200g', (SELECT CategoryId FROM Categories WHERE Name = 'Snacks'),        2.90, 1.60,   8, 10),
    ('GRO-001', '8903000000017', 'Basmati Rice 5kg',    (SELECT CategoryId FROM Categories WHERE Name = 'Groceries'),    12.50, 8.90,  40, 10),
    ('GRO-002', '8903000000024', 'Olive Oil 500ml',     (SELECT CategoryId FROM Categories WHERE Name = 'Groceries'),     7.80, 5.20,  35,  8),
    ('GRO-003', '8903000000031', 'Spaghetti 500g',      (SELECT CategoryId FROM Categories WHERE Name = 'Groceries'),     2.10, 1.10,  70, 15),
    ('HOU-001', '8904000000010', 'Dish Soap 750ml',     (SELECT CategoryId FROM Categories WHERE Name = 'Household'),     3.60, 2.00,  45, 10),
    ('HOU-002', '8904000000027', 'Paper Towels 2-pack', (SELECT CategoryId FROM Categories WHERE Name = 'Household'),     4.20, 2.60,   5, 10),
    ('PER-001', '8905000000013', 'Shampoo 400ml',       (SELECT CategoryId FROM Categories WHERE Name = 'Personal Care'), 6.90, 4.10,  30,  8),
    ('PER-002', '8905000000020', 'Toothpaste 120g',     (SELECT CategoryId FROM Categories WHERE Name = 'Personal Care'), 3.40, 1.70,  55, 12);

INSERT INTO InventoryLog (ProductId, ChangeQty, Reason, UserId)
SELECT ProductId, StockQty, 'Initial stock', (SELECT UserId FROM Users WHERE Username = 'admin') FROM Products;

INSERT INTO Customers (FullName, Phone, Email, Address) VALUES
    ('Alice Tan',   '+65 8111 2233', 'alice@example.com', '12 Orchard Road'),
    ('Brian Lim',   '+65 8222 3344', 'brian@example.com', '45 Bukit Timah Road'),
    ('Carla Gomez', '+65 8333 4455', NULL, NULL);

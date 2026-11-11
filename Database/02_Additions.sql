USE mori;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Category_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CategoryId, Name, Description
    FROM dbo.Categories
    ORDER BY Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_Search
    @Search NVARCHAR(200) = NULL,
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ProductId, p.Sku, p.Barcode, p.Name, p.CategoryId, c.Name AS CategoryName,
           p.Price, p.Cost, p.StockQty, p.ReorderLevel, p.IsActive
    FROM dbo.Products p
    JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    WHERE (@IncludeInactive = 1 OR p.IsActive = 1)
      AND (@Search IS NULL OR @Search = N''
           OR p.Sku LIKE N'%' + @Search + N'%'
           OR p.Barcode LIKE N'%' + @Search + N'%'
           OR p.Name LIKE N'%' + @Search + N'%')
    ORDER BY p.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_GetById
    @ProductId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ProductId, p.Sku, p.Barcode, p.Name, p.CategoryId, c.Name AS CategoryName,
           p.Price, p.Cost, p.StockQty, p.ReorderLevel, p.Image, p.IsActive
    FROM dbo.Products p
    JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
    WHERE p.ProductId = @ProductId;
END
GO

USE PosDB;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Order_GetById
    @OrderId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OrderId, OrderNumber, CustomerId, UserId, OrderDate, Subtotal, TaxAmount, DiscountAmount,
           Total, PaymentMethod, AmountPaid, ChangeDue, Status
    FROM dbo.Orders
    WHERE OrderId = @OrderId;

    SELECT d.ProductId, p.Sku, p.Name AS ProductName, d.Quantity, d.UnitPrice, d.LineTotal
    FROM dbo.OrderDetails d
    JOIN dbo.Products p ON p.ProductId = d.ProductId
    WHERE d.OrderId = @OrderId
    ORDER BY d.OrderDetailId;
END
GO

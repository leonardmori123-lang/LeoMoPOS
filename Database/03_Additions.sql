/* 03_Additions.sql
   Reads a saved order back (header and lines) so the app can show its order number and totals.
   The teacher's 01 script stays untouched. Safe to run more than once. */
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

    /* Result 1: the order header */
    SELECT OrderId, OrderNumber, CustomerId, UserId, OrderDate, Subtotal, TaxAmount, DiscountAmount,
           Total, PaymentMethod, AmountPaid, ChangeDue, Status
    FROM dbo.Orders
    WHERE OrderId = @OrderId;

    /* Result 2: the order lines */
    SELECT d.ProductId, p.Sku, p.Name AS ProductName, d.Quantity, d.UnitPrice, d.LineTotal
    FROM dbo.OrderDetails d
    JOIN dbo.Products p ON p.ProductId = d.ProductId
    WHERE d.OrderId = @OrderId
    ORDER BY d.OrderDetailId;
END
GO

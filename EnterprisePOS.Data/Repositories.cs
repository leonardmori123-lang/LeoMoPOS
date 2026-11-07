using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using EnterprisePOS.Domain;

namespace EnterprisePOS.Data
{
    public sealed class CategoryRepository
    {
        public List<Category> List()
        {
            var list = new List<Category>();

            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Category_List", connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new Category
                        {
                            Id = (int)reader["CategoryId"],
                            Name = (string)reader["Name"],
                            Description = reader["Description"] as string
                        });
                    }
                }
            }

            return list;
        }
    }

    public sealed class OrderRepository
    {
        public int Create(Order order, decimal taxRatePercent)
        {
            var xml = new StringBuilder("<Items>");
            foreach (var line in order.Lines)
            {
                xml.Append("<Item ProductId=\"")
                   .Append(line.ProductId.ToString(CultureInfo.InvariantCulture))
                   .Append("\" Quantity=\"")
                   .Append(line.Quantity.ToString(CultureInfo.InvariantCulture))
                   .Append("\"/>");
            }
            xml.Append("</Items>");

            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_Order_Create", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UserId", order.UserId);
                    command.Parameters.AddWithValue("@CustomerId", (object)order.CustomerId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@PaymentMethod", order.PaymentMethod);
                    command.Parameters.Add(DecimalParam("@DiscountAmount", order.DiscountAmount, 18, 2));
                    command.Parameters.Add(DecimalParam("@TaxRate", taxRatePercent, 5, 2));
                    command.Parameters.Add(DecimalParam("@AmountPaid", order.AmountPaid, 18, 2));
                    command.Parameters.Add(new SqlParameter("@ItemsXml", SqlDbType.Xml) { Value = xml.ToString() });
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number >= 50000) throw new InvalidOperationException(ex.Message);
                throw;
            }
        }

        public Order GetById(int orderId)
        {
            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Order_GetById", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@OrderId", orderId);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    var order = new Order
                    {
                        Id = (int)reader["OrderId"],
                        OrderNumber = (string)reader["OrderNumber"],
                        CustomerId = reader["CustomerId"] as int?,
                        UserId = (int)reader["UserId"],
                        OrderDate = (DateTime)reader["OrderDate"],
                        Subtotal = (decimal)reader["Subtotal"],
                        TaxAmount = (decimal)reader["TaxAmount"],
                        DiscountAmount = (decimal)reader["DiscountAmount"],
                        Total = (decimal)reader["Total"],
                        PaymentMethod = (string)reader["PaymentMethod"],
                        AmountPaid = (decimal)reader["AmountPaid"],
                        ChangeDue = (decimal)reader["ChangeDue"],
                        Status = (string)reader["Status"]
                    };

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            order.Lines.Add(new CartLine
                            {
                                ProductId = (int)reader["ProductId"],
                                Sku = (string)reader["Sku"],
                                ProductName = (string)reader["ProductName"],
                                Quantity = (int)reader["Quantity"],
                                UnitPrice = (decimal)reader["UnitPrice"]
                            });
                        }
                    }

                    return order;
                }
            }
        }

        private static SqlParameter DecimalParam(string name, decimal value, byte precision, byte scale)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = precision, Scale = scale, Value = value };
        }
    }

    public sealed class ReportRepository
    {
        public List<DailySales> GetDailySales(DateTime start, DateTime end)
        {
            var list = new List<DailySales>();
            using (var connection = Db.Open())
            using (var command = RangeCommand(connection, "dbo.usp_Report_DailySales", start, end))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new DailySales
                    {
                        SaleDate = (DateTime)reader["SaleDate"],
                        OrderCount = Convert.ToInt32(reader["OrderCount"]),
                        Revenue = Convert.ToDecimal(reader["Revenue"])
                    });
                }
            }
            return list;
        }

        public List<TopProduct> GetTopProducts(DateTime start, DateTime end, int top)
        {
            var list = new List<TopProduct>();
            using (var connection = Db.Open())
            using (var command = RangeCommand(connection, "dbo.usp_Report_TopProducts", start, end))
            {
                command.Parameters.AddWithValue("@Top", top);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new TopProduct
                        {
                            ProductName = (string)reader["ProductName"],
                            QtySold = Convert.ToInt32(reader["QtySold"]),
                            Revenue = Convert.ToDecimal(reader["Revenue"])
                        });
                    }
                }
            }
            return list;
        }

        public List<CategorySales> GetSalesByCategory(DateTime start, DateTime end)
        {
            var list = new List<CategorySales>();
            using (var connection = Db.Open())
            using (var command = RangeCommand(connection, "dbo.usp_Report_SalesByCategory", start, end))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new CategorySales
                    {
                        CategoryName = (string)reader["CategoryName"],
                        Revenue = Convert.ToDecimal(reader["Revenue"])
                    });
                }
            }
            return list;
        }

        public List<LowStockItem> GetLowStock()
        {
            var list = new List<LowStockItem>();
            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Report_LowStock", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new LowStockItem
                        {
                            ProductId = Convert.ToInt32(reader["ProductId"]),
                            Sku = (string)reader["Sku"],
                            Name = (string)reader["Name"],
                            StockQty = Convert.ToInt32(reader["StockQty"]),
                            ReorderLevel = Convert.ToInt32(reader["ReorderLevel"])
                        });
                    }
                }
            }
            return list;
        }

        public DashboardStats GetDashboard()
        {
            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Report_Dashboard", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return new DashboardStats();

                    return new DashboardStats
                    {
                        TodaySales = Convert.ToDecimal(reader["TodaySales"]),
                        TodayOrders = Convert.ToInt32(reader["TodayOrders"]),
                        LowStockCount = Convert.ToInt32(reader["LowStockCount"]),
                        ActiveProducts = Convert.ToInt32(reader["ActiveProducts"])
                    };
                }
            }
        }

        private static SqlCommand RangeCommand(SqlConnection connection, string procedure, DateTime start, DateTime end)
        {
            var command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add(new SqlParameter("@From", SqlDbType.DateTime) { Value = start });
            command.Parameters.Add(new SqlParameter("@To", SqlDbType.DateTime) { Value = end });
            return command;
        }
    }

    public sealed class ProductRepository
    {
        public List<Product> Search(string search, bool includeInactive = false)
        {
            var list = new List<Product>();

            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Product_Search", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Search",
                    string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : search.Trim());
                command.Parameters.AddWithValue("@IncludeInactive", includeInactive);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(ReadProduct(reader, false));
                    }
                }
            }

            return list;
        }

        public Product GetById(int productId)
        {
            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Product_GetById", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@ProductId", productId);

                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadProduct(reader, true) : null;
                }
            }
        }

        public int Insert(Product p, int userId)
        {
            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_Product_Insert", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Sku", p.Sku);
                    command.Parameters.AddWithValue("@Barcode", (object)p.Barcode ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Name", p.Name);
                    command.Parameters.AddWithValue("@CategoryId", p.CategoryId);
                    command.Parameters.Add(Money("@Price", p.Price));
                    command.Parameters.Add(Money("@Cost", p.Cost));
                    command.Parameters.AddWithValue("@StockQty", p.StockQty);
                    command.Parameters.AddWithValue("@ReorderLevel", p.ReorderLevel);
                    command.Parameters.Add(ImageParameter(p.Image));
                    command.Parameters.AddWithValue("@UserId", userId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        public void Update(Product p, bool changeImage)
        {
            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_Product_Update", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ProductId", p.Id);
                    command.Parameters.AddWithValue("@Sku", p.Sku);
                    command.Parameters.AddWithValue("@Barcode", (object)p.Barcode ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Name", p.Name);
                    command.Parameters.AddWithValue("@CategoryId", p.CategoryId);
                    command.Parameters.Add(Money("@Price", p.Price));
                    command.Parameters.Add(Money("@Cost", p.Cost));
                    command.Parameters.AddWithValue("@ReorderLevel", p.ReorderLevel);
                    command.Parameters.AddWithValue("@IsActive", p.IsActive);
                    command.Parameters.Add(ImageParameter(p.Image));
                    command.Parameters.AddWithValue("@ChangeImage", changeImage);
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        public void Deactivate(int productId)
        {
            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_Product_Delete", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ProductId", productId);
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        public int AdjustStock(int productId, int change, string reason, int userId)
        {
            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_Inventory_Adjust", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@ProductId", productId);
                    command.Parameters.AddWithValue("@ChangeQty", change);
                    command.Parameters.AddWithValue("@Reason", reason);
                    command.Parameters.AddWithValue("@UserId", userId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        private static Product ReadProduct(SqlDataReader reader, bool withImage)
        {
            return new Product
            {
                Id = (int)reader["ProductId"],
                Sku = (string)reader["Sku"],
                Barcode = reader["Barcode"] as string,
                Name = (string)reader["Name"],
                CategoryId = (int)reader["CategoryId"],
                CategoryName = (string)reader["CategoryName"],
                Price = (decimal)reader["Price"],
                Cost = (decimal)reader["Cost"],
                StockQty = (int)reader["StockQty"],
                ReorderLevel = (int)reader["ReorderLevel"],
                Image = withImage ? reader["Image"] as byte[] : null,
                IsActive = (bool)reader["IsActive"]
            };
        }

        private static SqlParameter Money(string name, decimal value)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = value };
        }

        private static SqlParameter ImageParameter(byte[] image)
        {
            return new SqlParameter("@Image", SqlDbType.VarBinary, -1) { Value = (object)image ?? DBNull.Value };
        }

        private static Exception Friendly(SqlException ex)
        {
            if (ex.Number == 2627 || ex.Number == 2601)
            {
                if (ex.Message.IndexOf("Sku", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new InvalidOperationException("That SKU is already used by another product.");
                if (ex.Message.IndexOf("Barcode", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new InvalidOperationException("That barcode is already used by another product.");
                return new InvalidOperationException("A product with the same value already exists.");
            }

            if (ex.Number >= 50000) return new InvalidOperationException(ex.Message);

            return ex;
        }
    }
}
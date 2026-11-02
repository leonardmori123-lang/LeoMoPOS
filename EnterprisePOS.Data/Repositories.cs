using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using EnterprisePOS.Domain;

namespace EnterprisePOS.Data
{
    public sealed class UserRepository
    {
        // Returns the user when the username exists, the account is active and the password matches.
        // Returns null in every other case, so the caller cannot tell which part was wrong.
        public User Login(string username, string password)
        {
            User user;
            string salt;
            string hash;

            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_Login", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@Username", username ?? string.Empty);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    salt = (string)reader["Salt"];
                    hash = (string)reader["PasswordHash"];
                    user = new User
                    {
                        Id = (int)reader["UserId"],
                        Username = (string)reader["Username"],
                        FullName = (string)reader["FullName"],
                        Role = (UserRole)(int)reader["RoleId"],
                        IsActive = (bool)reader["IsActive"]
                    };
                }
            }

            if (!user.IsActive) return null;
            if (!PasswordHasher.Verify(password, salt, hash)) return null;

            TouchLogin(user.Id);
            return user;
        }

        // Every user, active or not (no password data)
        public List<User> List()
        {
            var list = new List<User>();

            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_User_List", connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new User
                        {
                            Id = (int)reader["UserId"],
                            Username = (string)reader["Username"],
                            FullName = (string)reader["FullName"],
                            Role = (UserRole)(int)reader["RoleId"],
                            IsActive = (bool)reader["IsActive"],
                            LastLoginAt = reader["LastLoginAt"] as DateTime?
                        });
                    }
                }
            }
            return list;
        }

        // Adds a user. The password is salted and hashed here; only the hash and the salt are stored.
        public int Create(User user, string password)
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash(salt, password);

            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_User_Create", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Username", user.Username);
                    command.Parameters.AddWithValue("@PasswordHash", hash);
                    command.Parameters.AddWithValue("@Salt", salt);
                    command.Parameters.AddWithValue("@FullName", user.FullName);
                    command.Parameters.AddWithValue("@RoleId", (int)user.Role);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        // Changes the name, role and active flag of an existing user
        public void Update(User user)
        {
            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_User_Update", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UserId", user.Id);
                    command.Parameters.AddWithValue("@FullName", user.FullName);
                    command.Parameters.AddWithValue("@RoleId", (int)user.Role);
                    command.Parameters.AddWithValue("@IsActive", user.IsActive);
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        // Sets a new password. A fresh salt is made every time, then only the hash and the salt are stored.
        public void ResetPassword(int userId, string newPassword)
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash(salt, newPassword);

            try
            {
                using (var connection = Db.Open())
                using (var command = new SqlCommand("dbo.usp_User_ResetPassword", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@PasswordHash", hash);
                    command.Parameters.AddWithValue("@Salt", salt);
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                throw Friendly(ex);
            }
        }

        // 2627 / 2601 = the UNIQUE constraint on Username. 50000 and up are the messages written in the SQL script.
        private static Exception Friendly(SqlException ex)
        {
            if (ex.Number == 2627 || ex.Number == 2601) return new InvalidOperationException("That username is already taken.");
            if (ex.Number >= 50000) return new InvalidOperationException(ex.Message);
            return ex;
        }

        private static void TouchLogin(int userId)
        {
            using (var connection = Db.Open())
            using (var command = new SqlCommand("dbo.usp_User_TouchLogin", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@UserId", userId);
                command.ExecuteNonQuery();
            }
        }
    }

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
        // Saves a whole order. The stored procedure does everything in one transaction:
        // it takes prices from the Products table, checks stock, writes the order and its lines,
        // lowers the stock and logs each movement. If anything fails, nothing is saved.
        public int Create(Order order, decimal taxRatePercent)
        {
            // <Items><Item ProductId="1" Quantity="2"/></Items>  (only whole numbers go in, so no escaping is needed)
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
                // The messages raised by the teacher's procedure (for example "Insufficient stock for ...") read well as they are.
                if (ex.Number >= 50000) throw new InvalidOperationException(ex.Message);
                throw;
            }
        }

        // One saved order with its lines. Returns null when the id does not exist.
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

    // Reads the teacher's report procedures. Each one adds a day to the end date itself,
    // so the end date passed in is the last day to include.
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

        // Active products at or below their reorder level, right now (no date range)
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

        // The four numbers on the main screen (today's sales, today's orders, low stock count, active products)
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
        // Searches SKU, barcode and name. An empty search returns every product.
        // The Image property stays null here; the list does not need the pictures.
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

        // One product including its picture. Returns null when the id does not exist.
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

        // Adds a product. Starting stock is logged in InventoryLog by the stored procedure.
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

        // Changes a product. Stock is not touched here; stock changes go through the inventory adjustment.
        // The picture is only replaced when changeImage is true.
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

        // Soft delete: the row stays so old sales keep their product.
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

        // Adds (positive) or removes (negative) stock and writes the reason to InventoryLog.
        // Returns the new stock level. The procedure refuses a change that would go below zero.
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

        // DECIMAL(18,2) parameter, matching the table columns
        private static SqlParameter Money(string name, decimal value)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = value };
        }

        // A null picture must be sent as a typed VARBINARY(MAX) null, or SQL Server rejects the call
        private static SqlParameter ImageParameter(byte[] image)
        {
            return new SqlParameter("@Image", SqlDbType.VarBinary, -1) { Value = (object)image ?? DBNull.Value };
        }

        // Turns database errors into messages a person can read
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

            // Errors raised with THROW in the teacher's procedures (50000 and up) already read well
            if (ex.Number >= 50000) return new InvalidOperationException(ex.Message);

            return ex;
        }
    }
}
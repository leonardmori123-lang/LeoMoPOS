using System;
using System.Collections.Generic;
using System.Data.SQLite;
using EnterprisePOS.Domain;

namespace EnterprisePOS.Data
{
    public sealed class UserRepository
    {
        public User Login(string username, string password)
        {
            if (username == null) username = string.Empty;

            User user = null;
            string salt = string.Empty;
            string hash = string.Empty;

            string sql = "SELECT UserId, Username, PasswordHash, Salt, FullName, RoleId, IsActive " +
                         "FROM Users WHERE Username = @username";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@username", username);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    salt = reader.GetString(reader.GetOrdinal("Salt"));
                    hash = reader.GetString(reader.GetOrdinal("PasswordHash"));
                    user = ReadUser(reader);
                }
            }

            if (!user.IsActive) return null;
            if (!PasswordHasher.Verify(password, salt, hash)) return null;

            TouchLogin(user.Id);
            return user;
        }

        public List<User> List()
        {
            List<User> list = new List<User>();

            string sql = "SELECT UserId, Username, FullName, RoleId, IsActive, LastLoginAt " +
                         "FROM Users ORDER BY Username";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                int lastLoginColumn = reader.GetOrdinal("LastLoginAt");

                while (reader.Read())
                {
                    User user = ReadUser(reader);
                    if (!reader.IsDBNull(lastLoginColumn))
                    {
                        user.LastLoginAt = reader.GetDateTime(lastLoginColumn);
                    }
                    list.Add(user);
                }
            }

            return list;
        }

        public int Create(User user, string password)
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash(salt, password);

            string sql = "INSERT INTO Users (Username, PasswordHash, Salt, FullName, RoleId) " +
                         "VALUES (@username, @hash, @salt, @fullName, @roleId)";

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                using (SQLiteCommand command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", user.Username);
                    command.Parameters.AddWithValue("@hash", hash);
                    command.Parameters.AddWithValue("@salt", salt);
                    command.Parameters.AddWithValue("@fullName", user.FullName);
                    command.Parameters.AddWithValue("@roleId", (int)user.Role);
                    command.ExecuteNonQuery();

                    return (int)connection.LastInsertRowId;
                }
            }
            catch (SQLiteException ex)
            {
                if (IsDuplicate(ex)) throw new InvalidOperationException("That username is already taken.");
                throw;
            }
        }

        public void Update(User user)
        {
            int newActive = 0;
            if (user.IsActive) newActive = 1;

            try
            {
                using (SQLiteConnection connection = SqliteDb.Open())
                {
                    SqliteDb.BeginWrite(connection);
                    try
                    {
                        int oldRole = 0;
                        bool oldActive = false;

                        string findSql = "SELECT RoleId, IsActive FROM Users WHERE UserId = @userId";
                        using (SQLiteCommand find = new SQLiteCommand(findSql, connection))
                        {
                            find.Parameters.AddWithValue("@userId", user.Id);
                            using (SQLiteDataReader reader = find.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("User not found.");
                                oldRole = reader.GetInt32(0);
                                oldActive = reader.GetBoolean(1);
                            }
                        }

                        int admin = (int)UserRole.Admin;
                        bool wasActiveAdmin = (oldRole == admin) && oldActive;
                        bool stopsBeingActiveAdmin = ((int)user.Role != admin) || (newActive == 0);

                        if (wasActiveAdmin && stopsBeingActiveAdmin)
                        {
                            string countSql = "SELECT COUNT(*) FROM Users WHERE UserId <> @userId AND RoleId = @admin AND IsActive = 1";
                            using (SQLiteCommand count = new SQLiteCommand(countSql, connection))
                            {
                                count.Parameters.AddWithValue("@userId", user.Id);
                                count.Parameters.AddWithValue("@admin", admin);
                                long otherAdmins = Convert.ToInt64(count.ExecuteScalar());
                                if (otherAdmins == 0) throw new InvalidOperationException("At least one active admin must remain.");
                            }
                        }

                        string updateSql = "UPDATE Users SET FullName = @fullName, RoleId = @roleId, IsActive = @isActive " +
                                           "WHERE UserId = @userId";
                        using (SQLiteCommand update = new SQLiteCommand(updateSql, connection))
                        {
                            update.Parameters.AddWithValue("@fullName", user.FullName);
                            update.Parameters.AddWithValue("@roleId", (int)user.Role);
                            update.Parameters.AddWithValue("@isActive", newActive);
                            update.Parameters.AddWithValue("@userId", user.Id);
                            update.ExecuteNonQuery();
                        }

                        SqliteDb.Commit(connection);
                    }
                    catch
                    {
                        SqliteDb.Rollback(connection);
                        throw;
                    }
                }
            }
            catch (SQLiteException ex)
            {
                if (IsDuplicate(ex)) throw new InvalidOperationException("That username is already taken.");
                throw;
            }
        }

        public void ResetPassword(int userId, string newPassword)
        {
            string salt = PasswordHasher.NewSalt();
            string hash = PasswordHasher.Hash(salt, newPassword);

            string sql = "UPDATE Users SET PasswordHash = @hash, Salt = @salt WHERE UserId = @userId";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@hash", hash);
                command.Parameters.AddWithValue("@salt", salt);
                command.Parameters.AddWithValue("@userId", userId);

                int rows = command.ExecuteNonQuery();
                if (rows == 0) throw new InvalidOperationException("User not found.");
            }
        }
        private static User ReadUser(SQLiteDataReader reader)
        {
            User user = new User();
            user.Id = reader.GetInt32(reader.GetOrdinal("UserId"));
            user.Username = reader.GetString(reader.GetOrdinal("Username"));
            user.FullName = reader.GetString(reader.GetOrdinal("FullName"));
            user.Role = (UserRole)reader.GetInt32(reader.GetOrdinal("RoleId"));
            user.IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"));
            return user;
        }
        private static bool IsDuplicate(SQLiteException ex)
        {
            return ex.Message.Contains("UNIQUE constraint failed");
        }

        private static void TouchLogin(int userId)
        {
            string sql = "UPDATE Users SET LastLoginAt = datetime('now', 'localtime') WHERE UserId = @userId";

            using (SQLiteConnection connection = SqliteDb.Open())
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                command.ExecuteNonQuery();
            }
        }
    }
}
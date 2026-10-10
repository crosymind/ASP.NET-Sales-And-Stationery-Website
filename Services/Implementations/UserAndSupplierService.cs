using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Common;
using QuanLyKho.Data;
using QuanLyKho.Models.Entities;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly QuanLyKhoDbContext _context;

        public UserService(QuanLyKhoDbContext context)
        {
            _context = context;
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username && u.IsActive);
            if (user == null) return null;

            if (SecurityHelper.VerifyPassword(password, user.PasswordHash))
            {
                return user;
            }
            return null;
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<(bool Success, string Message)> SaveUserAsync(int? userId, string username, string? password, string fullName, string role, bool isActive, int performerId)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName))
            {
                return (false, "Tên đăng nhập và Họ tên không được để trống.");
            }

            if (userId == null || userId == 0)
            {
                // Create
                if (string.IsNullOrWhiteSpace(password))
                {
                    return (false, "Mật khẩu không được để trống khi tạo mới.");
                }

                bool exists = await _context.Users.AnyAsync(u => u.UserName == username);
                if (exists)
                {
                    return (false, "Tên đăng nhập đã tồn tại trong hệ thống.");
                }

                var newUser = new User
                {
                    UserName = username.Trim(),
                    PasswordHash = SecurityHelper.HashPassword(password),
                    FullName = fullName.Trim(),
                    Role = role,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = performerId,
                    Action = "USER_CREATED",
                    EntityType = "User",
                    EntityId = newUser.UserId.ToString(),
                    Details = $"Tạo tài khoản: {newUser.UserName} ({newUser.FullName}) - Role: {newUser.Role}",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return (true, "Tạo tài khoản thành công.");
            }
            else
            {
                // Update
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null) return (false, "Không tìm thấy người dùng.");

                string oldRole = user.Role;
                bool oldActive = user.IsActive;

                user.FullName = fullName.Trim();
                user.Role = role;
                user.IsActive = isActive;

                if (!string.IsNullOrWhiteSpace(password))
                {
                    user.PasswordHash = SecurityHelper.HashPassword(password);
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = performerId,
                    Action = "USER_UPDATED",
                    EntityType = "User",
                    EntityId = user.UserId.ToString(),
                    Details = $"Cập nhật user {user.UserName}. Role: {oldRole}->{user.Role}, Active: {oldActive}->{user.IsActive}",
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                return (true, "Cập nhật tài khoản thành công.");
            }
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return (false, "Người dùng không tồn tại.");

            if (!SecurityHelper.VerifyPassword(oldPassword, user.PasswordHash))
            {
                return (false, "Mật khẩu cũ không chính xác.");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                return (false, "Mật khẩu mới phải có ít nhất 6 ký tự.");
            }

            user.PasswordHash = SecurityHelper.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return (true, "Đổi mật khẩu thành công.");
        }
    }

    public class SupplierService : ISupplierService
    {
        private readonly QuanLyKhoDbContext _context;

        public SupplierService(QuanLyKhoDbContext context)
        {
            _context = context;
        }

        public async Task<List<Supplier>> GetAllAsync(string? search = null, bool activeOnly = false)
        {
            var query = _context.Suppliers.AsQueryable();
            if (activeOnly)
            {
                query = query.Where(s => s.IsActive);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(kw) || (s.Phone != null && s.Phone.Contains(kw)));
            }
            return await query.OrderBy(s => s.Name).ToListAsync();
        }

        public async Task<Supplier?> GetByIdAsync(int id)
        {
            return await _context.Suppliers.FindAsync(id);
        }

        public async Task<(bool Success, string Message)> SaveAsync(Supplier supplier, int userId)
        {
            if (string.IsNullOrWhiteSpace(supplier.Name))
            {
                return (false, "Tên nhà cung cấp không được để trống.");
            }

            if (supplier.SupplierId == 0)
            {
                supplier.CreatedAt = DateTime.UtcNow;
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "SUPPLIER_CREATED",
                    EntityType = "Supplier",
                    EntityId = supplier.SupplierId.ToString(),
                    Details = $"Thêm nhà cung cấp mới: {supplier.Name}",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return (true, "Thêm nhà cung cấp thành công.");
            }
            else
            {
                var existing = await _context.Suppliers.FindAsync(supplier.SupplierId);
                if (existing == null) return (false, "Nhà cung cấp không tồn tại.");

                existing.Name = supplier.Name;
                existing.Phone = supplier.Phone;
                existing.Email = supplier.Email;
                existing.Address = supplier.Address;
                existing.IsActive = supplier.IsActive;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "SUPPLIER_UPDATED",
                    EntityType = "Supplier",
                    EntityId = existing.SupplierId.ToString(),
                    Details = $"Cập nhật thông tin nhà cung cấp: {existing.Name}",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return (true, "Cập nhật nhà cung cấp thành công.");
            }
        }

        public async Task<(bool Success, string Message)> ToggleStatusAsync(int id, int userId)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null) return (false, "Không tìm thấy nhà cung cấp.");

            supplier.IsActive = !supplier.IsActive;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "SUPPLIER_STATUS_CHANGED",
                EntityType = "Supplier",
                EntityId = supplier.SupplierId.ToString(),
                Details = $"Đổi trạng thái NCC {supplier.Name}: Active={supplier.IsActive}",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return (true, $"Đã {(supplier.IsActive ? "kích hoạt" : "ngừng hoạt động")} nhà cung cấp.");
        }
    }
}

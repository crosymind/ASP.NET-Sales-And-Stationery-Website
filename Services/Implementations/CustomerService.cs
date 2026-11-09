using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyKho.Data;
using QuanLyKho.Models.Entities;
using QuanLyKho.Services.Interfaces;

namespace QuanLyKho.Services.Implementations
{
    public class CustomerService : ICustomerService
    {
        private readonly QuanLyKhoDbContext _context;

        public CustomerService(QuanLyKhoDbContext context)
        {
            _context = context;
        }

        public async Task<List<Customer>> GetAllAsync(string? search = null, bool activeOnly = false)
        {
            var query = _context.Customers.AsQueryable();

            if (activeOnly)
            {
                query = query.Where(c => c.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string kw = search.Trim().ToLower();
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(kw) ||
                    c.CustomerCode.ToLower().Contains(kw) ||
                    c.PhoneNumber.Contains(kw) ||
                    (c.Email != null && c.Email.ToLower().Contains(kw)) ||
                    (c.Address != null && c.Address.ToLower().Contains(kw)));
            }

            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task<Customer?> GetByIdAsync(int id)
        {
            return await _context.Customers
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(c => c.CustomerId == id);
        }

        public async Task<Customer?> GetByPhoneAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            string cleanPhone = phone.Trim();
            return await _context.Customers
                .FirstOrDefaultAsync(c => c.PhoneNumber == cleanPhone && c.IsActive);
        }

        public async Task<(bool Success, string Message, int CustomerId)> SaveAsync(Customer customer, int userId)
        {
            if (string.IsNullOrWhiteSpace(customer.FullName))
            {
                return (false, "Tên khách hàng không được để trống.", 0);
            }

            if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
            {
                return (false, "Số điện thoại không được để trống.", 0);
            }

            string cleanPhone = customer.PhoneNumber.Trim();

            if (customer.CustomerId == 0)
            {
                // Create
                // Check duplicate phone
                bool existsPhone = await _context.Customers.AnyAsync(c => c.PhoneNumber == cleanPhone);
                if (existsPhone)
                {
                    return (false, $"Số điện thoại '{cleanPhone}' đã tồn tại trong hệ thống.", 0);
                }

                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                {
                    customer.CustomerCode = $"KH{DateTime.UtcNow:yyMMdd}{new Random().Next(100, 999)}";
                }

                customer.PhoneNumber = cleanPhone;
                customer.FullName = customer.FullName.Trim();
                customer.CreatedAt = DateTime.UtcNow;
                customer.IsActive = true;

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "CUSTOMER_CREATED",
                    EntityType = "Customer",
                    EntityId = customer.CustomerId.ToString(),
                    Details = $"Thêm mới khách hàng: {customer.FullName} ({customer.CustomerCode})",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return (true, "Thêm khách hàng mới thành công!", customer.CustomerId);
            }
            else
            {
                // Update
                var existing = await _context.Customers.FindAsync(customer.CustomerId);
                if (existing == null)
                {
                    return (false, "Không tìm thấy khách hàng cần cập nhật.", 0);
                }

                bool existsPhone = await _context.Customers.AnyAsync(c => c.PhoneNumber == cleanPhone && c.CustomerId != customer.CustomerId);
                if (existsPhone)
                {
                    return (false, $"Số điện thoại '{cleanPhone}' đã được sử dụng bởi khách hàng khác.", 0);
                }

                existing.FullName = customer.FullName.Trim();
                existing.PhoneNumber = cleanPhone;
                existing.Email = customer.Email?.Trim();
                existing.Address = customer.Address?.Trim();
                existing.CustomerGroup = string.IsNullOrWhiteSpace(customer.CustomerGroup) ? "Thường" : customer.CustomerGroup;
                existing.IsActive = customer.IsActive;

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "CUSTOMER_UPDATED",
                    EntityType = "Customer",
                    EntityId = existing.CustomerId.ToString(),
                    Details = $"Cập nhật thông tin khách hàng: {existing.FullName} ({existing.CustomerCode})",
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return (true, "Cập nhật thông tin khách hàng thành công!", existing.CustomerId);
            }
        }

        public async Task<(bool Success, string Message)> ToggleStatusAsync(int id, int userId)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return (false, "Không tìm thấy khách hàng.");
            }

            customer.IsActive = !customer.IsActive;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "CUSTOMER_STATUS_CHANGED",
                EntityType = "Customer",
                EntityId = customer.CustomerId.ToString(),
                Details = $"Đổi trạng thái khách hàng {customer.FullName} sang {(customer.IsActive ? "Đang hoạt động" : "Ngừng hoạt động")}",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return (true, $"Đã {(customer.IsActive ? "kích hoạt" : "ngừng hoạt động")} khách hàng '{customer.FullName}'.");
        }

        public async Task<(bool Success, string Message)> DeleteAsync(int id, int userId)
        {
            var customer = await _context.Customers
                .Include(c => c.Invoices)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null)
            {
                return (false, "Không tìm thấy khách hàng.");
            }

            if (customer.Invoices != null && customer.Invoices.Count > 0)
            {
                return (false, $"Khách hàng '{customer.FullName}' đã phát sinh {customer.Invoices.Count} hóa đơn mua hàng nên không thể xóa vĩnh viễn. Vui lòng chuyển sang trạng thái 'Ngừng hoạt động' để lưu trữ lịch sử chứng từ.");
            }

            string info = $"{customer.FullName} ({customer.CustomerCode} - {customer.PhoneNumber})";
            _context.Customers.Remove(customer);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "CUSTOMER_DELETED",
                EntityType = "Customer",
                EntityId = id.ToString(),
                Details = $"Xóa khách hàng chưa phát sinh giao dịch: {info}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, $"Đã xóa khách hàng '{info}' thành công.");
        }

        public async Task<(bool Success, string Message)> AdjustPointsAsync(int id, int pointsDelta, string reason, int userId)
        {
            if (pointsDelta == 0)
            {
                return (false, "Số điểm điều chỉnh phải khác 0.");
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return (false, "Không tìm thấy khách hàng.");
            }

            int oldPoints = customer.Points;
            int newPoints = oldPoints + pointsDelta;
            if (newPoints < 0)
            {
                return (false, $"Điểm tích lũy hiện tại ({oldPoints} pts) không đủ để trừ {Math.Abs(pointsDelta)} pts.");
            }

            customer.Points = newPoints;

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = "CUSTOMER_POINTS_ADJUSTED",
                EntityType = "Customer",
                EntityId = customer.CustomerId.ToString(),
                Details = $"Điều chỉnh điểm KH {customer.FullName} ({customer.CustomerCode}): {(pointsDelta > 0 ? "+" : "")}{pointsDelta} pts ({oldPoints} -> {newPoints}). Lý do: {(string.IsNullOrWhiteSpace(reason) ? "Đổi thưởng / Điều chỉnh" : reason.Trim())}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return (true, $"Đã cập nhật điểm tích lũy của '{customer.FullName}' từ {oldPoints:N0} pts thành {newPoints:N0} pts.");
        }

        public async Task<List<Invoice>> GetCustomerInvoicesAsync(int customerId)
        {
            return await _context.Invoices
                .Include(i => i.Items)
                    .ThenInclude(it => it.Product)
                .Where(i => i.CustomerId == customerId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }
    }
}

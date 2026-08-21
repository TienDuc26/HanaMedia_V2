using System;
using System.Linq;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Controllers
{
    [Authorize(Roles = AppRoles.Director)]
    [Route("Director/Department")]
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _db;

        public DepartmentController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Director/Department
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Views/Director/Department.cshtml");
        }

        // GET: /Director/Department/ApiList
        // Trả về { success, data: { top: [...3 card...], list: [...bảng...] } }
        [HttpGet("ApiList")]
        public async Task<IActionResult> ApiList(string? q, string? status)
        {
            var query = _db.Departments.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim().ToLower();
                query = query.Where(d =>
                    d.Code.ToLower().Contains(kw) ||
                    d.Name.ToLower().Contains(kw) ||
                    (d.Description != null && d.Description.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                (status == "active" || status == "inactive"))
            {
                query = query.Where(d => d.Status == status);
            }

            var depts = await query.OrderBy(d => d.Code).ToListAsync();

            // Tính headcount + manager cho từng phòng
            var empByDept = await _db.Employees.AsNoTracking()
                .GroupBy(e => e.Department)
                .Select(g => new EmpCountByDept { DeptCode = g.Key, Count = g.Count() })
                .ToListAsync();

            // Tìm nhân viên cấp cao theo từng phòng (Position chứa "Trưởng phòng" / "Giám đốc")
            var managerByDept = await _db.Employees.AsNoTracking()
                .Where(e => e.Position.ToLower().Contains("trưởng phòng")
                         || e.Position.ToLower().Contains("giam doc")
                         || e.Position.ToLower().Contains("giám đốc")
                         || e.Position.ToLower().Contains("truong phong"))
                .Select(e => new ManagerRow { Department = e.Department, FullName = e.FullName, Position = e.Position })
                .ToListAsync();

            var top = depts.Take(3).Select(d => BuildDeptRow(d, empByDept, managerByDept)).ToList();
            var list = depts.Select(d => BuildDeptRow(d, empByDept, managerByDept)).ToList();

            return Ok(new
            {
                success = true,
                data = new { top, list },
                total = list.Count
            });
        }

        private static object BuildDeptRow(Department d,
            System.Collections.Generic.List<EmpCountByDept> empByDept,
            System.Collections.Generic.List<ManagerRow> managerByDept)
        {
            var count = empByDept.FirstOrDefault(x => x.DeptCode == d.Code)?.Count ?? 0;

            // Lấy trưởng phòng (nếu có)
            string manager = "Chưa bổ nhiệm";
            var m = managerByDept.FirstOrDefault(x =>
                string.Equals(x.Department, d.Code, StringComparison.OrdinalIgnoreCase));
            if (m != null) manager = m.FullName;

            // Hiệu suất: tạm thời sinh deterministic 70-95% dựa trên code để không nhảy số mỗi lần reload
            // Sẽ thay bằng query thật khi có bảng Performance
            int performance = 0;
            if (count > 0)
            {
                performance = 70 + (Math.Abs(d.Code.GetHashCode()) % 26);
            }

            string quality = performance >= 85 ? "Tốt"
                            : performance >= 70 ? "Theo dõi sát sao"
                            : "Cần cải thiện";

            string statusLabel = d.Status == "active" ? "Đang hoạt động" : "Ngừng hoạt động";

            return new
            {
                id = d.Id,
                code = d.Code,
                name = d.Name,
                description = d.Description,
                status = d.Status,
                statusLabel,
                manager,
                headcount = count,
                performance,
                quality
            };
        }

        private class EmpCountByDept
        {
            public string DeptCode { get; set; } = "";
            public int Count { get; set; }
        }

        private class ManagerRow
        {
            public string Department { get; set; } = "";
            public string FullName { get; set; } = "";
            public string Position { get; set; } = "";
        }

        // GET: /Director/Department/ApiGet/{id}
        [HttpGet("ApiGet/{id:int}")]
        public async Task<IActionResult> ApiGet(int id)
        {
            var dept = await _db.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dept == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy phòng ban." });
            }

            var count = await _db.Employees.CountAsync(e => e.Department == dept.Code);
            var manager = await _db.Employees
                .Where(e => e.Department == dept.Code &&
                       (e.Position.ToLower().Contains("trưởng phòng")
                        || e.Position.ToLower().Contains("truong phong")))
                .Select(e => e.FullName)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = dept.Id,
                    code = dept.Code,
                    name = dept.Name,
                    description = dept.Description,
                    status = dept.Status,
                    manager = manager ?? "Chưa bổ nhiệm",
                    headcount = count
                }
            });
        }

        // POST: /Director/Department/ApiCreate
        [HttpPost("ApiCreate")]
        public async Task<IActionResult> ApiCreate([FromBody] DepartmentDto input)
        {
            if (input == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var code = (input.Code ?? "").Trim();
            var name = (input.Name ?? "").Trim();

            if (string.IsNullOrEmpty(code) || code.Length > 20)
            {
                return BadRequest(new { success = false, message = "Mã phòng ban là bắt buộc, tối đa 20 ký tự." });
            }
            if (string.IsNullOrEmpty(name) || name.Length > 100)
            {
                return BadRequest(new { success = false, message = "Tên phòng ban là bắt buộc, tối đa 100 ký tự." });
            }

            var status = string.IsNullOrWhiteSpace(input.Status) ? "active" : input.Status!.Trim().ToLower();
            if (status != "active" && status != "inactive")
            {
                status = "active";
            }

            var exists = await _db.Departments.AnyAsync(d => d.Code.ToLower() == code.ToLower());
            if (exists)
            {
                return Conflict(new { success = false, message = $"Mã phòng ban '{code}' đã tồn tại." });
            }

            var dept = new Department
            {
                Code = code,
                Name = name,
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description!.Trim(),
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.Departments.Add(dept);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Tạo phòng ban thành công.",
                data = new { id = dept.Id }
            });
        }

        // POST: /Director/Department/ApiUpdate/{id}
        [HttpPost("ApiUpdate/{id:int}")]
        [HttpPut("ApiUpdate/{id:int}")]
        public async Task<IActionResult> ApiUpdate(int id, [FromBody] DepartmentDto input)
        {
            if (input == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (dept == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy phòng ban." });
            }

            var code = (input.Code ?? "").Trim();
            var name = (input.Name ?? "").Trim();

            if (string.IsNullOrEmpty(code) || code.Length > 20)
            {
                return BadRequest(new { success = false, message = "Mã phòng ban là bắt buộc, tối đa 20 ký tự." });
            }
            if (string.IsNullOrEmpty(name) || name.Length > 100)
            {
                return BadRequest(new { success = false, message = "Tên phòng ban là bắt buộc, tối đa 100 ký tự." });
            }

            var status = string.IsNullOrWhiteSpace(input.Status) ? dept.Status : input.Status!.Trim().ToLower();
            if (status != "active" && status != "inactive")
            {
                status = "active";
            }

            var dup = await _db.Departments.AnyAsync(d =>
                d.Id != id && d.Code.ToLower() == code.ToLower());
            if (dup)
            {
                return Conflict(new { success = false, message = $"Mã phòng ban '{code}' đã được dùng cho phòng ban khác." });
            }

            dept.Code = code;
            dept.Name = name;
            dept.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description!.Trim();
            dept.Status = status;
            dept.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Cập nhật phòng ban thành công." });
        }

        // POST: /Director/Department/ApiDelete/{id}
        [HttpPost("ApiDelete/{id:int}")]
        public async Task<IActionResult> ApiDelete(int id)
        {
            var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (dept == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy phòng ban." });
            }

            var hasEmployee = await _db.Employees.AnyAsync(e =>
                e.Department.ToLower() == dept.Code.ToLower());
            if (hasEmployee)
            {
                return Conflict(new
                {
                    success = false,
                    message = $"Không thể xóa: đang có nhân viên thuộc phòng ban '{dept.Code}'."
                });
            }

            _db.Departments.Remove(dept);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "Xóa phòng ban thành công." });
        }
    }

    public class DepartmentDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
    }
}
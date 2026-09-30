using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HanaMedia.Constants;
using HanaMedia.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Explicit, one-time LOCAL demo provisioning. Unlike V4Flow, these fixtures persist.
// Never resets an account, deletes data, changes business configuration, or runs on startup.
if (args.Length != 1 || args[0] != "--seed-local-demo")
    throw new Exception("Explicit opt-in required: --seed-local-demo. This creates persistent demo data.");
var root = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "HanaMedia.csproj"))) throw new Exception("Run from repository root.");
var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", true).AddUserSecrets("HanaMedia-V2-Local-Development").AddEnvironmentVariables().Build();
var connection = new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
if (connection.InitialCatalog != "HanaMedia" || !connection.DataSource.Equals("HAIRS-LAPTOP", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Safety guard: only the verified local HAIRS-LAPTOP/HanaMedia target is allowed.");
ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
var artifactDir = Path.Combine(root, "tests", "artifacts");
Directory.CreateDirectory(artifactDir);
var manifestPath = Path.Combine(artifactDir, "demo-v4-access.json");
if (File.Exists(manifestPath)) throw new Exception("Demo manifest already exists. Refusing duplicate creation/password reset.");
await using (var db = Db())
{
    if (await db.Users.AnyAsync(u => u.Username.StartsWith("demo_v4_"))) throw new Exception("Demo accounts already exist; no changes made.");
    if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Apply pending migrations first.");
}
using var readiness = new HttpClient();
(await readiness.GetAsync("http://127.0.0.1:5028/Login")).EnsureSuccessStatusCode();
await using var sql = new SqlConnection(connection.ConnectionString);
await sql.OpenAsync();
using var folderCmd = new SqlCommand("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))", sql);
var backup = Path.Combine((string)(await folderCmd.ExecuteScalarAsync())!, "HanaMedia_before_demo_v4_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak");
foreach (var statement in new[] { "BACKUP DATABASE [HanaMedia] TO DISK=@file WITH COPY_ONLY,CHECKSUM", "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM" })
{
    using var cmd = new SqlCommand(statement, sql) { CommandTimeout = 180 };
    cmd.Parameters.AddWithValue("@file", backup); await cmd.ExecuteNonQueryAsync();
}
Console.WriteLine("Verified backup: " + backup);
var password = "DemoV4!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
var people = new Dictionary<string, Employee>();
var specs = new[] {
    ("giamdoc", AppRoles.Director, "HCNS", "Giám đốc", true),
    ("qlbooking", AppRoles.BookingManager, "Booking", "QL Booking", true),
    ("booking1", AppRoles.BookingStaff, "Booking", "NV Booking 1", false),
    ("booking2", AppRoles.BookingStaff, "Booking", "NV Booking 2", false),
    ("qlytuong", AppRoles.IdeaManager, "Y_tuong", "QL Ý tưởng", true),
    ("creator", AppRoles.IdeaStaff, "Y_tuong", "Content Creator", false),
    ("phaply", AppRoles.LegalStaff, "HCNS", "NV Pháp lý", false),
    ("ketoan", AppRoles.Accountant, "HCNS", "NV Kế toán", false),
    ("qlhcns", AppRoles.HumanResourcesManager, "HCNS", "QL HCNS", true),
    ("hcns", AppRoles.HumanResourcesStaff, "HCNS", "NV HCNS", false)
};
var campaigns = new List<Campaign>();
var kols = new List<Kol>();
var checks = new List<string>();
var scenarios = new Dictionary<string, int>();
var ideaIds = new List<int>();
var complete = false;
async Task SaveManifest()
{
    await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(new {
        Complete = complete, Backup = backup, Password = password,
        BaseUrl = "http://localhost:5028", Accounts = people.Select(p => new { Key = p.Key, Username = p.Value.User!.Username, Role = p.Value.User.Role, EmployeeId = p.Value.Id, UserId = p.Value.UserId }),
        Campaigns = campaigns.Select(c => new { c.Id, c.Name }), Kols = kols.Select(k => new { k.Id, k.Name }), Bookings = scenarios, Ideas = ideaIds, Checks = checks
    }, new JsonSerializerOptions { WriteIndented = true }));
}
await using (var db = Db())
await using (var tx = await db.Database.BeginTransactionAsync())
{
    foreach (var (key, role, dept, label, manager) in specs)
    {
        var user = new User { Username = "demo_v4_" + key, Email = "demo_v4_" + key + "@example.test", Role = role,
            PasswordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant(),
            Status = "active", SecurityStamp = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.Now };
        var employee = new Employee { User = user, FullName = "[DEMO V4] " + label, Email = user.Email,
            Dob = new(1990, 1, 1), Phone = "000000" + (people.Count + 1).ToString("D4"), Address = "Dữ liệu demo, không phải nhân sự thật",
            Department = dept, Position = label, JoinedDate = DateOnly.FromDateTime(DateTime.Today), ContractType = "thu_viec",
            Status = "dang_lam_viec", IsManager = manager, CreatedAt = DateTime.Now };
        db.Employees.Add(employee); people[key] = employee;
    }
    await db.SaveChangesAsync();
    people["booking1"].ManagerId = people["booking2"].ManagerId = people["qlbooking"].Id;
    people["creator"].ManagerId = people["qlytuong"].Id; people["hcns"].ManagerId = people["qlhcns"].Id;
    foreach (var (label, status) in new[] { ("Ra mắt sản phẩm", "planning"), ("Chờ Giám đốc chốt", "planning"), ("Chiến dịch đang chờ ký", "planning") })
    {
        var c = new Campaign { Name = "[DEMO V4] " + label, Client = "[DEMO V4] Nhãn hàng thử nghiệm", Description = "Dữ liệu kiểm thử, không dùng để vận hành thực tế.",
            ManagerEmployeeId = people["qlbooking"].Id, StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            Budget = 1000000000m, Status = status, Notes = "DEMO V4", CreatedAt = DateTime.Now };
        db.Campaigns.Add(c); campaigns.Add(c);
    }
    foreach (var i in Enumerable.Range(1, 3))
    {
        var k = new Kol { Name = "[DEMO V4] Creator đối tác " + i, Platform = i == 1 ? "TikTok" : "Facebook", ProfileLink = "https://example.test/demo-creator-" + i,
            Location = "Demo", ContactInfo = "demo-creator-" + i + "@example.test", Niche = "Lifestyle", FollowersCount = i * 10000,
            Status = "tiem_nang", IsActive = true, ResponsibleStaffId = people["booking1"].Id, CreatedAt = DateTime.Now };
        db.Kols.Add(k); kols.Add(k);
    }
    db.SystemAuditLogs.Add(new SystemAuditLog { UserId = people["giamdoc"].UserId, Module = "Tai_Khoan", ActionType = "demo_seeded", IpAddress = "127.0.0.1",
        LogDetail = "[DEMO V4] User-requested local test fixtures: 10 employee-linked accounts, 3 campaigns, 3 KOLs. Not production activity.", CreatedAt = DateTime.Now });
    await db.SaveChangesAsync();
    await SaveManifest(); // Credentials retained even if a later HTTP scenario fails.
    await tx.CommitAsync();
}
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); checks.Add(name); Console.WriteLine("PASS: " + name); }
bool Denied(HttpResponseMessage r) => r.StatusCode == HttpStatusCode.Forbidden || r.StatusCode == HttpStatusCode.Redirect && r.Headers.Location?.OriginalString.Contains("/AccessDenied", StringComparison.OrdinalIgnoreCase) == true;
static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
// Genuine small DOCX fixture, clearly marked DEMO. No external template or executable content.
byte[] DemoDocument(string label)
{
    using var bytes = new MemoryStream();
    using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
    {
        void Entry(string path, string value) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8); writer.Write(value); }
        Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        Entry("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>DEMO V4 — KHÔNG CÓ GIÁ TRỊ SỬ DỤNG THỰC TẾ</w:t></w:r></w:p><w:p><w:r><w:t>" + label + "</w:t></w:r></w:p><w:p><w:r><w:t>Chứng từ giả lập để thử luồng Pháp lý, ký và nghiệm thu. Không phát sinh nghĩa vụ hoặc thanh toán thật.</w:t></w:r></w:p></w:body></w:document>");
    }
    return bytes.ToArray();
}
var clients = new Dictionary<string, HttpClient>();
async Task<HttpResponseMessage> Post(string role, string url, Dictionary<string, string>? data = null, string? fileField = null)
{
    data ??= new(); var client = clients[role]; data["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Profile"));
    HttpResponseMessage response;
    if (fileField != null)
    {
        using var body = new MultipartFormDataContent(); foreach (var kv in data) body.Add(new StringContent(kv.Value), kv.Key);
        body.Add(new ByteArrayContent(DemoDocument(fileField == "contractFile" ? "Hợp đồng demo" : "Biên bản nghiệm thu demo")), fileField, "DEMO-V4.docx");
        response = await client.PostAsync(url, body);
    }
    else response = await client.PostAsync(url, new FormUrlEncodedContent(data));
    if ((int)response.StatusCode >= 500) throw new Exception("Server error at " + url);
    return response;
}
async Task<Booking> Booking(int id) { await using var db = Db(); return await db.Bookings.Include(b => b.BookingKols).Include(b => b.BookingWages).SingleAsync(b => b.Id == id); }
try
{
    foreach (var key in people.Keys)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new("http://127.0.0.1:5028"), Timeout = TimeSpan.FromSeconds(30) };
        var response = await client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = "demo_v4_" + key, ["password"] = password, ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Login")) }));
        Check(response.StatusCode == HttpStatusCode.Redirect && (await client.GetAsync("/Profile")).IsSuccessStatusCode, "Login + profile: " + key); clients[key] = client;
    }
    var campaignId = campaigns[0].Id;
    Check((await clients["qlytuong"].GetAsync($"/api/Campaigns/{campaignId}")).StatusCode == HttpStatusCode.NotFound, "Unconfirmed campaign hidden from Ideas API");
    Check(Denied(await Post("qlbooking", $"/Campaigns/Confirm/{campaignId}")), "Booking manager cannot confirm campaign");
    await Post("giamdoc", $"/Campaigns/Confirm/{campaignId}");
    Check((await clients["qlytuong"].GetAsync($"/Campaigns/Details/{campaignId}")).IsSuccessStatusCode, "Director confirmation opens campaign to Ideas");
    Check((await clients["qlytuong"].GetAsync($"/api/Campaigns/{campaigns[1].Id}")).StatusCode == HttpStatusCode.NotFound, "Second campaign remains hidden");
    Dictionary<string, string> Form(int c, string label) => new() { ["CampaignId"] = c.ToString(), ["BookingPrice"] = "100000000", ["ActualCost"] = "1", ["Status"] = "dang_cho", ["JobDescription"] = "[DEMO V4] " + label + " — 3 video giới thiệu sản phẩm, không đăng thật.", ["Notes"] = "[DEMO V4] " + label, ["deadlineStr"] = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd"), ["kolIds[0]"] = kols[0].Id.ToString(), ["kolIds[1]"] = kols[1].Id.ToString(), ["kolIds[2]"] = kols[2].Id.ToString() };
    await Post("qlbooking", "/Bookings/Create", Form(campaigns[2].Id, "INVALID waiting"));
    await using (var db = Db()) Check(!await db.Bookings.AnyAsync(b => b.Notes == "[DEMO V4] INVALID waiting"), "Awaiting-signature campaign cannot receive a new Booking");
    var labels = new[] { "01 Nháp - thử chia thù lao", "02 Chờ Giám đốc duyệt", "03 Giám đốc từ chối", "04 Đã duyệt - chờ soạn hợp đồng", "05 Chờ Pháp lý", "06 Pháp lý trả sửa", "07 Chờ Giám đốc ký", "08 Đang triển khai", "09 Hoàn thành - đối soát" };
    for (int step = 0; step < labels.Length; step++)
    {
        await Post("qlbooking", "/Bookings/Create", Form(campaignId, labels[step]));
        int id; await using (var db = Db()) id = await db.Bookings.Where(b => b.Notes == "[DEMO V4] " + labels[step]).Select(b => b.Id).SingleAsync();
        scenarios[labels[step]] = id; await SaveManifest();
        var b = await Booking(id);
        Check(b.FinanceVersion == 1 && b.ActualCost == 50000000m && b.CommissionPool == 10000000m && b.CastPool == 40000000m && b.BookingKols.Sum(k => k.CastAmount) == 40000000m && b.BookingKols.Count == 3, $"Booking #{id}: server 50/10/40, 3 KOL, rounding exact");
        if (step == 0)
        {
            await Post("qlbooking", $"/Bookings/UpdateWages/{id}", new() { [$"wages[{people["booking1"].Id}]"] = "11000000" });
            Check((await Booking(id)).BookingWages.Count == 0, "Cannot allocate over commission pool");
        }
        await Post("qlbooking", $"/Bookings/UpdateWages/{id}", new() { [$"wages[{people["booking1"].Id}]"] = "2000000", [$"wages[{people["booking2"].Id}]"] = "1000000" });
        Check((await Booking(id)).BookingWages.Sum(w => w.AllocatedWage) == 3000000m, $"Booking #{id}: staff 3M, manager residual 7M");
        if (step == 0) continue;
        await Post("qlbooking", $"/Bookings/SubmitApproval/{id}"); Check((await Booking(id)).ContractStatus == "cho_duyet", $"Booking #{id}: submitted");
        if (step == 1) continue;
        if (step == 2) { await Post("giamdoc", $"/Bookings/Reject/{id}", new() { ["rejectionReason"] = "[DEMO V4] Bổ sung phạm vi công việc và gửi lại." }); Check((await Booking(id)).ContractStatus == "tu_choi", "Director rejection persists"); continue; }
        await Post("giamdoc", $"/Bookings/Approve/{id}"); Check((await Booking(id)).ContractStatus == "da_duyet", $"Booking #{id}: approved");
        if (step == 3) continue;
        await Post("giamdoc", $"/Bookings/SignContract/{id}", new() { ["confirmed"] = "true" }); Check((await Booking(id)).ContractStatus == "da_duyet", $"Booking #{id}: cannot bypass Legal");
        await Post("qlbooking", $"/Bookings/UploadContract/{id}", fileField: "contractFile"); Check((await Booking(id)).ContractStatus == "cho_phap_ly", $"Booking #{id}: draft sent to Legal");
        if (step == 4) continue;
        if (step == 5 || step == 8)
        {
            await Post("phaply", $"/Legal/Review/{id}", new() { ["revision"] = "1", ["approve"] = "false", ["feedback"] = "[DEMO V4] Bổ sung điều khoản nghiệm thu và thời hạn bàn giao." });
            Check((await Booking(id)).ContractStatus == "phap_ly_tu_choi", $"Booking #{id}: Legal returns with feedback");
            if (step == 5) continue;
            await Post("booking1", $"/Bookings/UploadContract/{id}", fileField: "contractFile"); Check((await Booking(id)).ContractRevision == 2, "Assigned staff resubmits revision 2");
            Check((await Post("phaply", $"/Legal/Review/{id}", new() { ["revision"] = "1", ["approve"] = "true" })).StatusCode == HttpStatusCode.Conflict, "Stale Legal review blocked");
        }
        await Post("phaply", $"/Legal/Review/{id}", new() { ["revision"] = (await Booking(id)).ContractRevision.ToString(), ["approve"] = "true" }); Check((await Booking(id)).ContractStatus == "cho_ky", $"Booking #{id}: Legal approved");
        if (step == 6) continue;
        await Post("giamdoc", $"/Bookings/SignContract/{id}", new() { ["confirmed"] = "true" }); Check((await Booking(id)).ContractStatus == "da_ky", $"Booking #{id}: director signed");
        await Post("qlbooking", $"/Bookings/Start/{id}"); Check((await Booking(id)).Status == "dang_trien_khai", $"Booking #{id}: implementation started");
        if (step == 7) continue;
        await Post("qlbooking", $"/Bookings/Acceptance/{id}", new() { ["postLink"] = "https://example.test/demo-v4-deliverable" }, "acceptanceFile");
        Check((await Booking(id)).Status == "hoan_thanh" && (await Booking(id)).AcceptanceFileUrl != null, "Acceptance completes booking");
        var payment = new Dictionary<string, string> { ["kind"] = "manager", ["payeeId"] = people["qlbooking"].Id.ToString(), ["paid"] = "true", ["amount"] = "7000000" };
        Check(Denied(await Post("giamdoc", $"/Accounting/Payment/{id}", new(payment))), "Director cannot mark payments");
        await Post("ketoan", $"/Accounting/Payment/{id}", payment);
        await using (var db = Db()) Check(await db.BookingPayments.AnyAsync(p => p.BookingId == id && p.Kind == "manager" && p.IsPaid && p.Amount == 7000000m), "Accountant marks 7M manager share paid (demo only)");
    }
    for (int i = 0; i < 2; i++)
    {
        var title = "[DEMO V4] " + (i == 0 ? "Creator nội bộ - chờ review" : "Creator đối tác - quản lý đã duyệt");
        await Post("qlytuong", "/ManageIdea/CreateIdea", new() { ["Title"] = title, ["CampaignId"] = campaignId.ToString(), ["Deadline"] = DateTime.Today.AddDays(15).ToString("yyyy-MM-dd"), ["PrimaryStaffId"] = (i == 0 ? people["creator"].Id : -kols[0].Id).ToString(), ["ReviewerEmployeeId"] = people["qlytuong"].Id.ToString(), ["Insight"] = "Khách hàng cần cách dùng sản phẩm dễ hiểu.", ["Concept"] = "Một ngày cùng sản phẩm demo.", ["ContentDetails"] = "Video 30 giây: vấn đề, giải pháp, trải nghiệm.", ["ScriptText"] = "Mở đầu câu hỏi, giới thiệu sản phẩm, minh họa và kết thúc. Nội dung demo không đăng thật." });
        int id; await using (var db = Db()) id = await db.Ideas.Where(x => x.Title == title).Select(x => x.Id).SingleAsync();
        ideaIds.Add(id); await SaveManifest();
        await Post("qlytuong", "/ManageIdea/SubmitIdea", new() { ["id"] = id.ToString() });
        await using (var db = Db()) Check((await db.Ideas.FindAsync(id))!.Status == "review", $"Idea #{id}: submitted to manager review");
        if (i == 1)
        {
            await Post("qlytuong", "/ManageIdea/ReviewIdea", new() { ["id"] = id.ToString(), ["decision"] = "approve", ["feedback"] = "[DEMO V4] Nội dung đạt yêu cầu review." });
            await using var db = Db(); var idea = (await db.Ideas.FindAsync(id))!;
            Check(idea.Status == "approved" && idea.PrimaryKolId == kols[0].Id && idea.PrimaryStaffId == null, "External creator uses KOL record, not a new employee account");
        }
    }
    foreach (var (role, path) in new[] { ("giamdoc", "/Director/Dashboard"), ("giamdoc", "/Director/Approve"), ("giamdoc", "/Director/SignContract"), ("giamdoc", "/Director/BookingCampaign"), ("qlbooking", "/Bookings"), ("qlytuong", "/ManageIdea/Idea"), ("phaply", "/Legal"), ("ketoan", "/Accounting"), ("ketoan", "/Accounting/Export") })
        Check((await clients[role].GetAsync(path)).IsSuccessStatusCode, "Page renders: " + role + " " + path);
    Check(Denied(await clients["qlbooking"].GetAsync("/Accounting")), "Booking manager cannot access Accounting");
    Check(Denied(await clients["ketoan"].GetAsync("/Bookings")), "Accountant cannot access Booking editor");
    Check(Denied(await clients["giamdoc"].GetAsync("/Legal")), "Director cannot act as Legal");
    var staffProfile = "/Profile/ByEmployee/" + people["booking1"].Id;
    foreach (var role in new[] { "qlbooking", "ketoan", "phaply", "qlytuong" }) Check(Denied(await clients[role].GetAsync(staffProfile)), role + " cannot read another salary profile");
    foreach (var role in new[] { "giamdoc", "qlhcns", "hcns", "booking1" }) Check((await clients[role].GetAsync(staffProfile)).IsSuccessStatusCode, role + " can read permitted salary profile");
    complete = true;
}
finally
{
    await SaveManifest();
    foreach (var client in clients.Values) client.Dispose();
    var report = new StringBuilder("# Bộ dữ liệu DEMO V4\n\nDữ liệu giả lập được giữ lại trong DB HanaMedia để tự test. KHÔNG dùng cho vận hành thật.\n\n")
        .AppendLine("Đăng nhập: http://localhost:5028/Login\n").AppendLine("Mật khẩu chung (chỉ tài khoản demo): `" + password + "`\n")
        .AppendLine("| Tài khoản | Vai trò |\n|---|---|");
    foreach (var s in specs) report.AppendLine($"| demo_v4_{s.Item1} | {s.Item4} |");
    report.AppendLine("\n## Chiến dịch\n"); foreach (var c in campaigns) report.AppendLine($"- #{c.Id}: {c.Name}");
    report.AppendLine("\n## Booking\n\nMỗi Booking demo: 100 triệu; công ty 50 triệu, quỹ hoa hồng 10 triệu, cast 40 triệu. NV 1: 2 triệu; NV 2: 1 triệu; QL còn 7 triệu. Cast chia cho 3 KOL, có làm tròn đến 2 chữ số thập phân.\n");
    foreach (var kv in scenarios) report.AppendLine($"- #{kv.Value}: {kv.Key} — http://localhost:5028/Bookings/Details/{kv.Value}");
    report.AppendLine("\nÝ tưởng: " + string.Join(", ", ideaIds.Select(i => "#" + i)));
    report.AppendLine("\nLuồng mẫu #09 đã đi qua từ chối Pháp lý → sửa phiên bản → duyệt → ký → triển khai → nghiệm thu → Kế toán đánh dấu đã trả 7 triệu cho QL (chỉ mô phỏng, không chuyển tiền).\n");
    report.AppendLine($"Hoàn tất: {complete}. Kiểm tra đạt: {checks.Count}.\n\nBackup trước khi tạo: {backup}\n\nDữ liệu demo làm tăng các tổng dashboard/báo cáo. Không tự chạy lại script; không tự xóa dữ liệu. File này chứa mật khẩu và được Git bỏ qua.\n");
    foreach (var check in checks) report.AppendLine("- PASS: " + check);
    await File.WriteAllTextAsync(Path.Combine(artifactDir, "demo-v4-access.md"), report.ToString());
}
Console.WriteLine($"Completed persistent demo: {people.Count} accounts, {campaigns.Count} campaigns, {scenarios.Count} bookings, {ideaIds.Count} ideas. {checks.Count} checks passed. Credentials: tests/artifacts/demo-v4-access.md");

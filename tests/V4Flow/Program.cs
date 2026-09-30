using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Services.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Real HTTP + SQL Server integration test. NEVER writes fixtures into the configured database.
// Makes a verified COPY_ONLY backup and restores a uniquely named test database first.
var root = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(root, "HanaMedia.csproj"))) throw new Exception("Run from repository root.");
var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", true).AddUserSecrets("HanaMedia-V2-Local-Development").AddEnvironmentVariables().Build();
var source = new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
var testName = "HanaMedia_V4_Test_" + Guid.NewGuid().ToString("N");
var sourceName = source.InitialCatalog;
var server = new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = "master" };
await using var admin = new SqlConnection(server.ConnectionString);
await admin.OpenAsync();
async Task<object?> Scalar(string sql) { using var cmd = new SqlCommand(sql, admin); return await cmd.ExecuteScalarAsync(); }
var backupRoot = (string)(await Scalar("SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))"))!;
var dataRoot = (string)(await Scalar("SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))"))!;
var logRoot = (string)(await Scalar("SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))"))!;
var backup = Path.Combine(backupRoot, testName + "_source.bak");
using (var cmd = new SqlCommand($"BACKUP DATABASE [{sourceName.Replace("]", "]]")}] TO DISK=@file WITH COPY_ONLY,CHECKSUM", admin) { CommandTimeout=180 })
{ cmd.Parameters.AddWithValue("@file",backup); await cmd.ExecuteNonQueryAsync(); }
using (var cmd = new SqlCommand("RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM",admin) {CommandTimeout=180})
{ cmd.Parameters.AddWithValue("@file",backup);await cmd.ExecuteNonQueryAsync(); }
Console.WriteLine("Verified source backup: " + backup);
var files = new List<(string Name,string Type)>();
using (var cmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK=@file",admin))
{cmd.Parameters.AddWithValue("@file",backup);using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync())files.Add((reader.GetString(reader.GetOrdinal("LogicalName")),reader.GetString(reader.GetOrdinal("Type"))));}
using (var cmd = new SqlCommand {Connection=admin,CommandTimeout=180})
{
    cmd.Parameters.AddWithValue("@backup",backup);
    var moves=new List<string>();
    for(var i=0;i<files.Count;i++) {cmd.Parameters.AddWithValue("@name"+i,files[i].Name);cmd.Parameters.AddWithValue("@path"+i,Path.Combine(files[i].Type=="L"?logRoot:dataRoot,testName+"_"+i+(files[i].Type=="L"?".ldf":".mdf")));moves.Add($"MOVE @name{i} TO @path{i}");}
    cmd.CommandText=$"RESTORE DATABASE [{testName}] FROM DISK=@backup WITH "+string.Join(",",moves);await cmd.ExecuteNonQueryAsync();
}
source.InitialCatalog=testName;
ApplicationDbContext Db()=>new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(source.ConnectionString).Options);
int passed=0;
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
bool Denied(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Forbidden ||
    (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString.Contains("/AccessDenied", StringComparison.OrdinalIgnoreCase) == true);
Process? web=null;
var createdFiles = new List<string>();
try
{
    await using(var db=Db())
    {
        var old=await db.Bookings.Select(b=>new{b.Id,b.BookingPrice,b.ActualCost}).ToListAsync();
        await db.Database.MigrateAsync();
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(),"Migration applies to clone, no pending migration");
        var after=await db.Bookings.Select(b=>new{b.Id,b.BookingPrice,b.ActualCost}).ToListAsync();
        Check(old.SequenceEqual(after),"Migration preserves historical financial amounts");
    }
    var password="V4test!"+Guid.NewGuid().ToString("N");
    var people=new Dictionary<string,Employee>();
    await using(var db=Db())
    {
        foreach(var (key,role,dept) in new[]{("manager",AppRoles.BookingManager,"Booking"),("other",AppRoles.BookingManager,"Booking"),("staff",AppRoles.BookingStaff,"Booking"),("director",AppRoles.Director,"HCNS"),("legal",AppRoles.LegalStaff,"HCNS"),("accountant",AppRoles.Accountant,"HCNS"),("idea",AppRoles.IdeaManager,"Y_tuong"),("hr",AppRoles.HumanResourcesStaff,"HCNS"),("admin",AppRoles.AdminIT,"IT")})
        {
            var user=new User{Username="v4_"+key,Email="v4_"+key+"@example.test",Role=role,PasswordHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant(),Status="active",SecurityStamp=Guid.NewGuid().ToString("N")};
            var emp=new Employee{User=user,FullName="V4 "+key,Email=user.Email,Dob=new(1990,1,1),Phone="090"+Random.Shared.Next(1000000,9999999),Address="Test",Department=dept,Position=key,JoinedDate=DateOnly.FromDateTime(DateTime.Today),ContractType="thu_viec",Status="dang_lam_viec",IsManager=key is "manager" or "other" or "idea"};
            db.Employees.Add(emp);people[key]=emp;
        }
        await db.SaveChangesAsync();
    }
    int campaignId,pausedId;int[] kolIds;
    await using(var db=Db())
    {
        var campaign=new Campaign{Name="V4 campaign",Client="V4 client",ManagerEmployeeId=people["manager"].Id,StartDate=DateOnly.FromDateTime(DateTime.Today),EndDate=DateOnly.FromDateTime(DateTime.Today.AddDays(30)),Status="planning"};
        var paused=new Campaign{Name="V4 paused",Client="V4 client",ManagerEmployeeId=people["manager"].Id,StartDate=campaign.StartDate,EndDate=campaign.EndDate,Status="paused"};
        db.Campaigns.AddRange(campaign,paused);
        var kols=Enumerable.Range(1,3).Select(i=>new Kol{Name="V4 KOL "+i,ProfileLink="https://example.test/"+i,Location="Test",ContactInfo="Test",Status="tiem_nang",IsActive=true}).ToArray();db.Kols.AddRange(kols);
        await db.SaveChangesAsync();campaignId=campaign.Id;pausedId=paused.Id;kolIds=kols.Select(k=>k.Id).ToArray();
    }
    var start=new ProcessStartInfo("dotnet"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    start.ArgumentList.Add(Path.Combine(root,"bin","Release","net8.0","HanaMedia.dll"));
    start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["ASPNETCORE_URLS"]="http://127.0.0.1:15028";
    start.Environment["ConnectionStrings__DefaultConnection"]=source.ConnectionString;start.Environment["Security__IpRestriction__AllowLoopback"]="true";
    web=Process.Start(start)!;
    var logs=new StringBuilder();web.OutputDataReceived+=(_,e)=>{if(e.Data!=null)lock(logs)logs.AppendLine(e.Data);};web.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)lock(logs)logs.AppendLine(e.Data);};web.BeginOutputReadLine();web.BeginErrorReadLine();
    HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new CookieContainer()}){BaseAddress=new Uri("http://127.0.0.1:15028"),Timeout=TimeSpan.FromSeconds(30)};
    using var anon=Client();
    for(int i=0;i<40;i++){try{if((await anon.GetAsync("/Login")).IsSuccessStatusCode)break;}catch(HttpRequestException){}await Task.Delay(250);}
    static string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    async Task<HttpResponseMessage> Post(HttpClient client,string url,Dictionary<string,string> data,string? fileField=null,string? fileText=null)
    {
        var html=await client.GetStringAsync("/Profile");data["__RequestVerificationToken"]=Token(html);
        if(fileField!=null){var content=new MultipartFormDataContent();foreach(var pair in data)content.Add(new StringContent(pair.Value),pair.Key);content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(fileText??"%PDF-1.4\nV4 test")),fileField,"v4.pdf");return await client.PostAsync(url,content);}
        return await client.PostAsync(url,new FormUrlEncodedContent(data));
    }
    var clients=new Dictionary<string,HttpClient>();
    foreach(var key in people.Keys){var client=Client();var html=await client.GetStringAsync("/Login");var response=await client.PostAsync("/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"username","v4_"+key},{"password",password},{"__RequestVerificationToken",Token(html)}}));Check(response.StatusCode==HttpStatusCode.Redirect,"Login "+key);clients[key]=client;}
    var manager=clients["manager"];var director=clients["director"];var legal=clients["legal"];var accountant=clients["accountant"];var idea=clients["idea"];
    var campaignForm = new Dictionary<string,string> { ["Name"]="V4 forged campaign", ["Client"]="V4 client", ["Budget"]="100000000", ["ManagerEmployeeId"]=people["manager"].Id.ToString(), ["startDateStr"]=DateTime.Today.ToString("yyyy-MM-dd"), ["endDateStr"]=DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"), ["Status"]="running", ["ConfirmedAt"]="2026-01-01", ["ConfirmedByUserId"]=people["director"].UserId.ToString()! };
    campaignForm["CompletedAt"]="2026-01-01"; campaignForm["AcceptedAt"]="2026-01-01";
    campaignForm["CompletedByUserId"]=people["manager"].UserId.ToString()!; campaignForm["AcceptedByUserId"]=people["manager"].UserId.ToString()!;
    await Post(manager,"/Campaigns/Create",new(campaignForm));
    int forgedCampaignId;
    await using(var db=Db()) { var c=await db.Campaigns.SingleAsync(c=>c.Name=="V4 forged campaign"); forgedCampaignId=c.Id; Check(c.Status=="planning"&&c.ConfirmedAt==null&&c.ConfirmedByUserId==null,"Campaign creation ignores forged running/confirmation fields"); }
    await using(var db=Db()) { var c=(await db.Campaigns.FindAsync(forgedCampaignId))!; Check(c.CompletedAt==null&&c.CompletedByUserId==null&&c.AcceptedAt==null&&c.AcceptedByUserId==null,"Create cannot forge completion or payment-receipt stamps"); }
    await Post(manager,$"/Campaigns/Edit/{forgedCampaignId}",new(campaignForm));
    await using(var db=Db()) {var c=(await db.Campaigns.FindAsync(forgedCampaignId))!;Check(c.Status=="planning"&&c.ConfirmedAt==null,"Manager edit cannot self-start campaign");}
    Check((await idea.GetAsync($"/Campaigns/Details/{campaignId}")).StatusCode==HttpStatusCode.Redirect,"Idea cannot view unconfirmed campaign");
    Check((await idea.GetAsync($"/api/Campaigns/{campaignId}")).StatusCode==HttpStatusCode.NotFound,"Campaign API hides unconfirmed campaign");
    Check(Denied(await Post(manager,$"/Campaigns/Confirm/{campaignId}",new())),"Manager cannot confirm campaign");
    await Post(director,$"/Campaigns/Confirm/{campaignId}",new());
    DateTime? confirmedAt;
    await using(var db=Db()) {var c=(await db.Campaigns.FindAsync(campaignId))!;confirmedAt=c.ConfirmedAt;Check(c.Status=="running"&&confirmedAt!=null&&c.ConfirmedByUserId==people["director"].UserId,"Director confirmation atomically starts campaign and records approver");}
    await Post(director,$"/Campaigns/Confirm/{campaignId}",new());
    await using(var db=Db()) Check((await db.Campaigns.FindAsync(campaignId))!.ConfirmedAt==confirmedAt,"Repeated confirmation preserves original confirmation");
    var confirmedEdit = new Dictionary<string,string>(campaignForm) { ["Name"]="V4 campaign", ["Budget"]="0", ["Status"]="planning" };
    await Post(manager,$"/Campaigns/Edit/{campaignId}",confirmedEdit);
    await using(var db=Db()) {var c=(await db.Campaigns.FindAsync(campaignId))!;Check(c.Status=="running"&&c.ConfirmedAt==confirmedAt,"Manager edit cannot undo running/confirmation");}
    Check((await idea.GetAsync($"/Campaigns/Details/{campaignId}")).IsSuccessStatusCode,"Confirmed campaign visible to Ideas");
    Dictionary<string,string> BookingForm(int c)=>new(){{"CampaignId",c.ToString()},{"BookingPrice","100000000"},{"ActualCost","1"},{"JobDescription","V4 deliverable"},{"deadlineStr",DateTime.Today.AddDays(20).ToString("yyyy-MM-dd")},{"Status","dang_cho"},{"kolIds[0]",kolIds[0].ToString()},{"kolIds[1]",kolIds[1].ToString()},{"kolIds[2]",kolIds[2].ToString()}};
    async Task<int> BookingCount(){await using var db=Db();return await db.Bookings.CountAsync();}
    var beforeCount=await BookingCount();await Post(manager,"/Bookings/Create",BookingForm(pausedId));Check(await BookingCount()==beforeCount,"Server rejects non-running campaign");
    await Post(manager,"/Bookings/Create",BookingForm(campaignId));
    int bookingId;
    await using(var db=Db()){var b=await db.Bookings.Include(b=>b.BookingKols).OrderByDescending(b=>b.Id).FirstAsync();bookingId=b.Id;Check(b.FinanceVersion==1&&b.ActualCost==50000000m&&b.CommissionPool==10000000m&&b.CastPool==40000000m,"50/10/40 server calculation ignores forged actual cost");Check(b.BookingKols.Count==3&&b.BookingKols.Sum(k=>k.CastAmount)==b.CastPool,"Three KOL share 40% with exact rounding total");Check(b.ContractStatus=="nhap"&&!await db.BookingWages.AnyAsync(w=>w.BookingId==bookingId),"New Booking is draft without staff assignment");}
    async Task<Booking> Reload(){await using var db=Db();return await db.Bookings.Include(b=>b.BookingWages).FirstAsync(b=>b.Id==bookingId);}
    Check(Denied(await Post(clients["other"],$"/Bookings/SubmitApproval/{bookingId}",new())),"Other manager cannot submit owned booking");
    await Post(manager,$"/Bookings/UpdateWages/{bookingId}",new(){{"wages["+people["staff"].Id+"]","11000000"}});Check((await Reload()).BookingWages.Count==0,"Allocation over 10% pool rejected");
    await Post(manager,$"/Bookings/UpdateWages/{bookingId}",new(){{"wages["+people["staff"].Id+"]","3000000"}});Check((await Reload()).BookingWages.Sum(w=>w.AllocatedWage)==3000000m,"Assign staff and allocate from commission after create");
    var details=await manager.GetAsync($"/Bookings/Details/{bookingId}");Check(details.IsSuccessStatusCode,"Booking details Razor renders");
    await Post(manager,$"/Bookings/SubmitApproval/{bookingId}",new());Check((await Reload()).ContractStatus=="cho_duyet","Manager submits booking");
    await Post(director,$"/Bookings/Approve/{bookingId}",new());Check((await Reload()).ContractStatus=="da_duyet","Director approves booking");
    await Post(director,$"/Bookings/SignContract/{bookingId}",new(){{"confirmed","true"}});Check((await Reload()).ContractStatus=="da_duyet","Cannot sign before legal approval");
    await Post(manager,$"/Bookings/UploadContract/{bookingId}",new(),"contractFile");var pending=await Reload();Check(pending.ContractStatus=="cho_phap_ly"&&pending.ContractRevision==1,"Draft goes to legal, not directly to director");
    createdFiles.Add(pending.ContractFileUrl!);
    Check((await legal.GetAsync("/Legal")).IsSuccessStatusCode,"Legal queue renders");
    await Post(legal,$"/Legal/Review/{bookingId}",new(){{"revision","1"},{"approve","false"},{"feedback","Bổ sung điều khoản"}});Check((await Reload()).ContractStatus=="phap_ly_tu_choi","Legal returns contract with feedback");
    await Post(clients["staff"],$"/Bookings/UploadContract/{bookingId}",new(),"contractFile");pending=await Reload();createdFiles.Add(pending.ContractFileUrl!);Check(pending.ContractRevision==2,"Assigned staff can resubmit new version");
    Check((await Post(legal,$"/Legal/Review/{bookingId}",new(){{"revision","1"},{"approve","true"}})).StatusCode==HttpStatusCode.Conflict,"Stale legal decision rejected");
    await Post(legal,$"/Legal/Review/{bookingId}",new(){{"revision","2"},{"approve","true"}});Check((await Reload()).ContractStatus=="cho_ky","Legal approval releases signing");
    await Post(director,$"/Bookings/SignContract/{bookingId}",new(){{"confirmed","true"}});Check((await Reload()).ContractStatus=="da_ky","Director signs legal-approved revision");
    await Post(manager,$"/Bookings/UploadContract/{bookingId}",new(),"contractFile");Check((await Reload()).ContractRevision==2,"Signed contract cannot be replaced");
    Check((await accountant.GetAsync("/Accounting")).IsSuccessStatusCode,"Accounting page renders");
    Check(Denied(await Post(director,$"/Accounting/Payment/{bookingId}",new(){{"kind","manager"},{"payeeId",people["manager"].Id.ToString()},{"paid","true"},{"amount","7000000"}})),"Director accounting is read-only");
    await Post(accountant,$"/Accounting/Payment/{bookingId}",new(){{"kind","manager"},{"payeeId",people["manager"].Id.ToString()},{"paid","true"},{"amount","7000000"}});
    await using(var db=Db())Check(await db.BookingPayments.AnyAsync(p=>p.BookingId==bookingId&&p.Kind=="manager"&&p.IsPaid&&p.Amount==7000000m),"Accountant records manager residual, not duplicate total commission");
    await Post(manager,$"/Bookings/UpdateWages/{bookingId}",new(){{"wages["+people["staff"].Id+"]","4000000"}});Check((await Reload()).BookingWages.Sum(w=>w.AllocatedWage)==3000000m,"Paid allocation cannot change");
    Check((await accountant.GetAsync("/Accounting/Export")).IsSuccessStatusCode,"Accounting CSV export");
    await Post(manager,$"/Bookings/Acceptance/{bookingId}",new(),"acceptanceFile");var accepted=await Reload();createdFiles.Add(accepted.AcceptanceFileUrl!);Check(accepted.Status=="hoan_thanh"&&accepted.AcceptanceFileUrl!=null,"Acceptance completes signed booking");
    Check((await anon.GetAsync(accepted.ContractFileUrl)).StatusCode==HttpStatusCode.Redirect,"Documents require login");
    Check((await accountant.GetAsync(accepted.ContractFileUrl)).IsSuccessStatusCode,"Accountant can read signed contract");
    var external=new Dictionary<string,string>{{"Title","V4 External Creator"},{"CampaignId",campaignId.ToString()},{"Deadline",DateTime.Today.AddDays(15).ToString("yyyy-MM-dd")},{"PrimaryStaffId",(-kolIds[0]).ToString()},{"ReviewerEmployeeId",people["idea"].Id.ToString()}};
    await Post(idea,"/ManageIdea/CreateIdea",external);
    await using(var db=Db())Check(await db.Ideas.AnyAsync(i=>i.Title=="V4 External Creator"&&i.PrimaryKolId==kolIds[0]&&i.PrimaryStaffId==null),"External KOL Content Creator without employee/account conversion");
    Check((await idea.GetAsync("/ManageIdea/Idea")).IsSuccessStatusCode,"Idea workspace renders");
    var qrName="v4_test_"+Guid.NewGuid().ToString("N")+".png";var qrUrl="/private-qr/"+qrName;var qrRoot=Path.Combine(root,"App_Data","qrcodes");Directory.CreateDirectory(qrRoot);await File.WriteAllBytesAsync(Path.Combine(qrRoot,qrName),Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII="));createdFiles.Add(qrUrl);
    await using(var db=Db()){var user=await db.Users.FindAsync(people["staff"].UserId);user!.QrCodeUrl=qrUrl;await db.SaveChangesAsync();}
    Check((await anon.GetAsync(qrUrl)).StatusCode==HttpStatusCode.Redirect,"QR requires login");
    Check(Denied(await clients["admin"].GetAsync(qrUrl)),"AdminIT cannot read another employee QR");
    Check(Denied(await manager.GetAsync(qrUrl)),"Booking manager cannot read salary QR");
    Check((await clients["staff"].GetAsync(qrUrl)).IsSuccessStatusCode,"QR owner can view");
    Check((await clients["hr"].GetAsync(qrUrl)).IsSuccessStatusCode,"HR staff can view QR");
    Check((await director.GetAsync(qrUrl)).IsSuccessStatusCode,"Director can view QR");
    foreach(var path in new[]{"/Director/Dashboard","/Director/BookingCampaign","/Director/Config","/Director/SignContract","/Director/Approve","/Reports","/Campaigns"})Check((await director.GetAsync(path)).IsSuccessStatusCode,"Renders "+path);
    Check(Denied(await accountant.GetAsync("/Bookings")), "Accountant cannot edit business Booking screen");
    Check(Denied(await manager.GetAsync("/Accounting")), "Manager cannot open accounting");
    Check(Denied(await director.GetAsync("/Legal")), "Director cannot act as Legal");
    Check((await Post(accountant,$"/Accounting/Payment/{bookingId}",new(){{"kind","cast"},{"payeeId",kolIds[0].ToString()},{"paid","true"},{"amount","1"}})).StatusCode==HttpStatusCode.Conflict,"Forged payable amount rejected");
    await Post(accountant,$"/Accounting/Payment/{bookingId}",new(){{"kind","cast"},{"payeeId",kolIds[0].ToString()},{"paid","true"},{"amount","13333333.33"}});
    await using(var db=Db())Check(await db.BookingPayments.AnyAsync(p=>p.BookingId==bookingId&&p.Kind=="cast"&&p.IsPaid&&p.Amount==13333333.33m),"Cast payment preserves decimal cents");
    Check((await accountant.PostAsync($"/Accounting/Payment/{bookingId}",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"Payment requires anti-forgery token");
    Check((await clients["staff"].PutAsJsonAsync("/Profile/UpdateBankAccount",new{BankName="Test bank",AccountNumber="123456",AccountHolderName="TEST"})).StatusCode==HttpStatusCode.BadRequest,"Bank update requires anti-forgery token");
    var bankRequest = new HttpRequestMessage(HttpMethod.Put,"/Profile/UpdateBankAccount") {Content=JsonContent.Create(new{BankName="Test bank",AccountNumber="123456",AccountHolderName="TEST STAFF",EmployeeId=people["manager"].Id})};
    bankRequest.Headers.Add("RequestVerificationToken",Token(await clients["staff"].GetStringAsync("/Profile")));
    Check((await clients["staff"].SendAsync(bankRequest)).IsSuccessStatusCode,"Employee can update own salary account");
    await using(var db=Db())Check(await db.EmployeeBankAccounts.AnyAsync(b=>b.EmployeeId==people["staff"].Id&&b.AccountNumber=="123456")&&!await db.EmployeeBankAccounts.AnyAsync(b=>b.EmployeeId==people["manager"].Id),"Forged employee ID cannot edit another salary account");
    foreach(var key in new[]{"manager","admin","accountant","legal"})Check(Denied(await clients[key].GetAsync("/Profile/ByEmployee/"+people["staff"].Id)),key+" cannot read another bank profile");
    foreach(var key in new[]{"director","hr","staff"})Check((await clients[key].GetStringAsync("/Profile/ByEmployee/"+people["staff"].Id)).Contains("123456"),key+" can read permitted bank profile");
    using (var pixels = new Image<Rgba32>(20,20))
    using (var stream = new MemoryStream())
    {
        pixels.SaveAsPng(stream);
        var qrRequest = new HttpRequestMessage(HttpMethod.Put,"/Profile/UpdateUser") {Content=JsonContent.Create(new{QrCodeBase64="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray())})};
        qrRequest.Headers.Add("RequestVerificationToken",Token(await clients["staff"].GetStringAsync("/Profile")));
        Check((await clients["staff"].SendAsync(qrRequest)).IsSuccessStatusCode,"Real PNG QR upload succeeds with patched image library");
        await using var db=Db();var url=(await db.Users.FindAsync(people["staff"].UserId))!.QrCodeUrl!;
        createdFiles.Add(url);
        Check(url.StartsWith("/private-qr/"),"New QR upload stored outside public static files");
        Check((await clients["staff"].GetAsync(url)).IsSuccessStatusCode,"Uploaded QR can be read by owner");
        Check(Denied(await clients["admin"].GetAsync("/uploads/users/qrcodes/"+Path.GetFileName(url))),"Legacy static QR alias also enforces privacy");
    }

    await Post(director,"/Director/Config",new(){{"CompanyPercent","60"},{"CommissionPercent","20"},{"CastPercent","30"}});
    await using(var db=Db())Check(!await db.BusinessConfigs.AnyAsync(c=>c.ConfigKey=="company_percent"&&c.ConfigValue=="60"),"Configuration must total 100 percent");
    await Post(director,"/Director/Config",new(){{"CompanyPercent","60"},{"CommissionPercent","10"},{"CastPercent","30"}});
    Check((await Reload()).CastPool==40000000m,"Config update does not rewrite existing Booking snapshot");
    await Post(manager,"/Bookings/Create",BookingForm(campaignId));int secondBookingId;
    await using(var db=Db()){var b=await db.Bookings.OrderByDescending(b=>b.Id).FirstAsync();secondBookingId=b.Id;Check(b.FinanceVersion==1&&b.CompanyPercent==60m&&b.ActualCost==40000000m,"New Booking uses updated finance configuration");}
    await Post(director,"/Director/Config",new(){{"CompanyPercent","50"},{"CommissionPercent","10"},{"CastPercent","40"}});
    await Post(manager,$"/Bookings/Edit/{secondBookingId}",BookingForm(campaignId));
    await using(var db=Db())Check((await db.Bookings.FindAsync(secondBookingId))!.CompanyPercent==60m,"Editing Booking preserves its saved rates");
    await Post(manager,$"/Bookings/Start/{secondBookingId}",new());
    await using(var db=Db())Check((await db.Bookings.FindAsync(secondBookingId))!.Status!="dang_trien_khai","Cannot start before signed contract");
    var unauthorizedIdea = new Dictionary<string,string>(external) { ["Title"]="V4 hidden campaign",["CampaignId"]=pausedId.ToString() };
    await Post(idea,"/ManageIdea/CreateIdea",unauthorizedIdea);
    await using(var db=Db())Check(!await db.Ideas.AnyAsync(i=>i.Title=="V4 hidden campaign"),"Cannot create idea for unconfirmed campaign via forged POST");
    Check(!(await idea.GetStringAsync("/Tasks")).Contains("V4 paused"),"Task campaign dropdown hides unconfirmed campaigns");
    Check((await idea.GetAsync("/Reports?type=ideas")).IsSuccessStatusCode,"Content Creator report renders");
    // Campaign receipt is a separate lifecycle: never auto-complete Bookings or pay wages.
    async Task<Campaign> ReloadCampaign() { await using var db=Db(); return (await db.Campaigns.FindAsync(forgedCampaignId))!; }
    Check((await Post(manager,$"/Campaigns/Complete/{forgedCampaignId}",new())).StatusCode==HttpStatusCode.BadRequest,"Waiting campaign cannot skip to completion");
    Check((await Post(manager,$"/Campaigns/Accept/{forgedCampaignId}",new(){{"receivedPayment","true"}})).StatusCode==HttpStatusCode.BadRequest,"Waiting campaign cannot skip to receipt");
    await Post(director,$"/Campaigns/Confirm/{forgedCampaignId}",new());
    Check((await Post(manager,$"/Campaigns/Accept/{forgedCampaignId}",new(){{"receivedPayment","true"}})).StatusCode==HttpStatusCode.BadRequest,"Running campaign must be completed before receipt");
    foreach(var actor in new[]{director,clients["staff"],accountant,legal,idea})
    {
        Check(Denied(await Post(actor,$"/Campaigns/Complete/{forgedCampaignId}",new())),"Non-Booking-manager cannot complete campaign");
        Check(Denied(await Post(actor,$"/Campaigns/Accept/{forgedCampaignId}",new(){{"receivedPayment","true"}})),"Non-Booking-manager cannot acknowledge receipt");
    }
    Check((await manager.PostAsync($"/Campaigns/Complete/{forgedCampaignId}",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode==HttpStatusCode.BadRequest,"Campaign completion requires anti-forgery token");
    await Post(manager,$"/Campaigns/Complete/{forgedCampaignId}",new());
    var completedCampaign=await ReloadCampaign();
    Check(completedCampaign.Status=="completed"&&completedCampaign.CompletedAt!=null&&completedCampaign.CompletedByUserId==people["manager"].UserId&&completedCampaign.AcceptedAt==null,"Completion records actor/time but does not imply receipt");
    await Post(manager,$"/Campaigns/Complete/{forgedCampaignId}",new());
    Check((await ReloadCampaign()).CompletedAt==completedCampaign.CompletedAt,"Repeated completion preserves original timestamp");
    Check((await Post(manager,$"/Campaigns/Accept/{forgedCampaignId}",new())).StatusCode==HttpStatusCode.BadRequest,"Receipt requires explicit money-received confirmation");
    var completedEdit=new Dictionary<string,string>(campaignForm){["Status"]="accepted"};
    await Post(manager,$"/Campaigns/Edit/{forgedCampaignId}",completedEdit);
    Check((await ReloadCampaign()).Status=="completed"&&(await ReloadCampaign()).AcceptedAt==null,"Generic edit cannot forge accepted state or receipt timestamp");
    Check((await Post(manager,$"/Campaigns/Delete/{forgedCampaignId}",new())).StatusCode==HttpStatusCode.BadRequest,"Completed campaign cannot be cancelled");
    Check((await manager.PostAsync($"/Campaigns/Accept/{forgedCampaignId}",new FormUrlEncodedContent(new Dictionary<string,string>{{"receivedPayment","true"}}))).StatusCode==HttpStatusCode.BadRequest,"Receipt requires anti-forgery token");
    int outstandingBeforeReceipt;
    await using(var db=Db()) outstandingBeforeReceipt=(await new CompanyDashboardService(db).GetAsync("month",includeDirectorMetrics:true)).OutstandingCampaignCount;
    await Post(manager,$"/Campaigns/Accept/{forgedCampaignId}",new(){{"receivedPayment","true"}});
    var acceptedCampaign=await ReloadCampaign();
    await using(var db=Db()) {var metrics=await new CompanyDashboardService(db).GetAsync("month",includeDirectorMetrics:true);Check(metrics.OutstandingCampaignCount==outstandingBeforeReceipt-1&&!metrics.OutstandingCampaigns.Any(c=>c.Id==forgedCampaignId),"Campaign receipt automatically removes it from director outstanding balance");}
    Check(acceptedCampaign.Status=="accepted"&&acceptedCampaign.AcceptedAt!=null&&acceptedCampaign.AcceptedByUserId==people["manager"].UserId,"Manager confirms receipt and campaign becomes accepted");
    await Post(manager,$"/Campaigns/Accept/{forgedCampaignId}",new(){{"receivedPayment","true"}});
    Check((await ReloadCampaign()).AcceptedAt==acceptedCampaign.AcceptedAt,"Repeated receipt is idempotent");
    Check((await Post(manager,$"/Campaigns/Edit/{forgedCampaignId}",new(campaignForm))).StatusCode==HttpStatusCode.BadRequest,"Accepted campaign is read-only");
    Check((await Post(manager,$"/Campaigns/Delete/{forgedCampaignId}",new())).StatusCode==HttpStatusCode.BadRequest,"Accepted campaign cannot be cancelled");
    await Post(director,$"/Campaigns/Confirm/{forgedCampaignId}",new());
    Check((await ReloadCampaign()).Status=="accepted","Repeated director confirmation cannot reopen accepted campaign");
    await using(var db=Db()) Check(await db.SystemAuditLogs.CountAsync(a=>a.LogDetail.Contains("Campaign#"+forgedCampaignId+"]")&&(a.ActionType=="campaign_completed"||a.ActionType=="campaign_accepted"))==2,"Exactly one audit for completion and one for receipt");
    await Post(manager,"/Bookings/Create",BookingForm(forgedCampaignId));
    await using(var db=Db()) Check(!await db.Bookings.AnyAsync(b=>b.CampaignId==forgedCampaignId),"Accepted campaign cannot receive new Booking");
    // Optimistic locking prevents two stale contexts from overwriting a terminal state.
    await using(var first=Db()) await using(var second=Db())
    {
        var a=(await first.Campaigns.FindAsync(pausedId))!;var b=(await second.Campaigns.FindAsync(pausedId))!;
        a.Status="cancelled";await first.SaveChangesAsync();b.Status="completed";
        var conflicted=false;try{await second.SaveChangesAsync();}catch(DbUpdateConcurrencyException){conflicted=true;}
        Check(conflicted,"Stale campaign state update is rejected");
    }
    // Director metrics: saved finance snapshots, no staff double count; current
    // outstanding campaigns include earlier periods but not accepted/cancelled ones.
    await using(var db=Db())
    {
        var service=new CompanyDashboardService(db);
        var baseline=await service.GetAsync("month",includeDirectorMetrics:true);
        db.Bookings.Add(new Booking {ClientName="Dashboard cancelled",CampaignName="Test",Deadline=DateOnly.FromDateTime(DateTime.Today),BookingPrice=999999999m,ActualCost=500000000m,FinanceVersion=1,CommissionPercent=10m,CastPercent=40m,CompanyPercent=50m,Status="huy",ContractStatus="nhap",CreatedAt=DateTime.Now});
        db.Bookings.Add(new Booking {ClientName="Dashboard legacy",CampaignName="Test",Deadline=DateOnly.FromDateTime(DateTime.Today),BookingPrice=100m,ActualCost=50m,FinanceVersion=0,Status="dang_cho",ContractStatus="nhap",CreatedAt=DateTime.Now});
        for(int i=0;i<21;i++)db.Campaigns.Add(new Campaign{Name="Dashboard earlier period "+i,Client="Test",ManagerEmployeeId=people["manager"].Id,StartDate=new(2025,1,1),EndDate=new(2025,2,1),CreatedAt=new DateTime(2025,1,1),Status="planning"});
        await db.SaveChangesAsync();
        var current=await service.GetAsync("month",includeDirectorMetrics:true);
        Check(current.BookingRemuneration==baseline.BookingRemuneration&&current.KolRemuneration==baseline.KolRemuneration,"Cancelled and legacy Bookings do not inflate remuneration pools");
        Check(current.LegacyRemunerationExcludedCount==baseline.LegacyRemunerationExcludedCount+1,"Legacy amounts are flagged, not guessed from current config");
        Check(current.OutstandingCampaignCount==baseline.OutstandingCampaignCount+21&&current.OutstandingCampaigns.Count==20,"Outstanding total includes old campaigns, preview capped at 20");
        foreach(var period in new[]{"day","month","quarter"})
        {
            var vm=await service.GetAsync(period,includeDirectorMetrics:true);
            var finance=await db.Bookings.AsNoTracking().Where(b=>b.Status!="huy"&&b.FinanceVersion==1&&b.CreatedAt>=vm.RangeStart&&b.CreatedAt<vm.RangeEndExclusive).ToListAsync();
            Check(vm.BookingRemuneration==finance.Sum(b=>b.CommissionPool)&&vm.KolRemuneration==finance.Sum(b=>b.CastPool),"Director remuneration matches saved per-Booking rounded pools for "+period);
            Check(vm.OutstandingCampaignCount==current.OutstandingCampaignCount,"Outstanding balance remains all-period for "+period);
        }
        var shared=await service.GetAsync("month");
        Check(shared.BookingRemuneration==0&&shared.KolRemuneration==0&&shared.OutstandingCampaigns.Count==0,"Shared dashboard does not load director-only additions by default");
    }
    Check(!(await clients["admin"].GetStringAsync("/AdminIT/Dashboard")).Contains("data-metric=\"booking-remuneration\""),"AdminIT UI does not gain director remuneration cards");
    Check((await director.GetStringAsync("/Director/Dashboard")).Contains("data-metric=\"booking-remuneration\""),"Director UI renders remuneration cards");
    Check(!(await director.GetStringAsync("/Campaigns?pendingAcceptance=true")).Contains("/Campaigns/Details/"+forgedCampaignId+"\""),"Outstanding drilldown excludes accepted campaign");
    // Creation and initial wage allocation must succeed or fail as one unit.
    var wageKey = "wages[" + people["staff"].Id + "]";
    var countBeforeWages = await BookingCount();
    foreach (var invalidAmount in new[] { "11000000", "-1", "1.001", "not-money" })
    {
        var invalid = BookingForm(campaignId); invalid[wageKey] = invalidAmount;
        await Post(manager,"/Bookings/Create",invalid);
        Check(await BookingCount()==countBeforeWages,"Create rejects invalid wage without partial Booking: "+invalidAmount);
    }
    var invalidPerson=BookingForm(campaignId);invalidPerson["wages[-999999]"]="100";
    await Post(manager,"/Bookings/Create",invalidPerson);
    Check(await BookingCount()==countBeforeWages,"Create rejects wage to nonexistent employee");
    await using(var db=Db()){var employee=await db.Employees.FindAsync(people["staff"].Id);employee!.Status="ngung_hoat_dong";await db.SaveChangesAsync();}
    var inactivePerson=BookingForm(campaignId);inactivePerson[wageKey]="100";
    await Post(manager,"/Bookings/Create",inactivePerson);
    Check(await BookingCount()==countBeforeWages,"Create rejects wage to inactive employee");
    await using(var db=Db()){var employee=await db.Employees.FindAsync(people["staff"].Id);employee!.Status="dang_lam_viec";await db.SaveChangesAsync();}
    var initialWages=BookingForm(campaignId);initialWages[wageKey]="2500000.25";
    initialWages["wages["+people["hr"].Id+"]"]="500000";
    initialWages["wages["+people["idea"].Id+"]"]="750000";
    await Post(manager,"/Bookings/Create",initialWages);
    int crossDepartmentBookingId;
    await using(var db=Db())
    {
        var created=await db.Bookings.Include(b=>b.BookingWages).ThenInclude(w=>w.Employee).OrderByDescending(b=>b.Id).FirstAsync();
        crossDepartmentBookingId=created.Id;
        Check(await BookingCount()==countBeforeWages+1&&created.BookingWages.Single(w=>w.EmployeeId==people["staff"].Id).AllocatedWage==2500000.25m,"Create atomically saves selected employee and exact manual allocation");
        Check(created.BookingWages.Count==3&&created.BookingWages.Any(w=>w.EmployeeId==people["hr"].Id)&&created.BookingWages.Any(w=>w.EmployeeId==people["idea"].Id),"Creation accepts HR and Ideas employees, not only Booking role");
        Check(created.CommissionPool-created.BookingWages.Sum(w=>w.AllocatedWage)==6249999.75m,"Initial allocation preserves manager residual");
        Check(HanaMedia.Controllers.BookingPayables.For(created).Any(p=>p.Kind=="staff"&&p.PayeeId==people["hr"].Id&&p.Amount==500000m),"Accounting recognizes cross-department wage recipients");
        Check(await db.SystemAuditLogs.AnyAsync(a=>a.LogDetail.Contains("Booking#"+created.Id+"]")&&a.ActionType==AuditActions.WageChanged),"Initial wage allocation is audited");
    }
    await Post(manager,$"/Bookings/UpdateWages/{crossDepartmentBookingId}",new(){{"wages["+people["idea"].Id+"]","600000"},{"wages["+people["hr"].Id+"]","300000"}});
    await using(var db=Db())Check(await db.BookingWages.Where(w=>w.BookingId==crossDepartmentBookingId).SumAsync(w=>w.AllocatedWage)==900000m,"Details allocation accepts employees from other departments");
    Check(Denied(await clients["hr"].GetAsync($"/Bookings/Details/{crossDepartmentBookingId}")),"Wage participation does not grant unrelated Booking permissions");
    if (args.Contains("--ui"))
    {
        int filterStaffId;
        await using(var db=Db())
        {
            var extra=new Employee {FullName="V4 Đặng Ánh",Email="v4_filter@example.test",Phone="0900000099",Dob=new(1990,1,1),Address="Test",Department="HCNS",Position="Test",JoinedDate=DateOnly.FromDateTime(DateTime.Today),ContractType="thu_viec",Status="thu_viec"};
            db.Employees.Add(extra);await db.SaveChangesAsync();filterStaffId=extra.Id;
        }
        var uiCampaignForm=new Dictionary<string,string>(campaignForm){["Name"]="V4 browser campaign lifecycle"};
        await Post(manager,"/Campaigns/Create",uiCampaignForm);
        int uiCampaignId;await using(var db=Db())uiCampaignId=await db.Campaigns.Where(c=>c.Name=="V4 browser campaign lifecycle").Select(c=>c.Id).SingleAsync();
        await Post(director,$"/Campaigns/Confirm/{uiCampaignId}",new());
        var uiStart=new ProcessStartInfo("node"){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        uiStart.ArgumentList.Add("tests/v4-ui.cjs");
        uiStart.Environment["V4_PASSWORD"]=password;
        uiStart.Environment["V4_BOOKING_ID"]=bookingId.ToString();
        uiStart.Environment["V4_DRAFT_ID"]=secondBookingId.ToString();
        uiStart.Environment["V4_CAMPAIGN_ID"]=campaignId.ToString();
        uiStart.Environment["V4_CAMPAIGN_FLOW_ID"]=uiCampaignId.ToString();
        uiStart.Environment["V4_KOL_IDS"]=string.Join(",",kolIds);
        uiStart.Environment["V4_STAFF_ID"]=people["staff"].Id.ToString();
        uiStart.Environment["V4_FILTER_STAFF_ID"]=filterStaffId.ToString();
        using var ui=Process.Start(uiStart)!;var uiOutput=ui.StandardOutput.ReadToEndAsync();var uiError=ui.StandardError.ReadToEndAsync();await ui.WaitForExitAsync();Console.WriteLine(await uiOutput);Console.WriteLine(await uiError);Check(ui.ExitCode==0,"Browser desktop/mobile and interaction checks");
    }
    await using(var db=Db())Check(await db.SystemAuditLogs.CountAsync(a=>a.LogDetail.Contains("Booking#"+bookingId+"]"))>=8,"Workflow audit records persisted");
    foreach(var client in clients.Values)client.Dispose();
    Console.WriteLine($"COMPLETE: {passed} checks passed. Test database: {testName}");
}
finally
{
    if(web!=null&&!web.HasExited){web.Kill(entireProcessTree:true);await web.WaitForExitAsync();}
    foreach(var url in createdFiles.Where(u=>u!=null)){var folder=url.StartsWith("/private-qr/")?"qrcodes":"booking-documents";var path=Path.GetFullPath(Path.Combine(root,"App_Data",folder,Path.GetFileName(url)));if(path.StartsWith(Path.GetFullPath(Path.Combine(root,"App_Data"))+Path.DirectorySeparatorChar)&&File.Exists(path))File.Delete(path);}
    // This exact database was created by this run; never drop the configured source database.
    if(Regex.IsMatch(testName,"^HanaMedia_V4_Test_[a-f0-9]{32}$")&&testName!=sourceName)
    {SqlConnection.ClearAllPools();using var drop=new SqlCommand($"ALTER DATABASE [{testName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{testName}]",admin);await drop.ExecuteNonQueryAsync();Console.WriteLine("Removed isolated test database; source backup retained.");}
}

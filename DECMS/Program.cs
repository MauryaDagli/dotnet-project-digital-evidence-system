using DECMS.Data;
using DECMS.Models;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Register MongoDbContext (connects to local MongoDB instance: mongodb://127.0.0.1:27017/DECMS)
builder.Services.AddSingleton<MongoDbContext>();

// Register MongoDB Identity Stores
builder.Services.AddScoped<IUserStore<ApplicationUser>, MongoUserStore>();
builder.Services.AddScoped<IRoleStore<ApplicationRole>, MongoRoleStore>();

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 4;
})
.AddUserStore<MongoUserStore>()
.AddRoleStore<MongoRoleStore>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.AccessDeniedPath = "/Auth/AccessDenied";
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed MongoDB Database with Roles, Users, Cases, Evidence, Custody, Analyses, and Audit Logs
await DbSeeder.SeedRolesAndUsersAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

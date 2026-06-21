using DeploymentManager.Web.Data;
using DeploymentManager.Web.Hubs;
using DeploymentManager.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add Entity Framework SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add HTTP Client Factory
builder.Services.AddHttpClient();

// Add SignalR
builder.Services.AddSignalR();

// Add Data Protection for secure password encryption
builder.Services.AddDataProtection();

// Add Dependency Injection services
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IDeploymentService, DeploymentService>();

var app = builder.Build();

// Auto-initialize SQLite database schema and seed local test server
//using (var scope = app.Services.CreateScope())
//{
//    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
//    context.Database.EnsureCreated();

//    if (!context.Servers.Any())
//    {
//        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
//        context.Servers.Add(new DeploymentManager.Web.Models.Server
//        {
//            Id = Guid.Parse("d3b07384-d113-4a0b-80df-807d2c16130b"),
//            Name = "Localhost Agent (Test)",
//            IpAddress = "127.0.0.1",
//            Domain = "localhost",
//            IisSiteName = "Mock IIS Site",
//            Port = 5297,
//            AdminUsername = "Administrator",
//            AdminPasswordEncrypted = encryptionService.Encrypt("P@ssword123"),
//            ApiBaseUrl = "http://localhost:5297",
//            ApiKey = "SecretAgentApiKey12345",
//            Status = "Inactive",
//            CreatedAt = DateTime.UtcNow
//        });
//        context.SaveChanges();
//    }
//}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Map SignalR Hub
app.MapHub<DeploymentHub>("/deploymentHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

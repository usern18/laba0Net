using AuthService.Configuration;
using AuthService.Data;
using AuthService.Models;
using AuthService.Repositories;
using AuthService.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default must be configured.");

builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService.Services.AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.MigrateAsync();
    await SeedBootstrapAdminAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), builder.Configuration);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();

static async Task SeedBootstrapAdminAsync(AuthDbContext db, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
{
    var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
    var password = configuration["BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
        throw new InvalidOperationException("Set BootstrapAdmin:Email and a BootstrapAdmin:Password of at least 12 characters using user-secrets or environment variables.");

    var role = await db.Roles.SingleAsync(item => item.Name == "Admin");
    var admin = await db.Users.Include(user => user.UserRoles).SingleOrDefaultAsync(user => user.Email == email);
    if (admin is null)
    {
        admin = new User
        {
            Email = email,
            DisplayName = "Administrator",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        admin.UserRoles.Add(new UserRole { User = admin, Role = role });
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
    else if (!admin.UserRoles.Any(userRole => userRole.RoleId == role.Id))
    {
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
    }
}
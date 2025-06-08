using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using RealtimeChat.Components;
using RealtimeChat.Components.Account;
using RealtimeChat.Data;
using RealtimeChat.Hubs;
using RealtimeChat.Models;
using RealtimeChat.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register the HubConnection with authentication
builder.Services.AddScoped(sp =>
{
    var navigationManager = sp.GetRequiredService<NavigationManager>();
    var authenticationStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
    var hubConnectionBuilder = new HubConnectionBuilder()
        .WithUrl(navigationManager.ToAbsoluteUri("/chathub"), options =>
        {
            options.AccessTokenProvider = async () =>
            {
                var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    // In a server-side Blazor app with Identity, the authentication cookie is automatically sent
                    // with the request if the hub is behind the authentication middleware. No additional token
                    // is needed here, but we can still set an empty token to satisfy the option.
                    return null; // Cookie authentication handles this
                }
                return null;
            };
        })
        .Build();
    return hubConnectionBuilder;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddSignalR();
builder.Services.AddScoped<IChatService, ChatService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Add MudBlazor services.
builder.Services.AddMudServices();
builder.Services.AddScoped(x => new MudTheme());

builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = true;
});

// Build the application
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.MapHub<ChatHub>("/chathub");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await SeedDataAsync(context, userManager);
}

app.Run();

#region Seeding
// Database seeding method (unchanged)
async Task SeedDataAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
{
    await context.Database.EnsureCreatedAsync();

    // Seed users
    if (!context.Users.Any())
    {
        var users = new[]
        {
            new ApplicationUser { UserName = "alice@example.com", Email = "alice@example.com", DisplayName = "Alice Johnson" },
            new ApplicationUser { UserName = "bob@example.com", Email = "bob@example.com", DisplayName = "Bob Smith" },
            new ApplicationUser { UserName = "charlie@example.com", Email = "charlie@example.com", DisplayName = "Charlie Brown" }
        };

        foreach (var user in users)
        {
            await userManager.CreateAsync(user, "Password123!");
        }
    }

    // Seed chat rooms
    if (!context.ChatRooms.Any())
    {
        var firstUser = await userManager.FindByEmailAsync("alice@example.com");
        if (firstUser != null)
        {
            var rooms = new[]
            {
                new ChatRoom { Name = "General", Description = "General discussion", IsPrivate = false, CreatedById = firstUser.Id },
                new ChatRoom { Name = "Tech Talk", Description = "Technology discussions", IsPrivate = false, CreatedById = firstUser.Id },
                new ChatRoom { Name = "Random", Description = "Random conversations", IsPrivate = false, CreatedById = firstUser.Id }
            };

            context.ChatRooms.AddRange(rooms);
            await context.SaveChangesAsync();

            // Add all users to all public rooms
            var allUsers = await userManager.Users.ToListAsync();
            foreach (var room in rooms)
            {
                foreach (var user in allUsers)
                {
                    context.ChatRoomMembers.Add(new ChatRoomMember
                    {
                        UserId = user.Id,
                        ChatRoomId = room.Id,
                        IsAdmin = user.Id == firstUser.Id
                    });
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
#endregion
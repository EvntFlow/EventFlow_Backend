using EventFlow.Data;
using EventFlow.Identity;
using EventFlow.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PostmarkDotNet;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true);

// Add services to the container.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var adminDomain = builder.Configuration["Admin:Domain"];
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EventFlowEmployee", policy =>
    {
        policy.RequireAssertion(context =>
            context.User.FindFirst(ClaimTypes.Email)?.Value?.EndsWith($"@{adminDomain}")
                ?? false
        );
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("Google OAuth ClientId not found.");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
            ?? throw new InvalidOperationException("Google OAuth ClientSecret not found.");
    })
    .AddMicrosoftAccount(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Microsoft:ClientId"]
            ?? throw new InvalidOperationException("Microsoft OAuth ClientId not found.");
        options.ClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"]
            ?? throw new InvalidOperationException("Microsoft OAuth ClientSecret not found.");
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<Account>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddSingleton<IEmailSender<Account>, IdentityEmailSender>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailService, NoOpEmailService>();
}
else
{
    builder.Services.AddSingleton(new PostmarkClient(builder.Configuration["ApiKeys:Postmark"]
        ?? throw new InvalidOperationException("Postmark API key not found.")));
    builder.Services.AddSingleton<IEmailService, PostmarkEmailService>();
}

builder.Services.AddControllers();

builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PaymentService>();

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

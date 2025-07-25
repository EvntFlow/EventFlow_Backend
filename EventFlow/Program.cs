using EventFlow.Data;
using EventFlow.Identity;
using EventFlow.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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

builder.Services.AddCors(options =>
{
    var servers = builder.Configuration.GetSection("AllowedOrigins").Get<List<string>>();

    options.AddDefaultPolicy(policy =>
    {
        if (servers is not null && servers.Count > 0)
        {
            policy.WithOrigins([.. servers])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetIsOriginAllowedToAllowWildcardSubdomains();
        }
    });
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
    .AddIdentityCookies(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.ApplicationCookie?.Configure(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.None;
            });

            options.ExternalCookie?.Configure(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.None;
            });
        }
    });

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

builder.Services.AddSingleton<CloudinaryDotNet.ICloudinary>(new CloudinaryDotNet.Cloudinary(
    new CloudinaryDotNet.Account(
        builder.Configuration["ApiKeys:Cloudinary:CloudName"]
            ?? throw new InvalidOperationException("Cloudinary Cloud Name not found."),
        builder.Configuration["ApiKeys:Cloudinary:ApiKey"]
            ?? throw new InvalidOperationException("Cloudinary API key not found."),
        builder.Configuration["ApiKeys:Cloudinary:ApiSecret"]
            ?? throw new InvalidOperationException("Cloudinary API secret not found.")
    )
));
builder.Services.AddSingleton<IImageService, CloudinaryImageService>();

builder.Services.AddControllers();

builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<TicketService>();

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen();

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();

    app.UseSwagger(options =>
    {
        var servers = builder.Configuration.GetSection("Swagger:Servers").Get<List<string>>();

        if (servers is not null && servers.Count > 0)
        {
            options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
            {
                swaggerDoc.Servers = [.. servers.Select(s => new OpenApiServer { Url = s })];
            });
        }
    });
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("v1/swagger.json", "v1");
    });
}
else
{
    app.UseForwardedHeaders();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SkilllubLearnbox.Services;
using SkilllubLearnbox.Utilities;
using Supabase;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ConfigHelper>();

var configHelper = new ConfigHelper(builder.Configuration);
builder.Services.AddSingleton(configHelper);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Skilllub API",
        Version = "v1",
        Description = "API для образовательной платформы Skilllub"
    });

});
builder.Services.AddSingleton(provider =>
{
    var configHelper = provider.GetRequiredService<ConfigHelper>();
    return new Supabase.Client(configHelper.SupabaseUrl, configHelper.SupabaseKey);
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<CourseService>();
builder.Services.AddScoped<QuizService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(configHelper.AllowedOrigins)
              .WithMethods(configHelper.AllowedMethods)
              .WithHeaders(configHelper.AllowedHeaders)
              .AllowCredentials();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configHelper.JwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = configHelper.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = configHelper.JwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Skilllub API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Skilllub API Documentation";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
using ForestIQ.Domain;
using ForestIQ.Domain.Interface;
using ForestIQ.Extensions;
using ForestIQ.Infrastructure.Data;
using ForestIQ.Middleware;
using ForestIQ.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.SQLite;
using ForestIQ.Service.Jobs;

namespace ForestIQ
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowPolicy",
                    policy =>
                    {
                        policy
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowAnyOrigin();
                    });
            });

            builder.Services.AddMemoryCache();
            builder.Services.AddHttpContextAccessor();

            builder.Configuration.InitializeRuntime();

            var connectionString = builder.Configuration.GetConnectionString("ForestIqSqlite") ?? "Data Source=forestiq.db";
            builder.Services.AddDbContext<ForestIqDbContext>(options => options.UseSqlite(connectionString));

            // Register application services
            builder.Services.RegisterApplicationServices();

            #region Hangfire

            var rawHangfireConn = builder.Configuration.GetConnectionString("HangfireSqlite") ?? "Data Source=hangfire.db;";

            builder.Services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSQLiteStorage(rawHangfireConn, new SQLiteStorageOptions()));

            builder.Services.AddHangfireServer(options =>
            {
                options.WorkerCount = 1;
            });

            #endregion

            #region Jwt Authentication
            var key = Encoding.UTF8.GetBytes(Runtime.Jwt.Key);

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,

                            ValidIssuer =
                                Runtime.Jwt.Issuer,

                            ValidAudience =
                                Runtime.Jwt.Audience,

                            IssuerSigningKey =
                                new SymmetricSecurityKey(key)
                        };
                });
            #endregion

            #region Swagger Configuration
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer",
                    new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header
                    });

                options.AddSecurityRequirement(
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                             Array.Empty<string>()
                    }
                    });
            });

            builder.Services.AddAuthorization();

            #endregion

            var app = builder.Build();

            #region Migrate Database and Seed Super Admin

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ForestIqDbContext>();
                dbContext.Database.Migrate();

                // Seed Super Admin if no users exist
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                if (!await userService.HasAnyUsersAsync())
                {
                    var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
                    await userService.AddUserAsync(new Domain.DTO.User
                    {
                        Email = "admin@aislytics.com",
                        EncryptedPassword = encryptionService.Protect("@dminP@ssw0rd!"),
                        Role = "SuperAdmin"
                    });
                }
            }

            #endregion
            
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors("AllowPolicy");

            app.UseHttpsRedirection();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseMiddleware<ExceptionMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseHangfireDashboard("/api/hangfire", new DashboardOptions
            {
                Authorization = new[] { new HangfireAuthorizationFilter() }
            });
            
            app.ScheduleRecurringJobs();

            app.MapControllers();
            app.MapFallbackToFile("index.html");

            app.Run();
        }
    }
}

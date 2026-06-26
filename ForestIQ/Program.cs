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

            builder.Services.AddSingleton<IKerberosService, KerberosService>();
            builder.Services.AddSingleton<IAdConnectionCache, AdConnectionCache>();
            builder.Services.AddSingleton<IJwtService, JwtService>();
            builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
            builder.Services.AddScoped<IConfigureRepository, ConfigureRepository>();
            builder.Services.AddScoped<IConfigureService, ConfigureService>();
            builder.Services.AddScoped<IPowerShellService, PowerShellService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IPerformanceHistoryRepository, PerformanceHistoryRepository>();

            //var rawHangfireConn = builder.Configuration.GetConnectionString("HangfireSqlite") ?? "Data Source=hangfire.db;";
            //if (!rawHangfireConn.Contains(";")) 
            //{
            //    rawHangfireConn += ";";
            //}

            //builder.Services.AddHangfire(configuration => configuration
            //    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            //    .UseSimpleAssemblyNameTypeSerializer()
            //    .UseRecommendedSerializerSettings()
            //    .UseSQLiteStorage(rawHangfireConn, new SQLiteStorageOptions()));

            //builder.Services.AddHangfireServer(options =>
            //{
            //    options.WorkerCount = 1;
            //});

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

            var app = builder.Build();

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

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors("AllowPolicy");

            //app.UseHttpsRedirection();

            app.UseMiddleware<ExceptionMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            
            //app.UseHangfireDashboard("/hangfire", new DashboardOptions
            //{
            //    Authorization = new[] { new HangfireAuthorizationFilter() }
            //});
            //RecurringJob.AddOrUpdate<PerformancePollingJob>("poll-performance", job => job.ExecuteAsync(), "*/15 * * * *");

            app.MapControllers();

            app.Run();
        }
    }
}

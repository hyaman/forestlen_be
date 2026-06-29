using ForestIQ.Domain.Interface;
using ForestIQ.Infrastructure.Data;
using ForestIQ.Service;
using Microsoft.Extensions.DependencyInjection;

namespace ForestIQ.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection RegisterApplicationServices(this IServiceCollection services)
        {
            // Singletons
            services.AddSingleton<IKerberosService, KerberosService>();
            services.AddSingleton<IAdConnectionCache, AdConnectionCache>();
            services.AddSingleton<IJwtService, JwtService>();
            services.AddSingleton<IEncryptionService, EncryptionService>();

            // Scoped Services & Repositories
            services.AddScoped<IConfigureRepository, ConfigureRepository>();
            services.AddScoped<IConfigureService, ConfigureService>();
            services.AddScoped<IPowerShellService, PowerShellService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPerformanceHistoryRepository, PerformanceHistoryRepository>();
            services.AddScoped<IRefreshHistoryRepository, RefreshHistoryRepository>();
            services.AddScoped<IRefreshHistoryService, RefreshHistoryService>();

            services.AddScoped<ForestIQ.Domain.Interface.Licensing.IRsaHelper, ForestIQ.Service.Licensing.RsaHelper>();
            services.AddScoped<ForestIQ.Domain.Interface.Licensing.ILicenseGenerator, ForestIQ.Service.Licensing.LicenseGenerator>();
            services.AddScoped<ForestIQ.Domain.Interface.Licensing.ILicenseValidator, ForestIQ.Service.Licensing.LicenseValidator>();
            services.AddScoped<ForestIQ.Domain.Interface.Licensing.ILicenseService, ForestIQ.Service.Licensing.LicenseService>();

            return services;
        }
    }
}

using ForestIQ.Domain.DTO;

namespace ForestIQ.Domain.Interface
{
    public interface IConfigureService
    {
        Task<ConfigureResponse> ConfigureAsync(ConfigureRequest request);
        Task<GetConfigurationResponse> GetConfigurationAsync();
        Task<ConfigureResponse> DeleteConfigurationAsync();
    }
}

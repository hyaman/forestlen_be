using System.Threading.Tasks;
using ForestIQ.Domain.DTO.IdentityXRay;

namespace ForestIQ.Domain.Interface
{
    public interface IIdentityXRayService
    {
        Task<DomainPostureDto?> GetDomainPostureAsync(IdentityXRayFilterRequest filter);
        Task<UserRiskDto?> GetUserRiskAsync(IdentityXRayFilterRequest filter);
        Task<GroupRiskDto?> GetGroupRiskAsync(IdentityXRayFilterRequest filter);
        Task<ComputerRiskDto?> GetComputerRiskAsync(IdentityXRayFilterRequest filter);
        Task<AclExposureDto?> GetAclExposureAsync(IdentityXRayFilterRequest filter);
        Task<GpoExposureDto?> GetGpoExposureAsync(IdentityXRayFilterRequest filter);
        Task<LogonIntelligenceDto?> GetLogonIntelligenceAsync(IdentityXRayFilterRequest filter);
    }
}

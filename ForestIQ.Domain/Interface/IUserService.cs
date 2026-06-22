using ForestIQ.Domain.DTO;

namespace ForestIQ.Domain.Interface
{
    public interface IUserService
    {
        Task<User?> GetUserByEmailAsync(string email);
        Task<bool> HasAnyUsersAsync();
        Task AddUserAsync(User user);
    }
}

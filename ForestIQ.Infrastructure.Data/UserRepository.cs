using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace ForestIQ.Infrastructure.Data
{
    public class UserRepository : IUserRepository
    {
        private readonly ForestIqDbContext _dbContext;

        public UserRepository(ForestIqDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<bool> HasAnyUsersAsync()
        {
            return await _dbContext.Users.AnyAsync();
        }

        public async Task AddUserAsync(User user)
        {
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
        }
    }
}

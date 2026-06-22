namespace ForestIQ.Domain.DTO
{
    public class User
    {
        public long Id { get; set; }
        
        public string Email { get; set; } = string.Empty;
        
        public string EncryptedPassword { get; set; } = string.Empty;
        
        public string Role { get; set; } = "SuperAdmin";
    }
}

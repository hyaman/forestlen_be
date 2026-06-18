namespace ForestIQ.Domain.DTO
{
    public class ConfigureResponse
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public long? ConfigurationId { get; set; }
    }
}

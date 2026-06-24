namespace ForestIQ.Domain.DTO
{
    public class DashboardFilterRequest
    {
        public string TargetDc { get; set; } = "All";
        public string Forest { get; set; } = "All";
        public string Domain { get; set; } = "All";
        public string Site { get; set; } = "All";
        public string Health { get; set; } = "All";
        public bool RefreshView { get; set; } = false;

        // Specific to auth-summary, defaults to 24
        public int LookBackHours { get; set; } = 24;
    }
}

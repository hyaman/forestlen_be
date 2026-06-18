namespace ForestIQ.Domain.Interface
{
    public interface IKerberosService
    {
        string BuildPrincipal(string domainName, string userName);

        string BuildCachePath(string sessionKey);

        bool IsIpAddress(string host);

        Task<(bool Success, string? Error)> AcquireTicketAsync(
            string domainName,
            string userName,
            string password,
            string cachePath);

        void DestroyTicket(string? cachePath);
    }
}

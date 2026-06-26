using Hangfire.Dashboard;

namespace ForestIQ.Middleware
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // By default, Hangfire only allows local requests.
            // Returning true here allows external requests (e.g. accessing it from a browser via a Docker host port).
            return true;
        }
    }
}

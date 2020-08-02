using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace SmartHomeGateway.Utils
{
    public class DashboardNoAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext dashboardContext)
        {
            return true;
        }
    }
}

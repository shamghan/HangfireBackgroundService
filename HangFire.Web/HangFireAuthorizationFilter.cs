using Hangfire.Dashboard;
using System.Diagnostics.CodeAnalysis;

namespace HangFire.Web
{
    public class HangFireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context)
        {
            return true;
        }
    }
}

using System.Security.Claims;

namespace MarketplaceAPI.Services
{
    public static class ClaimsExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            return Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }

        public static Guid? GetCompanyId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue("companyId");
            return string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);
        }
    }
}
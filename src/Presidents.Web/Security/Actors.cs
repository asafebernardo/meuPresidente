using System.Security.Claims;
using Presidents.Application.Security;

namespace Presidents.Web.Security;

public static class Actors
{
    public static ActorContext From(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return ActorContext.Anonymous;
        return new ActorContext(user.FindFirstValue(ClaimTypes.NameIdentifier), AppRoles.All.Where(user.IsInRole).ToArray());
    }
}

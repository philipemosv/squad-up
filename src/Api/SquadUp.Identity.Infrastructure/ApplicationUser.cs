using Microsoft.AspNetCore.Identity;

namespace SquadUp.Identity.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }
}

using Microsoft.AspNetCore.Identity;

namespace SquadUp.Identity.Infrastructure;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SquadUp.Identity.Application;

namespace SquadUp.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid>(options)
{
    public const string SchemaName = "identity";
    public const string MigrationsHistoryTable = "migration_history";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(SchemaName);

        ConfigureUsers(builder);
        ConfigureRoles(builder);
        ConfigureUserClaims(builder);
        ConfigureUserLogins(builder);
        ConfigureUserRoles(builder);
        ConfigureUserTokens(builder);
        ConfigureRoleClaims(builder);
    }

    private static void ConfigureUsers(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id).HasName("pk_users");
            entity.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(user => user.UserName).HasColumnName("user_name").HasMaxLength(256);
            entity.Property(user => user.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256);
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(256);
            entity.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256);
            entity.Property(user => user.EmailConfirmed).HasColumnName("email_confirmed");
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash");
            entity.Property(user => user.SecurityStamp).HasColumnName("security_stamp");
            entity.Property(user => user.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.Property(user => user.PhoneNumber).HasColumnName("phone_number");
            entity.Property(user => user.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            entity.Property(user => user.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            entity.Property(user => user.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(user => user.LockoutEnabled).HasColumnName("lockout_enabled");
            entity.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count");
            entity.HasIndex(user => user.NormalizedUserName)
                .IsUnique()
                .HasDatabaseName("ux_users_normalized_user_name");
            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("ix_users_normalized_email");
        });
    }

    private static void ConfigureRoles(ModelBuilder builder)
    {
        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.Id).HasName("pk_roles");
            entity.Property(role => role.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(role => role.Name).HasColumnName("name").HasMaxLength(256);
            entity.Property(role => role.NormalizedName).HasColumnName("normalized_name").HasMaxLength(256);
            entity.Property(role => role.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            entity.HasIndex(role => role.NormalizedName)
                .IsUnique()
                .HasDatabaseName("ux_roles_normalized_name");
            entity.HasData(
                CreateRole(ApplicationRoleDefaults.PlayerId, SquadUpRoles.Player),
                CreateRole(ApplicationRoleDefaults.ModeratorId, SquadUpRoles.Moderator),
                CreateRole(ApplicationRoleDefaults.AdminId, SquadUpRoles.Admin));
        });
    }

    private static ApplicationRole CreateRole(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = null
    };

    private static void ConfigureUserClaims(ModelBuilder builder)
    {
        builder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("user_claims");
            entity.HasKey(claim => claim.Id).HasName("pk_user_claims");
            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.UserId).HasColumnName("user_id");
            entity.Property(claim => claim.ClaimType).HasColumnName("claim_type");
            entity.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(claim => claim.UserId)
                .HasConstraintName("fk_user_claims_users_user_id");
            entity.HasIndex(claim => claim.UserId).HasDatabaseName("ix_user_claims_user_id");
        });
    }

    private static void ConfigureUserLogins(ModelBuilder builder)
    {
        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("user_logins");
            entity.HasKey(login => new { login.LoginProvider, login.ProviderKey })
                .HasName("pk_user_logins");
            entity.Property(login => login.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
            entity.Property(login => login.ProviderKey).HasColumnName("provider_key").HasMaxLength(256);
            entity.Property(login => login.ProviderDisplayName).HasColumnName("provider_display_name");
            entity.Property(login => login.UserId).HasColumnName("user_id");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(login => login.UserId)
                .HasConstraintName("fk_user_logins_users_user_id");
            entity.HasIndex(login => login.UserId).HasDatabaseName("ix_user_logins_user_id");
        });
    }

    private static void ConfigureUserRoles(ModelBuilder builder)
    {
        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId })
                .HasName("pk_user_roles");
            entity.Property(userRole => userRole.UserId).HasColumnName("user_id");
            entity.Property(userRole => userRole.RoleId).HasColumnName("role_id");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(userRole => userRole.UserId)
                .HasConstraintName("fk_user_roles_users_user_id");
            entity.HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(userRole => userRole.RoleId)
                .HasConstraintName("fk_user_roles_roles_role_id");
            entity.HasIndex(userRole => userRole.RoleId).HasDatabaseName("ix_user_roles_role_id");
        });
    }

    private static void ConfigureUserTokens(ModelBuilder builder)
    {
        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("user_tokens");
            entity.HasKey(token => new { token.UserId, token.LoginProvider, token.Name })
                .HasName("pk_user_tokens");
            entity.Property(token => token.UserId).HasColumnName("user_id");
            entity.Property(token => token.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
            entity.Property(token => token.Name).HasColumnName("name").HasMaxLength(128);
            entity.Property(token => token.Value).HasColumnName("value");
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .HasConstraintName("fk_user_tokens_users_user_id");
        });
    }

    private static void ConfigureRoleClaims(ModelBuilder builder)
    {
        builder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("role_claims");
            entity.HasKey(claim => claim.Id).HasName("pk_role_claims");
            entity.Property(claim => claim.Id).HasColumnName("id");
            entity.Property(claim => claim.RoleId).HasColumnName("role_id");
            entity.Property(claim => claim.ClaimType).HasColumnName("claim_type");
            entity.Property(claim => claim.ClaimValue).HasColumnName("claim_value");
            entity.HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(claim => claim.RoleId)
                .HasConstraintName("fk_role_claims_roles_role_id");
            entity.HasIndex(claim => claim.RoleId).HasDatabaseName("ix_role_claims_role_id");
        });
    }
}

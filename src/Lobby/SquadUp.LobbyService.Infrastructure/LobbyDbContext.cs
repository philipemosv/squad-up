using Microsoft.EntityFrameworkCore;
using SquadUp.LobbyService.Domain;

namespace SquadUp.LobbyService.Infrastructure;

public sealed class LobbyDbContext(DbContextOptions<LobbyDbContext> options) : DbContext(options)
{
    public const string SchemaName = "lobby";
    public const string MigrationsHistoryTable = "migration_history";

    public DbSet<Lobby> Lobbies => Set<Lobby>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaName);

        ConfigureCatalog(modelBuilder);
        ConfigureLobbies(modelBuilder);
    }

    private static void ConfigureCatalog(ModelBuilder builder)
    {
        builder.Entity<LobbyCatalogEntry>(entity =>
        {
            entity.ToTable("game_catalog");
            entity.HasKey(game => game.Id).HasName("pk_game_catalog");
            entity.Property(game => game.Id).HasColumnName("id").HasMaxLength(RankRequirement.MaxGameIdLength);
            entity.Property(game => game.Name).HasColumnName("name").HasMaxLength(64);
            entity.Property(game => game.IsActive).HasColumnName("is_active");
            entity.HasData(new
            {
                Id = LobbyCatalog.Dota2GameId,
                Name = LobbyCatalog.Dota2GameName,
                IsActive = true
            });
        });

        builder.Entity<LobbyRankTierEntry>(entity =>
        {
            entity.ToTable("rank_tiers");
            entity.HasKey(tier => new { tier.GameId, tier.TierId }).HasName("pk_rank_tiers");
            entity.Property(tier => tier.GameId).HasColumnName("game_id").HasMaxLength(RankRequirement.MaxGameIdLength);
            entity.Property(tier => tier.TierId).HasColumnName("tier_id").HasMaxLength(32);
            entity.Property(tier => tier.Name).HasColumnName("name").HasMaxLength(64);
            entity.Property(tier => tier.Ordinal).HasColumnName("ordinal");
            entity.Property(tier => tier.IsActive).HasColumnName("is_active");
            entity.HasOne<LobbyCatalogEntry>()
                .WithMany()
                .HasForeignKey(tier => tier.GameId)
                .HasConstraintName("fk_rank_tiers_game_catalog_game_id");
            entity.HasIndex(tier => new { tier.GameId, tier.Ordinal })
                .IsUnique()
                .HasDatabaseName("ux_rank_tiers_game_id_ordinal");
            entity.ToTable(table => table.HasCheckConstraint("ck_rank_tiers_ordinal_positive", "ordinal > 0"));
            entity.HasData(LobbyCatalog.Dota2RankTiers.Select(tier => new
            {
                GameId = LobbyCatalog.Dota2GameId,
                TierId = tier.TierId,
                Name = tier.Name,
                Ordinal = tier.Ordinal,
                IsActive = true
            }));
        });
    }

    private static void ConfigureLobbies(ModelBuilder builder)
    {
        builder.Entity<Lobby>(entity =>
        {
            entity.ToTable("lobbies", table =>
            {
                table.HasCheckConstraint(
                    "ck_lobbies_capacity_range",
                    $"capacity >= {Lobby.MinimumCapacity} AND capacity <= {Lobby.MaximumCapacity}");
                table.HasCheckConstraint(
                    "ck_lobbies_member_count_range",
                    "members_count >= 0 AND members_count <= capacity");
                table.HasCheckConstraint("ck_lobbies_rank_range", "minimum_ordinal > 0 AND (maximum_ordinal IS NULL OR maximum_ordinal >= minimum_ordinal)");
                table.HasCheckConstraint("ck_lobbies_status", "status IN ('Recruiting', 'Full', 'Provisioning', 'Ready', 'Cancelled', 'Completed', 'Expired')");
            });
            entity.HasKey(lobby => lobby.Id).HasName("pk_lobbies");
            entity.Ignore(lobby => lobby.CompletedEvents);
            entity.Ignore(lobby => lobby.Members);
            entity.Property(lobby => lobby.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(lobby => lobby.OwnerPlayerId).HasColumnName("owner_player_id");
            entity.Property(lobby => lobby.Capacity).HasColumnName("capacity");
            entity.Property(lobby => lobby.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
            entity.Property(lobby => lobby.MembersCount).HasField("membersCount").HasColumnName("members_count");
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
            entity.OwnsOne(lobby => lobby.RankRequirement, requirement =>
            {
                requirement.Property(value => value.GameId).HasColumnName("game_id").HasMaxLength(RankRequirement.MaxGameIdLength);
                requirement.Property(value => value.MinimumOrdinal).HasColumnName("minimum_ordinal");
                requirement.Property(value => value.MaximumOrdinal).HasColumnName("maximum_ordinal");
            });
            entity.HasMany<LobbyMember>("members")
                .WithOne()
                .HasForeignKey("LobbyId")
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation("members").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<LobbyMember>(entity =>
        {
            entity.ToTable("lobby_members", table =>
            {
                table.HasCheckConstraint("ck_lobby_members_discord_user_id", "char_length(discord_user_id) BETWEEN 1 AND 32");
                table.HasCheckConstraint("ck_lobby_members_display_name", "char_length(display_name) BETWEEN 1 AND 32");
                table.HasCheckConstraint("ck_lobby_members_rank_ordinal_positive", "rank_ordinal > 0");
            });
            entity.HasKey("LobbyId", nameof(LobbyMember.PlayerId)).HasName("pk_lobby_members");
            entity.Property<Guid>("LobbyId").HasColumnName("lobby_id");
            entity.Property(member => member.PlayerId).HasColumnName("player_id");
            entity.Property(member => member.DiscordUserId).HasColumnName("discord_user_id").HasMaxLength(LobbyMember.MaxDiscordUserIdLength);
            entity.Property(member => member.DisplayName).HasColumnName("display_name").HasMaxLength(LobbyMember.MaxDisplayNameLength);
            entity.OwnsOne(member => member.Rank, rank =>
            {
                rank.Property(value => value.GameId).HasColumnName("rank_game_id").HasMaxLength(RankRequirement.MaxGameIdLength);
                rank.Property(value => value.Ordinal).HasColumnName("rank_ordinal");
            });
        });
    }
}

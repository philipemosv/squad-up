using Microsoft.EntityFrameworkCore;
using SquadUp.Profile.Domain;

namespace SquadUp.Profile.Infrastructure;

public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    public const string SchemaName = "profile";
    public const string MigrationsHistoryTable = "migration_history";

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();

    public DbSet<PlayerGame> PlayerGames => Set<PlayerGame>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<RankTier> RankTiers => Set<RankTier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaName);

        ConfigureGames(modelBuilder);
        ConfigureRankTiers(modelBuilder);
        ConfigurePlayerProfiles(modelBuilder);
        ConfigurePlayerGames(modelBuilder);
    }

    private static void ConfigureGames(ModelBuilder builder)
    {
        builder.Entity<Game>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(game => game.Id).HasName("pk_games");
            entity.Property(game => game.Id).HasColumnName("id").HasMaxLength(Game.MaxIdLength);
            entity.Property(game => game.Name).HasColumnName("name").HasMaxLength(Game.MaxNameLength);
            entity.Property(game => game.IsActive).HasColumnName("is_active");
            entity.HasData(new { Id = Dota2Catalog.GameId, Name = Dota2Catalog.GameName, IsActive = true });
        });
    }

    private static void ConfigureRankTiers(ModelBuilder builder)
    {
        builder.Entity<RankTier>(entity =>
        {
            entity.ToTable("rank_tiers");
            entity.HasKey(tier => new { tier.GameId, tier.TierId }).HasName("pk_rank_tiers");
            entity.Property(tier => tier.GameId).HasColumnName("game_id").HasMaxLength(Game.MaxIdLength);
            entity.Property(tier => tier.TierId).HasColumnName("tier_id").HasMaxLength(RankTier.MaxTierIdLength);
            entity.Property(tier => tier.Name).HasColumnName("name").HasMaxLength(RankTier.MaxNameLength);
            entity.Property(tier => tier.Ordinal).HasColumnName("ordinal");
            entity.Property(tier => tier.IsActive).HasColumnName("is_active");
            entity.HasOne<Game>()
                .WithMany()
                .HasForeignKey(tier => tier.GameId)
                .HasConstraintName("fk_rank_tiers_games_game_id");
            entity.HasIndex(tier => new { tier.GameId, tier.Ordinal })
                .IsUnique()
                .HasDatabaseName("ux_rank_tiers_game_id_ordinal");
            entity.HasData(Dota2Catalog.RankTiers.Select(tier => new
            {
                GameId = Dota2Catalog.GameId,
                TierId = tier.TierId,
                Name = tier.Name,
                Ordinal = tier.Ordinal,
                IsActive = true
            }));
        });
    }

    private static void ConfigurePlayerProfiles(ModelBuilder builder)
    {
        builder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("player_profiles");
            entity.HasKey(profile => profile.PlayerId).HasName("pk_player_profiles");
            entity.Property(profile => profile.PlayerId).HasColumnName("player_id").ValueGeneratedNever();
            entity.Property(profile => profile.Nickname)
                .HasColumnName("nickname")
                .HasMaxLength(PlayerProfile.MaxNicknameLength);
            entity.Property(profile => profile.TimeZoneId)
                .HasColumnName("time_zone_id")
                .HasMaxLength(PlayerProfile.MaxTimeZoneIdLength);
            entity.Property(profile => profile.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
        });
    }

    private static void ConfigurePlayerGames(ModelBuilder builder)
    {
        builder.Entity<PlayerGame>(entity =>
        {
            entity.ToTable("player_games");
            entity.HasKey(playerGame => new { playerGame.PlayerId, playerGame.GameId })
                .HasName("pk_player_games");
            entity.Property(playerGame => playerGame.PlayerId).HasColumnName("player_id");
            entity.Property(playerGame => playerGame.GameId)
                .HasColumnName("game_id")
                .HasMaxLength(Game.MaxIdLength);
            entity.Property(playerGame => playerGame.RankTierId)
                .HasColumnName("rank_tier_id")
                .HasMaxLength(RankTier.MaxTierIdLength);
            entity.Property(playerGame => playerGame.Region).HasColumnName("region").HasMaxLength(8);
            entity.Property(playerGame => playerGame.VerifiedAtUtc).HasColumnName("verified_at_utc");
            entity.HasOne<PlayerProfile>()
                .WithMany()
                .HasForeignKey(playerGame => playerGame.PlayerId)
                .HasConstraintName("fk_player_games_player_profiles_player_id");
            entity.HasOne<Game>()
                .WithMany()
                .HasForeignKey(playerGame => playerGame.GameId)
                .HasConstraintName("fk_player_games_games_game_id");
            entity.HasOne<RankTier>()
                .WithMany()
                .HasForeignKey(playerGame => new { playerGame.GameId, playerGame.RankTierId })
                .HasConstraintName("fk_player_games_rank_tiers_game_id_rank_tier_id");
            entity.HasIndex(playerGame => playerGame.GameId).HasDatabaseName("ix_player_games_game_id");
            entity.HasIndex(playerGame => new { playerGame.GameId, playerGame.RankTierId })
                .HasDatabaseName("ix_player_games_game_id_rank_tier_id");
        });
    }
}

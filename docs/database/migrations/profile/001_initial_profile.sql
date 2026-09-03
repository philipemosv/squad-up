DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'profile') THEN
        CREATE SCHEMA profile;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS profile.migration_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK_migration_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'profile') THEN
            CREATE SCHEMA profile;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE TABLE profile.games (
        id character varying(32) NOT NULL,
        name character varying(64) NOT NULL,
        is_active boolean NOT NULL,
        CONSTRAINT pk_games PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE TABLE profile.player_profiles (
        player_id uuid NOT NULL,
        nickname character varying(32) NOT NULL,
        time_zone_id character varying(64),
        status character varying(16) NOT NULL,
        CONSTRAINT pk_player_profiles PRIMARY KEY (player_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE TABLE profile.rank_tiers (
        game_id character varying(32) NOT NULL,
        tier_id character varying(32) NOT NULL,
        name character varying(64) NOT NULL,
        ordinal integer NOT NULL,
        is_active boolean NOT NULL,
        CONSTRAINT pk_rank_tiers PRIMARY KEY (game_id, tier_id),
        CONSTRAINT fk_rank_tiers_games_game_id FOREIGN KEY (game_id) REFERENCES profile.games (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE TABLE profile.player_games (
        player_id uuid NOT NULL,
        game_id character varying(32) NOT NULL,
        rank_tier_id character varying(32) NOT NULL,
        region character varying(8) NOT NULL,
        verified_at_utc timestamp with time zone,
        CONSTRAINT pk_player_games PRIMARY KEY (player_id, game_id),
        CONSTRAINT fk_player_games_games_game_id FOREIGN KEY (game_id) REFERENCES profile.games (id) ON DELETE CASCADE,
        CONSTRAINT fk_player_games_player_profiles_player_id FOREIGN KEY (player_id) REFERENCES profile.player_profiles (player_id) ON DELETE CASCADE,
        CONSTRAINT fk_player_games_rank_tiers_game_id_rank_tier_id FOREIGN KEY (game_id, rank_tier_id) REFERENCES profile.rank_tiers (game_id, tier_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE INDEX ix_player_games_game_id ON profile.player_games (game_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE INDEX ix_player_games_game_id_rank_tier_id ON profile.player_games (game_id, rank_tier_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    CREATE UNIQUE INDEX ux_rank_tiers_game_id_ordinal ON profile.rank_tiers (game_id, ordinal);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191038_InitialProfile') THEN
    INSERT INTO profile.migration_history ("MigrationId", "ProductVersion")
    VALUES ('20260903191038_InitialProfile', '10.0.11');
    END IF;
END $EF$;
COMMIT;


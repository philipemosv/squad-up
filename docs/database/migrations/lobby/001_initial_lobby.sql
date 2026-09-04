DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'lobby') THEN
        CREATE SCHEMA lobby;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS lobby.migration_history (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK_migration_history" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'lobby') THEN
            CREATE SCHEMA lobby;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    CREATE TABLE lobby.game_catalog (
        id character varying(32) NOT NULL,
        name character varying(64) NOT NULL,
        is_active boolean NOT NULL,
        CONSTRAINT pk_game_catalog PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    CREATE TABLE lobby.lobbies (
        id uuid NOT NULL,
        owner_player_id uuid NOT NULL,
        capacity integer NOT NULL,
        game_id character varying(32) NOT NULL,
        minimum_ordinal integer NOT NULL,
        maximum_ordinal integer,
        status character varying(16) NOT NULL,
        members_count integer NOT NULL,
        CONSTRAINT pk_lobbies PRIMARY KEY (id),
        CONSTRAINT ck_lobbies_capacity_range CHECK (capacity >= 2 AND capacity <= 100),
        CONSTRAINT ck_lobbies_member_count_range CHECK (members_count >= 0 AND members_count <= capacity),
        CONSTRAINT ck_lobbies_rank_range CHECK (minimum_ordinal > 0 AND (maximum_ordinal IS NULL OR maximum_ordinal >= minimum_ordinal)),
        CONSTRAINT ck_lobbies_status CHECK (status IN ('Recruiting', 'Full', 'Provisioning', 'Ready', 'Cancelled', 'Completed', 'Expired'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    CREATE TABLE lobby.rank_tiers (
        game_id character varying(32) NOT NULL,
        tier_id character varying(32) NOT NULL,
        name character varying(64) NOT NULL,
        ordinal integer NOT NULL,
        is_active boolean NOT NULL,
        CONSTRAINT pk_rank_tiers PRIMARY KEY (game_id, tier_id),
        CONSTRAINT ck_rank_tiers_ordinal_positive CHECK (ordinal > 0),
        CONSTRAINT fk_rank_tiers_game_catalog_game_id FOREIGN KEY (game_id) REFERENCES lobby.game_catalog (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    CREATE TABLE lobby.lobby_members (
        player_id uuid NOT NULL,
        lobby_id uuid NOT NULL,
        discord_user_id character varying(32) NOT NULL,
        display_name character varying(32) NOT NULL,
        rank_game_id character varying(32) NOT NULL,
        rank_ordinal integer NOT NULL,
        CONSTRAINT pk_lobby_members PRIMARY KEY (lobby_id, player_id),
        CONSTRAINT ck_lobby_members_discord_user_id CHECK (char_length(discord_user_id) BETWEEN 1 AND 32),
        CONSTRAINT ck_lobby_members_display_name CHECK (char_length(display_name) BETWEEN 1 AND 32),
        CONSTRAINT ck_lobby_members_rank_ordinal_positive CHECK (rank_ordinal > 0),
        CONSTRAINT "FK_lobby_members_lobbies_lobby_id" FOREIGN KEY (lobby_id) REFERENCES lobby.lobbies (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    INSERT INTO lobby.game_catalog (id, is_active, name)
    VALUES ('dota2', TRUE, 'Dota 2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'ancient', TRUE, 'Ancient', 6);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'archon', TRUE, 'Archon', 4);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'crusader', TRUE, 'Crusader', 3);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'divine', TRUE, 'Divine', 7);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'guardian', TRUE, 'Guardian', 2);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'herald', TRUE, 'Herald', 1);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'immortal', TRUE, 'Immortal', 8);
    INSERT INTO lobby.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'legend', TRUE, 'Legend', 5);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    CREATE UNIQUE INDEX ux_rank_tiers_game_id_ordinal ON lobby.rank_tiers (game_id, ordinal);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904011444_InitialLobby') THEN
    INSERT INTO lobby.migration_history ("MigrationId", "ProductVersion")
    VALUES ('20260904011444_InitialLobby', '10.0.11');
    END IF;
END $EF$;
COMMIT;


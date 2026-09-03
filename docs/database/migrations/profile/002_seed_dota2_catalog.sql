START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191310_SeedDota2Catalog') THEN
    INSERT INTO profile.games (id, is_active, name)
    VALUES ('dota2', TRUE, 'Dota 2');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191310_SeedDota2Catalog') THEN
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'ancient', TRUE, 'Ancient', 6);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'archon', TRUE, 'Archon', 4);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'crusader', TRUE, 'Crusader', 3);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'divine', TRUE, 'Divine', 7);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'guardian', TRUE, 'Guardian', 2);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'herald', TRUE, 'Herald', 1);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'immortal', TRUE, 'Immortal', 8);
    INSERT INTO profile.rank_tiers (game_id, tier_id, is_active, name, ordinal)
    VALUES ('dota2', 'legend', TRUE, 'Legend', 5);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM profile.migration_history WHERE "MigrationId" = '20260903191310_SeedDota2Catalog') THEN
    INSERT INTO profile.migration_history ("MigrationId", "ProductVersion")
    VALUES ('20260903191310_SeedDota2Catalog', '10.0.11');
    END IF;
END $EF$;
COMMIT;

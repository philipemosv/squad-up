START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.migration_history WHERE "MigrationId" = '20260903173241_SeedApplicationRoles') THEN
    INSERT INTO identity.roles (id, concurrency_stamp, name, normalized_name)
    VALUES ('01990a98-6380-7000-8000-000000000001', NULL, 'Player', 'PLAYER');
    INSERT INTO identity.roles (id, concurrency_stamp, name, normalized_name)
    VALUES ('01990a98-6380-7000-8000-000000000002', NULL, 'Moderator', 'MODERATOR');
    INSERT INTO identity.roles (id, concurrency_stamp, name, normalized_name)
    VALUES ('01990a98-6380-7000-8000-000000000003', NULL, 'Admin', 'ADMIN');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.migration_history WHERE "MigrationId" = '20260903173241_SeedApplicationRoles') THEN
    INSERT INTO identity.user_roles (user_id, role_id)
    SELECT id, '01990a98-6380-7000-8000-000000000001'::uuid
    FROM identity.users
    ON CONFLICT (user_id, role_id) DO NOTHING;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM identity.migration_history WHERE "MigrationId" = '20260903173241_SeedApplicationRoles') THEN
    INSERT INTO identity.migration_history ("MigrationId", "ProductVersion")
    VALUES ('20260903173241_SeedApplicationRoles', '10.0.11');
    END IF;
END $EF$;
COMMIT;

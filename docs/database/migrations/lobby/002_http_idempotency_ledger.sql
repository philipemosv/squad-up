START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904120722_AddHttpIdempotencyLedger') THEN
    CREATE TABLE lobby.http_idempotency_keys (
        owner_player_id uuid NOT NULL,
        key character varying(128) NOT NULL,
        request_hash bytea NOT NULL,
        expires_at_utc timestamp with time zone NOT NULL,
        response_status_code integer,
        response_code character varying(64),
        response_detail character varying(512),
        response_location character varying(256),
        response_body character varying(2048),
        CONSTRAINT pk_http_idempotency_keys PRIMARY KEY (owner_player_id, key),
        CONSTRAINT ck_http_idempotency_keys_hash_length CHECK (octet_length(request_hash) = 32),
        CONSTRAINT ck_http_idempotency_keys_key_length CHECK (char_length(key) BETWEEN 1 AND 128),
        CONSTRAINT ck_http_idempotency_keys_response_status CHECK (response_status_code IS NULL OR response_status_code BETWEEN 100 AND 599)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904120722_AddHttpIdempotencyLedger') THEN
    CREATE INDEX ix_http_idempotency_keys_expires_at_utc ON lobby.http_idempotency_keys (expires_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM lobby.migration_history WHERE "MigrationId" = '20260904120722_AddHttpIdempotencyLedger') THEN
    INSERT INTO lobby.migration_history ("MigrationId", "ProductVersion")
    VALUES ('20260904120722_AddHttpIdempotencyLedger', '10.0.11');
    END IF;
END $EF$;
COMMIT;

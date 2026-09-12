-- Appended to the Up method of the AddHardening migration by apply-pr24.ps1.
-- The audit log is append-only, and "append-only" has to be enforced where the
-- data lives: REVOKE does not help, because the application connects to Neon as
-- the owner of the table and an owner keeps the rights it granted away.
--
-- A trigger refuses UPDATE and DELETE for everyone, owner included. TRUNCATE is
-- deliberately left alone so the integration tests can still reset the database.
CREATE OR REPLACE FUNCTION audit_log_is_append_only() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'audit_log is append-only; % is not permitted', TG_OP
        USING ERRCODE = 'restrict_violation';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_log_no_update
    BEFORE UPDATE ON audit_log
    FOR EACH ROW EXECUTE FUNCTION audit_log_is_append_only();

CREATE TRIGGER audit_log_no_delete
    BEFORE DELETE ON audit_log
    FOR EACH ROW EXECUTE FUNCTION audit_log_is_append_only();

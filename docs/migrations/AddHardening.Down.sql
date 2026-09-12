-- Appended to the Down method of the AddHardening migration by apply-pr24.ps1.
DROP TRIGGER IF EXISTS audit_log_no_delete ON audit_log;
DROP TRIGGER IF EXISTS audit_log_no_update ON audit_log;
DROP FUNCTION IF EXISTS audit_log_is_append_only();

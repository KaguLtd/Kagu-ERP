-- The runtime role intentionally has no UPDATE privilege on immutable due lines.
-- The trigger function needs SELECT FOR UPDATE solely to serialize its capacity check.
ALTER FUNCTION party.enforce_open_item_capacity() SECURITY DEFINER;

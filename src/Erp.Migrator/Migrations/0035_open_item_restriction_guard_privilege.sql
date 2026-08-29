-- The runtime role intentionally has no UPDATE privilege on immutable due lines.
-- The owner-held trigger function needs SELECT FOR UPDATE only to serialize the
-- append-only restriction stream and prevent two concurrent active states.
ALTER FUNCTION party.enforce_open_item_restriction_stream() SECURITY DEFINER;

ALTER TABLE reporting.party_statement_projection_line
    DROP CONSTRAINT uq_party_statement_projection_event;

ALTER TABLE reporting.party_statement_projection_line
    ADD CONSTRAINT uq_party_statement_projection_event
    UNIQUE (tenant_id, company_id, statement_id, event_id);

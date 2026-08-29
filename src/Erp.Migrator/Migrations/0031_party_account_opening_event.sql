ALTER TABLE party.party_account
    ADD CONSTRAINT uq_party_account_opening_context UNIQUE
        (tenant_id, company_id, party_account_id, balance_side, currency, control_account_id);

CREATE TABLE party.party_account_opening_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    opening_event_id uuid NOT NULL,
    source_version bigint NOT NULL,
    party_account_id uuid NOT NULL,
    balance_side smallint NOT NULL,
    currency char(3) NOT NULL,
    control_account_id uuid NOT NULL,
    entry_side smallint NOT NULL,
    original_amount numeric(20,4) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    CONSTRAINT pk_party_account_opening_event PRIMARY KEY
        (tenant_id, company_id, opening_event_id),
    CONSTRAINT uq_party_account_opening_source_version UNIQUE
        (tenant_id, company_id, opening_event_id, source_version),
    CONSTRAINT fk_party_account_opening_context FOREIGN KEY
        (tenant_id, company_id, party_account_id, balance_side, currency, control_account_id)
        REFERENCES party.party_account
            (tenant_id, company_id, party_account_id, balance_side, currency, control_account_id),
    CONSTRAINT ck_party_account_opening_source_version CHECK (source_version = 1),
    CONSTRAINT ck_party_account_opening_balance_side CHECK (balance_side IN (1, 2)),
    CONSTRAINT ck_party_account_opening_currency CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_party_account_opening_entry_side CHECK (entry_side IN (1, 2)),
    CONSTRAINT ck_party_account_opening_amount CHECK (original_amount > 0)
);

COMMENT ON TABLE party.party_account_opening_event IS
    'Immutable PartyAccount opening source. It has no balance effect until the exact source/version is approved and posted.';
COMMENT ON COLUMN party.party_account_opening_event.entry_side IS
    '1=debit, 2=credit. This is independent of the PartyAccount receivable/payable classification.';

ALTER TABLE party.party_account_opening_event OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE party.party_account_opening_event FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE party.party_account_opening_event TO kagu_erp_app;

ALTER TABLE party.party_account_opening_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.party_account_opening_event FORCE ROW LEVEL SECURITY;

CREATE POLICY party_account_opening_scope_policy ON party.party_account_opening_event FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));

CREATE POLICY party_account_opening_owner_policy ON party.party_account_opening_event FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

ALTER TABLE party.party_account
    ADD COLUMN balance_side smallint;

ALTER TABLE party.party_account
    DROP CONSTRAINT uq_party_account_identity_currency;

-- NOT VALID preserves already-persisted, unclassified technical-spike rows while
-- enforcing explicit classification for every row created after this migration.
ALTER TABLE party.party_account
    ADD CONSTRAINT ck_party_account_balance_side_required
    CHECK (balance_side IS NOT NULL AND balance_side IN (1, 2)) NOT VALID;

CREATE UNIQUE INDEX uq_party_account_role_currency
    ON party.party_account
        (tenant_id, company_id, party_id, currency, balance_side)
    WHERE balance_side IS NOT NULL;

CREATE UNIQUE INDEX uq_party_account_legacy_identity_currency
    ON party.party_account
        (tenant_id, company_id, party_id, currency)
    WHERE balance_side IS NULL;

COMMENT ON COLUMN party.party_account.balance_side IS
    '1=receivable, 2=payable. NULL is reserved only for pre-0030 rows awaiting controlled classification.';

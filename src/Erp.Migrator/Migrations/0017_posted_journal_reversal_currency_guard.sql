CREATE OR REPLACE FUNCTION accounting.enforce_exact_posted_journal_reversal()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, accounting
AS $$
DECLARE
    original_header accounting.posted_journal%ROWTYPE;
    reversal_header accounting.posted_journal%ROWTYPE;
BEGIN
    SELECT * INTO original_header FROM accounting.posted_journal
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id AND journal_id = NEW.original_journal_id;
    SELECT * INTO reversal_header FROM accounting.posted_journal
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id AND journal_id = NEW.reversal_journal_id;

    IF original_header.journal_id IS NULL OR reversal_header.journal_id IS NULL
       OR original_header.functional_currency <> reversal_header.functional_currency
       OR original_header.total_debit <> reversal_header.total_credit
       OR original_header.total_credit <> reversal_header.total_debit
       OR original_header.line_count <> reversal_header.line_count
       OR EXISTS (
            SELECT 1
            FROM accounting.posted_journal_line original_line
            FULL JOIN accounting.posted_journal_line reversal_line
              ON reversal_line.journal_id = NEW.reversal_journal_id
             AND reversal_line.line_number = original_line.line_number
            WHERE original_line.journal_id = NEW.original_journal_id
              AND (reversal_line.journal_id IS NULL
                   OR original_line.account_id <> reversal_line.account_id
                   OR original_line.source_line_id IS DISTINCT FROM reversal_line.source_line_id
                   OR original_line.debit <> reversal_line.credit
                   OR original_line.credit <> reversal_line.debit
                   OR original_line.dimensions <> reversal_line.dimensions
                   OR (original_line.currency_snapshot IS NULL) <> (reversal_line.currency_snapshot IS NULL)
                   OR (original_line.currency_snapshot IS NOT NULL AND (
                       original_line.currency_snapshot - 'TransactionAmount' - 'FunctionalAmount'
                           <> reversal_line.currency_snapshot - 'TransactionAmount' - 'FunctionalAmount'
                       OR (original_line.currency_snapshot #>> '{TransactionAmount,Debit}')::numeric
                           <> (reversal_line.currency_snapshot #>> '{TransactionAmount,Credit}')::numeric
                       OR (original_line.currency_snapshot #>> '{TransactionAmount,Credit}')::numeric
                           <> (reversal_line.currency_snapshot #>> '{TransactionAmount,Debit}')::numeric
                       OR (original_line.currency_snapshot #>> '{FunctionalAmount,Debit}')::numeric
                           <> (reversal_line.currency_snapshot #>> '{FunctionalAmount,Credit}')::numeric
                       OR (original_line.currency_snapshot #>> '{FunctionalAmount,Credit}')::numeric
                           <> (reversal_line.currency_snapshot #>> '{FunctionalAmount,Debit}')::numeric)))
       ) THEN
        RAISE EXCEPTION 'Posted reversal is not the exact opposite of its original journal.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_posted_journal_reversal_exact';
    END IF;

    IF EXISTS (
        SELECT 1 FROM accounting.posted_journal_reversal existing
        WHERE existing.tenant_id = NEW.tenant_id AND existing.company_id = NEW.company_id
          AND (existing.reversal_journal_id = NEW.original_journal_id
               OR existing.original_journal_id = NEW.reversal_journal_id)
    ) THEN
        RAISE EXCEPTION 'Reversal chains are not supported.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_posted_journal_reversal_chain';
    END IF;

    RETURN NEW;
END;
$$;

ALTER FUNCTION accounting.enforce_exact_posted_journal_reversal() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON FUNCTION accounting.enforce_exact_posted_journal_reversal() FROM PUBLIC;

ALTER TABLE treasury.payment_economic_event
    ADD COLUMN rounding_policy_id uuid,
    ADD COLUMN rounding_policy_version bigint,
    ADD COLUMN rounding_scale smallint,
    ADD COLUMN rounding_mode smallint,
    ADD COLUMN unrounded_functional_amount numeric(28,12),
    ADD COLUMN rounding_difference numeric(28,12);

UPDATE treasury.payment_economic_event
SET rounding_policy_id = rate_snapshot_id,
    rounding_policy_version = 1,
    rounding_scale = 4,
    rounding_mode = 2,
    unrounded_functional_amount = functional_amount,
    rounding_difference = 0;

ALTER TABLE treasury.payment_economic_event
    ALTER COLUMN rounding_policy_id SET NOT NULL,
    ALTER COLUMN rounding_policy_version SET NOT NULL,
    ALTER COLUMN rounding_scale SET NOT NULL,
    ALTER COLUMN rounding_mode SET NOT NULL,
    ALTER COLUMN unrounded_functional_amount SET NOT NULL,
    ALTER COLUMN rounding_difference SET NOT NULL,
    DROP CONSTRAINT ck_payment_economic_event_amount,
    DROP CONSTRAINT ck_payment_economic_event_currency,
    DROP CONSTRAINT ck_payment_economic_event_rate,
    ADD CONSTRAINT ck_payment_economic_event_amount CHECK
        (transaction_amount > 0 AND functional_amount > 0),
    ADD CONSTRAINT ck_payment_economic_event_currency CHECK
        (transaction_currency ~ '^[A-Z]{3}$' AND functional_currency ~ '^[A-Z]{3}$'),
    ADD CONSTRAINT ck_payment_economic_event_rate CHECK
        (rate_version > 0 AND rate_type = btrim(rate_type) AND rate_type <> '' AND
         rate_source = btrim(rate_source) AND rate_source <> '' AND rate_date = effective_date AND
         functional_units_numerator > 0 AND transaction_units_denominator > 0),
    ADD CONSTRAINT ck_payment_economic_event_rounding CHECK
        (rounding_policy_version > 0 AND rounding_scale BETWEEN 0 AND 4 AND rounding_mode = 2),
    ADD CONSTRAINT ck_payment_economic_event_conversion CHECK
        (unrounded_functional_amount = round(
             transaction_amount * functional_units_numerator / transaction_units_denominator, 12)
         AND functional_amount = round(unrounded_functional_amount, rounding_scale)
         AND rounding_difference = functional_amount - unrounded_functional_amount);

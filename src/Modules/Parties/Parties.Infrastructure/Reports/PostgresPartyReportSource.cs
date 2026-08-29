using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Parties.Domain.Accounts;
using KaguERP.Modules.Parties.Domain.Openings;
using KaguERP.Modules.Parties.Domain.OpenItems;
using KaguERP.Modules.Parties.Infrastructure.Persistence;
using Npgsql;

namespace KaguERP.Modules.Parties.Infrastructure.Reports;

public sealed record PartySourcePostingEvidence(
    Guid JournalId,
    string SourceType,
    Guid SourceEventId,
    long SourceVersion,
    string PostingPurpose,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    DateTimeOffset PostedAt);

public delegate ValueTask<PartySourcePostingEvidence?> PartySourcePostingEvidenceLoader(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    ExecutionScope scope,
    Guid companyId,
    string sourceType,
    Guid sourceEventId,
    long sourceVersion,
    string postingPurpose,
    DateOnly effectiveAsOf,
    DateTimeOffset recordedCutoff,
    CancellationToken cancellationToken);

public enum PartySourcePostingLifecycleState
{
    NotPosted = 0,
    Active = 1,
    Reversed = 2,
}

public sealed record PartySourcePostingReversalEvidence(
    Guid OriginalJournalId,
    Guid ReversalJournalId,
    DateOnly EffectiveDate,
    DateTimeOffset RecordedAt,
    DateTimeOffset PostedAt,
    DateTimeOffset LinkedAt);

public sealed record PartySourcePostingLifecycleEvidence(
    PartySourcePostingLifecycleState State,
    PartySourcePostingEvidence? Posting,
    PartySourcePostingReversalEvidence? Reversal);

public delegate ValueTask<PartySourcePostingLifecycleEvidence> PartySourcePostingLifecycleEvidenceLoader(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    ExecutionScope scope,
    Guid companyId,
    string sourceType,
    Guid sourceEventId,
    long sourceVersion,
    string postingPurpose,
    DateOnly effectiveAsOf,
    DateTimeOffset recordedCutoff,
    CancellationToken cancellationToken);

public sealed class PostgresPartyReportSource(
    NpgsqlDataSource dataSource,
    ExecutionScope scope,
    PartySourcePostingEvidenceLoader postingEvidenceLoader,
    PartySourcePostingLifecycleEvidenceLoader postingLifecycleEvidenceLoader) : IPartyReportSource
{
    public async ValueTask<PartyReportSourceBatch?> LoadAsync(
        PartyReportSourceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TenantId == Guid.Empty || query.CompanyId == Guid.Empty || query.PartyAccountId == Guid.Empty)
        {
            throw new ArgumentException("Tenant, company and PartyAccount IDs are required.", nameof(query));
        }
        if (query.EffectiveAsOf == default)
        {
            throw new ArgumentException("Effective as-of date is required.", nameof(query));
        }
        if (query.RecordedCutoff.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Recorded cutoff must use the UTC offset.", nameof(query));
        }
        scope.EnsureAllowed(query.TenantId, query.CompanyId);

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);

        PartyAccountReportContext? account = await LoadAccountAsync(
            connection,
            transaction,
            query,
            cancellationToken);
        if (account is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var activeEvidence = new List<PartySourcePostingEvidence>();
        decimal openingExposure = await LoadOpeningExposureAsync(
            connection,
            transaction,
            query,
            account,
            activeEvidence,
            cancellationToken);
        IReadOnlyList<PartyOpenItemSourceFact> openItems = await LoadOpenItemsAsync(
            connection,
            transaction,
            query,
            account,
            activeEvidence,
            cancellationToken);
        (string watermarkFrom, string watermarkTo) = CreateLineageWatermarks(activeEvidence);

        var result = PartyReportSourceBatch.Create(
            query.TenantId,
            query.CompanyId,
            query.PartyAccountId,
            account.ControlAccountId,
            (PartyReportBalanceSide)account.BalanceSide,
            account.Currency,
            query.EffectiveAsOf,
            query.RecordedCutoff,
            openingExposure,
            watermarkFrom,
            watermarkTo,
            openItems);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async ValueTask<decimal> LoadOpeningExposureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyReportSourceQuery query,
        PartyAccountReportContext account,
        List<PartySourcePostingEvidence> activeEvidence,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT opening_event_id, source_version, balance_side, currency, control_account_id,
                   entry_side, original_amount, effective_date, recorded_at
            FROM party.party_account_opening_event
            WHERE tenant_id=$1 AND company_id=$2 AND party_account_id=$3
              AND effective_date <= $4 AND recorded_at <= $5
            ORDER BY effective_date, recorded_at, opening_event_id
            """;
        var candidates = new List<OpeningCandidate>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue(query.TenantId);
            command.Parameters.AddWithValue(query.CompanyId);
            command.Parameters.AddWithValue(query.PartyAccountId);
            command.Parameters.AddWithValue(query.EffectiveAsOf);
            command.Parameters.AddWithValue(query.RecordedCutoff);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add(new OpeningCandidate(
                    reader.GetGuid(0),
                    reader.GetInt64(1),
                    (PartyAccountBalanceSide)reader.GetInt16(2),
                    reader.GetString(3),
                    reader.GetGuid(4),
                    (PartyAccountOpeningEntrySide)reader.GetInt16(5),
                    reader.GetDecimal(6),
                    reader.GetFieldValue<DateOnly>(7),
                    reader.GetFieldValue<DateTimeOffset>(8)));
            }
        }

        decimal exposure = decimal.Zero;
        foreach (OpeningCandidate candidate in candidates)
        {
            if (candidate.BalanceSide != account.BalanceSide ||
                !string.Equals(candidate.Currency, account.Currency, StringComparison.Ordinal) ||
                candidate.ControlAccountId != account.ControlAccountId)
            {
                throw CreateException(
                    "PARTY_REPORT_OPENING_CONTEXT_CONFLICT",
                    "An opening source conflicts with its authoritative PartyAccount context.");
            }

            PartySourcePostingEvidence? evidence = await LoadAndValidateEvidenceAsync(
                connection,
                transaction,
                query,
                PartyAccountOpeningDraft.SourceType,
                candidate.OpeningEventId,
                candidate.SourceVersion,
                PartyAccountOpeningDraft.PostingPurpose,
                candidate.EffectiveDate,
                candidate.RecordedAt,
                cancellationToken);
            if (evidence is null)
            {
                continue;
            }

            activeEvidence.Add(evidence);
            bool increasesNaturalBalance =
                account.BalanceSide == PartyAccountBalanceSide.Receivable
                    ? candidate.EntrySide == PartyAccountOpeningEntrySide.Debit
                    : candidate.EntrySide == PartyAccountOpeningEntrySide.Credit;
            checked
            {
                exposure += increasesNaturalBalance ? candidate.OriginalAmount : -candidate.OriginalAmount;
            }
        }

        if (exposure < decimal.Zero)
        {
            throw CreateException(
                "PARTY_REPORT_NEGATIVE_OPENING_EXPOSURE_UNSUPPORTED",
                "The current report contract cannot represent a net opening balance opposite to the PartyAccount side.");
        }
        return exposure;
    }

    private async ValueTask<IReadOnlyList<PartyOpenItemSourceFact>> LoadOpenItemsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyReportSourceQuery query,
        PartyAccountReportContext account,
        List<PartySourcePostingEvidence> activeEvidence,
        CancellationToken cancellationToken)
    {
        const string scheduleSql = """
            SELECT due_schedule_id
            FROM party.due_schedule
            WHERE tenant_id=$1 AND company_id=$2 AND party_account_id=$3 AND recorded_at <= $4
            ORDER BY recorded_at, due_schedule_id
            """;
        var scheduleIds = new List<Guid>();
        await using (var command = new NpgsqlCommand(scheduleSql, connection, transaction))
        {
            command.Parameters.AddWithValue(query.TenantId);
            command.Parameters.AddWithValue(query.CompanyId);
            command.Parameters.AddWithValue(query.PartyAccountId);
            command.Parameters.AddWithValue(query.RecordedCutoff);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                scheduleIds.Add(reader.GetGuid(0));
            }
        }

        var openItems = new List<PartyOpenItemSourceFact>();
        var activeSourceIdentities = new HashSet<(string SourceType, Guid EventId, string Purpose)>();
        foreach (Guid scheduleId in scheduleIds)
        {
            LoadedDueSchedule schedule = await PostgresDueScheduleLoader.LoadAsync(
                connection,
                transaction,
                scope,
                query.CompanyId,
                scheduleId,
                cancellationToken)
                ?? throw CreateException(
                    "PARTY_REPORT_DUE_SCHEDULE_UNAVAILABLE",
                    "A due schedule disappeared inside the repeatable-read report snapshot.");
            if (schedule.SourceEffectiveDate > query.EffectiveAsOf)
            {
                continue;
            }
            if (schedule.Schedule.PartyAccountId != query.PartyAccountId ||
                !string.Equals(schedule.Schedule.Currency.Value, account.Currency, StringComparison.Ordinal) ||
                schedule.Schedule.Lines.Any(line => line.ControlAccountId != account.ControlAccountId))
            {
                throw CreateException(
                    "PARTY_REPORT_DUE_CONTEXT_CONFLICT",
                    "A due schedule conflicts with its authoritative PartyAccount context.");
            }

            PartySourcePostingEvidence? evidence = await LoadAndValidateEvidenceAsync(
                connection,
                transaction,
                query,
                schedule.SourceType,
                schedule.Schedule.SourceEventId,
                schedule.SourceVersion,
                schedule.SourcePostingPurpose,
                schedule.SourceEffectiveDate,
                schedule.RecordedAt,
                cancellationToken);
            if (evidence is null)
            {
                continue;
            }
            if (!activeSourceIdentities.Add(
                    (schedule.SourceType, schedule.Schedule.SourceEventId, schedule.SourcePostingPurpose)))
            {
                throw CreateException(
                    "PARTY_REPORT_MULTIPLE_ACTIVE_SOURCE_VERSIONS",
                    "More than one active source version would duplicate the same Party exposure.");
            }
            activeEvidence.Add(evidence);

            foreach (var line in schedule.Schedule.Lines)
            {
                DerivedOpenItemSnapshot snapshot = await PostgresOpenItemSnapshotLoader.LoadAsync(
                    connection,
                    transaction,
                    scope,
                    query.CompanyId,
                    line.DueScheduleLineId,
                    query.EffectiveAsOf,
                    query.RecordedCutoff,
                    cancellationToken)
                    ?? throw CreateException(
                        "PARTY_REPORT_OPEN_ITEM_UNAVAILABLE",
                        "A due line disappeared inside the repeatable-read report snapshot.");
                var activeImpacts = new List<OpenItemImpactEvent>();
                var impactLifecycles = new Dictionary<Guid, PartySourcePostingLifecycleEvidence>();
                foreach (OpenItemImpactEvent impact in snapshot.ConsideredEvents)
                {
                    PartySourcePostingLifecycleEvidence lifecycle = await LoadAndValidateLifecycleEvidenceAsync(
                        connection,
                        transaction,
                        query,
                        impact.SourceType,
                        impact.EventId,
                        impact.SourceVersion,
                        impact.SourcePostingPurpose,
                        impact.EffectiveDate,
                        impact.RecordedAt,
                        cancellationToken);
                    impactLifecycles.Add(impact.EventId, lifecycle);
                }

                foreach (OpenItemImpactEvent impact in snapshot.ConsideredEvents)
                {
                    PartySourcePostingLifecycleEvidence lifecycle = impactLifecycles[impact.EventId];
                    if (impact.ReversesEventId is Guid originalEventId)
                    {
                        OpenItemImpactEvent? original = snapshot.ConsideredEvents.SingleOrDefault(
                            candidate => candidate.EventId == originalEventId);
                        if (original is null || original.ReversesEventId.HasValue)
                        {
                            throw CreateException(
                                "PARTY_REPORT_IMPACT_COUNTER_ORIGINAL_UNAVAILABLE",
                                "An open-item counter source does not resolve to one original impact in the same snapshot.");
                        }
                        PartySourcePostingLifecycleEvidence originalLifecycle = impactLifecycles[originalEventId];
                        if (lifecycle.State != PartySourcePostingLifecycleState.NotPosted &&
                            originalLifecycle.Posting is null)
                        {
                            throw CreateException(
                                "PARTY_REPORT_IMPACT_COUNTER_POSTING_ORDER_CONFLICT",
                                "A posted counter impact has no posted original source in the same Accounting lifecycle.");
                        }
                        if (lifecycle.Posting is not null && originalLifecycle.Posting is not null &&
                            lifecycle.Posting.PostedAt < originalLifecycle.Posting.PostedAt)
                        {
                            throw CreateException(
                                "PARTY_REPORT_IMPACT_COUNTER_POSTING_ORDER_CONFLICT",
                                "A counter impact was posted before its original source.");
                        }
                        if (lifecycle.State == PartySourcePostingLifecycleState.Active &&
                            originalLifecycle.State != PartySourcePostingLifecycleState.Active)
                        {
                            throw CreateException(
                                "PARTY_REPORT_IMPACT_COUNTER_ACTIVE_ORIGINAL_CONFLICT",
                                "An active counter impact cannot cancel an inactive original Accounting source.");
                        }
                    }

                    if (lifecycle.State == PartySourcePostingLifecycleState.Active)
                    {
                        activeEvidence.Add(lifecycle.Posting!);
                        activeImpacts.Add(impact);
                    }
                }
                DerivedOpenItemSnapshot activeSnapshot = DerivedOpenItemSnapshot.Create(
                    line,
                    query.EffectiveAsOf,
                    query.RecordedCutoff,
                    activeImpacts);
                PartyReportImpactFact[] impactFacts = activeSnapshot.ConsideredEvents
                    .Select(impact => new PartyReportImpactFact(
                        impact.EventId,
                        MapImpactKind(impact.Kind),
                        impact.PaymentId,
                        impact.Amount,
                        impact.EffectiveDate,
                        impact.RecordedAt,
                        impact.ReversesEventId))
                    .ToArray();
                DerivedOpenItemRestrictionSnapshot restrictionSnapshot =
                    await PostgresOpenItemRestrictionSnapshotLoader.LoadAsync(
                        connection,
                        transaction,
                        scope,
                        query.CompanyId,
                        line.DueScheduleLineId,
                        query.EffectiveAsOf,
                        query.RecordedCutoff,
                        cancellationToken)
                    ?? throw CreateException(
                        "PARTY_REPORT_RESTRICTION_SOURCE_UNAVAILABLE",
                        "The open-item restriction source disappeared inside the report snapshot.");
                openItems.Add(new PartyOpenItemSourceFact(
                    line.DueScheduleLineId,
                    schedule.Schedule.SourceEventId,
                    line.DueScheduleLineId,
                    schedule.SourceType,
                    line.OriginalAmount,
                    activeSnapshot.RemainingAmount,
                    schedule.SourceEffectiveDate,
                    line.DueDate,
                    schedule.RecordedAt,
                    MapRestriction(restrictionSnapshot),
                    Array.AsReadOnly(impactFacts)));
            }
        }
        return openItems.AsReadOnly();
    }

    private async ValueTask<PartySourcePostingEvidence?> LoadAndValidateEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyReportSourceQuery query,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly sourceEffectiveDate,
        DateTimeOffset sourceRecordedAt,
        CancellationToken cancellationToken)
    {
        PartySourcePostingEvidence? evidence = await postingEvidenceLoader(
            connection,
            transaction,
            scope,
            query.CompanyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            query.EffectiveAsOf,
            query.RecordedCutoff,
            cancellationToken);
        if (evidence is null)
        {
            return null;
        }

        bool exactIdentity = evidence.JournalId != Guid.Empty &&
            string.Equals(evidence.SourceType, sourceType, StringComparison.Ordinal) &&
            evidence.SourceEventId == sourceEventId && evidence.SourceVersion == sourceVersion &&
            string.Equals(evidence.PostingPurpose, postingPurpose, StringComparison.Ordinal);
        bool exactDates = evidence.EffectiveDate == sourceEffectiveDate &&
            evidence.RecordedAt == sourceRecordedAt && evidence.PostedAt.Offset == TimeSpan.Zero &&
            evidence.PostedAt <= query.RecordedCutoff;
        if (!exactIdentity || !exactDates)
        {
            throw CreateException(
                "PARTY_REPORT_POSTING_EVIDENCE_MISMATCH",
                "Accounting posting evidence does not exactly match the immutable Party source snapshot.");
        }
        return evidence;
    }

    private async ValueTask<PartySourcePostingLifecycleEvidence> LoadAndValidateLifecycleEvidenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyReportSourceQuery query,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly sourceEffectiveDate,
        DateTimeOffset sourceRecordedAt,
        CancellationToken cancellationToken)
    {
        PartySourcePostingLifecycleEvidence lifecycle = await postingLifecycleEvidenceLoader(
            connection,
            transaction,
            scope,
            query.CompanyId,
            sourceType,
            sourceEventId,
            sourceVersion,
            postingPurpose,
            query.EffectiveAsOf,
            query.RecordedCutoff,
            cancellationToken);
        bool shapeIsValid = lifecycle.State switch
        {
            PartySourcePostingLifecycleState.NotPosted => lifecycle.Posting is null && lifecycle.Reversal is null,
            PartySourcePostingLifecycleState.Active => lifecycle.Posting is not null && lifecycle.Reversal is null,
            PartySourcePostingLifecycleState.Reversed => lifecycle.Posting is not null && lifecycle.Reversal is not null,
            _ => false,
        };
        if (!shapeIsValid)
        {
            throw CreateException(
                "PARTY_REPORT_POSTING_LIFECYCLE_INVALID",
                "Accounting returned an internally inconsistent source lifecycle.");
        }
        if (lifecycle.Posting is not null)
        {
            ValidateEvidence(
                lifecycle.Posting,
                query,
                sourceType,
                sourceEventId,
                sourceVersion,
                postingPurpose,
                sourceEffectiveDate,
                sourceRecordedAt);
        }
        if (lifecycle.Reversal is not null)
        {
            bool exactLink = lifecycle.Reversal.OriginalJournalId == lifecycle.Posting!.JournalId &&
                lifecycle.Reversal.ReversalJournalId != Guid.Empty;
            bool visibleDates = lifecycle.Reversal.EffectiveDate <= query.EffectiveAsOf &&
                lifecycle.Reversal.RecordedAt.Offset == TimeSpan.Zero &&
                lifecycle.Reversal.PostedAt.Offset == TimeSpan.Zero &&
                lifecycle.Reversal.LinkedAt.Offset == TimeSpan.Zero &&
                lifecycle.Reversal.RecordedAt <= query.RecordedCutoff &&
                lifecycle.Reversal.PostedAt <= query.RecordedCutoff &&
                lifecycle.Reversal.LinkedAt <= query.RecordedCutoff;
            if (!exactLink || !visibleDates)
            {
                throw CreateException(
                    "PARTY_REPORT_POSTING_LIFECYCLE_MISMATCH",
                    "Accounting reversal evidence does not match the requested report cut.");
            }
        }
        return lifecycle;
    }

    private static void ValidateEvidence(
        PartySourcePostingEvidence evidence,
        PartyReportSourceQuery query,
        string sourceType,
        Guid sourceEventId,
        long sourceVersion,
        string postingPurpose,
        DateOnly sourceEffectiveDate,
        DateTimeOffset sourceRecordedAt)
    {
        bool exactIdentity = evidence.JournalId != Guid.Empty &&
            string.Equals(evidence.SourceType, sourceType, StringComparison.Ordinal) &&
            evidence.SourceEventId == sourceEventId && evidence.SourceVersion == sourceVersion &&
            string.Equals(evidence.PostingPurpose, postingPurpose, StringComparison.Ordinal);
        bool exactDates = evidence.EffectiveDate == sourceEffectiveDate &&
            evidence.RecordedAt == sourceRecordedAt && evidence.PostedAt.Offset == TimeSpan.Zero &&
            evidence.PostedAt <= query.RecordedCutoff;
        if (!exactIdentity || !exactDates)
        {
            throw CreateException(
                "PARTY_REPORT_POSTING_EVIDENCE_MISMATCH",
                "Accounting posting evidence does not exactly match the immutable Party source snapshot.");
        }
    }

    private static async ValueTask<PartyAccountReportContext?> LoadAccountAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PartyReportSourceQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT balance_side, currency, control_account_id
            FROM party.party_account
            WHERE tenant_id=$1 AND company_id=$2 AND party_account_id=$3
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(query.TenantId);
        command.Parameters.AddWithValue(query.CompanyId);
        command.Parameters.AddWithValue(query.PartyAccountId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        if (reader.IsDBNull(0) ||
            !Enum.IsDefined(typeof(PartyAccountBalanceSide), reader.GetInt16(0)))
        {
            throw CreateException(
                "PARTY_REPORT_ACCOUNT_UNCLASSIFIED",
                "The PartyAccount has no usable receivable/payable classification.");
        }
        return new PartyAccountReportContext(
            (PartyAccountBalanceSide)reader.GetInt16(0),
            reader.GetString(1),
            reader.GetGuid(2));
    }

    private async ValueTask SetExecutionContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                set_config('app.tenant_id', $1, true),
                set_config('app.actor_id', $2, true),
                set_config('app.company_ids', $3, true)
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(scope.TenantId.ToString());
        command.Parameters.AddWithValue(scope.ActorId.ToString());
        command.Parameters.AddWithValue("{" + string.Join(',', scope.CompanyIds.Order()) + "}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static (string From, string To) CreateLineageWatermarks(
        IReadOnlyCollection<PartySourcePostingEvidence> evidence)
    {
        PartySourcePostingEvidence[] ordered = evidence
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.SourceType, StringComparer.Ordinal)
            .ThenBy(item => item.SourceEventId)
            .ThenBy(item => item.SourceVersion)
            .ThenBy(item => item.PostingPurpose, StringComparer.Ordinal)
            .ThenBy(item => item.JournalId)
            .ToArray();
        if (ordered.Length == 0)
        {
            return ("posted-journal:none", "posted-set-v1:none");
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (PartySourcePostingEvidence item in ordered)
        {
            AddFramed(hash, item.SourceType);
            AddFramed(hash, item.SourceEventId.ToString("N"));
            AddFramed(hash, item.SourceVersion.ToString(CultureInfo.InvariantCulture));
            AddFramed(hash, item.PostingPurpose);
            AddFramed(hash, item.JournalId.ToString("N"));
            AddFramed(hash, item.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AddFramed(hash, item.RecordedAt.ToString("O", CultureInfo.InvariantCulture));
            AddFramed(hash, item.PostedAt.ToString("O", CultureInfo.InvariantCulture));
        }
        string digest = Convert.ToHexStringLower(hash.GetHashAndReset());
        return ($"posted-journal:{ordered[0].JournalId:N}", $"posted-set-v1:{digest}");
    }

    private static void AddFramed(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(Encoding.ASCII.GetBytes(bytes.Length.ToString(CultureInfo.InvariantCulture)));
        hash.AppendData(":"u8);
        hash.AppendData(bytes);
    }

    private static AuthoritativePartyReportSourceException CreateException(string code, string message) =>
        new(code, message);

    private static PartyReportImpactKind MapImpactKind(OpenItemImpactKind kind) => kind switch
    {
        OpenItemImpactKind.Allocation => PartyReportImpactKind.Allocation,
        OpenItemImpactKind.Unallocation => PartyReportImpactKind.Unallocation,
        OpenItemImpactKind.WriteOff => PartyReportImpactKind.WriteOff,
        OpenItemImpactKind.WriteOffReversal => PartyReportImpactKind.WriteOffReversal,
        _ => throw CreateException("PARTY_REPORT_IMPACT_KIND_INVALID", "Open-item impact kind is invalid."),
    };

    private static PartyReportRestrictionEvidence MapRestriction(
        DerivedOpenItemRestrictionSnapshot snapshot) =>
        (snapshot.IsDisputed, snapshot.IsCollectionBlocked) switch
        {
            (false, false) => PartyReportRestrictionEvidence.Clear,
            (true, false) => PartyReportRestrictionEvidence.Disputed,
            (false, true) => PartyReportRestrictionEvidence.Blocked,
            (true, true) => PartyReportRestrictionEvidence.DisputedAndBlocked,
        };

    private sealed record PartyAccountReportContext(
        PartyAccountBalanceSide BalanceSide,
        string Currency,
        Guid ControlAccountId);

    private sealed record OpeningCandidate(
        Guid OpeningEventId,
        long SourceVersion,
        PartyAccountBalanceSide BalanceSide,
        string Currency,
        Guid ControlAccountId,
        PartyAccountOpeningEntrySide EntrySide,
        decimal OriginalAmount,
        DateOnly EffectiveDate,
        DateTimeOffset RecordedAt);
}

public sealed class AuthoritativePartyReportSourceException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

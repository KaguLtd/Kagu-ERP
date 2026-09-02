using System.Data;
using System.Security.Cryptography;
using System.Text;
using KaguERP.BuildingBlocks.Application.Security;
using KaguERP.Modules.Parties.Contracts.Reports;
using KaguERP.Modules.Reporting.Application.PartyReports;
using KaguERP.Modules.Reporting.Domain.ControlAccounts;
using Npgsql;

namespace KaguERP.Modules.Reporting.Infrastructure.PartyReports;

public sealed record PartyGeneralLedgerControlAccountEvidence(
    Guid TenantId,
    Guid CompanyId,
    Guid ControlAccountId,
    string Currency,
    DateOnly EffectiveAsOf,
    DateTimeOffset RecordedCutoff,
    decimal OpeningBalance,
    decimal Debits,
    decimal Credits,
    decimal ClosingBalance,
    long RowCount,
    string SourceChecksumSha256);

public delegate ValueTask<PartyGeneralLedgerControlAccountEvidence> PartyGeneralLedgerEvidenceLoader(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    ExecutionScope scope,
    PartyReportSourceBatch source,
    CancellationToken cancellationToken);

public sealed class PostgresPartyControlAccountEvidenceSource(
    NpgsqlDataSource dataSource,
    ExecutionScope scope,
    PartyGeneralLedgerEvidenceLoader generalLedgerLoader) : IPartyControlAccountEvidenceSource
{
    public async ValueTask<PartyControlAccountEvidence?> LoadAsync(
        PartyReportSourceBatch source,
        FinancialReportSlice reportSlice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reportSlice);
        scope.EnsureAllowed(source.TenantId, source.CompanyId);
        EnsureSliceMatches(source, reportSlice);
        if (reportSlice.Dimensions.Assignments.Count != 0)
        {
            throw new PartyControlAccountEvidenceException(
                "PARTY_CONTROL_ACCOUNT_DIMENSION_FILTER_UNSUPPORTED",
                "Party control-account evidence currently supports only the unsegmented report total.");
        }

        ControlAccountBalanceSnapshot subledger = CreateSubledgerSnapshot(source, reportSlice);
        if (source.PostingLineage.Count == 0 && subledger.ClosingBalance != decimal.Zero)
        {
            throw new PartyControlAccountEvidenceException(
                "PARTY_CONTROL_ACCOUNT_POSTING_LINEAGE_REQUIRED",
                "A non-zero Party balance requires exact active GL posting lineage.");
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        await SetExecutionContextAsync(connection, transaction, cancellationToken);
        PartyGeneralLedgerControlAccountEvidence generalLedger = await generalLedgerLoader(
            connection,
            transaction,
            scope,
            source,
            cancellationToken);
        EnsureGeneralLedgerContext(source, generalLedger);
        ControlAccountBalanceSnapshot generalLedgerSnapshot = ControlAccountBalanceSnapshot.Create(
            CreateSnapshotId(reportSlice.ProjectionGenerationId, LedgerSide.GeneralLedger),
            source.ControlAccountId,
            LedgerSide.GeneralLedger,
            generalLedger.OpeningBalance,
            generalLedger.Debits,
            generalLedger.Credits,
            generalLedger.ClosingBalance,
            generalLedger.RowCount,
            generalLedger.SourceChecksumSha256,
            reportSlice);
        await transaction.CommitAsync(cancellationToken);
        return new PartyControlAccountEvidence(subledger, generalLedgerSnapshot);
    }

    private static ControlAccountBalanceSnapshot CreateSubledgerSnapshot(
        PartyReportSourceBatch source,
        FinancialReportSlice reportSlice)
    {
        decimal debits = decimal.Zero;
        decimal credits = decimal.Zero;
        long rowCount = 0;
        AddExposureEffect(source, source.OpeningExposure, ref debits, ref credits);
        if (source.OpeningExposure != decimal.Zero)
        {
            rowCount++;
        }
        foreach (PartyOpenItemSourceFact item in source.OpenItems)
        {
            AddExposureEffect(source, item.OriginalAmount, ref debits, ref credits);
            rowCount++;
            foreach (PartyReportImpactFact impact in item.Impacts)
            {
                decimal effect = impact.Kind switch
                {
                    PartyReportImpactKind.Allocation or PartyReportImpactKind.WriteOff => -impact.Amount,
                    PartyReportImpactKind.Unallocation or PartyReportImpactKind.WriteOffReversal => impact.Amount,
                    _ => throw new ArgumentOutOfRangeException(nameof(source)),
                };
                AddExposureEffect(source, effect, ref debits, ref credits);
                rowCount++;
            }
        }
        decimal closing;
        checked
        {
            closing = debits - credits;
        }
        return ControlAccountBalanceSnapshot.Create(
            CreateSnapshotId(reportSlice.ProjectionGenerationId, LedgerSide.Subledger),
            source.ControlAccountId,
            LedgerSide.Subledger,
            decimal.Zero,
            debits,
            credits,
            closing,
            rowCount,
            source.SourceChecksumSha256,
            reportSlice);
    }

    private static void AddExposureEffect(
        PartyReportSourceBatch source,
        decimal exposureEffect,
        ref decimal debits,
        ref decimal credits)
    {
        decimal signedGlEffect = source.BalanceSide == PartyReportBalanceSide.Receivable
            ? exposureEffect
            : -exposureEffect;
        checked
        {
            if (signedGlEffect >= decimal.Zero)
            {
                debits += signedGlEffect;
            }
            else
            {
                credits += -signedGlEffect;
            }
        }
    }

    private static void EnsureSliceMatches(PartyReportSourceBatch source, FinancialReportSlice slice)
    {
        if (source.TenantId != slice.TenantId || source.CompanyId != slice.CompanyId ||
            source.EffectiveAsOf != slice.EffectiveAsOf || source.RecordedCutoff != slice.DataCutoffAt ||
            !string.Equals(source.Currency, slice.Currency.Value, StringComparison.Ordinal))
        {
            throw new PartyControlAccountEvidenceException(
                "PARTY_CONTROL_ACCOUNT_SOURCE_SLICE_MISMATCH",
                "Party source and report slice do not share the same scope, currency and bitemporal cut.");
        }
    }

    private static void EnsureGeneralLedgerContext(
        PartyReportSourceBatch source,
        PartyGeneralLedgerControlAccountEvidence evidence)
    {
        if (source.TenantId != evidence.TenantId || source.CompanyId != evidence.CompanyId ||
            source.ControlAccountId != evidence.ControlAccountId ||
            !string.Equals(source.Currency, evidence.Currency, StringComparison.Ordinal) ||
            source.EffectiveAsOf != evidence.EffectiveAsOf ||
            source.RecordedCutoff != evidence.RecordedCutoff)
        {
            throw new PartyControlAccountEvidenceException(
                "PARTY_GENERAL_LEDGER_EVIDENCE_CONTEXT_MISMATCH",
                "Accounting evidence does not match the authoritative Party source context.");
        }
    }

    private static Guid CreateSnapshotId(Guid projectionGenerationId, LedgerSide ledgerSide)
    {
        byte[] input = Encoding.UTF8.GetBytes(
            $"party-control-account-snapshot-v1:{projectionGenerationId:N}:{(int)ledgerSide}");
        byte[] digest = SHA256.HashData(input);
        Span<byte> bytes = digest.AsSpan(0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
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
}

public sealed class PartyControlAccountEvidenceException(string code, string message)
    : InvalidOperationException(message), IPartyReportRefreshFailure
{
    public string Code { get; } = code;
}

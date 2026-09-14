using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace KaguERP.Modules.Inventory.Domain;

public sealed record InventoryIssuePositionTotal(Guid ItemId, Guid WarehouseId, InventoryUomCode BaseUom,
    InventoryQuantity QuantityChange, decimal Amount);

/// <summary>Read-only source reconciliation, not a posted ledger or authorization result.</summary>
public sealed class InventoryIssueCostBatch
{
    private InventoryIssueCostBatch(InventoryIssueCostAmount[] lines, InventoryIssuePositionTotal[] positions, decimal total)
    {
        Lines = Array.AsReadOnly(lines);
        Positions = Array.AsReadOnly(positions);
        TotalAmount = total;
        Fingerprint = ComputeFingerprint(lines);
    }
    public IReadOnlyList<InventoryIssueCostAmount> Lines { get; }
    public IReadOnlyList<InventoryIssuePositionTotal> Positions { get; }
    public decimal TotalAmount { get; }
    public string Fingerprint { get; }
    public string Currency => Lines[0].Selection.Currency;

    public static InventoryIssueCostBatch Create(IEnumerable<InventoryIssueCostAmount> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        var lines = amounts.Take(501).ToArray();
        if (lines.Length is < 1 or > 500 || lines.Any(line => line is null || line.Selection is null))
            throw Invalid();
        var first = lines[0];
        var source = first.Selection.Issue.Source;
        if (lines.Any(line => line.Selection.Issue.Source.TenantId != source.TenantId ||
                line.Selection.Issue.Source.CompanyId != source.CompanyId ||
                line.Selection.Issue.Source.SourceType != source.SourceType ||
                line.Selection.Issue.Source.SourceEventId != source.SourceEventId ||
                line.Selection.Issue.Source.SourceVersion != source.SourceVersion ||
                line.Selection.Issue.Source.PostingPurpose != source.PostingPurpose ||
                line.Selection.Currency != first.Selection.Currency ||
                line.RoundingPolicySnapshotId != first.RoundingPolicySnapshotId || line.AmountScale != first.AmountScale ||
                line.Selection.Issue.EffectiveDate != first.Selection.Issue.EffectiveDate) ||
            lines.Select(line => line.Selection.Issue.MovementId).Distinct().Count() != lines.Length ||
            lines.Select(line => line.Selection.Issue.Source.SourceLineId).Distinct().Count() != lines.Length ||
            lines.Select(line => (line.Selection.Issue.ItemId, line.Selection.Issue.WarehouseId,
                line.Selection.Issue.SequenceKey)).Distinct().Count() != lines.Length)
            throw Invalid();
        foreach (var line in lines)
            if (line.Amount != line.Selection.CalculateAmount(line.RoundingPolicySnapshotId, line.AmountScale).Amount)
                throw Invalid();
        var totals = new List<InventoryIssuePositionTotal>();
        foreach (var group in lines.GroupBy(line => (line.Selection.Issue.ItemId, line.Selection.Issue.WarehouseId)))
        {
            var history = group.First().Selection.History;
            if (group.Any(line => line.Selection.History.BaseUom != history.BaseUom))
                throw Invalid();
            totals.Add(new(group.Key.ItemId, group.Key.WarehouseId, history.BaseUom,
                InventoryQuantity.Create(group.Sum(line => line.Selection.Issue.BaseQuantity.Value)),
                InventoryCostArithmetic.Money(group.Sum(line => line.Amount))));
        }
        return new(lines, totals.ToArray(), InventoryCostArithmetic.Money(lines.Sum(line => line.Amount)));
    }

    private static InventoryInvariantException Invalid() => new("INVENTORY_ISSUE_COST_BATCH_CONFLICT",
        "Issue cost batch must preserve one source, currency, policy and consistent position units.");

    private static string ComputeFingerprint(InventoryIssueCostAmount[] lines)
    {
        static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = "inventory-issue-cost-batch/v1",
            Lines = lines.OrderBy(line => line.Selection.Issue.MovementId).Select(line =>
            {
                var issue = line.Selection.Issue;
                var history = line.Selection.History;
                var mark = history.Watermark;
                return new
                {
                    issue.TenantId,
                    issue.CompanyId,
                    issue.MovementId,
                    issue.ItemId,
                    issue.WarehouseId,
                    Uom = issue.BaseUom.Value,
                    Quantity = Number(issue.BaseQuantity.Value),
                    EffectiveDate = issue.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    RecordedAt = issue.RecordedAt.ToString("O", CultureInfo.InvariantCulture),
                    issue.SequenceKey,
                    issue.ReversalOfMovementId,
                    Source = new
                    {
                        issue.Source.SourceType,
                        issue.Source.SourceEventId,
                        issue.Source.SourceLineId,
                        issue.Source.SourceVersion,
                        issue.Source.PostingPurpose
                    },
                    line.RoundingPolicySnapshotId,
                    line.AmountScale,
                    Amount = Number(line.Amount),
                    History = new
                    {
                        history.Currency,
                        history.SnapshotId,
                        Origin = (int)history.Origin,
                        UnitCost = Number(history.UnitCost),
                        EffectiveDate = mark.Position.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        mark.Position.SequenceKey,
                        mark.ProjectionGeneration,
                        RecordedCutoff = mark.RecordedCutoff.ToString("O", CultureInfo.InvariantCulture),
                        mark.SourceChecksumSha256,
                    },
                };
            }),
        })));
    }
}

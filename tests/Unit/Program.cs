using KaguERP.Modules.Accounting.Domain.Journals;
using PartyAllocations = KaguERP.Modules.Parties.Domain.Allocations;

var checks = new (string Name, Action Run)[]
{
    ("ACC-INV-001 balanced decimal journal", BalancedJournalIsAccepted),
    ("ACC-INV-001 exact imbalance rejection", ImbalancedJournalIsRejected),
    ("Journal line amount boundaries", InvalidJournalAmountsAreRejected),
    ("Journal scope and source requirements", MissingContextIsRejected),
    ("Journal UTC timestamp requirement", NonUtcRecordedTimeIsRejected),
    ("Journal currency format requirement", InvalidCurrencyIsRejected),
    ("Validated journal immutability", ValidatedJournalCopiesAndProtectsLines),
    ("ACC-INV-001 deterministic decimal distributions", DecimalDistributionsRemainBalanced),
    ("ACC-INV-005 canonical posting identity", PostingIdentityIsCanonicalAndComparable),
    ("ACC-INV-005 duplicate source rejection", DuplicateJournalSourceIsRejected),
    ("ACC-INV-005 scope separation and set immutability", PostingIdentityScopeAndDraftSetAreProtected),
    ("PARTY-INV-001 allocation amount and capacity boundaries", AllocationAmountBoundariesAreEnforced),
    ("PARTY-INV-002 allocation scope and currency boundaries", AllocationScopeAndCurrencyAreEnforced),
    ("PARTY-INV-003 multi-item allocation capacity", MultiItemAllocationCapacityIsEnforced),
    ("PARTY allocation order and immutability", AllocationOrderAndImmutabilityAreProtected),
};

var failures = new List<string>();
foreach (var check in checks)
{
    try
    {
        check.Run();
    }
    catch (Exception exception)
    {
        failures.Add($"{check.Name}: {exception.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("Domain unit checks failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine($"Domain unit checks passed: {checks.Length} checks.");
return 0;

static void BalancedJournalIsAccepted()
{
    var draft = CreateDefaultDraft(
        JournalLineDraft.Create(Guid.NewGuid(), Guid.NewGuid(), JournalAmount.Create(125.4321m, 0m)),
        JournalLineDraft.Create(Guid.NewGuid(), Guid.NewGuid(), JournalAmount.Create(0m, 125.4321m)));

    Equal(125.4321m, draft.TotalDebit, "Unexpected total debit.");
    Equal(125.4321m, draft.TotalCredit, "Unexpected total credit.");
    Equal(new DateOnly(2026, 8, 21), draft.EffectiveDate, "Effective date changed.");
    Equal(TimeSpan.Zero, draft.RecordedAt.Offset, "Recorded timestamp is not UTC.");
}

static void ImbalancedJournalIsRejected()
{
    ExpectInvariant(
        "JOURNAL_NOT_BALANCED",
        () => CreateDefaultDraft(
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(10.0001m, 0m)),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 10.0000m))));
}

static void InvalidJournalAmountsAreRejected()
{
    ExpectInvariant("JOURNAL_AMOUNT_NEGATIVE", () => JournalAmount.Create(-0.0001m, 0m));
    ExpectInvariant("JOURNAL_AMOUNT_SIDE_INVALID", () => JournalAmount.Create(0m, 0m));
    ExpectInvariant("JOURNAL_AMOUNT_SIDE_INVALID", () => JournalAmount.Create(1m, 1m));
    ExpectInvariant(
        "JOURNAL_ACCOUNT_REQUIRED",
        () => JournalLineDraft.Create(Guid.Empty, null, JournalAmount.Create(1m, 0m)));
    ExpectInvariant(
        "JOURNAL_SOURCE_LINE_INVALID",
        () => JournalLineDraft.Create(Guid.NewGuid(), Guid.Empty, JournalAmount.Create(1m, 0m)));
}

static void MissingContextIsRejected()
{
    var validLines = new[]
    {
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(1m, 0m)),
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 1m)),
    };

    ExpectInvariant(
        "JOURNAL_TENANT_REQUIRED",
        () => CreateConfiguredDraft(validLines, tenantId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_COMPANY_REQUIRED",
        () => CreateConfiguredDraft(validLines, companyId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_SOURCE_REQUIRED",
        () => CreateConfiguredDraft(validLines, sourceEventId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_RULE_VERSION_REQUIRED",
        () => CreateConfiguredDraft(validLines, postingRuleVersionId: Guid.Empty));
    ExpectInvariant(
        "JOURNAL_SOURCE_TYPE_REQUIRED",
        () => CreateConfiguredDraft(validLines, sourceType: " "));
    ExpectInvariant(
        "JOURNAL_PURPOSE_REQUIRED",
        () => CreateConfiguredDraft(validLines, postingPurpose: string.Empty));
    ExpectInvariant("JOURNAL_LINES_INSUFFICIENT", () => CreateDefaultDraft(validLines[..1]));
}

static void NonUtcRecordedTimeIsRejected()
{
    var localOffsetTime = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.FromHours(3));
    ExpectInvariant(
        "JOURNAL_RECORDED_AT_NOT_UTC",
        () => CreateConfiguredDraft(
            [
                JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(1m, 0m)),
                JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 1m)),
            ],
            recordedAt: localOffsetTime));
}

static void InvalidCurrencyIsRejected()
{
    ExpectInvariant("JOURNAL_CURRENCY_INVALID", () => CurrencyCode.Create("try"));
    ExpectInvariant("JOURNAL_CURRENCY_INVALID", () => CurrencyCode.Create("EURO"));
    Equal("TRY", CurrencyCode.Create("TRY").Value, "Currency code changed.");
}

static void ValidatedJournalCopiesAndProtectsLines()
{
    var originalLines = new[]
    {
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(50m, 0m)),
        JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, 50m)),
    };
    var firstAccountId = originalLines[0].AccountId;
    var draft = CreateDefaultDraft(originalLines);

    originalLines[0] = JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(999m, 0m));
    Equal(firstAccountId, draft.Lines[0].AccountId, "Validated journal retained a mutable input collection.");

    if (draft.Lines is IList<JournalLineDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = originalLines[0]);
    }
}

static void DecimalDistributionsRemainBalanced()
{
    for (var index = 1; index <= 100; index++)
    {
        var total = index * 0.0001m;
        var firstCredit = total / 4m;
        var secondCredit = total - firstCredit;
        var draft = CreateDefaultDraft(
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(total, 0m)),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, firstCredit)),
            JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, secondCredit)));

        Equal(draft.TotalDebit, draft.TotalCredit, $"Distribution {index} is not balanced.");
    }
}

static void PostingIdentityIsCanonicalAndComparable()
{
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var sourceEventId = Guid.NewGuid();
    var first = JournalPostingIdentity.Create(
        tenantId,
        companyId,
        " sales.invoice ",
        sourceEventId,
        " revenue ");
    var second = JournalPostingIdentity.Create(
        tenantId,
        companyId,
        "sales.invoice",
        sourceEventId,
        "revenue");

    Equal(first, second, "Canonical posting identities differ.");
    Equal("sales.invoice", first.SourceType, "Source type was not canonicalized.");
    Equal("revenue", first.PostingPurpose, "Posting purpose was not canonicalized.");
    ExpectInvariant(
        "JOURNAL_SOURCE_TYPE_REQUIRED",
        () => JournalPostingIdentity.Create(tenantId, companyId, " ", sourceEventId, "revenue"));
    ExpectInvariant(
        "JOURNAL_PURPOSE_REQUIRED",
        () => JournalPostingIdentity.Create(tenantId, companyId, "sales.invoice", sourceEventId, " "));
}

static void DuplicateJournalSourceIsRejected()
{
    var tenantId = Guid.NewGuid();
    var companyId = Guid.NewGuid();
    var sourceEventId = Guid.NewGuid();
    var first = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: companyId,
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var second = CreateConfiguredDraft(
        CreateBalancedLines(25m),
        tenantId: tenantId,
        companyId: companyId,
        sourceEventId: sourceEventId,
        postingRuleVersionId: Guid.NewGuid(),
        sourceType: " sales.invoice ",
        postingPurpose: " revenue ");

    ExpectInvariant(
        "JOURNAL_SOURCE_DUPLICATE",
        () => ValidatedJournalDraftSet.Create([first, second]));
}

static void PostingIdentityScopeAndDraftSetAreProtected()
{
    var tenantId = Guid.NewGuid();
    var sourceEventId = Guid.NewGuid();
    var firstCompanyId = Guid.NewGuid();
    var first = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: firstCompanyId,
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var second = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: Guid.NewGuid(),
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var third = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: firstCompanyId,
        sourceEventId: sourceEventId,
        sourceType: "sales.invoice",
        postingPurpose: "receivable");
    var fourth = CreateConfiguredDraft(
        CreateBalancedLines(10m),
        tenantId: tenantId,
        companyId: firstCompanyId,
        sourceEventId: Guid.NewGuid(),
        sourceType: "sales.invoice",
        postingPurpose: "revenue");
    var input = new[] { first, second, third, fourth };
    var draftSet = ValidatedJournalDraftSet.Create(input);

    input[0] = CreateDefaultDraft(CreateBalancedLines(99m));
    if (!ReferenceEquals(first, draftSet.Drafts[0]))
    {
        throw new InvalidOperationException("Validated draft set retained a mutable input collection.");
    }

    if (draftSet.Drafts is IList<ValidatedJournalDraft> list)
    {
        Throws<NotSupportedException>(() => list[0] = second);
    }

    ExpectInvariant("JOURNAL_DRAFT_SET_EMPTY", () => ValidatedJournalDraftSet.Create([]));
}

static void AllocationAmountBoundariesAreEnforced()
{
    var context = CreateAllocationTestContext();
    var openItem = CreateOpenItem(context, 10m);

    ExpectAllocationInvariant(
        "ALLOCATION_AMOUNT_INVALID",
        () => PartyAllocations.AllocationPlanLine.Create(openItem, 0m));
    ExpectAllocationInvariant(
        "ALLOCATION_OPEN_ITEM_EXCEEDED",
        () => PartyAllocations.AllocationPlanLine.Create(openItem, 10.0001m));
    ExpectAllocationInvariant(
        "ALLOCATION_PAYMENT_CAPACITY_INVALID",
        () => CreatePayment(context, 0m));
    ExpectAllocationInvariant(
        "ALLOCATION_OPEN_ITEM_CAPACITY_INVALID",
        () => CreateOpenItem(context, -0.0001m));
    ExpectAllocationInvariant(
        "ALLOCATION_CURRENCY_INVALID",
        () => PartyAllocations.AllocationCurrencyCode.Create("gbp"));

    var exactLine = PartyAllocations.AllocationPlanLine.Create(openItem, 10m);
    Equal(0m, exactLine.OpenItemRemainingAfter, "Exact allocation left an unexpected remainder.");
}

static void AllocationScopeAndCurrencyAreEnforced()
{
    var context = CreateAllocationTestContext();
    var payment = CreatePayment(context, 100m);

    ExpectAllocationInvariant(
        "ALLOCATION_TENANT_MISMATCH",
        () => CreateAllocationPlan(payment, CreateOpenItem(context with { TenantId = Guid.NewGuid() }, 10m), 10m));
    ExpectAllocationInvariant(
        "ALLOCATION_COMPANY_MISMATCH",
        () => CreateAllocationPlan(payment, CreateOpenItem(context with { CompanyId = Guid.NewGuid() }, 10m), 10m));
    ExpectAllocationInvariant(
        "ALLOCATION_PARTY_ACCOUNT_MISMATCH",
        () => CreateAllocationPlan(payment, CreateOpenItem(context with { PartyAccountId = Guid.NewGuid() }, 10m), 10m));
    ExpectAllocationInvariant(
        "ALLOCATION_CROSS_CURRENCY_REQUIRES_RATE_SNAPSHOT",
        () => CreateAllocationPlan(
            payment,
            CreateOpenItem(context with { Currency = PartyAllocations.AllocationCurrencyCode.Create("EUR") }, 10m),
            10m));
}

static void MultiItemAllocationCapacityIsEnforced()
{
    var context = CreateAllocationTestContext();
    var payment = CreatePayment(context, 100m);
    var firstItem = CreateOpenItem(context, 70m);
    var secondItem = CreateOpenItem(context, 80m);
    var plan = PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
        payment,
        [
            PartyAllocations.AllocationPlanLine.Create(firstItem, 60m),
            PartyAllocations.AllocationPlanLine.Create(secondItem, 40m),
        ]);

    Equal(100m, plan.TotalAllocated, "Unexpected total allocation.");
    Equal(0m, plan.PaymentRemainingAfter, "Unexpected payment remainder.");
    Equal(10m, plan.Lines[0].OpenItemRemainingAfter, "Unexpected first open-item remainder.");
    Equal(40m, plan.Lines[1].OpenItemRemainingAfter, "Unexpected second open-item remainder.");

    ExpectAllocationInvariant(
        "ALLOCATION_PAYMENT_EXCEEDED",
        () => PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
            payment,
            [
                PartyAllocations.AllocationPlanLine.Create(firstItem, 70m),
                PartyAllocations.AllocationPlanLine.Create(secondItem, 30.0001m),
            ]));
    ExpectAllocationInvariant(
        "ALLOCATION_OPEN_ITEM_DUPLICATE",
        () => PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
            payment,
            [
                PartyAllocations.AllocationPlanLine.Create(firstItem, 30m),
                PartyAllocations.AllocationPlanLine.Create(firstItem, 20m),
            ]));
    ExpectAllocationInvariant(
        "ALLOCATION_PAYMENT_EXCEEDED",
        () => PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
            CreatePayment(context, decimal.MaxValue),
            [
                PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, decimal.MaxValue), decimal.MaxValue),
                PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 1m), 1m),
            ]));
}

static void AllocationOrderAndImmutabilityAreProtected()
{
    var context = CreateAllocationTestContext();
    var payment = CreatePayment(context, 100m);
    var firstLine = PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 70m), 60m);
    var secondLine = PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 80m), 30m);
    var input = new[] { firstLine, secondLine };
    var firstPlan = PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(payment, input);
    var reversedPlan = PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(payment, input.Reverse());

    Equal(firstPlan.TotalAllocated, reversedPlan.TotalAllocated, "Line order changed the total allocation.");
    Equal(firstPlan.PaymentRemainingAfter, reversedPlan.PaymentRemainingAfter, "Line order changed the payment remainder.");

    input[0] = PartyAllocations.AllocationPlanLine.Create(CreateOpenItem(context, 1m), 1m);
    if (!ReferenceEquals(firstLine, firstPlan.Lines[0]))
    {
        throw new InvalidOperationException("Validated allocation retained a mutable input collection.");
    }

    if (firstPlan.Lines is IList<PartyAllocations.AllocationPlanLine> list)
    {
        Throws<NotSupportedException>(() => list[0] = input[0]);
    }
}

static AllocationTestContext CreateAllocationTestContext() =>
    new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        PartyAllocations.AllocationCurrencyCode.Create("GBP"));

static PartyAllocations.PaymentAllocationCapacity CreatePayment(
    AllocationTestContext context,
    decimal usableAmount) =>
    PartyAllocations.PaymentAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        usableAmount);

static PartyAllocations.OpenItemAllocationCapacity CreateOpenItem(
    AllocationTestContext context,
    decimal remainingAmount) =>
    PartyAllocations.OpenItemAllocationCapacity.Create(
        context.TenantId,
        context.CompanyId,
        context.PartyAccountId,
        Guid.NewGuid(),
        context.Currency,
        remainingAmount);

static PartyAllocations.ValidatedSameCurrencyAllocationPlan CreateAllocationPlan(
    PartyAllocations.PaymentAllocationCapacity payment,
    PartyAllocations.OpenItemAllocationCapacity openItem,
    decimal amount) =>
    PartyAllocations.ValidatedSameCurrencyAllocationPlan.Create(
        payment,
        [PartyAllocations.AllocationPlanLine.Create(openItem, amount)]);

static void ExpectAllocationInvariant(string expectedCode, Action action)
{
    var exception = Throws<PartyAllocations.AllocationInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected allocation invariant code.");
}

static JournalLineDraft[] CreateBalancedLines(decimal amount) =>
[
    JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(amount, 0m)),
    JournalLineDraft.Create(Guid.NewGuid(), null, JournalAmount.Create(0m, amount)),
];

static ValidatedJournalDraft CreateDefaultDraft(
    params JournalLineDraft[] lines) => CreateConfiguredDraft(lines);

static ValidatedJournalDraft CreateConfiguredDraft(
    IEnumerable<JournalLineDraft> lines,
    Guid? tenantId = null,
    Guid? companyId = null,
    Guid? sourceEventId = null,
    Guid? postingRuleVersionId = null,
    string sourceType = "synthetic.accounting-event",
    string postingPurpose = "technical-invariant-spike",
    DateTimeOffset? recordedAt = null) =>
    ValidatedJournalDraft.Create(
        tenantId ?? Guid.NewGuid(),
        companyId ?? Guid.NewGuid(),
        sourceEventId ?? Guid.NewGuid(),
        postingRuleVersionId ?? Guid.NewGuid(),
        sourceType,
        postingPurpose,
        new DateOnly(2026, 8, 21),
        recordedAt ?? new DateTimeOffset(2026, 8, 21, 9, 0, 0, TimeSpan.Zero),
        CurrencyCode.Create("TRY"),
        lines);

static void ExpectInvariant(string expectedCode, Action action)
{
    var exception = Throws<JournalInvariantException>(action);
    Equal(expectedCode, exception.Code, "Unexpected invariant code.");
}

static TException Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

static void Equal<T>(T expected, T actual, string message)
    where T : IEquatable<T>
{
    if (!actual.Equals(expected))
    {
        throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
    }
}

internal sealed record AllocationTestContext(
    Guid TenantId,
    Guid CompanyId,
    Guid PartyAccountId,
    PartyAllocations.AllocationCurrencyCode Currency);

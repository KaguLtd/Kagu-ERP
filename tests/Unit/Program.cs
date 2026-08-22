using KaguERP.Modules.Accounting.Domain.Journals;

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

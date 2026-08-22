namespace KaguERP.Modules.Accounting.Domain.Periods;

public sealed record PeriodCloseTransition
{
    private PeriodCloseTransition(PeriodCloseStage from, PeriodCloseStage to)
    {
        From = from;
        To = to;
    }

    public PeriodCloseStage From { get; }
    public PeriodCloseStage To { get; }

    public static PeriodCloseTransition Create(PeriodCloseStage from, PeriodCloseStage to)
    {
        RequireStage(from);
        RequireStage(to);

        if (from == to)
        {
            throw new PeriodInvariantException(
                "PERIOD_TRANSITION_NO_CHANGE",
                "A period close transition must change the stage.");
        }

        if (to < from)
        {
            throw new PeriodInvariantException(
                "PERIOD_REOPEN_REQUIRES_APPROVED_WORKFLOW",
                "A backward period transition requires an approved reopen workflow snapshot.");
        }

        if ((int)to != (int)from + 1)
        {
            throw new PeriodInvariantException(
                "PERIOD_TRANSITION_INVALID",
                "A period close transition cannot skip a close stage.");
        }

        return new PeriodCloseTransition(from, to);
    }

    private static void RequireStage(PeriodCloseStage stage)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new PeriodInvariantException("PERIOD_CLOSE_STAGE_INVALID", "Period close stage is invalid.");
        }
    }
}

using KaguERP.Modules.Reporting.Application.PartyReports;

namespace KaguERP.Bootstrap;

public sealed class UnavailablePartyAccountDetailReportQuery : IPartyAccountDetailReportQuery
{
    public ValueTask<PartyAccountDetailReportQueryResult> ExecuteAsync(
        PartyAccountDetailReportQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(new PartyAccountDetailReportQueryResult(
            PartyAccountDetailReportQueryOutcome.Unavailable,
            null));
    }
}

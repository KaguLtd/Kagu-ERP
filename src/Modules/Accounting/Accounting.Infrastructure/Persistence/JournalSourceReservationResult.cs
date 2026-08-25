namespace KaguERP.Modules.Accounting.Infrastructure.Persistence;

public sealed record JournalSourceReservationResult(Guid ReservationId, bool Created, string DraftHash);

public sealed class JournalSourceReservationConflictException : Exception
{
    public JournalSourceReservationConflictException(Guid existingReservationId)
        : base("The journal source identity is already reserved with different validated content.")
    {
        ExistingReservationId = existingReservationId;
    }

    public Guid ExistingReservationId { get; }
}

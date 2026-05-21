namespace Ambiquality.Evidence.Api.Domain.Rooms;

/// <summary>Raised when a referenced room does not exist.</summary>
public sealed class RoomNotFoundException : DomainException
{
    public RoomNotFoundException(Guid roomId)
        : base($"Room '{roomId}' was not found.") { }
}

/// <summary>
/// Raised when removing a pollution source that has no open history row on the
/// room — there is nothing to close.
/// </summary>
public sealed class PollutionSourceNotFoundException : DomainException
{
    public PollutionSourceNotFoundException(string sourceCode)
        : base($"No open pollution source '{sourceCode}' was found on this room.") { }
}

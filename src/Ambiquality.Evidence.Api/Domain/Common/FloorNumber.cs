namespace Ambiquality.Evidence.Api.Domain.Common;

public sealed record FloorNumber
{
    public byte Value { get; }

    private FloorNumber(byte value)
    {
        Value = value;
    }

    public static FloorNumber Create(byte value)
    {
        if (value > 100)
            throw new ArgumentException("Floor number must be between 0 and 100", nameof(value));

        return new FloorNumber(value);
    }

    public static implicit operator byte(FloorNumber floor) => floor.Value;
}

using System.Globalization;
using System.Text;

namespace Ambiquality.Public.Api.Application.Observations;

/// <summary>
/// Opaque keyset cursor capturing the last seen <c>(received_at, id)</c> tuple.
/// Encoded as Base64URL of <c>"{received_at:O}|{id:D}"</c> so it round-trips
/// without colliding with URL-reserved characters. The ISO-8601 "O" form keeps the
/// UTC kind; received_at is microsecond-precise in the DB, so the parsed value
/// compares equal to the stored one.
/// </summary>
public sealed record ObservationCursor(DateTime ReceivedAt, Guid Id)
{
    public string Encode()
    {
        var raw = $"{ReceivedAt:O}|{Id:D}";
        return Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? value, out ObservationCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            var raw = Encoding.UTF8.GetString(Base64UrlDecode(value));
            var separator = raw.IndexOf('|');
            if (separator < 0)
                return false;

            var timePart = raw[..separator];
            var idPart = raw[(separator + 1)..];

            if (!DateTime.TryParse(timePart, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var receivedAt))
                return false;
            if (!Guid.TryParse(idPart, out var id))
                return false;

            cursor = new ObservationCursor(receivedAt, id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        return Convert.FromBase64String(s);
    }
}

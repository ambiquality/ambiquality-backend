using System.Text.Json;
using Ambiquality.Evidence.Api.Application.Abstractions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ambiquality.Evidence.Api.Api;

/// <summary>
/// Operator-facing address autocomplete over RÚIAN (ČÚZK, CC BY 4.0). Lets the building
/// registration form be filled from a picked suggestion instead of the operator hand-copying
/// the OFN <c>Adresy</c> components. Authenticated only — it is a convenience for registrars,
/// not an open geocoding proxy. The resolved address is still re-validated by the registration
/// command, so this never bypasses the domain rules.
/// </summary>
public static class AddressLookupEndpoints
{
    public static void MapAddressLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/address-lookup")
            .WithTags("AddressLookup")
            .RequireAuthorization();

        group.MapGet("/suggest", Suggest)
            .WithName("SuggestAddresses")
            .WithDescription("Autocomplete Czech addresses against RÚIAN (ČÚZK, CC BY 4.0)");

        group.MapGet("/resolve", Resolve)
            .WithName("ResolveAddress")
            .WithDescription("Resolve a suggestion key to the full OFN address (RÚIAN codes + WGS84 coordinates)");
    }

    private static async Task<Results<Ok<AddressSuggestionsResponse>, ProblemHttpResult>> Suggest(
        string? q,
        int? limit,
        IAddressGeocoder geocoder,
        CancellationToken cancellationToken)
    {
        try
        {
            var suggestions = await geocoder.SuggestAsync(q ?? string.Empty, limit ?? 10, cancellationToken);
            return TypedResults.Ok(new AddressSuggestionsResponse(suggestions));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller went away — not an upstream failure.
        }
        catch (Exception ex) when (IsUpstreamFailure(ex))
        {
            return Problems.AddressLookupUnavailable(ex.Message);
        }
    }

    private static async Task<Results<Ok<ResolvedAddress>, ProblemHttpResult>> Resolve(
        string? key,
        IAddressGeocoder geocoder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Problems.InvalidAttributeValue("The 'key' query parameter is required.");

        try
        {
            var resolved = await geocoder.ResolveAsync(key, cancellationToken);
            return resolved is null
                ? Problems.AddressNotFound()
                : TypedResults.Ok(resolved);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsUpstreamFailure(ex))
        {
            return Problems.AddressLookupUnavailable(ex.Message);
        }
    }

    // A transport, timeout or malformed-response failure from the external RÚIAN service — surfaced
    // as 502 (not 500) so the client degrades to manual entry rather than treating it as our fault.
    private static bool IsUpstreamFailure(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException;
}

/// <summary>Autocomplete suggestions for the address-lookup endpoint.</summary>
public sealed record AddressSuggestionsResponse(IReadOnlyList<AddressSuggestion> Suggestions);

namespace Ambiquality.Evidence.Api.Application.Abstractions;

/// <summary>
/// Issues per-sensor API keys. The plaintext key is shown to the operator
/// exactly once at registration; only its SHA-256 hash is persisted. SHA-256
/// (not a password KDF) is deliberate: keys are high-entropy random values, so
/// a single fast hash is collision/brute-force safe while keeping ingestion-side
/// verification cheap enough for the ≥100 msg/s throughput target.
/// </summary>
public interface ISensorApiKeyService
{
    /// <summary>Generates a fresh plaintext key and the hash to store for it.</summary>
    (string PlainKey, string KeyHash) Generate();
}

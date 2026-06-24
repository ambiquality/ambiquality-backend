# Parameters & units

Every reading you send must use the **canonical unit** for its parameter, and its value
must fall within the **permitted range**. A different unit rejects the batch
(`unit-mismatch`); a value outside the range rejects it (`value-out-of-range`).

These are the **built-in parameters** seeded by the platform. (Operators of an instance
may add extension parameters with their own canonical unit and range; ask your platform
administrator if you need one that isn't listed.)

| Parameter code | Quantity | Canonical unit | Permitted range |
|----------------|----------|----------------|-----------------|
| `co2` | Carbon dioxide | `ppm` | 0 – 50 000 |
| `eco2` | Equivalent CO₂ | `ppm` | 0 – 65 000 |
| `co` | Carbon monoxide | `ppm` | 0 – 2 000 |
| `o3` | Ozone | `µg/m³` | 0 – 500 |
| `no2` | Nitrogen dioxide | `µg/m³` | 0 – 500 |
| `so2` | Sulphur dioxide | `µg/m³` | 0 – 500 |
| `voc` | Volatile organic compounds | `ppb` | 0 – 60 000 |
| `pm1` | Particulate matter ≤ 1 µm | `µg/m³` | 0 – 500 |
| `pm2_5` | Particulate matter ≤ 2.5 µm | `µg/m³` | 0 – 500 |
| `pm4` | Particulate matter ≤ 4 µm | `µg/m³` | 0 – 1 000 |
| `pm10` | Particulate matter ≤ 10 µm | `µg/m³` | 0 – 1 000 |
| `temperature` | Air temperature | `°C` | −40 – 85 |
| `humidity` | Relative humidity | `%` | 0 – 100 |
| `air_velocity` | Air velocity | `m/s` | 0 – 10 |
| `pressure` | Atmospheric pressure | `Pa` | 85 000 – 110 000 |
| `illuminance` | Illuminance | `lx` | 0 – 100 000 |
| `cct` | Correlated colour temperature | `K` | 1 000 – 20 000 |
| `laeq` | A-weighted equivalent sound level | `dB(A)` | 0 – 140 |

## Notes

- The **unit string must match exactly**, including the casing and the symbols
  (`µg/m³`, `dB(A)`, `°C`). Send the unit string from the table verbatim.
- A sensor may only report parameters it **declared** at registration. Declaring
  `co2` and `temperature`, then sending `humidity`, rejects the batch
  (`parameter-not-declared`) — add the parameter to the sensor first.
- Ranges are **inclusive** sanity bounds, not calibration limits. They exist to catch
  obviously broken readings (a stuck probe, a wrong unit), not to grade air quality.

The authoritative, machine-readable list for a running instance is the
`ieq.parameter_ranges` table; the table above is the seeded default.

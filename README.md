# ETS2/ATS Telemetry

A Macro Deck 3 plugin that reads Euro Truck Simulator 2 / American Truck Simulator telemetry and exposes it as variables for use in your deck layouts.

## Requirements

- Macro Deck 3 (3.0.0-beta.15 or later)
- An ETS2/ATS telemetry server running on `localhost:25555` (e.g. the [ETS2/ATS Telemetry Web Server](https://github.com/Funbit/ets2-telemetry-web-server))

## What it does

The plugin polls the telemetry server every 200ms and exposes the following variables:

| Variable | Type | Description |
| --- | --- | --- |
| `speed` | Text | Current speed (km/h or mph) |
| `fuel-percent` | Text | Fuel level as a percentage |
| `gear` | Text | Current gear (N, 1-12, R1-R2) |
| `speed-limit` | Text | Current speed limit |
| `low-beam-on` | Boolean | Low beam lights |
| `high-beam-on` | Boolean | High beam lights |
| `parking-lights-on` | Boolean | Parking lights |
| `left-blinker-on` | Boolean | Left turn signal |
| `right-blinker-on` | Boolean | Right turn signal |
| `hazard-lights-on` | Boolean | Hazard lights |
| `parking-brake-on` | Boolean | Parking brake |
| `cruise-control-on` | Boolean | Cruise control active |
| `cruise-control-speed` | Text | Cruise control set speed |

## Widgets

| Widget | Description |
| --- | --- |
| `ETS2 Gauge` | Configurable speedometer or fuel gauge |
| `ETS2 Indicator` | Gradient arc gauge with a choice of sleepiness or engine RPM |
| `Destination Distance` | Digital readout of the remaining route distance. Choose kilometres or miles in the widget settings; miles are converted from the telemetry distance in metres |

Sleepiness is estimated from the telemetry server's time until the game starts yawning, scaled across
an 11-hour driving interval. RPM is scaled against the truck's reported maximum engine RPM. The
indicator and distance counter show `--` when their required telemetry data is unavailable.

## Actions

| Action | Description |
| --- | --- |
| `start-telemetry` | Starts polling the telemetry server |
| `stop-telemetry` | Stops polling the telemetry server |

## Configuration

The plugin has a one-step config flow with a single setting:

- **Use mph** - When enabled, speed values are shown in mph instead of km/h

## Building gauges

The existing `ETS2 Gauge` uses telemetry variables for speed or fuel. The separate `ETS2 Indicator`
and `Destination Distance` widgets display their telemetry directly. Add the indicator to a deck and
choose sleepiness or RPM in its settings. In the destination counter settings, choose kilometres or
miles.

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```

## License

MIT

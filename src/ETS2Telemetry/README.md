# ETS2/ATS Telemetry Plugin

A Macro Deck 3 plugin that reads Euro Truck Simulator 2 / American Truck Simulator telemetry and exposes it as variables.

## Requirements

- Macro Deck 3 (3.0.0-beta.15 or later)
- An ETS2/ATS telemetry server running on `localhost:25555`

## Variables

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
| `ETS2 Gauge` | Existing configurable speedometer or fuel gauge |
| `ETS2 Indicator` | Gradient arc gauge. Choose sleepiness or engine RPM in the widget configuration |
| `Destination Distance` | Digital readout of the remaining route distance. Choose kilometres or miles in the widget settings; miles are converted from the telemetry distance in metres |

The sleepiness gauge estimates fatigue from the telemetry server's time until the game starts yawning,
scaled across an 11-hour driving interval. The RPM gauge uses the engine's current and maximum RPM.
The indicator and distance counter show `--` when their required telemetry data is unavailable.

## Actions

| Action | Description |
| --- | --- |
| `start-telemetry` | Starts polling the telemetry server |
| `stop-telemetry` | Stops polling the telemetry server |

## Configuration

- **Use mph** - When enabled, speed values are shown in mph instead of km/h

## Building gauges

The existing `ETS2 Gauge` uses telemetry variables for speed or fuel. The separate `ETS2 Indicator`
and `Destination Distance` widgets display their telemetry directly. Add the indicator to a deck and
choose sleepiness or RPM in its settings. In the destination counter settings, choose kilometres or
miles.

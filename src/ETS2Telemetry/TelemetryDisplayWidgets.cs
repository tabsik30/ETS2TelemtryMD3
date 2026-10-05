using System.Globalization;
using System.Text.Json;
using ETS2Telemetry.Services;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Widgets;
using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;

namespace ETS2Telemetry;

internal sealed class TelemetryDisplayWidgetProvider(TelemetryPollingService telemetry)
    : IWidgetTypeProvider, IUiProvider
{
    private const string IndicatorType = "indicator";
    private const string IndicatorQualifiedType = "com.tabsik12.ets2-telemetry::indicator";
    private const string DistanceType = "destination-distance";
    private const string DistanceQualifiedType = "com.tabsik12.ets2-telemetry::destination-distance";
    private const int ArcSegmentCount = 32;
    private static readonly TimeSpan FatigueInterval = TimeSpan.FromHours(11);

    private static readonly string IndicatorSchema = """
        {
          "type": "object",
          "properties": {
            "mode": { "type": "string", "enum": ["fatigue", "rpm"] }
          },
          "additionalProperties": true
        }
        """;

    private static readonly string DistanceSchema = """
        {
          "type": "object",
          "properties": {
            "unit": { "type": "string", "enum": ["km", "mi"] }
          },
          "additionalProperties": true
        }
        """;

    public string ProviderName => "ETS2 Telemetry";

    public IReadOnlyList<UiSurfaceDeclaration> Surfaces { get; } =
    [
        new UiSurfaceDeclaration { Kind = UiSurfaceKinds.Widget, SessionMode = UiSessionModes.Shared },
        new UiSurfaceDeclaration { Kind = UiSurfaceKinds.Preview, SessionMode = UiSessionModes.Shared },
        new UiSurfaceDeclaration { Kind = UiSurfaceKinds.Config, SessionMode = UiSessionModes.Exclusive },
    ];

    public IReadOnlyList<WidgetTypeDescriptor> GetWidgetTypes() =>
    [
        new WidgetTypeDescriptor(
            IndicatorType,
            "ETS2 Indicator",
            "Gradient arc gauge for sleepiness or engine RPM",
            "{\"mode\":\"fatigue\"}",
            IndicatorSchema,
            HasConfiguration: true),
        new WidgetTypeDescriptor(
            DistanceType,
            "Destination Distance",
            "Digital counter for the remaining route distance in kilometres or miles",
            "{\"unit\":\"km\"}",
            DistanceSchema,
            HasConfiguration: true),
    ];

    public async Task InitializeAsync(
        IWidgetTypeProviderContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var widgetType in GetWidgetTypes())
        {
            await context.RegisterWidgetTypeAsync(widgetType, cancellationToken);
        }
    }

    public Task<IUiSession?> CreateSessionAsync(
        UiSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Surface.Kind == UiSurfaceKinds.Config)
        {
            if (request.Surface.Attributes.TryGetValue(UiWidgetSurfaceAttributes.WidgetType, out var configType) &&
                configType.GetString() == IndicatorQualifiedType)
            {
                return Task.FromResult<IUiSession?>(new IndicatorConfigurationSession(request.Surface));
            }

            if (request.Surface.Attributes.TryGetValue(UiWidgetSurfaceAttributes.WidgetType, out configType) &&
                configType.GetString() == DistanceQualifiedType)
            {
                return Task.FromResult<IUiSession?>(new DistanceConfigurationSession(request.Surface));
            }

            return Task.FromResult<IUiSession?>(null);
        }

        if (request.Surface.Kind is not UiSurfaceKinds.Widget and not UiSurfaceKinds.Preview ||
            !request.Surface.Attributes.TryGetValue(UiWidgetSurfaceAttributes.WidgetType, out var typeElement))
        {
            return Task.FromResult<IUiSession?>(null);
        }

        var widgetType = typeElement.GetString();
        if (widgetType == IndicatorQualifiedType)
        {
            var mode = ReadMode(request.Surface);
            return Task.FromResult<IUiSession?>(new TelemetryDisplaySession(request.Surface, telemetry, mode));
        }

        if (widgetType == DistanceQualifiedType)
        {
            return Task.FromResult<IUiSession?>(
                new TelemetryDisplaySession(request.Surface, telemetry, null, ReadDistanceUnit(request.Surface)));
        }

        return Task.FromResult<IUiSession?>(null);
    }

    private static string ReadMode(UiSurface surface)
    {
        if (surface.Attributes.TryGetValue(UiWidgetSurfaceAttributes.Data, out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("mode", out var mode) &&
            mode.ValueKind == JsonValueKind.String)
        {
            return mode.GetString() == "rpm" ? "rpm" : "fatigue";
        }

        if (surface.Attributes.TryGetValue("widgetData", out var widgetData) &&
            widgetData.ValueKind == JsonValueKind.Object &&
            widgetData.TryGetProperty("mode", out var configMode) &&
            configMode.ValueKind == JsonValueKind.String)
        {
            return configMode.GetString() == "rpm" ? "rpm" : "fatigue";
        }

        return "fatigue";
    }

    private static string ReadDistanceUnit(UiSurface surface)
    {
        if (TryReadStringData(surface, UiWidgetSurfaceAttributes.Data, "unit", out var unit) ||
            TryReadStringData(surface, "widgetData", "unit", out unit))
        {
            return unit == "mi" ? "mi" : "km";
        }

        return "km";
    }

    private static bool TryReadStringData(
        UiSurface surface,
        string dataAttribute,
        string propertyName,
        out string value)
    {
        if (surface.Attributes.TryGetValue(dataAttribute, out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? "";
            return true;
        }

        value = "";
        return false;
    }

    private sealed class IndicatorConfigurationSession : IUiSession
    {
        private readonly UiView _view;

        public IndicatorConfigurationSession(UiSurface surface)
        {
            var mode = new UiState<string>(ReadMode(surface));
            var root = new UiWidgetConfiguration
            {
                Key = "root",
                Properties = new UiWidgetProperties
                {
                    Key = "properties",
                    Children =
                    [
                        new UiChoiceInput
                        {
                            Key = "mode",
                            Label = "Indicator",
                            Binding = Bind.To(mode),
                            Options = UiValue.Of<IReadOnlyList<UiOption>>(
                            [
                                UiOption.Of("fatigue", "Sleepiness"),
                                UiOption.Of("rpm", "Engine RPM"),
                            ]),
                            Segmented = UiValue.Of(true),
                        },
                    ],
                },
            };
            _view = new UiView(surface, root);
        }

        public event EventHandler? Changed
        {
            add => _view.Changed += value;
            remove => _view.Changed -= value;
        }

        public event EventHandler<UiSessionFaultedEventArgs>? Faulted
        {
            add { }
            remove { }
        }

        public UiTree BuildTree() => _view.Tree;

        public IReadOnlyList<UiPatch> DrainPatches() => _view.DrainPatches();

        public void Dispatch(UiEvent uiEvent) => _view.Dispatch(uiEvent);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DistanceConfigurationSession : IUiSession
    {
        private readonly UiView _view;

        public DistanceConfigurationSession(UiSurface surface)
        {
            var unit = new UiState<string>(ReadDistanceUnit(surface));
            var root = new UiWidgetConfiguration
            {
                Key = "root",
                Properties = new UiWidgetProperties
                {
                    Key = "properties",
                    Children =
                    [
                        new UiChoiceInput
                        {
                            Key = "unit",
                            Label = "Distance unit",
                            Binding = Bind.To(unit),
                            Options = UiValue.Of<IReadOnlyList<UiOption>>(
                            [
                                UiOption.Of("km", "Kilometres"),
                                UiOption.Of("mi", "Miles"),
                            ]),
                            Segmented = UiValue.Of(true),
                        },
                    ],
                },
            };
            _view = new UiView(surface, root);
        }

        public event EventHandler? Changed
        {
            add => _view.Changed += value;
            remove => _view.Changed -= value;
        }

        public event EventHandler<UiSessionFaultedEventArgs>? Faulted
        {
            add { }
            remove { }
        }

        public UiTree BuildTree() => _view.Tree;

        public IReadOnlyList<UiPatch> DrainPatches() => _view.DrainPatches();

        public void Dispatch(UiEvent uiEvent) => _view.Dispatch(uiEvent);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TelemetryDisplaySession : IUiSession
    {
        private readonly UiSurface _surface;
        private readonly TelemetryPollingService _telemetry;
        private readonly string? _mode;
        private readonly string _distanceUnit;
        private readonly CancellationTokenSource _stop = new();
        private readonly CancellationToken _cancellationToken;
        private readonly object _sync = new();
        private readonly List<UiPatch> _patches = [];
        private UiTree _tree;
        private Reading _lastReading;
        private int _revision;
        private bool _disposed;

        public TelemetryDisplaySession(
            UiSurface surface,
            TelemetryPollingService telemetry,
            string? mode,
            string distanceUnit = "km")
        {
            _surface = surface;
            _telemetry = telemetry;
            _mode = mode;
            _distanceUnit = distanceUnit;
            _cancellationToken = _stop.Token;
            _lastReading = ReadReading();
            _tree = new UiTree
            {
                Revision = 0,
                Surface = surface,
                Root = BuildRoot(_lastReading),
            };
            _ = UpdateLoopAsync();
        }

        public event EventHandler? Changed;

        public event EventHandler<UiSessionFaultedEventArgs>? Faulted
        {
            add { }
            remove { }
        }

        public UiTree BuildTree()
        {
            lock (_sync)
            {
                return _tree;
            }
        }

        public IReadOnlyList<UiPatch> DrainPatches()
        {
            lock (_sync)
            {
                var patches = _patches.ToArray();
                _patches.Clear();
                return patches;
            }
        }

        public void Dispatch(UiEvent uiEvent)
        {
        }

        public ValueTask DisposeAsync()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return ValueTask.CompletedTask;
                }

                _disposed = true;
            }

            _stop.Cancel();
            _stop.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task UpdateLoopAsync()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var reading = ReadReading();
                    if (reading != _lastReading)
                    {
                        Update(reading);
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(100), _cancellationToken);
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
            }
        }

        private Reading ReadReading()
        {
            if (_mode is null)
            {
                if (_telemetry.EstimatedDistanceMeters is not { } distanceMeters)
                {
                    return new Reading("--", "KM TO DESTINATION", 0);
                }

                var distance = _distanceUnit == "mi" ? distanceMeters / 1609.344 : distanceMeters / 1000;
                var unitLabel = _distanceUnit == "mi" ? "MI TO DESTINATION" : "KM TO DESTINATION";
                return new Reading(distance.ToString("0.0", CultureInfo.InvariantCulture), unitLabel, 0);
            }

            if (_mode == "rpm")
            {
                if (_telemetry.EngineRpm is not { } engineRpm ||
                    _telemetry.EngineRpmMax is not { } engineRpmMax ||
                    engineRpmMax <= 0)
                {
                    return new Reading("--", "RPM", 0);
                }

                var rpm = Math.Max(0, engineRpm);
                var filledSegments = (int)Math.Round(
                    Math.Clamp(rpm / engineRpmMax, 0, 1) * ArcSegmentCount);
                return new Reading(
                    Math.Round(rpm).ToString("0", CultureInfo.InvariantCulture),
                    "RPM",
                    filledSegments);
            }

            if (_telemetry.TimeUntilYawning is not { } timeUntilYawning)
            {
                return new Reading("--", "SLEEPINESS", 0);
            }

            var sleepiness = Math.Clamp(
                1 - timeUntilYawning.TotalSeconds / FatigueInterval.TotalSeconds,
                0,
                1);
            var percentage = (int)Math.Round(sleepiness * 100);
            return new Reading(
                $"{percentage}%",
                "SLEEPINESS",
                (int)Math.Round(sleepiness * ArcSegmentCount));
        }

        private void Update(Reading reading)
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                var previousFilledSegments = _lastReading.FilledSegments;
                _lastReading = reading;
                _revision++;
                var operations = new List<UiPatchOperation>
                {
                    new()
                    {
                        Op = UiPatchOperations.SetProperties,
                        NodeId = "value-text",
                        Properties = new Dictionary<string, JsonElement>
                        {
                            ["text"] = JsonSerializer.SerializeToElement(reading.Value),
                        },
                    },
                };

                if (_mode is not null)
                {
                    for (var index = 0; index < ArcSegmentCount; index++)
                    {
                        if ((index < previousFilledSegments) == (index < reading.FilledSegments))
                        {
                            continue;
                        }

                        operations.Add(new UiPatchOperation
                        {
                            Op = UiPatchOperations.SetProperties,
                            NodeId = $"arc-segment-{index}-text",
                            Properties = new Dictionary<string, JsonElement>
                            {
                                ["color"] = JsonSerializer.SerializeToElement(
                                    index < reading.FilledSegments ? GradientColor(index) : "#333333"),
                            },
                        });
                    }
                }

                _patches.Add(new UiPatch
                {
                    FromRevision = _revision - 1,
                    ToRevision = _revision,
                    Operations = operations,
                });
                _tree = new UiTree { Revision = _revision, Surface = _surface, Root = _tree.Root };
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private UiNode BuildRoot(Reading reading)
        {
            var children = new List<UiNode> { BuildBackgroundNode() };
            if (_mode is not null)
            {
                for (var index = 0; index < ArcSegmentCount; index++)
                {
                    var angle = -120 + index * 240.0 / (ArcSegmentCount - 1);
                    children.Add(new UiNode
                    {
                        Id = $"arc-segment-{index}",
                        Type = "ui.transform",
                        Properties = TransformProperties(angle),
                        Children =
                        [
                            new UiNode
                            {
                                Id = $"arc-segment-{index}-position",
                                Type = "ui.stack",
                                Properties = new Dictionary<string, JsonElement>
                                {
                                    ["fill"] = JsonSerializer.SerializeToElement(true),
                                    ["direction"] = JsonSerializer.SerializeToElement("vertical"),
                                    ["justify"] = JsonSerializer.SerializeToElement("start"),
                                    ["align"] = JsonSerializer.SerializeToElement("center"),
                                    ["padding"] = JsonSerializer.SerializeToElement(new { basis = 0.08 }),
                                },
                                Children =
                                [
                                    new UiNode
                                    {
                                        Id = $"arc-segment-{index}-text",
                                        Type = "ui.text",
                                        Properties = TextProperties(
                                            "●",
                                            0.075,
                                            index < reading.FilledSegments ? GradientColor(index) : "#333333",
                                            "center"),
                                    },
                                ],
                            },
                        ],
                    });
                }
            }

            children.Add(new UiNode
            {
                Id = "value",
                Type = "ui.stack",
                Properties = new Dictionary<string, JsonElement>
                {
                    ["fill"] = JsonSerializer.SerializeToElement(true),
                    ["direction"] = JsonSerializer.SerializeToElement("vertical"),
                    ["justify"] = JsonSerializer.SerializeToElement("center"),
                    ["align"] = JsonSerializer.SerializeToElement("center"),
                    ["padding"] = JsonSerializer.SerializeToElement(new { basis = 0.12 }),
                },
                Children =
                [
                    new UiNode
                    {
                        Id = "value-text",
                        Type = "ui.text",
                        Properties = TextProperties(
                            reading.Value,
                            _mode is null ? 0.22 : 0.25,
                            "#ffffff",
                            "center"),
                    },
                    new UiNode
                    {
                        Id = "caption-text",
                        Type = "ui.text",
                        Properties = TextProperties(reading.Caption, 0.075, "#888888", "center"),
                    },
                ],
            });

            return new UiNode
            {
                Id = "root",
                Type = "ui.layer",
                Children = children,
            };
        }

        private static UiNode BuildBackgroundNode() =>
            new()
            {
                Id = "tile-background",
                Type = "ui.shape",
                Properties = new Dictionary<string, JsonElement>
                {
                    ["shape"] = JsonSerializer.SerializeToElement("rounded-rectangle"),
                    ["cornerRadius"] = JsonSerializer.SerializeToElement(new { basis = 0.12 }),
                    ["color"] = JsonSerializer.SerializeToElement("#000000"),
                    ["fill"] = JsonSerializer.SerializeToElement(true),
                },
                Fallback = new UiNode
                {
                    Id = "tile-background-fallback",
                    Type = "ui.stack",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["fill"] = JsonSerializer.SerializeToElement(true),
                        ["background"] = JsonSerializer.SerializeToElement("#000000"),
                    },
                },
            };

        private static Dictionary<string, JsonElement> TransformProperties(double rotation) =>
            new()
            {
                ["fill"] = JsonSerializer.SerializeToElement(true),
                ["originX"] = JsonSerializer.SerializeToElement(0.5),
                ["originY"] = JsonSerializer.SerializeToElement(0.5),
                ["rotation"] = JsonSerializer.SerializeToElement(rotation),
            };

        private static Dictionary<string, JsonElement> TextProperties(
            string text,
            double size,
            string color,
            string align) =>
            new()
            {
                ["text"] = JsonSerializer.SerializeToElement(text),
                ["size"] = JsonSerializer.SerializeToElement(new { basis = size }),
                ["color"] = JsonSerializer.SerializeToElement(color),
                ["align"] = JsonSerializer.SerializeToElement(align),
            };

        private static string GradientColor(int index)
        {
            var position = index / (ArcSegmentCount - 1.0);
            var red = position < 0.5 ? (int)(50 + position * 2 * 205) : 255;
            var green = position < 0.5
                ? (int)(210 + position * 2 * 35)
                : (int)(245 - (position - 0.5) * 2 * 245);
            var blue = position < 0.5 ? (int)(100 - position * 2 * 100) : 0;
            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        private sealed record Reading(string Value, string Caption, int FilledSegments);
    }
}

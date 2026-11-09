# MusselChart - Charts for Blazor

[![NuGet](https://img.shields.io/nuget/v/MusselChart.svg)](https://www.nuget.org/packages/MusselChart/)

## 🐚 What is this?

Charts for Blazor drawn as SVG, made for dashboards and widgets.  
Lines, areas, bars, range bars, scatter and bubble plots, pies, radars, heatmaps, state timelines, KPI tiles, sparklines, bar gauges and radial bars follow the look of [ClamChart](https://github.com/usausa/clam-chart) and stay light on Blazor Server, where only the new points of a time series go over the circuit.  

| 🖥️ Monitoring board | 📈 Charts | 🔢 Widgets |
|:-:|:-:|:-:|
| <img src="Document/monitor.png" width="240" /> | <img src="Document/charts.png" width="240" /> | <img src="Document/widgets.png" width="240" /> |

| 📊 Bars | ✴️ Scatter and bubbles | 🍩 Pies |
|:-:|:-:|:-:|
| <img src="Document/bars.png" width="240" /> | <img src="Document/points.png" width="240" /> | <img src="Document/pie.png" width="240" /> |

| 🕸️ Radars | 🔥 Heatmaps | 🚥 State timelines |
|:-:|:-:|:-:|
| <img src="Document/radar.png" width="240" /> | <img src="Document/heatmap.png" width="240" /> | <img src="Document/timeline.png" width="240" /> |

| ⭕ Radial bars |
|:-:|
| <img src="Document/radial.png" width="240" /> |

## 🚀 Quick Start

Import the namespace; the styles come with the `{App}.styles.css` of the application.  

```razor
@using MusselChart
```

Write the axes and the series inside a chart.  

```razor
<MusselChartView Height="240" TrackingGroup="monitor">
    <DateTimeAxis WindowSize="TimeSpan.FromMinutes(3)" />
    <NumericAxis Minimum="0" Maximum="100" />
    <LineSeries Items="Samples" X="static x => x.Time" Y="static x => x.Cpu" Title="CPU" ValueFormat="{0:N1}%" />
</MusselChartView>
```

Fill a `ChartRingBuffer<T>` from a background service; every page shares it and each chart draws only the new items.  

```csharp
public ChartRingBuffer<Sample> Samples { get; } = new(190);
```

An X is a time (`X`), a number (`XValue`) or a name (`Category`), so the same series make rankings and plots.  

```razor
<MusselChartView Height="240" Orientation="ChartOrientation.Horizontal">
    <BarSeries Items="Stores" Category="static x => x.Name" Y="static x => x.Sales" Title="Sales" StackGroup="total" />
    <BarSeries Items="Stores" Category="static x => x.Name" Y="static x => x.Online" Title="Online" StackGroup="total" />
</MusselChartView>

<MusselChartView Height="240">
    <NumericXAxis />
    <BubbleSeries Items="Regions" XValue="static x => x.Income" Y="static x => x.Age" Size="static x => x.People" Title="Regions" />
</MusselChartView>
```

The other chart types are views of their own.  

```razor
<MusselPieChartView Items="Expenses" CenterLabel="Total" LegendToggleEnabled="true" />

<MusselRadialBarView Items="Servers" Thresholds="Levels" DisplayMode="ChartRadialBarMode.Segmented" />

<MusselRadarChartView>
    <RadarSeries Items="Scores" Category="static x => x.Name" Y="static x => x.Score" Title="Score" />
</MusselRadarChartView>

<MusselHeatmapView Items="Accesses" Row="static x => x.Day" Column="static x => x.Hour" Value="static x => x.Count" />

<MusselStateTimelineView Items="States" Row="static x => x.Server" Start="static x => x.Start" State="static x => x.State" Maximum="Now" ValueMappings="Mappings" />
```

## 🎨 Look

| Part | Detail |
|---|---|
| Theme | CSS variables (`--mussel-primary-text`, `--mussel-grid-line` and more) with light defaults; `MusselChart.css` adds the dark `mussel-dark` |
| Colors | `Palette` (`ChartPalettes`: default, Grafana, Material and shades) or `Color` of a series |
| Thresholds | Lines and bands on charts, the state of tiles, the colors of gauges and rings, and the steps of state timelines |
| Tiles and gauges | `ColorMode` and `LayoutMode` of a KPI tile, basic and segmented gauges and rings, gradients, threshold markers and glow of rings |
| Pies | `InnerRadius`, `StartAngle` and `SweepAngle` for half donuts, `MaxSlices` with a merged slice, labels inside or outside with leader lines, and the center value |
| Heatmaps | A matrix or a calendar, the Grafana Oranges scale or a `ColorScale` of your own, `CellGap` and a color scale |

## ✅ Supported features

| Category | Detail |
|---|---|
| **Charts** | Line, area, bar, range bar, scatter and bubble series mixed in one chart, grouped, stacked and 100% stacked bars, horizontal bars, stacked areas, pie, donut and half donut, radar with polygon or circle grids, matrix and calendar heatmaps, state timelines, KPI tiles, line, area, bar and win-loss sparklines, bar gauges and radial bars |
| **Axes** | A date and time axis with a sliding window, category and numeric X axes, a value axis with fixed and soft ranges, label thinning |
| **Data** | Typed delegates, `ChartEntry` items, `ChartRingBuffer<T>` read incrementally on a time axis, gaps for missing values |
| **Interaction** | A crosshair and a tooltip on hover or touch, shared by `TrackingGroup`, tooltips of slices, rings and cells, and legend toggles of series and slices |
| **Legend** | A list or a table with last, minimum, maximum and mean, and values and sides of the legend of other views |

## 📖 API

See the [API reference](Document/API.md).  

## 🌊 Mussel family

- [MusselChart](https://github.com/usausa/mussel-chart) - Charts
- [MusselSheet ](https://github.com/usausa/mussel-sheet) - Input sheet

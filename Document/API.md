# MusselDiagram API reference

The behaviour is in the [specification](SPEC.md). Coordinates and sizes are in world coordinates (CSS pixels at 100% zoom, y pointing down) unless a row says screen pixels.

## 📋 MusselDiagramView

The view component. `Diagram` is required, and other attributes such as `class` and `id` go to the root element. Templates go in the child content.

| Name | Type | Default | Description |
|---|---|---|---|
| `Diagram` | `Diagram` | required | The diagram to show and edit |
| `ReadOnly` | `bool` | `false` | Takes no editing operations |
| `PanEnabled`, `ZoomEnabled`, `SelectionEnabled`, `BoxSelectionEnabled`, `MoveEnabled`, `ResizeEnabled`, `ConnectEnabled`, `DropEnabled`, `TextEditEnabled`, `TooltipEnabled` | `bool` | `true` | Turn each built-in operation off |
| `CommitMode` | `DiagramCommitMode` | `Optimistic` | `Optimistic` shows the result without waiting for C#; `Wait` waits for the answer |
| `LinkType` | `string?` | `null` | The type of links drawn from ports that declare none; `null` uses `DefaultLinkType` of the schema |
| `WheelMode` | `DiagramWheelMode` | `Pan` | What the plain wheel does: `Pan` or `Zoom` |
| `MinZoom`, `MaxZoom` | `double` | `0.05`, `8` | Zoom limits |
| `GridVisible` | `bool` | `false` | Shows the grid |
| `GridStyle` | `DiagramGridStyle` | `Dots` | `Dots` or `Lines` |
| `GridSize` | `double` | `10` | Grid spacing, also for snapping |
| `SnapToGrid`, `SnapToElements` | `bool` | `true` | Snapping to the grid, and to other elements with guides |
| `SnapTolerance` | `double` | `8` | Snapping distance in screen pixels |
| `ConnectionRadius` | `double` | `20` | Distance in screen pixels within which a port becomes the target of a connection |
| `LinkHitWidth` | `double` | `10` | Width in screen pixels of the hit area of links |
| `LivePreviewLimit` | `int` | `32` | Above this number of moving elements, only the frame of their bounds moves while dragging |
| `QuickAddSpacing` | `double` | `80` | Distance of an element added from a connect arrow or a port |
| `SearchNormalizer` | `Func<string, string>?` | `null` | Normalizes text before the palette and the type picker compare it |
| `Issues` | `IReadOnlyList<DiagramIssue>?` | `null` | Issues to show on the elements |
| `Overlays` | `IReadOnlyDictionary<string, DiagramOverlay>?` | `null` | State overlays by element ID |
| `BadgeKinds` | `IReadOnlyList<DiagramBadgeKind>?` | `null` | Badge kinds of the overlays |
| `ChildContent` | `RenderFragment?` | `null` | `DiagramTemplate` elements |

### Events

| Name | Arguments | Description |
|---|---|---|
| `SelectionChanged` | `IReadOnlyList<string>` | The selected element IDs |
| `ViewportChanged` | `DiagramViewport` | `Zoom`, `Center` and `VisibleBounds`, when panning and zooming stop |
| `ElementClicked`, `ElementDoubleClicked` | `DiagramElementPointerEventArgs` | `ElementId`, `Position`, `ShiftKey`, `CtrlKey`, `AltKey`, `MetaKey` |
| `BackgroundClicked`, `BackgroundDoubleClicked` | `DiagramPointerEventArgs` | `Position` and the modifier keys |
| `BadgeClicked` | `DiagramBadgeClickedEventArgs` | `ElementId` and `Badge`, for kinds with `Clickable` |
| `Moving`, `Moved` | `DiagramMovingEventArgs`, `DiagramMovedEventArgs` | `ElementIds`, `Offset`, `ParentChanged` and `NewParentId` (`null` for the top level) |
| `Resizing`, `Resized` | `DiagramResizingEventArgs`, `DiagramResizedEventArgs` | `ElementId` and `Bounds` |
| `Connecting`, `Connected` | `DiagramConnectingEventArgs`, `DiagramConnectedEventArgs` | `Source`, `Target`, `LinkType` and `PlacedElement` (from the type picker); `Connected` also has `LinkId` |
| `Reconnecting`, `Reconnected` | `DiagramReconnectingEventArgs`, `DiagramReconnectedEventArgs` | `LinkId`, `End` (`DiagramLinkEndSide`) and `NewEnd` |
| `Dropping`, `Dropped` | `DiagramDroppingEventArgs`, `DiagramDroppedEventArgs` | `ElementId` (of the new element), `Type`, `Bounds`, `ParentId`, `Value` and `SplitLinkId` |
| `Deleting`, `Deleted` | `DiagramDeletingEventArgs`, `DiagramDeletedEventArgs` | `ElementIds`, including the elements deleted along with them, and `IsCut` |
| `TextCommitting`, `TextCommitted` | `DiagramTextCommittingEventArgs`, `DiagramTextCommittedEventArgs` | `ElementId`, `Slot`, `OldText` and `NewText` |
| `Pasting`, `Pasted` | `DiagramPastingEventArgs`, `DiagramPastedEventArgs` | `Elements` and `IdMap` (old to new IDs) |
| `CommitRejected` | `DiagramCommitRejectedEventArgs` | `Operation` (`DiagramOperation`) and `Reason` (`DiagramReason`) |

The arguments of the before events (`…ing`) have `Cancel` and `Reason` (`string?`), and arrive after the declared rules and the app rules have passed. The arguments of the after events (`…ed`) have `Snapshot`, the snapshot after the change.  
`DiagramOperation`: `Move`, `Resize`, `Connect`, `Reconnect`, `Drop`, `Delete`, `TextEdit`, `Paste` or `Command`.  

### Members

| Name | Description |
|---|---|
| `SelectedIds`, `SelectAsync(ids)`, `ClearSelectionAsync()` | The selection |
| `Zoom`, `Center`, `SetViewportAsync(zoom, center)` | The zoom and the center of the visible area |
| `ZoomToFitAsync()`, `ZoomToSelectionAsync()`, `CenterOnAsync(id)` | Fitting and centering |
| `OpenTypePickerAsync(position)` | Opens the type picker for quick add at a position, or at the pointer when `null` |
| `Commands` | The `DiagramCommandRegistry` of the view |
| `ExportSvgAsync(options, cancellationToken)`, `ExportImageAsync(options, cancellationToken)` | Export; see Export |
| `FocusAsync()` | Gives the view the focus |

### Templates

```razor
<MusselDiagramView Diagram="diagram">
    <DiagramTemplate Type="task" Context="item">
        <rect width="@item.Width" height="@item.Height" rx="4" />
        <text x="8" y="20">@item.Label</text>
    </DiagramTemplate>
</MusselDiagramView>
```

`DiagramTemplate` has `Type` (the type name) and `ChildContent` (`RenderFragment<DiagramTemplateContext>`). `DiagramTemplateContext`: `Element` (the node), `Width`, `Height`, `Label`, `Texts` and `Data`. The origin is the top left of the element.  
Templates are for node types. The view still draws the ports and the tooltip. A template renders again only when its node changes. An element with `data-slot="<slot name>"` is edited in place, and an element with the class `mussel-diagram-shape` gets the built-in looks (states, issues and render overrides).  

## 🧰 MusselDiagramPalette

| Name | Type | Default | Description |
|---|---|---|---|
| `View` | `MusselDiagramView` | required | The view to place into |
| `Categories` | `IReadOnlyList<string>?` | `null` | Shows only these categories |

## 🖐️ MusselDiagramDragSource

Wraps HTML of the app so that a drag into the view starts from it.  

| Name | Type | Default | Description |
|---|---|---|---|
| `View` | `MusselDiagramView` | required | The view to place into |
| `Type` | `string` | required | The type of the element to place |
| `Value` | `string?` | `null` | Passed to `Dropping`, `Dropped` and `CreateContent` of the type |
| `ChildContent` | `RenderFragment?` | `null` | The content to drag from |

## ⌨️ Commands

`DiagramCommandRegistry` is a view's registry of commands.  

| Name | Description |
|---|---|
| `this[name]`, `TryGet(name, out command)`, `All` | The commands |
| `Add(command)`, `Remove(name)` | Commands of the app |
| `SetKeys(name, keys)` | Replaces the keys of a command; an empty list removes them |
| `SetCanExecute(name, canExecute)` | Adds a check to a built-in command; both must allow it |
| `SetExecute(name, execute)` | Replaces what a built-in command does |
| `ExecuteAsync(name, argument)` | Runs a command with the current selection |
| `Changed` | The enabled or checked state of a command changed |

`DiagramCommand` has `Name`, `DisplayName`, `Keys`, `IsEnabled`, `IsChecked`, `CanExecute`, `GetIsChecked` and `Execute`. The functions receive `DiagramCommandContext`: `View`, `Diagram`, `SelectedIds` and `Argument`.  
`DiagramCommands` holds the names of the built-in commands: `Undo`, `Redo`, `Delete`, `Cut`, `Copy`, `Paste`, `SelectAll`, `ClearSelection`, `EditText`, `QuickAdd`, `Group`, `Ungroup`, `CreateOverlayGroup`, `AddMembers`, `RemoveMembers`, `BringToFront`, `SendToBack`, `BringForward`, `SendBackward`, `AlignLeft`, `AlignCenter`, `AlignRight`, `AlignTop`, `AlignMiddle`, `AlignBottom`, `DistributeHorizontally`, `DistributeVertically`, `ToggleLock`, `ZoomIn`, `ZoomOut`, `ResetZoom`, `ZoomToFit`, `ZoomToSelection`, `NextIssue` and `PreviousIssue`. The arguments are a type name for `Group` and `CreateOverlayGroup`, and a `DiagramPoint` for `QuickAdd`.  
Keys are strings such as `Ctrl+Shift+Z`, with the modifiers `Ctrl`, `Alt` and `Shift`. On Mac, `Ctrl` matches ⌘.  

## 🧱 Diagram

The model. It works without a view. Writing is not thread-safe.  

| Name | Description |
|---|---|
| `new Diagram(schema)`, `new Diagram(schema, snapshot)` | An empty diagram, or one that starts from a snapshot, with an empty history |
| `Schema` | The `DiagramSchema`; setting it replaces it and keeps the snapshot and the history |
| `Snapshot`, `Find(id)` | The current `DiagramSnapshot`, and an element of it |
| `Add(element)`, `Delete(ids)`, `Replace(element)` | Adds, deletes, and replaces with a record made by `with` |
| `Move(ids, offset)`, `Resize(id, bounds)` | Moves (descendants and members follow, parents stay) and resizes |
| `Connect(link)`, `Reconnect(linkId, side, end)` | Adds a link, and moves one end (`DiagramLinkEndSide`: `Source` or `Target`) |
| `SetParent(ids, parentId)`, `SetMembers(groupId, memberIds)` | Changes parents (`null` for the top level), and the members of an overlay group |
| `BringToFront(ids)`, `SendToBack(ids)`, `BringForward(ids)`, `SendBackward(ids)` | Changes the order in the layer |
| `BeginUpdate()` | An `IDisposable` scope; the changes inside become one change when the outermost scope ends |
| `Changed` | `DiagramChangedEventArgs` after each change; see below |
| `CanUndo`, `CanRedo`, `Undo()`, `Redo()`, `UndoLimit`, `ClearHistory()` | Undo; `UndoLimit` is 100 by default |
| `Validate()` | The issues (`DiagramIssue`) of the current snapshot |
| `Save(stream)`, `SaveAsync(stream)`, `ToJson()` | Writes the current snapshot as JSON |
| `Load(stream)`, `LoadAsync(stream)`, `LoadJson(json)` | Replaces the contents with JSON, and returns the issues of what was fixed. Throws `DiagramException` when the text is not a diagram or its version is newer |
| `IsModified`, `MarkSaved()` | Whether the contents changed since `MarkSaved` |
| `GetPorts(id)` | The ports of an element (`DiagramPort`) with their positions |
| `GetLinkGeometry(id)` | `DiagramLinkGeometry`: `Points`, `SourcePoint`, `TargetPoint`, `SourceAngle` and `TargetAngle` (the direction each end marker points, in degrees: 0 right, 90 down), and `Path` (shortened at ends with markers) |

Every writing method takes an optional `DiagramWriteOptions`: `Tag` (any object, given back in `Changed`) and `History` (`DiagramHistoryMode`: `Record`, `Skip` or `Merge`). `BeginUpdate` takes it too, and the outermost scope decides.  
A write that breaks the structure (duplicate IDs, missing ends, parents or members, a cycle of parents, a group whose kind differs from its type or changes, an overlay group without members, data that is not the registered data type or `JsonElement`, sizes that are not finite and positive) throws `DiagramException` and leaves the snapshot as it was.  

`DiagramChangedEventArgs`:  

| Name | Description |
|---|---|
| `Before`, `After` | The snapshots |
| `Origin` | `DiagramChangeOrigin`: `User`, `Api`, `Undo`, `Redo` or `Load` |
| `Tag` | The tag of the write, or `null` |
| `Added`, `Deleted`, `Updated` | The element IDs; empty with `IsReset` |
| `GetChanges(id)` | `DiagramChangeParts` flags of an updated element: `Position`, `Size`, `Ends`, `Parent`, `Members`, `Label`, `Data`, `Order` and `Route` |
| `IsReset` | Everything changed (loading) |

`DiagramJson.Write(stream, snapshot, schema)`, `DiagramJson.Read(stream, schema)` (a `DiagramLoadResult` with `Snapshot` and `Issues`) and their `…Async` forms do the same without a `Diagram`, for a server.  

## 📸 DiagramSnapshot

An immutable state of the diagram, readable from any thread.  

| Name | Description |
|---|---|
| `Version` | Grows by 1 with each change |
| `Nodes`, `Groups`, `Links`, `SharedItems` | The elements, in stacking order |
| `Find(id)`, `Contains(id)` | An element by ID |
| `GetLinks(id, direction, portId)` | The links of an element (`DiagramLinkDirection`: `In`, `Out` or `Both`), optionally of one port |
| `GetPredecessors(id)`, `GetSuccessors(id)` | The elements one link before and after |
| `GetUpstream(id)`, `GetDownstream(id)` | The elements reached against and along the links |
| `Cycles`, `GetUnreachable(startIds)` | The elements in cycles, and the nodes and linked groups not reachable from the given ones |
| `GetParent(id)`, `GetChildren(id)`, `GetAncestors(id)`, `GetDescendants(id)`, `TopLevel` | The tree |
| `GetMembers(groupId)`, `GetOverlayGroups(id)` | Overlay groups |
| `GetReferrers(sharedItemId, schema)`, `GetUnusedSharedItems(schema)` | Shared items and the elements that refer to them |
| `Validate(schema)` | The whole-diagram check |

## 🧩 Elements

All elements are records, and an ID is created when none is given.  

| Type | Members |
|---|---|
| `DiagramElement` | The abstract base of the elements: `Id`, `Type`, `Data` |
| `DiagramBox` | The abstract base of nodes and groups: `Bounds` (`DiagramRect`), `ParentId`, `Label`, `Texts`, `Icon`, `Locked`, `Permissions` |
| `DiagramNode` | A `DiagramBox` |
| `DiagramGroup` | A `DiagramBox` with `Kind` (`DiagramGroupKind`: `Tree` or `Overlay`) and `MemberIds`. Overlay groups have no `Bounds` or `ParentId` of their own; their `Bounds` is computed |
| `DiagramLink` | A `DiagramElement` with `Source`, `Target` (`DiagramLinkEnd` with `ElementId` and `PortId`), `Route` (`DiagramPoint` list), `RouteState` (`DiagramRouteState`: `Provisional`, `Routed` or `Failed`), `RouteError`, `Locked`, `Permissions` |
| `DiagramSharedItem` | A `DiagramElement` with `Label` |
| `DiagramPort` | Computed: `Id`, `Direction` (`DiagramPortDirection`: `In`, `Out` or `Both`), `Kind`, `Side` (`DiagramSide`), `Position`, `Label`, `Visibility` |

`Texts` is an `ImmutableDictionary<string, string>` by slot name. `Icon` is `prefix:ID` of an imported icon or an image URL. `Data` is the registered data type, a `JsonElement`, or `null`.  
`Permissions` is a `DiagramPermissions?` with the flags `Select`, `Move`, `Resize`, `Delete`, `Connect` and `EditText`; when set, it replaces the permissions of the type. `Locked` stops everything except `Select`.  
`DiagramPoint` (`X`, `Y`), `DiagramSize` (`Width`, `Height`), `DiagramRect` (`X`, `Y`, `Width`, `Height`) and `DiagramThickness` (`Left`, `Top`, `Right`, `Bottom`) are value types.  

## 🧾 DiagramSchema

An immutable record. The declarative parts can also be read with `DiagramSchemaJson.Read(stream)` and given functions afterwards with `with`.  

| Name | Description |
|---|---|
| `NodeTypes`, `GroupTypes`, `LinkTypes`, `SharedItemTypes` | The types |
| `Categories` | `DiagramCategory`: `Name`, `DisplayName` and `Collapsed`, in palette order |
| `IconSets` | `DiagramIconSet`; see Icons |
| `Presets` | `DiagramPreset`: a `Name` and a `DiagramLook` that types refer to |
| `DefaultLinkType`, `DefaultGroupType` | Type names |
| `Router` | `IDiagramRouter?`; the built-in router when `null` |
| `Rules` | `DiagramAppRules`; see App rules |

### Types

| Type | Members |
|---|---|
| `DiagramType` | The abstract base of the types: `Name`, `DisplayName`, `Category`, `SearchWords`, `DataType` (`JsonTypeInfo?`), `DefaultData`, `GetReferences` (the shared item IDs in the data), `Permissions` (all by default) |
| `DiagramBoxType` | The abstract base of node and group types: `Look`, `Size`, `MinSize`, `Slots`, `Ports`, `GetPorts` (ports from the data), `ConnectsAsWhole`, `InPalette`, `CreateContent`, `AllowedParents`, `TopLevel`, `StayInParent`, `MaxCount`, `Required`, `Undeletable`, `Start`, `GetTooltip` |
| `DiagramNodeType` | A `DiagramBoxType` |
| `DiagramGroupType` | A `DiagramBoxType` with `Kind`, `Sizing` (`DiagramGroupSizing`: `Manual` or `Content`), `Padding` (20, 40, 20, 20), `Header`, `AllowedChildren`, `AllowedMembers` and `FillHitTest` |
| `DiagramLinkType` | A `DiagramType` with `Look`, `Form` (`DiagramLineForm`: `Orthogonal`, `Straight` or `Curved`), `StubLength` (20), `ObstacleMargin` (10), `SourceMarker`, `TargetMarker` (`DiagramMarker`: `None`, `Arrow`, `OpenArrow`, `Circle` or `Diamond`), `Router`, `AllowSelf`, `AllowDuplicate` (`false`), `AllowCrossParent`, `AllowCycles` (`true`), `GetTooltip` |
| `DiagramSharedItemType` | A `DiagramType` with `MaxCount`, `Required` and `Undeletable` |

`CreateContent` receives `DiagramCreateContext` (`Snapshot`, `Type` and the `Value` of a drag source) and returns `DiagramContent` (`Label`, `Texts` and `Data`). `GetTooltip` receives the element and returns text or `null`.  

| Type | Members |
|---|---|
| `DiagramLook` | `Shape` (`DiagramShape`: `Rectangle`, `Ellipse`, `Diamond` or `Path`), `Path` (in a 0–1 box), `Icon`, `Fill`, `Stroke`, `StrokeWidth`, `Dash` (`DiagramDash`: `Solid`, `Dashed` or `Dotted`), `Preset`. Colors are CSS colors or `var(…)` |
| `DiagramLabelSlot` | `Name`, `Placement` (`DiagramLabelPlacement`), `Format`, `Width`, `MaxLines`, `Editable`, `DefaultText` |
| `DiagramTextFormat` | `FontFamily`, `FontSize`, `FontWeight`, `Color`, `Alignment` |
| `DiagramPortDefinition` | `Id`, `Direction`, `Kind`, `Side`, `Ratio`, `Row`, `Offset`, `MaxLinks`, `Required`, `Accepts`, `Rejects`, `LinkType`, `Label`, `Visibility` (`DiagramPortVisibility`: `Always` or `WhileConnecting`) |
| `DiagramGroupHeader` | `Form` (`DiagramHeaderForm`: `TopLeft`, `TopCenter` or `NameOnly`), `IconSize`, `NameLeft`, `NameTop`, `NameAlignment` |

`DiagramLabelPlacement`: inside `TopLeft`, `Top`, `TopRight`, `Left`, `Center`, `Right`, `BottomLeft`, `Bottom`, `BottomRight`; outside `Above`, `Below`, `Before`, `After`; and `Header`. `DiagramSide`: `Top`, `Right`, `Bottom` or `Left`. `Row` and the header and row heights apply to the left and right sides.  

### App rules

`DiagramAppRules` holds synchronous functions. Each returns `DiagramRuleResult.Allow` or `DiagramRuleResult.Reject(text)`.  

| Name | Receives | Decides |
|---|---|---|
| `CanConnect` | `DiagramConnectionContext`: `Snapshot`, `Source`, `Target`, `LinkType`, `Reconnecting` (the ID of the link when it already exists) | A connection |
| `CanNest` | `DiagramNestingContext`: `Snapshot`, `ElementId` or `Type`, `ParentId` | A parent, `null` for the top level |
| `CanDelete` | `DiagramDeletionContext`: `Snapshot`, `ElementIds` | A deletion |
| `InPalette` | `DiagramPaletteContext`: `Snapshot`, `Type` | Returns `bool` |
| `Check` | `DiagramSnapshot` | Returns issues for the whole-diagram check |

### Routing

`IDiagramRouter.RouteAsync(request, cancellationToken)` returns a `ValueTask<DiagramRouteResult>`. A router that finishes synchronously gives the route at commit; one that does not gives a simple route first. The token is canceled when the result is no longer needed.  
`DiagramRouteRequest`: `Source` and `Target` (`DiagramRouteEnd` with `Position` and `Direction`), `Obstacles` (rectangles) and `LinkType`. `DiagramRouteResult`: `Points`, or `Error` with a reason.  

## 🖼️ Icons

| Name | Description |
|---|---|
| `DiagramIconSet` | `Prefix` and `Icons` |
| `DiagramIcon` | `Id`, `Body` (the SVG content), `Width`, `Height`, `Name`, `Category`, `SearchWords` |
| `DiagramIconImport.FromIconify(stream)` | Reads Iconify JSON |
| `DiagramIconImport.FromZip(stream, prefix, map)`, `FromFolder(path, prefix, map)` | Reads SVG files. `map` makes `DiagramIconInfo` (`Id`, `Name`, `Category`) from a relative path, or `null` to skip the file |
| `DiagramIconImport.CreateNodeTypes(set, baseType, filter)`, `CreateGroupTypes(set, baseType, filter)` | A type per icon, named `prefix:ID` |

Each import returns a `DiagramIconImportResult` with `IconSet` and `Issues` (what sanitizing removed).  

## 🔎 Issues and reasons

| Type | Members |
|---|---|
| `DiagramIssue` | `Severity` (`DiagramIssueSeverity`: `Error` or `Warning`), `Kind`, `Message`, `ElementIds`, `PortId`, `Slot`, `Arguments` |
| `DiagramReason` | `Kind` (`DiagramReasonKind`), `Message`, `Arguments` |

`DiagramIssueKind`:  

| Group | Values |
|---|---|
| Whole-diagram check | `ConnectionRule`, `NestingRule`, `MaxCount`, `RequiredType`, `RequiredPort`, `MissingPort`, `MissingSharedItem`, `UnregisteredType`, `App` |
| Loading | `DuplicateId`, `MissingEnd`, `InvalidParent`, `InvalidMember`, `NoMembers`, `KindMismatch`, `InvalidSize`, `UnreadableData` |
| Sanitizing | `RemovedSvgContent`, `RejectedUrl`, `UnreadableIcon` |
| Export | `ImageNotEmbedded` |

`DiagramReasonKind`: `NoConnectionPoint`, `Direction`, `PortKind`, `Permission`, `Limit`, `SelfOrDuplicate`, `PeerType`, `CrossParent`, `Cycle`, `Count`, `Nesting`, `Changed`, `Canceled`, `TooLarge` (an image larger than the canvas limits) and `App`.  

## 🏷️ Overlays

| Type | Members |
|---|---|
| `DiagramOverlay` | `States` (names), `Style` (`DiagramRenderOverride`), `Badges` (`DiagramBadge` list) |
| `DiagramRenderOverride` | `Fill`, `Stroke`, `StrokeWidth`, `Dash`, `Opacity`; `null` keeps the look of the type |
| `DiagramBadge` | `Kind`, `Text`, `Icon`, `Tone` (`DiagramBadgeTone`: `Default`, `Info`, `Success`, `Warning` or `Error`), `Tooltip` |
| `DiagramBadgeKind` | `Name`, `Corner` (`DiagramCorner`: `TopLeft`, `TopRight`, `BottomLeft` or `BottomRight`), `Order`, `Clickable` |

On links, badges line up at the middle of the route and the corner is not used.  

## 💾 JSON

```json
{
  "format": "mussel-diagram",
  "version": 1,
  "groups": [{ "id": "vpc", "type": "aws:vpc", "kind": "tree", "x": 0, "y": 0, "width": 600, "height": 400, "label": "VPC" }],
  "nodes": [{ "id": "web", "type": "aws:Arch_Amazon-EC2", "x": 120, "y": 100, "width": 80, "height": 80, "parent": "vpc", "label": "Web" }],
  "links": [{ "id": "l1", "type": "arrow", "source": { "element": "web" }, "target": { "element": "db", "port": "in" }, "route": [[200, 140], [320, 140]] }],
  "sharedItems": []
}
```

| Element | Fields |
|---|---|
| Node | `id`, `type`, `x`, `y`, `width`, `height`, `parent`, `label`, `texts`, `icon`, `data`, `locked`, `permissions` |
| Group | The fields of a node, and `kind` (`tree` or `overlay`) and `members`. An overlay group has no `x`, `y`, `width`, `height` or `parent` |
| Link | `id`, `type`, `source`, `target` (`element` and `port`), `route` (points as `[x, y]`, only when routed), `data`, `locked`, `permissions` |
| Shared item | `id`, `type`, `label`, `data` |

Fields without a value and default values are not written, except `kind`, `x`, `y`, `width` and `height`. `permissions` is a list of names such as `["select", "move"]`.  

## 🖼️ Export

`ExportSvgAsync` and `ExportImageAsync` return a `DiagramExportResult`: `Content` (a `Stream` at its start that the app disposes, or `null` when nothing was exported), `Error` (a `DiagramReason` of the kind `TooLarge`) and `Issues` (`ImageNotEmbedded` for images that could not be embedded).  
Tooltips, classes and styles are not exported; `data-id` and `data-type` are kept.  

| `DiagramSvgExportOptions` | Default | Description |
|---|---|---|
| `Margin` | `10` | Space around the bounds of the elements |
| `Background` | `Transparent` | `DiagramExportBackground`: `Transparent` or `Theme` (the background color) |
| `IncludeOverlays` | `false` | States, render overrides, badges and issue badges |
| `EmbedFonts` | `false` | Embeds the fonts the page loaded with `@font-face` and the diagram uses |

`DiagramImageExportOptions` has the same members except `EmbedFonts` (fonts are always embedded), and also `Format` (`DiagramImageFormat`: `Png` or `Jpeg`, `Png` by default), `Scale` (`1`) and `Quality` (`0.92`, JPEG only). JPEG always uses the `Theme` background.  

## 🌐 Languages

`DiagramCultures.Texts` maps culture names (case-insensitive) to `DiagramTexts`. The empty name (`DiagramTexts.English`) and `ja` (`JapaneseLanguage.Texts`) are built in. Register before the first diagram is created.  
Each item is looked up from the culture to its parents; a missing or empty item falls back, and English is the last resort. So a language can set only some items, and the built-in texts change with `with`.  

```csharp
DiagramCultures.Texts["ja"] = JapaneseLanguage.Texts with { NoMatches = "該当なし" };
```

| `DiagramTexts` | Description |
|---|---|
| `Reasons` | A format string for each `DiagramReasonKind` |
| `Issues` | A format string for each `DiagramIssueKind` |
| `Commands` | The display name of each built-in command, by name |
| `SearchPlaceholder`, `NoMatches` | The palette and the type picker |

`JapaneseLanguage` is in `MusselDiagram.Languages.Japanese` of `MusselDiagram.Core`. A language of your own is written the same way.  

## 🎨 Theme

Link `_content/MusselDiagram/MusselDiagram.css` in the page head. Colors come from CSS variables; an app changes them from any selector. `.mussel-dark` on an ancestor switches to the dark values.  

These are shared with the other Mussel libraries.  

| Variable | Use |
|---|---|
| `--mussel-primary-text`, `--mussel-secondary-text`, `--mussel-muted-text` | Labels; headers and the palette; placeholders and disabled items |
| `--mussel-grid-line` | The grid |
| `--mussel-tooltip-background`, `--mussel-tooltip-border`, `--mussel-tooltip-text` | The type picker, rejection reasons and the text editor |
| `--mussel-critical` | Errors |
| `--mussel-font-family` | Font family |

These are for the diagram only. Set them on `.mussel-diagram`, on a `class` given to the view, or on a container.  

| Variable | Use |
|---|---|
| `--mussel-diagram-background` | The background of the view |
| `--mussel-diagram-grid-color`, `--mussel-diagram-grid-major-color` | The grid, and every fourth line |
| `--mussel-diagram-node-fill`, `--mussel-diagram-node-stroke`, `--mussel-diagram-text-color` | Shapes and labels without their own colors |
| `--mussel-diagram-group-fill`, `--mussel-diagram-group-stroke` | Groups without their own colors |
| `--mussel-diagram-link-color`, `--mussel-diagram-link-width` | Links without their own look |
| `--mussel-diagram-port-fill`, `--mussel-diagram-port-stroke` | Ports |
| `--mussel-diagram-selection-color`, `--mussel-diagram-handle-fill` | The selection frame, highlighted links and handles |
| `--mussel-diagram-guide-color`, `--mussel-diagram-candidate-color`, `--mussel-diagram-drop-target-color`, `--mussel-diagram-not-allowed-color` | Guides, connection candidates, drop targets and places where an element cannot go |
| `--mussel-diagram-pending-opacity` | Elements awaiting a commit |
| `--mussel-diagram-error-color`, `--mussel-diagram-warning-color` | Elements with issues |
| `--mussel-diagram-badge-default`, `--mussel-diagram-badge-info`, `--mussel-diagram-badge-success`, `--mussel-diagram-badge-warning`, `--mussel-diagram-badge-error`, `--mussel-diagram-badge-text` | Badge tones and their text |
| `--mussel-diagram-palette-background`, `--mussel-diagram-palette-hover` | The palette |

| Class or attribute | Use |
|---|---|
| `mussel-diagram`, `mussel-diagram-palette` | The roots of the view and the palette |
| `mussel-diagram-node`, `mussel-diagram-group`, `mussel-diagram-link`, `mussel-diagram-port`, and `data-type` | Elements and their type names |
| `data-template` | Nodes drawn by a template |
| `data-selected`, `data-not-allowed`, `data-candidate`, `data-drop-target`, `data-pending` | Built-in states that the view sets while editing; hover is `:hover` |
| `mussel-diagram-error`, `mussel-diagram-warning`, `mussel-diagram-locked`, `mussel-diagram-unregistered` | Built-in states from issues, locks and unregistered types |
| `mussel-diagram-state-<name>` | States of overlays |
| `--mussel-diagram-override-fill`, `--mussel-diagram-override-stroke`, `--mussel-diagram-override-stroke-width`, `--mussel-diagram-override-dash`, `--mussel-diagram-override-opacity` | Render overrides, set on the element |
| `mussel-diagram-badge` and `data-tone` | Badges and their tones |
| `mussel-diagram-nodrag`, `mussel-diagram-nowheel` | Set by the app on elements in templates |

## ⚠️ Limits

| Item | Limit |
|---|---|
| Render mode | Needs an interactive render mode (Server, WebAssembly or Auto). Prerendering and static SSR show nothing in the first version |
| Size | The target is a diagram of 500 nodes, 50 groups and 600 links with a frame time under 16 ms (95th percentile) while dragging, panning and zooming |
| Export | Only in a browser, through the view. The image size is limited by the canvas of the browser |
| Icons | None are bundled. AWS icons are read from the package the user downloads |
| Text | Labels are plain text; HTML in labels is shown as text |

## 🚧 Not supported yet

Automatic layout, editing waypoints, link labels, labels that move on their own, elements on group frames, collapsing groups, rotation, the OS clipboard and copying between diagrams, duplicating, PDF export, context menus, keyboard navigation between elements, screen readers, hover-only ports, page frames, and switching icons for dark backgrounds.

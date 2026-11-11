# MusselDiagram specification

The behaviour of the diagram. Parameters, types, theme variables and limits are in [API.md](API.md).

## 📚 Terms

| Term | Meaning |
|---|---|
| Diagram (`Diagram`) | The model that holds the elements. It works without a UI |
| Element | A node, link, group or shared item. Each has a type and an ID that is unique in the diagram |
| Node (`DiagramNode`) | An element with a position and a size, drawn as an icon or a shape |
| Port (`DiagramPort`) | A connection point on a node or a group. It has a direction (in, out or both) and a kind. Its position comes from the type and the size of the element |
| Link (`DiagramLink`) | A line between two ends. Both ends always attach to a node or a group, or to one of its ports. It has a type |
| Group (`DiagramGroup`) | A frame that gathers elements. Its kind (`Kind`) makes it a tree group or an overlay group |
| Tree group | A group with children. A child has one parent and moves with it (a subnet in a VPC) |
| Overlay group | A group drawn around the bounds of chosen elements. It is outside the parent tree and can enclose elements with different parents (an Auto Scaling group across Availability Zones) |
| Label | Text drawn on an element: the main label (`Label`) and the text of each slot the type declares (`Texts`) |
| Shared item (`DiagramSharedItem`) | An element that the view does not draw. Other elements refer to it by ID in their data (like a configuration node in Node-RED) |
| Type | A kind of element, registered by the app: look, default size, ports, label slots, data type, rules and maximum count |
| Schema (`DiagramSchema`) | The types, the palette categories, the icon sets and the app rules. A diagram has one |
| Icon set | Icons under one prefix. The app imports it and adds it to the schema |
| Rule | Whether a connection, nesting, move or delete is allowed. Rules declared in the types are checked the same way in the browser and in C#. Rules the app adds in C# are checked at commit |
| View (`MusselDiagramView`) | The component that shows and edits a diagram. It can be read-only |
| Palette | The component that lists the types that can be placed. It sits apart from the view |
| Type picker | A small list that opens over the view to find and choose a type. It adds an element from a link, an arrow or a port, and searches and places a type |
| Command | A named operation in the command registry, run by keys or by the toolbar and menus of the app |
| Commit | Writing the result of an operation to the model, with events before and after |
| Snapshot | The state of the model at one time. The model never changes in place: each change makes a new snapshot, and undo goes back to an earlier one |
| World coordinates | One coordinate system for the whole diagram. Elements in nested groups use it too, not coordinates relative to their parent |
| Badge | A mark of state drawn over an element without changing the model |

| Interaction state | Meaning | Entered by |
|---|---|---|
| Idle | No operation | Loading, commit, undo, Esc |
| Pressed | Pressed, but not moved past the threshold. Neither the model nor the display changes | Pressing an element, a port, the background or a palette item |
| Dragging | The browser moves a preview: a move, a resize, a connection, a drop from the palette, a box selection or a pan | Moving past the threshold |
| Awaiting commit | The commit is sent and the view waits for C#. Only with `Wait`; with `Optimistic` the view returns to idle at once | Release |
| Text editing | A label is edited in place | Double-click, F2, Enter |

Esc or a lost pointer capture during a drag returns to idle without changing the model.

## 📦 Components and packages

| Package | Contents | References |
|---|---|---|
| `MusselDiagram.Core` | The diagram model, types and rules, validation, routing, undo, JSON reading and writing, icon import. No UI | .NET standard libraries only |
| `MusselDiagram` | The view and palette components, the command registry, JS and CSS | `MusselDiagram.Core`, `Microsoft.AspNetCore.Components.Web` |

With `MusselDiagram.Core` alone, a server, a worker or a command-line tool can create, validate and save diagrams.

| Component | Role |
|---|---|
| `MusselDiagramView` | The view. It draws the diagram and handles selection, moving, connecting, panning and zooming |
| `MusselDiagramPalette` | The palette. It lists the types that can be placed and drags them onto the view |
| `DiagramCommandRegistry` | The command registry. Not a visual component. It holds the named operations and whether each is enabled |
| `MusselDiagramDragSource` | Wraps an HTML element of the app so that a drag into the view can start from it (Palette) |

Each is used on its own.

- A view works alone. Without a palette, the app adds elements through the API
- A palette places into the one view passed as its parameter. It can be anywhere on the page, and drags start outside the view
- Each view has a command registry. The keys of the view and the toolbar and menus of the app run the same commands. There are no toolbar, menu or property panel components
- A `ReadOnly` view takes no editing operations (move, resize, connect, delete, drop, text editing). Panning, zooming, selection, hover and tooltips stay, and each can be turned off with its `…Enabled`

The view takes its size from its parent element. When the parent is resized, it redraws and keeps the center and the zoom.

| Item | Content |
|---|---|
| Render modes | Works the same in Interactive Server, WebAssembly and Auto. Prerendering and static SSR are not supported in the first version |
| Handled in the browser | Pointer and keys, the display while dragging, snapping, connection candidates, the simple route while dragging, label wrapping |
| Handled in C# | The model, commits, validation, routing, rendering |
| Drawing nodes | By default the component draws the shape the type declares. A type with a template registered on the view is drawn by that Razor template, which renders SVG elements |
| Static assets | Two: the ES module `MusselDiagram.js`, with no external libraries, and `MusselDiagram.css` |

## 🧱 Model

A diagram (`Diagram`) holds a schema (Types and rules) and the current snapshot. The elements are nodes, groups, links and shared items, all immutable records.

### Elements

| Element | Holds |
|---|---|
| Node (`DiagramNode`) | ID, type, position and size, parent, label, icon, data |
| Group (`DiagramGroup`) | ID, type, kind (`Kind`), label, icon, data. A tree group (`Tree`) has a position, a size and a parent, and can have children. An overlay group (`Overlay`) has a list of members |
| Link (`DiagramLink`) | ID, type, source and target, route, data |
| Shared item (`DiagramSharedItem`) | ID, type, label, data. The view does not draw it |

- An ID is a non-empty string, unique among the elements of the diagram. When the app gives none, the diagram makes one of 16 random hexadecimal digits. IDs never change and are looked up with `Find`. They are separate from labels
- A type is a name in the schema. Elements of unregistered types are kept intact (Types and rules)
- Positions and sizes are in world coordinates (CSS pixels at 100% zoom, y pointing down). Children are not relative to their parent. There is no origin or boundary
- The position is the top left corner. There is no rotation or flipping
- Nodes and groups draw the icon of their type, or the `Icon` of the element when it has one (the ID of an imported icon, or an image URL). Importing is in Palette and AWS architecture diagrams
- Nodes, groups and links also have a lock (`Locked`) and element permissions (`Permissions`) (Types and rules)

### Labels

- An element has the main label (`Label`) and the text of the other slots (`Texts`, by slot name). Line breaks split the lines
- The type declares the slots: name, placement, format, maximum lines, whether it is editable in place, and the default text when empty. The first slot shows the main label, the others the `Texts` entry of the same name
- A type without slots draws the main label at the center of the shape
- An empty slot shows the default text of the type, which is not saved. Without default text it shows nothing
- Text for slots the type does not have is kept and not drawn
- In-place editing works on editable slots only (Commands and keys)
- Placement, format and wrapping are in Rendering, group headers in AWS architecture diagrams. Link labels and labels that move on their own are not in the first version

### Ports and links

- Ports are not saved. Node and group types declare them (some types derive their number from the data), and their positions are computed from the declaration and the size of the element
- A port ID is unique within its element
- A link end refers to an element ID and a port ID. Without a port ID it attaches to the whole element. An end can be a node or a group
- An end always attaches to an element. When the element of an end is deleted, or a change of data removes the port, the attached links are deleted in the same change
- A link holds its route, a list of points. How it is made is in Link routes

### Groups

| Item | Tree group | Overlay group |
|---|---|---|
| Contents | Children (nodes and tree groups). The child refers to its parent, and it has only one | Members (nodes and tree groups). The group holds the list. Members can have different parents, and an element can be in several overlay groups |
| Size | Set by the type. A manually sized group grows when its children plus the padding of the type no longer fit, and never shrinks. A group sized to its contents is the bounds of its children plus the padding of the type (the size of the type when empty), and cannot be resized by hand | The union of the bounds of the members plus the padding of the type. Cannot be resized by hand |
| Moving the group | The descendants move by the same amount | The members and their descendants move by the same amount. The parents of the members do not change |
| The contents move, grow or shrink | A group sized to its contents is resized in the same change | The frame is computed again in the same change |
| Deleting the group | The descendants are deleted | The members stay |
| No contents left | Stays | Deleted in the same change |
| Where it can be | At the top level or in a tree group. The tree has no cycles | It has no parent and is never a member of an overlay group |

Changing parents:

- Operations decide the parent, and positions never decide it again. A child that sticks out of its frame or overlaps a crossing group keeps its parent
- When elements moved in the view are released (the selected elements whose ancestors are not selected) and all of them have the same parent, the new parent is the innermost tree group under the pointer that accepts all of them (by the nesting rules). When no group accepts them, they go to the top level
- Moving elements with different parents together, or moving an overlay group, does not change parents
- Overlay groups are never parents or drop targets. Their members change through commands (Commands and keys) and the API
- `Move` in the API does not change the parent. `SetParent` changes the parent, and `SetMembers` the members
- Types that stay inside their parent, and limits on where elements can move, are in Types and rules

### Stacking order

- From back to front: tree groups, overlay groups, links, nodes. Badges, the selection frame and previews are in front (Rendering)
- A tree group is drawn behind its children
- Within a layer the order is the list order: among siblings for tree groups, one list per layer for the others. Bring to front and send to back (Selection and moving) change this order

### Data and shared items

- The data of an element (`Data`) is a C# record of the app. The schema takes the `JsonTypeInfo<T>` of the data type (source generated)
- Only the data type registered for the type, or `JsonElement`, can be written, or no data. Anything else throws on writing
- The data of unregistered types, and of types with no registered data type, is kept as `JsonElement` and saved as it is
- Data is treated as immutable, lists inside the records included. Changing it after writing also changes the snapshots kept for undo
- An element placed from the palette gets the default data of its type
- Shared items are not drawn, and are never link ends, parents or members. The app builds their list and settings panel
- Other elements hold the IDs of shared items in their data. The type registers a function that returns the IDs its data refers to
- Deleting a shared item leaves the data that refers to it unchanged. The whole-diagram check reports IDs with no target (Commit and validation)

### Snapshots and changes

- `Diagram.Snapshot` is the current snapshot (`DiagramSnapshot`). It is immutable and can be read from any thread. Each change makes a new snapshot that shares the unchanged elements with the previous one. Its number (`Version`) grows by 1 with each change
- Only the methods of `Diagram` change a diagram: add, delete, move, resize, connect, reconnect, change parents and members, change the order, and replace. `Replace` takes an element made with `with`. Changing a position does not move the descendants
- Writing to a `Diagram` is not thread-safe. A diagram passed to a view is written from the synchronization context of the view (`InvokeAsync`)
- One call is one change. Dependent changes (moving the descendants, group sizes, deleting the attached links, removing empty overlay groups, routes) are in the same change. The changes inside `BeginUpdate` become one change when the outermost scope ends, and reads inside see them
- Writing checks the following, and throws without changing the snapshot when one fails
  - IDs are unique
  - The elements of ends, parents and members exist, and so do the ports (registered types only). A parent is a tree group, an end is a node or a group, and a member is a node or a tree group
  - The kind of a group matches the kind of its type (registered types only)
  - The parent tree has no cycle
  - No field contradicts the kind of the group (members of a tree group, a parent of an overlay group, an overlay group without members), and the kind of a group does not change
  - The data is the data type registered for the type, or `JsonElement`
  - Positions and sizes are finite, and sizes are greater than 0
- The type rules (connection, nesting, counts) are not checked. Rules limit the user, not the app. The whole-diagram check reports whether a diagram follows them (Commit and validation)

| Event | When | Content |
|---|---|---|
| `Diagram.Changed` | Once after each change, whatever its origin | The snapshots before and after, the origin, the tag, the changed elements (added, deleted, changed) and what changed (position, size, ends, parent, members, label, data, order, route). Loading reports only that everything changed, without listing elements |
| `…ing` of the view | Before a user operation is written | What will be written. `Cancel` stops it (Commit and validation) |
| `…ed` of the view | After a user operation is written | What was written |

| Origin | Change |
|---|---|
| `User` | An operation of the user in the view |
| `Api` | A method call of the app |
| `Undo`, `Redo` | Undo and redo |
| `Load` | Loading JSON |

The app can pass a tag, any value, when writing, and gets it back in `Changed` to recognize its own writes. Which changes go into the undo history is in Undo.

### Queries

Through the methods of a snapshot.

| Question | Content |
|---|---|
| Connections | The links of an element (in and out, optionally by port), and the elements one link before and after |
| Traversal | Upstream and downstream (the elements reached along the links), the elements in cycles, the nodes and linked groups not reachable from given elements |
| Tree | Parent, children, ancestors, descendants, top-level elements |
| Overlay | The members of a group, the overlay groups around an element |
| Shared items | The elements that refer to a shared item (the use count), the shared items nothing refers to |

There is no analysis such as shortest paths or centrality.

### Use without a view

- Without a view, a diagram can be created, changed, queried, checked, routed, and saved to and loaded from JSON
- Selection, hover, and the position and zoom of the view are not in the diagram. The view holds them
- One diagram can be passed to several views. There is one undo history per diagram and one selection per view

## 🧾 Types and rules

A schema (`DiagramSchema`) is an immutable record of the node, group, link and shared item types, the palette categories, the icon sets (Palette) and the app rules. The app builds it in code (the declarative parts can also be read from JSON) and passes it to the diagram (`Diagram.Schema`).

### Schema

| Item | Content | Types |
|---|---|---|
| Name | What the `Type` of an element refers to. Unique in the schema | All |
| Display name, category, search words | The palette list and search | All |
| Palette | Whether the palette shows it (yes by default), and a function that makes the label and data of a placed element (Palette) | Nodes, groups |
| Look | Shape, icon, default and minimum size, format, preset (Rendering). Templates are registered on the view | Nodes, groups, links |
| Label slots | Labels in Model | Nodes, groups |
| Ports | Ports below | Nodes, groups |
| Data | The `JsonTypeInfo<T>` of the data type, the default data, and a function that returns the IDs of the shared items the data refers to | All |
| Nesting | Allowed parent types, whether it can be at the top level, whether it stays inside its parent (Nesting rules) | Nodes, groups |
| Counts | Maximum count, required, undeletable, start mark (Counts and permissions) | Nodes, groups, shared items |
| Permissions | Select, move, resize, delete, connect, edit text | Nodes, groups, links |
| Tooltip | A function that returns text from the data (Selection and moving) | Nodes, groups, links |
| Groups only | Kind (tree or overlay), sizing (manual or to the contents), padding, header form (AWS architecture diagrams), allowed child or member types | Groups |
| Links only | Line form, stub length, end markers, a replacement router (Link routes), and whether self, duplicate, cross-parent and cyclic links are allowed (Connection rules) | Links |

- The `Kind` of a group must equal the kind of its type. Otherwise writing throws
- A schema can have a default link type (Ports below) and a default group type (grouping in Commands and keys)
- When the schema is replaced, the view sends it to JS again. The snapshot and the undo history stay. Elements of removed types become unregistered, and the whole-diagram check reports links to removed ports

### Unregistered types

- An element whose type is not in the schema is drawn as a placeholder: a dashed frame with the type name. It can be selected, moved and deleted, but not connected, nested or text-edited
- Its label, data and links are kept and saved as they are. Ports are not checked on writing
- Its links are drawn towards its outline
- Once the type is registered, it is drawn as a normal element

### Ports

| Item | Content |
|---|---|
| ID | Unique in the element. Link ends refer to it |
| Direction | In (`In`), out (`Out`) or both (`Both`) |
| Kind | A string. Only ports of the same kind connect. A port without a kind connects to any kind |
| Position | Side, ratio, order, row (Connecting and Rendering) |
| Link limit | A port at its limit neither starts nor accepts links |
| Required | An error in the whole-diagram check when nothing is connected |
| Peer types | The types the element at the other end may have (`Accepts`) and may not have (`Rejects`) |
| Link type | The type of the links drawn from this port |
| Label | The name shown on hover |
| Visibility | Always, or only while a connection is dragged (Connecting) |

- A type without ports makes the whole element one connection point, in both directions and without a kind. A type with ports declares whether the whole element connects too
- A type that derives its ports from the data (an output per branch) registers a function that returns the ports from the data. The diagram computes them per snapshot
- The type of a new link is the declaration of the starting port, otherwise the `LinkType` of the view (which the app switches from its toolbar), otherwise the default link type of the schema

### Connection rules

The declared rules are checked in this order, and the first one that fails gives the reason. The reason has a kind and a text, and the view shows it (Connecting).

| Order | Check | Reason |
|---|---|---|
| 1 | Both ends have a connection point (unregistered types and shared items have none) | No connection point |
| 2 | From out to in. A port for both can be either | Direction |
| 3 | The kinds are the same, or one has no kind | Kind |
| 4 | Both elements allow connecting (Counts and permissions) | Permission |
| 5 | Neither port is at its link limit | Limit |
| 6 | The link type allows self links (within one element) and duplicates (a second link between the same two connection points). By default it does not | Self, duplicate |
| 7 | The peer types of each port allow the type of the other element | Peer type |
| 8 | When the parents of the ends differ, the link type allows it. By default it does | Cross-parent |
| 9 | When the link makes a cycle, the link type allows it. By default it does. Only links of the same link type count | Cycle |
| 10 | The app rules (at commit only) | The reason of the app |

When reconnecting, the link being moved does not count for limits, duplicates or cycles.

### Nesting rules

| Declaration | On | Content |
|---|---|---|
| Allowed child types | Tree groups | Any type when not declared |
| Allowed member types | Overlay groups | Any node or tree group when not declared |
| Allowed parent types | Nodes, groups | Any tree group when not declared |
| Top level | Nodes, groups | Whether it can be at the top level. Yes by default |
| Stay inside parent | Nodes, groups | The parent does not change, and the element cannot leave the frame of a manually sized parent (a node in a loop) |

- An element enters a parent only when the declarations of both the parent and the child allow it. This decides drops and the parent after a move (Model)
- Allowed member types are used by the commands that change the members of an overlay group
- Releasing where an element cannot be placed rejects the drop and shows the reason (Palette)

### Counts and permissions

| Declaration | Content |
|---|---|
| Maximum count | How many the diagram can have. At the limit, the type leaves the palette, and duplicating and pasting are rejected |
| Required | An error in the whole-diagram check when the diagram has none |
| Undeletable | The user cannot delete it |
| Start | Marks start elements. Queries use them to find unreachable elements |

Permissions come from three levels, and any one of them can stop an operation.

| Level | Content |
|---|---|
| View | `ReadOnly` and `…Enabled` (Components and packages) |
| Element | The lock (`Locked`, which stops everything but selection) and the element permissions (`Permissions`, which replace those of the type when set) |
| Type | The permissions of the type |

The permissions are select, move, resize, delete, connect and edit text. Counts, undeletable and permissions limit the user, not the app.

### Palette filtering

- The palette shows the node and group types that are set to appear in it and are below their maximum count. App rules can hide more. This is checked again whenever the diagram changes
- The type picker (Palette) shows the same types, limited to those that can be placed at its position. When it opens from a link, an arrow or a port, it also includes the types not shown in the palette, limited to those with a connection point that can connect there

### App rules

- The app adds C# rules (synchronous functions) to the schema. They decide whether a connection, a nesting or a deletion is allowed (with a reason), whether a type is in the palette, and the whole-diagram check
- At commit, they check only the operations that passed the declared rules. They do not run while dragging
- The whole-diagram check uses the same rules, so the result is the same where there is no view (on a server)
- The `…ing` events of the view arrive after the app rules have passed

### Checks in the browser

- The view sends the declarative part of the schema to JS as JSON, at first and when the schema is replaced. Functions, data types and app rules are not sent
- For each element, it sends the computed ports and permissions. Data is not sent
- The declared rules are checked the same way by JS while dragging and by C# at commit. At commit, C# checks again on the current snapshot

## 🖼️ Rendering

The view draws the diagram into one SVG from the type declarations and the templates. While panning and zooming, JS changes only transforms, and C# does not render in the meantime.

### Layers

From back to front:

| Layer | Contents |
|---|---|
| Background | The background color and the grid |
| Tree groups | Frame, fill, header, ports |
| Overlay groups | Frame, header, ports |
| Links | Line, end markers, a wide transparent line for hit testing |
| Nodes | Shape, icon, labels, ports |
| Badges | State overlays (State overlays) |
| Interaction | Selection frame, handles, guides, preview lines, the box selection frame, connection candidates |

- Only the interaction layer is drawn in a separate SVG laid over the diagram SVG
- Icons and end markers are placed once in `<defs>` and referenced with `<use>`. The grid is one pattern that fills the visible area
- The text editor is HTML over the SVG

### Panning and zooming

| Operation | Behaviour |
|---|---|
| Pan | Space+drag, middle-button drag, wheel (vertical), Shift+wheel (horizontal). On touch, a one-finger drag on the background. A left drag on the background is box selection (Selection and moving) |
| Zoom | Ctrl+wheel, Alt+wheel, pinch (keeping the point under the pointer still), and the zoom commands |
| Fit | Sets the zoom and position so that the whole diagram, or the selected elements, fit with a margin. An element can also be brought to the center |

- `WheelMode` makes the plain wheel zoom instead
- The zoom goes from `MinZoom` (default 0.05) to `MaxZoom` (default 8). Command zoom moves in steps that pass 100% and sit on a 5% grid. Wheel and pinch use a 1% grid
- While zooming with the wheel or a pinch, the drawn diagram is scaled as it is, and it is drawn again at the new zoom when the zoom stops
- The API of the view reads and writes the zoom and the center (`Zoom`, `Center`), fits the whole diagram or the selected elements, and centers an element. When panning and zooming stop, `ViewportChanged` reports it
- The position and zoom of the view are not in the diagram. To keep them, the app saves the values of `ViewportChanged`
- The view has no edges. Page frames and limits on the visible area are not in the first version

### Grid

- `GridVisible` shows the grid, with the spacing `GridSize` (default 10). It is drawn as dots or lines, with every fourth line darker
- At zoom levels where the spacing on screen is under 8 px, the spacing is doubled until it is not. The snapping spacing does not change

### Culling

- The visible area is extended by half its size on each side, and elements outside it are not drawn
- When panning and zooming stop, the area is computed again and the missing elements are drawn
- Selected elements, elements being moved, and links crossing the area (even with both ends outside) are always drawn

### Nodes and groups

- The component draws the declared shape (rectangle, ellipse, diamond, path), the icon and the labels. A path is written in a 0–1 box and stretched to the size of the element, without stretching the stroke width
- A type with a template is drawn by the template. Templates are registered on the view by type name. A template receives the element with its size, labels and data, and renders SVG elements with the top left of the element as the origin. HTML can go inside `foreignObject`
- Templates do not render again when states such as selection or hover change. CSS draws the states
- Icons are `<symbol>` elements drawn with `<use>`. When the `Icon` of an element is an image URL, it is drawn with `<image>`
- A group draws its frame, fill and header (icon and name). The fill is not hit-tested; the frame, the header and the ports are (the type can make the fill hit-tested too)
- Group headers and presets for AWS are in AWS architecture diagrams

### Labels

| Placement | Content |
|---|---|
| Inside | 9 places in the shape (the corners, the middle of each side, the center) |
| Outside | Above, below, left or right of the shape (a name under an icon) |
| Header | Next to the icon in the header of a group |

- Slots in the same placement are stacked in the declared order
- The format is the font, size, weight, color and alignment. Colors can also be CSS variables
- The type declares the width of a slot; without it, the width of the element is used
- JS wraps the text. C# passes the text, the width of the slot and the maximum lines, and JS measures with the fonts of the browser and splits the lines
- Lines break between words, and inside a word only when the word is wider than the slot. A punctuation mark or a closing bracket never starts a line. A line break in the text always breaks
- Past the maximum lines, the end of the last line becomes "…", the rest is left out, and `<title>` holds the full text. Without a maximum, all lines are drawn
- The text is wrapped again when fonts finish loading, and when the text, the width of the slot or the format changes
- The number of lines is not returned to C#. The size of a label never changes the size or position of an element
- Export writes the wrapped lines (Saving and export). A rule that keeps set words (AWS, Amazon) on the same line as the next word is not in the first version

### Port positions

C# and JS compute the port positions with the same formula from the type and the position and size of the element. The DOM is not measured.

| Declaration | Position |
|---|---|
| Side | Top, right, bottom, left |
| Ratio | The position along the side (0–1, from the top or the left) |
| Order | Without a ratio, the ports on a side are placed at `(i + 1) / (count + 1)` in the declared order |
| Row | On the left or right side, at the header height + (row + 0.5) × the row height (the type declares both heights) |
| Offset | Pixels outwards from the side |

- When an element is resized, its ports follow by ratio
- CSS can draw each kind of port differently. Ports shown only on hover are not in the first version

### Styles and themes

- The colors, strokes and text of a type are values or CSS variables. Named sets of looks (presets) are registered in the schema, and types use them by name
- The SVG elements of an element have a class for its kind (`mussel-diagram-node` and so on) and an attribute with its type name, so CSS of the app can style each type
- CSS draws each state: selected, hover, not allowed, connection candidate, drop target, awaiting commit, error, warning
- Colors are `--mussel-diagram-…` CSS variables, which default to the shared `--mussel-…` variables. Dark mode is `.mussel-dark` on an ancestor
- The selection frame, the handles, the guides, the preview lines and the marks of connection candidates keep the same size and width on screen at any zoom

### Updating

- Each element is a component (`@key` is the ID), and only the elements that changed in the snapshot render again. An unchanged element is the same record as in the previous snapshot, so comparing references is enough
- A `BeginUpdate` scope renders once, when it ends
- C# does not render while panning, zooming or dragging

### Cursors

| Place or state | Cursor |
|---|---|
| Background | Default |
| Panning (while Space is held, middle button) | `grab`, and `grabbing` while moving |
| A movable element | `move` |
| A resize handle | The `…-resize` for its direction |
| A port that can start a link | `crosshair` |
| A place where the dragged item cannot be placed or connected | `not-allowed` |
| The text editor | `text` |

CSS can change the cursors.

## 🖱️ Selection and moving

The browser handles the pointer and the keys, and sends only commits to C# (the interaction states in Terms).

### Selection

- The selection is a set of element IDs per view. JS changes and draws it at once and reports it to C# (`SelectionChanged`). The app reads it with `SelectedIds` and changes it with `SelectAsync` and `ClearSelectionAsync`
- Nodes, groups and links can be selected. Shared items cannot
- A click selects the frontmost element under the pointer. A group is selected by its frame, header or ports (Rendering)
- A click on a port that can start a link does not change the selection and opens the type picker (Palette)
- Shift+click and Ctrl+click (Cmd on Mac) add to or remove from the selection
- Box selection, a left drag on the background, selects the elements entirely inside the rectangle. With Shift, it adds to the current selection. Selecting the elements that only touch the rectangle is not in the first version
- A click on the background, and Esc outside an operation, clear the selection. Deleted elements leave the selection
- Selected nodes and groups show a frame, selected links a highlight and a handle at each end (reconnecting is in Connecting), and resizable elements show resize handles. All of them keep the same width and size on screen
- Commands and commits that JS sends to C# carry the selection of that moment. Even when the selection known to C# is behind, they act on the selection the user saw

### Moving

- Dragging a selected element moves the whole selection. Dragging an unselected element selects only it and moves it
- The descendants of tree groups and the members of overlay groups move with the group (Model). Links follow their end elements; dragging a link alone does not move it
- Elements that cannot move (lock, permissions) stay even when selected. An element that stays inside its parent moves only inside the frame of a manually sized parent (Types and rules)
- Snapping and guides work while moving (Snapping and guides). On release, the parent is decided again (Model) and the move is committed as one change (Commit and validation)
- When the release will give a new parent, that group shows the drop target look while moving
- Releasing a single node without links on a link splices it into the link (Palette)
- While dragging, JS moves the elements and their links at once. With more moving elements than `LivePreviewLimit` (default 32), only the frame of their bounds moves, and they are drawn after the release
- When the pointer comes within 40 px of an edge of the view while dragging, the view pans, faster nearer the edge, up to 16 px per frame

### Resizing

- A resizable element shows handles at its corners and the middle of its sides when selected (on each selected element). Only the element of the handle changes
- An element never gets smaller than the minimum size of its type. A tree group never gets smaller than the bounds of its children plus its padding. The children neither move nor change size
- Groups sized to their contents and overlay groups show no handles
- Ports follow by ratio. While resizing, the edges snap to the grid and the guides
- Keeping the aspect ratio and locking an axis are not in the first version

### Align, distribute, order and lock

These are commands (Commands and keys), each one change.

| Command | Behaviour |
|---|---|
| Align | Aligns each element to the left, horizontal center, right, top, vertical center or bottom of the bounds of the selected elements |
| Distribute | In horizontal or vertical order, keeps the outer elements and spaces the centers of the others evenly |
| Bring to front, send to back | Moves to the end of the order in the layer. Bring forward and send backward exist too. Tree groups move among their siblings |
| Lock, unlock | Switches `Locked` of the selected elements |

Align and distribute do not move elements that cannot move. The descendants of groups move with them, and parents do not change.

### Pointer and touch

Pointer Events take the mouse, the pen and touch together. The view has `touch-action: none`.

| Input | Background | Element |
|---|---|---|
| Left mouse button, pen | Drag for box selection | Click to select, drag to move |
| Middle mouse button, Space+left | Drag to pan | Drag to pan |
| One finger | Drag to pan. After a long press (250 ms), drag for box selection | Tap to select, drag to move, long press to add to or remove from the selection |
| Two fingers | Pinch to zoom, move to pan | The same as on the background |

- Nothing is a drag until the pointer moves 4 px on screen (8 px for touch). A release before that is a click (a tap)
- When a second finger touches during a touch operation, the edit stops and is undone, and zooming and panning take over. One-finger movement after that does not edit again
- Esc, `pointercancel` and a lost capture stop the operation and undo it

### Click, double-click and tooltips

| Operation | Behaviour |
|---|---|
| Click | Changes the selection, and `ElementClicked` (`BackgroundClicked` for the background) reports the position and the modifier keys |
| Double-click | On an editable slot, starts editing its text (Commands and keys). Elsewhere, `ElementDoubleClicked` (`BackgroundDoubleClicked` for the background) reports it |
| Hover | Shows the tooltip of the element with `<title>`: the text that a function of the schema makes from the data, otherwise the full text of a shortened label |

Context menus on right-click and long press are not in the first version.

### Keys

- The view takes keys only when it has the focus. Pressing the view gives it the focus
- During text editing, and when an input in a template has the focus, the keys used for typing go to the input and are not commands (Commands and keys)
- Which key runs which command is in Commands and keys

### Inputs in templates

- Pressing an input element in a template (`input`, `textarea`, `select`, `button`, `contenteditable`) starts no selection, drag or pan
- Neither does pressing an element with the `mussel-diagram-nodrag` class. Over an element with `mussel-diagram-nowheel`, the wheel neither pans nor zooms

### Turning behaviour off and adding it

- Each built-in operation can be turned off with its `…Enabled`: pan, zoom, selection, box selection, move, resize, connect, drop, text editing, tooltip. `ReadOnly` turns off all editing operations (Components and packages)
- The app adds its own behaviour through the click and double-click events and through commands. Switching modes is not in the first version

## 🧲 Snapping and guides

JS computes snapping and guides while dragging. They work for moving, resizing and dragging from the palette.

| Setting | Default | Content |
|---|---|---|
| `SnapToGrid` | On | Snaps to the grid |
| `SnapToElements` | On | Snaps to the edges and centers of other elements, and shows guides |
| `SnapTolerance` | 8 | The snapping distance (screen pixels, divided by the zoom for the distance in the diagram) |

### Grid

- When moving, the position (top left) of the pressed element is rounded to the grid, and the same offset moves the whole selection. The distances between the selected elements do not change
- When resizing, the moving edges are rounded to the grid
- An element placed from the palette has its top left rounded to the grid
- Rounding the offset instead is not in the first version

### Snapping to other elements and guides

- The left, horizontal center and right, and the top, vertical center and bottom of the bounds of the moving elements snap to the same three lines of other elements. On each axis, the nearest line within the tolerance wins
- Every line that matches exactly at the snapped position shows as a guide. A guide spans both the moving rectangle and the other one, and keeps its width on screen. Guides are never saved or exported
- When resizing, only the moving edges snap
- Only the nodes and groups in the visible area count. The moving elements, their descendants and ancestors, and links do not
- The candidates are collected once, when the drag passes the threshold, and looked up through indexes sorted per axis
- Snapping to equal spacing and to equal sizes is not in the first version

### Priority and turning snapping off

- On each axis, snapping to elements comes first. On an axis that snapped to an element, the grid is not used
- Snapping uses the position before snapping. A snapped position is never the base of the next check
- While Alt is held, neither the grid nor other elements snap, and no guides show

## 🔗 Connecting

Creating links and reconnecting their ends. The connection rules in Types and rules decide: JS checks them while dragging, and C# at commit. Port positions and their order are in Rendering.

### Starting

- Dragging an out or both port draws a link from it. A click on it is in Palette (adding a connected element). Links cannot be drawn from in ports in the first version
- For a type that connects as a whole, resting the pointer on the element for 140 ms (on touch, selecting only that element) shows "connect arrows" above, below, left and right of it. Dragging an arrow draws a link from the whole element. A click on an arrow is in Palette (adding a connected element)
- Ports are always shown. A type can declare ports that show only while a connection is dragged (the way draw.io uses its connection points)
- The type of the new link is in Types and rules: the declaration of the port, then the `LinkType` of the view, then the default of the schema

### Targets

While dragging, the target is searched in this order. Targets the rules reject are not candidates.

| Order | Target | How |
|---|---|---|
| 1 | Port | The ports within `ConnectionRadius` (default 20 screen pixels) of the pointer. The one right under the pointer first, otherwise the nearest |
| 2 | Whole element | The innermost element under the pointer that connects as a whole |

- Once a target is found, the end of the preview line snaps to it
- Links do not connect to links

### While drawing

- A preview line runs from the start to the current target (or the pointer), with the look of the new link type. Its route is the simple route used while dragging (Link routes)
- The elements and ports that can be targets are highlighted. Ports shown only while connecting appear on the elements that can be targets
- A target under the pointer that cannot be connected shows the "not allowed" look and the reason (the texts are in Languages)
- Auto-scrolling at the edges and Esc work as for moving (Selection and moving)

### Releasing

| Released on | Behaviour |
|---|---|
| A target that can be connected | Creates the link and commits it (Commit and validation) |
| A target that cannot | Creates nothing and shows the reason |
| Empty space | Opens the type picker (Palette) at the release point, with the types that can connect to the start. Choosing a type places the element with its center at the release point (snapped to the grid) and connects it at its first declared connection point that can connect. Adding the element and connecting are one change. Closing the list creates nothing |
| The connection point it started from | Creates nothing |

When the rules of C# reject the commit, the preview line goes away and the reason shows (Commit and validation).

### Reconnecting

- A selected link has a handle at each end. Dragging an end handle reconnects that end to another target. Targets, checks and snapping work as when drawing, and the moving link does not count for limits, duplicates or cycles
- Releasing on empty space puts the end back
- Links and end elements whose permissions or lock stop connecting show no handles

### Events

| Event | When |
|---|---|
| `Connecting`, `Connected` | Before and after a link is created. Before, `Cancel` stops it and a reason can be returned |
| `Reconnecting`, `Reconnected` | Before and after an end is reconnected. Before, `Cancel` stops it and a reason can be returned |

- Each carries the elements and ports of both ends, the link type, and an element placed by releasing on empty space
- Links are deleted with the delete command (Commands and keys). `Changed` of the diagram reports every change

## 〰️ Link routes

C# computes the route of a link (a list of points) after the commit and stores it in the link. While dragging, JS draws a simple route.

### Line forms

The link type declares the form.

| Form | Content |
|---|---|
| Orthogonal (default) | Horizontal and vertical segments. It avoids nodes |
| Straight | One segment between the ends |
| Curved | A cubic Bézier. The control points stick out from each end in the direction of the end |

- The direction of an end is outwards from the side of its port, or, for a whole element, outwards from the side facing the other end
- An end attached to a whole element leaves from the middle of the side facing the other end. Ends that slide along the outline are not in the first version
- Editing waypoints, rounded corners and jumps over crossing lines are not in the first version

### Orthogonal routes

- A route leaves each end straight out for the stub length of the link type (default 20) before it turns
- It avoids the nodes as rectangles with a margin (default 10). The two end elements are avoided too, beyond the stubs, with half the stub length as the margin when that is smaller. Groups are not avoided, and routes may cross group frames
- It takes the route with the lowest length plus a penalty per bend. The same layout always gives the same route
- Points on one straight line are removed
- When no route avoids the nodes (for example, the end of a stub is inside another node), the simple orthogonal route that does not avoid them is used

### When routes are computed again

- When an element is added, deleted, moved or resized, the links attached to it, and the links whose routes pass within the margin of its rectangle before or after, are computed again. The routes of the other links do not change
- The new routes are in the same change as the operation, for up to 20 ms of computing per change. Beyond that, a link whose simple route avoids the nodes gets it at once, and the others get a provisional simple route and are computed afterwards, 4 ms at a time with the browser drawing in between. Loading works the same way
- The later results join the change without a new undo step. They are written together when all are done, or when the oldest has waited a second. A result that crosses a node added or moved in the meantime is computed again, and so are such links after undoing a step that moved nodes
- Computing afterwards needs the browser (WebAssembly) or a `SynchronizationContext` (Blazor Server). Elsewhere, such as in a console or on a server without a view, every route is computed in the change
- While dragging, JS draws a simple route that does not avoid nodes (the same form, with 1 or 2 bends), and the route of C# replaces it after the commit
- Computed routes are saved, and loading computes again only the links whose ends no longer match (Saving and export)

### Replacing the router and reading routes

- The app can replace the router by registering one in the schema, also per link type. It receives the positions and directions of both ends, the rectangles of the nodes to avoid and the link type, and returns a list of points or the reason it could not route
- With an asynchronous router, the commit stores a simple route, and only the route is replaced when the router finishes. The replacement joins the undo step of the operation. When the diagram has changed in the meantime, the result is thrown away and the route is computed again (Commit and validation)
- The cancellation token of a request is canceled when its result is no longer needed: the route is asked for again, the link is deleted (by undo too), or the diagram is loaded. Links that are provisional without a request after undo and redo are asked for again
- A link knows whether its route is provisional or a result of the router, or why it could not be made
- The app can read the points of a route and the drawn geometry (end positions, marker positions and directions, the path)

### Ends

- The end markers are none, arrow, open arrow, circle and diamond, chosen separately for the source and the target (link type)
- An end attached to a whole element stops at its outline: the shape for rectangles, ellipses and diamonds, the bounds for path shapes and icons (an AWS resource icon stops at its 48 px frame)
- An end attached to a port stops at the port
- At an end with a marker, the line is shortened by the marker so that it does not stick out of it
- Stopping before a label placed outside the element is not in the first version

### Line styles and hit testing

- The link type declares the color, the width and the dash (solid, dashed, dotted) as values or CSS variables (Rendering)
- Selection and hover use a transparent line over the visible one, `LinkHitWidth` wide on screen (default 10 px)
- A selected link is highlighted (Selection and moving)

### Geometry

- The core computes bounds, intersections, distances, intersections with outlines and whether a point is inside a shape. The routing and checks of C# use them, and so can the app
- JS has the same formulas for the simple route while dragging and for cutting the ends

## 🧰 Palette

The palette (`MusselDiagramPalette`) lists the types of the schema that can be placed, and places them by dragging onto the view. The view also has a type picker to search for and choose a type. JS handles the drag with pointer events.

### List and categories

- The items come from the schema of the diagram of the view passed as a parameter (`View`), and are built again when the schema changes
- There is one item per type, for node and group types. Which types appear is in Palette filtering (Types and rules). Link types are chosen with the `LinkType` of the view, and shared item types never appear
- An item shows the icon of the type (otherwise a small version of its shape) and the display name. A display name that does not fit is shortened, with the full text in `title`
- Items are grouped under category headers, and a click on a header opens or closes it. The order, the display names and whether a category starts closed are in the category list of the schema. Categories not in the list come after it, and types without a category come first, without a header. Within a category, the order is the order in the schema
- `Categories` limits the categories shown (for tabs per category in the app, for example)
- The items of closed categories, and items outside the visible part, are not drawn. Item icons reference the same `<symbol>` as the view
- CSS changes the look. Colors use the same CSS variables as the view (Rendering)
- Items are disabled when the view is `ReadOnly` or `DropEnabled` is false
- Item descriptions, large previews, recently used types, and items the user adds are not in the first version

### Search

- Typing in the search box above the list filters by display name, search words, category display name and type name
- Matching is partial and case-insensitive. With words separated by spaces, only the items that contain all of them appear
- While searching, only the categories with matches show, open. Within a category, exact matches of the display name come first, then prefix matches, then the rest, otherwise the order in the schema. Without matches, a message says so (the texts are in Languages)
- The app can pass a function that normalizes text before comparing (full-width and half-width, hiragana and katakana and so on)
- The type picker uses the same search

### Placing

| Step | Behaviour |
|---|---|
| Press | With the mouse and the pen, pressing an item and moving 4 px starts a drag. On touch, moving after a long press (250 ms) starts it, and moving before that scrolls the palette |
| Outside the view | The icon of the item follows the pointer |
| Over the view | A semi-transparent preview of the element (the shape and icon of the type, the default text of its label slots) is drawn at its real size for the zoom. The center of the preview follows the pointer, with snapping and guides (Snapping and guides). For a type drawn by a template, the preview is a frame of the default size with the icon |
| Target highlight | The group that will be the parent, and a link to splice into, show the drop target look. Where the element cannot be placed, the "not allowed" look and the reason show |
| Release | Where the element can be placed, it is added. Outside the view, over the palette, and where it cannot be placed, nothing happens |

- The new element is placed at the position of the preview, with the default size of the type. Its label and data are the defaults of the type. When the type registers a function, the function makes them from the diagram and the value of an external drag source (External drag sources), for example a numbered name
- The new element is selected, and the view gets the focus
- Adding is one change, which includes splicing into a link. `Dropping` and `Dropped` report the type, the position, the parent, the value and the link spliced into, before and after. Before, `Cancel` stops it and a reason can be returned (Commit and validation)
- A placed group does not take the elements under it as children
- Auto-scrolling at the edges, Esc and a second finger work as for moving (Selection and moving)
- Placing by click, placing repeatedly, an animation back after a rejected drop, and dropping files, images and text from the OS are not in the first version

### Parent and where an element can be placed

- The parent is the innermost tree group under the pointer at the release that accepts the type (as for moving, Model). Locked groups are never parents. Without such a group, the element goes to the top level
- The following checks run in order, and the first one that fails gives the reason. JS checks while dragging, and C# at commit

| Order | Check | Reason |
|---|---|---|
| 1 | The type is below its maximum count | Count |
| 2 | A group accepts it, or it can be at the top level | Nesting |
| 3 | The app rules (at commit only) | The reason of the app |

- The reason has a kind and a text. While dragging, it shows next to the pointer. A rejection at commit is in Commit and validation (the texts are in Languages)
- Placing from the type picker runs the same checks. The parent when placing from an arrow or a port is in Adding a connected element

### Dropping on a link

- Releasing one node on a link splices the node into the link. This works when placing from the palette, and when moving a single node that has no links
- On a link means that the pointer is in the hit area of the link (`LinkHitWidth`). With several links, the frontmost counts
- Splicing works only when both new links, from the old source to the node and from the node to the old target, pass the connection rules with the link type of the old link. The connection points of the node, on the incoming and on the outgoing side, are the first declared ones that can connect
- When splicing works, the link is highlighted. On release, the target of the old link moves to the node, and a link of the same type goes from the node to the old target (with the default data of the type). The old link keeps its ID and data
- When splicing does not work, the link is not highlighted, and the release places the node normally
- While Alt is held, nothing is spliced. Groups are never spliced

### Type picker

A small list that opens over the view to find and choose a type. It has a search box, and items that look like those of the palette.

| Opened by | Types | Placed at |
|---|---|---|
| Releasing a link on empty space (Connecting) | Types that can connect to the starting connection point | The release point, as the center |
| A click on a connect arrow, or on a port that can start a link | Types that can connect to that connection point | Away from the arrow or the port, in its direction (Adding a connected element) |
| The command "Quick add" | Types shown in the palette | The pointer position as the center (the center of the visible area when the pointer is outside the view) |

- Every list shows only the types that can be placed at that position (Palette filtering in Types and rules). Without such types, it does not open
- The order is the order of the palette, and typing filters as in the palette
- ↑ and ↓ choose, and Enter or a click places. Esc or a click outside closes the list and creates nothing
- While the list is open, a preview frame with the size of the chosen type is drawn at the position. When it was opened from a link, an arrow or a port, a preview line from the connection point is drawn too. The list opens where it fits in the view
- The placed element is snapped to the grid, passes the checks of Parent and where an element can be placed, and is selected. When the list was opened from a link, an arrow or a port, the element is connected at its first declared connection point that can connect, and adding and connecting are one change (`Connecting`, `Connected`). When it was opened by the command, `Dropping` and `Dropped` report it
- The app can open it too, at a position it gives (on a double-click on the background, for example)
- It places one element at a time. Adding several in a row is not in the first version

### Adding a connected element

- A click on a connect arrow (Connecting), or on a port that can start a link, opens the type picker from that connection point. A click on a port does not change the selection
- The element of the chosen type is placed in the direction of the arrow or the port, `QuickAddSpacing` (default 80) away from the bounds of the original element. On the other axis, its center lines up with the center of the original element for an arrow, and with the port for a port
- The parent is that of the original element. When the nesting rules do not allow it, the position decides (Parent and where an element can be placed)
- The element stays where it is placed, even over other elements. Looking for free space is not in the first version

### External drag sources

- Wrapping an HTML element of the app in `MusselDiagramDragSource` lets a drag start from it, as from a palette item. Its parameters are the view, the type and a value (a string)
- The value goes to `Dropping`, `Dropped` and the function that makes the label and data of the placed element (Placing), for example to make an element from a row of a table of the app
- Palette items work the same way

### Importing icon sets

- An icon set is a prefix and its icons (ID, SVG content, width and height, name, category, search words). It is added to the schema, and the icon of a type and the `Icon` of an element refer to an icon as `prefix:ID`
- The core reads Iconify JSON (the prefix, the content and size of the icons, the categories) and sets of SVG files (a zip or a folder). A function can make the ID, the name and the category from the relative path of a file (by default, the file name is the ID and the name, and the folder name is the category)
- It reads only the streams and files the app passes, never the network. No icon sets are bundled
- Imported SVG is sanitized (Saving and export). The IDs inside an icon get a prefix per icon
- When the app passes a base type (shape, default size, label slots, ports, rules), a type is made for each chosen icon. The type name is `prefix:ID` of the icon by default, and the display name, the category and the search words come from the icon. The app adds the types to the schema and replaces it
- Icons are sent to the view once and placed in the document once. The view and the palette reference the same ones
- How to read the official AWS package and make its IDs is in AWS architecture diagrams. Replacing a set with a newer version of it is not in the first version

## 🚦 Commit and validation

A user operation is shown by the browser first, then checked by C# and written to the model. Changes through the methods of the app pass neither rules nor events (Model).

### Commit flow

On release (for a command, when it runs), JS sends the operation, the snapshot it was based on and the selection of that moment to C#, once. C# takes them one at a time, in the order they arrive for each diagram, and checks and writes in this order.

| Order | Step | Reason for rejecting |
|---|---|---|
| 1 | What the operation writes (the positions and sizes of the moved elements, parents, ends, text) has not changed since the snapshot it was based on | The diagram changed |
| 2 | The declared rules (Types and rules) are checked again on the current snapshot | The reason of the rule |
| 3 | The app rules | The reason of the app |
| 4 | The before event (`…ing`). `Cancel` stops the operation, and a reason can be returned | The reason of the app, otherwise "Canceled" |
| 5 | The model is written. Dependent changes (descendants, group sizes, deleting attached links, routes) are in the same change | — |
| 6 | The after event (`…ed`) and `Diagram.Changed` | — |

- When a commit is rejected, the commits sent before JS received that answer are rejected too, without a reason
- When the diagram changes while step 4 is awaited, steps 1 to 3 run again before writing
- App rules are synchronous functions. Checks that wait (asking a server, for example) belong in step 4

### Events

| Operation | Before | After |
|---|---|---|
| Move | `Moving` | `Moved` |
| Resize | `Resizing` | `Resized` |
| Connect (including elements placed by releasing a link, and connected elements added from an arrow or a port) | `Connecting` | `Connected` |
| Reconnect | `Reconnecting` | `Reconnected` |
| Place (palette, external drag sources, quick add, splicing into a link included) | `Dropping` | `Dropped` |
| Delete, cut | `Deleting` | `Deleted` |
| Text editing | `TextCommitting` | `TextCommitted` |
| Paste, duplicate | `Pasting` | `Pasted` |

- The before events carry what will be written, and the after events what was written and the snapshot. Elements deleted along with it are included
- Align, distribute, order, lock, group and ungroup, and changing members raise no before or after events, but still pass the rules. To stop them, disable the command (Commands and keys)
- Undo and redo pass neither the rules nor the before and after events (Undo)

### Waiting

| `CommitMode` | Behaviour |
|---|---|
| `Optimistic` (default) | JS shows the result at the release, and takes the next operation without waiting for the answer. When the commit is rejected, the result is undone and the reason shows |
| `Wait` | JS shows the result with the awaiting commit look, and starts no new editing operation until the answer arrives (panning, zooming and hover still work). When the commit is rejected, the result is undone and the reason shows |

- JS shows the results first for pointer operations (move, resize, connect, reconnect, place) and for text editing. For commands, the rendering of C# shows the result in both modes
- When a text edit is rejected with `Wait`, the editor stays open with the reason (Esc gives up). With `Optimistic`, the old text comes back
- When the commit call fails (the connection is lost, for example), the result is undone as for a rejection

### On rejection

- JS removes the provisional display and returns to the current snapshot
- The reason shows next to where the operation happened, until the next operation starts or for 3 seconds. CSS can change its look (the texts are in Languages)
- `CommitRejected` reports the operation and the reason (for notifications of the app, for example)

### Provisional display and late results

- Until the rendering of the snapshot after the commit arrives from C#, JS keeps the provisional display (the moved elements, links with simple routes, the preview of a placed element, the written text). Each rendering carries its snapshot number, which tells JS which one has arrived
- A rendering of a snapshot older than the commit that arrives late does not undo the provisional display
- The provisional display computes port positions and cut ends with the same formulas as C#. When the rendering arrives and replaces it, elements do not move; only the links change from the simple route to the route of C#
- The result of an asynchronous router is applied when both ends and the nodes to avoid have not changed since the snapshot the computation started from. Otherwise it is thrown away and computed again (Link routes)

### Whole-diagram check

- `Validate` returns the list of issues (`DiagramIssue`) for a snapshot and a schema. It never throws, and works the same without a view (on a server)
- An issue has a severity (error or warning), a kind, a text, and element IDs (with a port or a slot)

| Checked | Severity |
|---|---|
| Links that break the connection rules (permissions excluded, app rules included) | Error |
| Parents that break the nesting rules (app rules included), and top-level elements of types that cannot be at the top level | Error |
| Types over their maximum count, and required types the diagram does not have | Error |
| Required ports without links, and links to ports that no longer exist | Error |
| Shared item IDs with no target | Error |
| Elements of unregistered types | Warning |
| The whole-diagram check of the app | Decided by the app |

- The app decides when to check (on every change, before saving and so on)
- Structural errors fixed while loading (duplicate IDs, links without ends, parent cycles) come back in the same form (Saving and export)

### Showing the results

- Passing a list of issues to `Issues` of the view gives the elements with issues the error or warning look, shows a badge (State overlays) and adds the texts to the tooltip
- Issues without elements (a missing required type, for example) are not drawn in the view
- The commands "Next issue" and "Previous issue" select the elements with issues from top to bottom and left to right, and bring them into view
- There is no list component. The app builds the list, and uses the API of the view to select an element and bring it into view

## ⏪ Undo

The diagram (`Diagram`) holds one undo history, which every view of the diagram goes through. A step is the snapshots before and after a change.

### What is recorded

| Origin | Recording |
|---|---|
| `User` | One step per commit |
| `Api` | By default, one step per call (one step for a `BeginUpdate` scope). The history mode can be chosen when writing |
| `Undo`, `Redo` | Not recorded. They move through the history |
| `Load` | Not recorded. It clears the history |

| History mode | Behaviour |
|---|---|
| `Record` (default) | Recorded as one step |
| `Skip` | Not recorded (updates from a server, automatic corrections and so on; Changes that are not recorded below) |
| `Merge` | Merged into the previous step (when writing in an after event right after an operation, for example). Without a previous step, or when there are steps to redo, the same as `Record` |

- The history mode is passed to the methods and to `BeginUpdate`. The changes inside a scope follow the mode of the outermost scope
- A step includes the dependent changes (descendants, group sizes, deleting attached links, routes, splicing into a link, connecting a placed element). The result of an asynchronous router joins the step of its operation (Link routes)
- Changes with nothing different before and after are not recorded. The selection, and the position and zoom of the view, are not in the diagram, so they are not recorded
- Recording a step clears the steps to redo. `Skip` changes do not clear them
- The history keeps up to `UndoLimit` steps (default 100), dropping the oldest
- Code that builds a diagram writes with `Skip`, or clears the history with `ClearHistory` when it is done

### Changes that are not recorded

A `Skip` change is not undone. It is applied to the snapshots of every step in the history too, and steps that no longer change anything are removed.

- An added element is added to the snapshots that have its parent and ends
- A deleted element is removed from every snapshot, with its links and descendants
- A changed element is changed, in what changed only, in the snapshots that have it. When a parent, an end or a member refers to an element that a snapshot does not have, that part is not applied there

### Undo and redo

- Undo replaces the snapshot with the one before the step, redo with the one after. Both get a new snapshot number, and `Diagram.Changed` reports them with the origin `Undo` or `Redo`, listing the changed elements
- They pass neither the rules nor the before and after events (Commit and validation)
- When a command of a view runs them, that view selects the changed elements of the step that still exist
- The app can call `Undo` and `Redo` too. `CanUndo` and `CanRedo` tell whether they can run (for enabling commands)
- They are not taken during a drag, or while a commit is awaited with `Wait`. With `Optimistic`, they run after the commits that wait for an answer
- The keys are in Commands and keys. During text editing, they undo inside the editor
- Names for steps and saving the history are not in the first version

### Snapshots in the history

- Snapshots are immutable and share the unchanged elements with the previous snapshot (Model). Recording a step does not copy the whole diagram
- A step holds the snapshots before and after, the changed elements and what changed, and the origin. The changes that are not recorded are applied to the snapshots of the steps too, so undoing only replaces the snapshot

## ⌨️ Commands and keys

A command is a named operation in the command registry of a view (`DiagramCommandRegistry`). The keys of the view and the toolbar and menus of the app run the same commands.

### Command registry

- `Commands` of the view gives the registry. It starts with the built-in commands (the table below)
- A command has a name, a display name, keys, whether it is enabled, a checked state, and what it does. The commands of the app are added in the same form
- For built-in commands, keys can be changed and removed, an enabled check can be added, and what they do can be replaced
- The enabled and checked states are checked again whenever the selection, the diagram or the state of the view changes, and `Changed` reports it (to enable and disable toolbars and menus)
- `ExecuteAsync` (a name and an argument) runs a command. `Group` and `CreateOverlayGroup` take a type, and `QuickAdd` a position
- Display names follow the texts of the language (Languages)
- There are no toolbar, menu, context menu or command palette components. The app builds them from the registry

### Keys

- The view takes keys only when it has the focus (Selection and moving)
- A key is written as modifiers (Ctrl, Alt, Shift) and a key joined with `+` (`Ctrl+Shift+Z`). On Mac, Ctrl means ⌘ and Alt means Option
- A key belongs to one command only. The last assignment wins
- JS matches the keys, and stops (`preventDefault`) and runs only the keys of enabled commands. The keys of disabled commands go to the browser
- Commands that only change the view or the selection (zoom, fit, select all, clear selection) run in JS at once when a key starts them. For the others, JS sends the name and the selection of that moment to C#
- During text editing, and when an input in a template has the focus, the keys used for typing (no modifier, Shift only, Ctrl+Z, Ctrl+Y, Ctrl+A, Ctrl+C, Ctrl+X, Ctrl+V) go to the input. During IME composition, no key is a command
- Esc cancels the operation and clears the selection, and cannot be changed (Selection and moving)
- Moving elements with the arrow keys, and moving between elements with Tab, are not in the first version

### Built-in commands

The keys follow draw.io.

| Command | Name | Keys |
|---|---|---|
| Undo, redo | `Undo`, `Redo` | Ctrl+Z; Ctrl+Y and Ctrl+Shift+Z |
| Delete | `Delete` | Delete, Backspace |
| Cut, copy, paste | `Cut`, `Copy`, `Paste` | Ctrl+X, Ctrl+C, Ctrl+V |
| Select all, clear selection | `SelectAll`, `ClearSelection` | Ctrl+A, Ctrl+Shift+A |
| Edit text | `EditText` | F2, Enter |
| Quick add (Palette) | `QuickAdd` | Ctrl+Enter (with nothing selected) |
| Group, ungroup | `Group`, `Ungroup` | Ctrl+G, Ctrl+Shift+U |
| Create overlay group, add members, remove members | `CreateOverlayGroup`, `AddMembers`, `RemoveMembers` | — |
| Bring to front, send to back | `BringToFront`, `SendToBack` | Ctrl+Shift+F, Ctrl+Shift+B |
| Bring forward, send backward | `BringForward`, `SendBackward` | Ctrl+Alt+Shift+F, Ctrl+Alt+Shift+B |
| Align (Selection and moving) | `AlignLeft`, `AlignCenter`, `AlignRight`, `AlignTop`, `AlignMiddle`, `AlignBottom` | — |
| Distribute | `DistributeHorizontally`, `DistributeVertically` | — |
| Toggle lock | `ToggleLock` | Ctrl+L |
| Zoom in, zoom out, 100% | `ZoomIn`, `ZoomOut`, `ResetZoom` | Ctrl+Plus, Ctrl+Minus, Ctrl+0 |
| Fit all, fit selection | `ZoomToFit`, `ZoomToSelection` | Ctrl+Shift+H; — |
| Next issue, previous issue (Commit and validation) | `NextIssue`, `PreviousIssue` | F8, Shift+F8 |

- Toggle lock locks all the selected elements when any of them is unlocked, and otherwise unlocks them all
- Zooming by keys and commands keeps the center of the view still. 100% sets the zoom to 1 and keeps the center

### Delete

- Deletes the selected elements. A tree group is deleted with its descendants, and the attached links are deleted in the same change. For an overlay group, only the group is deleted, and the members stay (Model)
- Elements that cannot be deleted (lock, permissions, undeletable types) stay. A tree group with a descendant that cannot be deleted is not deleted. With nothing deletable, the command is disabled
- Deleting a node in the middle and joining its neighbours is not in the first version

### Copy, cut and paste

- Copy keeps a copy of the selected elements in the view. The OS clipboard, and copying between diagrams, are not in the first version
- The copy holds the selected nodes and groups, the descendants of tree groups, the links with both ends in the copy, and the shared items the data of the copied elements refers to. An overlay group keeps only the members in the copy, and is left out when it has none
- Cut is a copy and a delete in one change (`Deleting`, `Deleted`)
- Paste adds the copied elements with new IDs and selects them
  - The parent is the parent in the copy. Otherwise it is the original parent (when it still exists and the nesting rules allow it), otherwise the top level
  - Each paste moves the position right and down by 2 grid spacings more from the copied position
  - A shared item with the same ID in the diagram is referred to. Otherwise it is added with the same ID
  - When an element cannot be placed because of a maximum count or the nesting rules, the paste is rejected with the reason
- `Pasting` and `Pasted` hold the mapping from the old to the new IDs. The app fixes references to other elements inside data by writing with `Merge` in `Pasted` (Undo)
- Pasting at the original position and placing at the cursor are not in the first version

### Group and ungroup

- Group makes the selected nodes and tree groups the children of a new tree group. The type is the argument, otherwise the default group type of the schema. Without either, the command is disabled
- It works only when all the selected elements have the same parent. Links and overlay groups are left out
- The group gets the bounds of the children plus the padding of its type, and becomes a child of the previous parent of the children. When the nesting rules do not allow it, it is rejected with the reason
- Ungroup deletes the selected tree groups and moves their children to the parent of the group (the positions do not change). The links attached to the group are deleted. For an overlay group, only the group is deleted
- The new group, or the children after ungrouping, are selected

### Members of overlay groups

| Command | Selection | Behaviour |
|---|---|---|
| Create overlay group | Nodes and tree groups | Creates an overlay group of the type in the argument (disabled without one), with the selected elements as members |
| Add members | One overlay group and other elements | Adds the other elements to the members of that group |
| Remove members | One overlay group and some of its members | Removes the selected members. When no members are left, the group is deleted too |

- When an element does not match the allowed member types (Types and rules), the command is rejected with the reason

### Editing text in place

- Starting: a double-click on an editable slot edits that slot. F2 and Enter edit the first editable slot of the only selected element
- A slot is editable when its type declares it editable and the text editing permission (view, element, type) does not stop it
- An HTML input lies over the SVG at the position and width of the slot, with a text size that follows the zoom. It grows with the lines. An empty slot shows the default text faintly, without filling it in

| Key | Behaviour |
|---|---|
| Enter, Ctrl+Enter | Commits |
| Shift+Enter, Alt+Enter | Adds a line (not in a slot with a maximum of 1 line) |
| Esc | Gives up |

- During IME composition, Enter and Esc belong to the composition, and neither commit nor give up
- Losing the focus (pressing outside the view, Tab and so on) commits
- The commit is `TextCommitting` and `TextCommitted` (Commit and validation). Unchanged text writes nothing. An emptied slot shows the default text
- Panning and zooming work while editing, and the input follows the element. When the element is deleted, editing stops
- Starting to edit by typing is not in the first version

## 💾 Saving and export

The core saves and loads diagrams as JSON. The view exports SVG, PNG and JPEG, because export uses the rendering of the browser.

### Saving and loading JSON

- `Save` writes the current snapshot as JSON, and `Load` reads JSON and replaces the contents of the diagram (with the origin `Load`; the undo history is cleared). Both take streams and strings
- The schema is not in the JSON (the code of the app holds it)
- Reading and writing are source generated, and work with trimming and Native AOT

| Field | Content |
|---|---|
| `format` | `"mussel-diagram"` |
| `version` | The version of the save format (now 1) |
| `nodes`, `groups`, `links`, `sharedItems` | Arrays of elements, in stacking order (Model) |

- The fields of an element have short lower-case names (`id`, `type`, `x`, `y`, `width`, `height`, `parent`, `label`, `texts`, `icon`, `data` and so on). Fields without a value, and default values, are not written. The details are in [API.md](API.md)
- Data is written with the `JsonTypeInfo<T>` of the data type registered for the type. `JsonElement` data is written as it is
- Unknown fields are skipped when reading

### What loading fixes

When the text is not JSON, the `format` differs, or the version is newer than the current one, loading throws and the diagram does not change. Otherwise, the parts that break the rules of the model (Model) are fixed as follows, and the fixes come back as issues (Commit and validation).

| Problem | Fix |
|---|---|
| Duplicate IDs | The first one stays, and the later ones are dropped |
| An end referring to an element that does not exist | The link is dropped |
| A parent that does not exist or is not a tree group, and parent cycles | The element goes to the top level |
| Members referring to elements that do not exist, or that are not nodes or tree groups | Those members are removed. An overlay group with no members left is dropped |
| A group whose kind does not match its type | Treated like an unregistered type |
| A size of 0 or less | The default size of the type |
| Data the data type cannot read | Kept as `JsonElement` (a warning) |

- Links to ports that no longer exist stay. They are drawn towards the outline of the element, and the whole-diagram check reports them as errors (Types and rules)
- Elements of unregistered types are kept as they are (Types and rules)

### Versions of the save format

- `version` is the version of the save format. Older versions are migrated step by step to the current one when read. Saving always writes the current version
- The app versions and migrates the contents of data in its data types

### Saving routes

- Computed routes are saved, only when they are results of the router. Provisional routes, and the fact that a route could not be made, are not written
- Loading computes again only the links without a route, and the links whose route end points no longer match the end positions computed from the elements
- With an asynchronous router, the diagram loads with simple routes, and the results replace them when they arrive. The replacements are not recorded for undo

### Modified flag

- `IsModified` tells whether the contents of the diagram have changed since `MarkSaved` was last called. `Load` and `MarkSaved` clear it
- Undoing back to the contents at the time of saving clears it. `Skip` changes set it too
- It can be read again on each `Diagram.Changed` (to enable a save button, for example)

### Sanitizing

- Loaded JSON and the SVG of icons are treated as untrusted
- Labels, slot texts and tooltips are always drawn as text, never parsed as HTML
- The SVG of an icon keeps only allowed elements and attributes. Scripts, event attributes, `foreignObject` and references to external URLs are removed (the prefixes of the IDs inside are in Palette)
- The `Icon` URL of an element is used only when it is an http, https or relative URL, or a data URL of an image. Other URLs are not drawn
- What was removed, and the URLs not used, come back as issues (warnings)

### SVG export

- `ExportSvgAsync` of the view returns the whole diagram as one SVG. Export without a view is not in the first version
- It has the groups, links and nodes, and the wrapped labels. Culled elements are drawn before writing. The selection frame, handles, guides, preview lines and the grid are left out
- State overlays (states, render overrides, badges) and issue badges are included when chosen (not by default)
- The area is the bounds of all the elements plus a margin (default 10). Choosing an area is not in the first version
- The background is transparent by default, and can be filled with the background color. Colors follow the current light or dark mode
- The colors, strokes and text formats that CSS variables and classes decide are written to the attributes of the elements as computed values, so the SVG looks the same without CSS
- Each `<use>` of an icon is replaced with a copy of its content, with the IDs inside made unique per copy. An icon from an image URL is embedded as a data URL when it can be fetched, and otherwise left out and reported as an issue (because of CORS, for example)
- Fonts are written by name only by default. When chosen, the fonts used among those the page loaded with `@font-face` are embedded as data URLs
- PDF, and images with the diagram data embedded, are not in the first version

### PNG and JPEG export

- `ExportImageAsync` of the view takes the format (PNG or JPEG), the scale (default 1) and the background, draws the exported SVG on a canvas of the browser, and returns the image
- The SVG it draws always embeds the fonts used among those the page loaded with `@font-face`
- JPEG cannot be transparent, so the background is filled with the background color. The quality can be chosen
- When the image would be larger than the canvas limits of the browser, nothing is exported and the reason is returned

### Sending large data

- The results of export go from JS to C# as streams (`IJSStreamReference`), and to the app as streams, because they can be larger than what C# accepts in one message in Interactive Server (32 KB by default)
- From C# to JS, the schema and the icons are sent only when they change, and elements only as changes

## 🏷️ State overlays

The app can draw states over elements without changing the model (run states, change marks, the results of the whole-diagram check and so on). Overlays are passed to each view, and are in none of saving, undo and `Diagram.Changed`.

### Passing overlays

- `Overlays` of the view takes a dictionary from element IDs to overlays (`DiagramOverlay`). To change them, pass a new dictionary
- The view compares it with the previous dictionary, and draws again only the overlays of the elements that changed. Elements and templates do not render again
- Nodes, groups and links can have overlays. IDs of elements that do not exist are ignored

| Overlay | Content |
|---|---|
| States | A list of state names. The element gets the class `mussel-diagram-state-<name>`, and CSS of the app draws it |
| Render override | Fill, stroke color, stroke width, dash and opacity. It takes priority over the look of the type |
| Badges | Small marks on the corners of the element (Badges below) |

- State names are letters, digits and hyphens
- The classes are separate from the built-in states (selected, hover, awaiting commit, error, warning and so on; Rendering), and both can apply at once
- They work the same on elements drawn by templates
- The app builds run state displays, highlighting the links a run took, and dimming other elements with these

### Badges

- A badge has a kind, text, an icon, a tone and a tooltip
- Kinds are registered in `BadgeKinds` of the view, with a name, a corner (top left, top right, bottom left, bottom right), an order and whether they can be clicked. Unregistered kinds go last in the top right
- Badges are packed from their corner along the side, in order, 4 px apart. They are 16 high and scale with the diagram
- The tones are default, info, success, warning and error. Their colors are CSS variables (Rendering)
- A badge without text is a dot. The tooltip uses `<title>`
- A click on a badge of a clickable kind does not change the selection, and `BadgeClicked` reports it. Clicks on other badges go through to the element

### Issue badges

- The issues in `Issues` of the view (Commit and validation) show as badges of a built-in kind, first in the top right
- There is one per element, with the tone of the most severe issue, and the count when there are 2 or more. The tooltip has all the texts

### Export and screen readers

- Whether export includes them is in Saving and export (not by default)
- Reading badges with screen readers is not in the first version

## ☁️ AWS architecture diagrams

AWS architecture diagrams are drawn with the general features of the library. The types for AWS (the looks of groups, the types of icon nodes and arrows, how to read the official package) are not in the library. The Example shows them in code and documentation, and apps copy them. No icons are bundled.

### Elements of the diagram and how to draw them

| Element of the diagram | How |
|---|---|
| Service and resource icons | Node types. The icons are 80 px for services and 48 px for resources, and permissions turn resizing off. The name is under the icon (outside, below), up to 2 lines |
| Groups such as AWS Cloud, Region, VPC and subnets | Tree group types. Manually sized, with the header and line look from presets (Group headers and Types in the Example below) |
| Availability Zone | A tree group when it is placed inside a VPC, an overlay group when it crosses a VPC |
| Auto Scaling group | An overlay group (across Availability Zones and subnets) |
| Arrows | Link types: open arrows (at one end, both ends, none), orthogonal routes, ends attached to whole elements (Link routes) |
| Extra text such as CIDR blocks | A label slot of the group type |
| Light and dark backgrounds | The theme (`.mussel-dark`). CSS variables switch the line and name of AWS Cloud and the color of the arrows |

Not in the first version: elements on a frame that move with it (an Internet gateway on the top edge of a VPC), a fill behind an icon, switching icons for light and dark backgrounds, link names, numbered callouts, and wrapping that keeps AWS and Amazon on the same line as the next word.

### Group headers

A group type declares the form of its header.

| Form | Icon | Name |
|---|---|---|
| Top left | On the top left corner of the frame | Right of the icon |
| Top center | On the middle of the top edge | Centered under the icon |
| Name only | None | Top center or top left |

- The type declares the size of the icon, the position of the name (left and top margins) and its format
- The name in the header is the main label, drawn in the slot with the Header placement (Rendering)
- A group is hit by its frame and header (Rendering)

### Reading the official package

- The app reads the Icon package zip, which the user downloaded from the official page, with the icon import (Palette). The Example has a function that makes the ID, the name and the category from the relative path of a file
- The ID is the file name without the release date and the size, with the prefix `aws`. It stays the same when a new release (three a year) replaces the package

| File | ID | Size read |
|---|---|---|
| Service `Arch_<name>_<size>.svg` | `aws:Arch_<name>` (for example `aws:Arch_Amazon-EC2`) | The `64` folder (80 px) |
| Resource `Res_<service>_<resource>_48.svg` | `aws:Res_<service>_<resource>` (for example `aws:Res_Amazon-EC2_Instance`) | 48 px |
| Group `<name>_32.svg` | `aws:<name>` | 40 px |

- The category comes from the name of the category folder of services and resources
- Only SVG is read. Category icons and icons for dark backgrounds (`_Dark` and `Res_48_Dark`) are not read
- The types of icons removed in a release become unregistered types (Types and rules). Reporting removed or renamed icons is not in the first version

### Types in the Example

- Groups: the 20 groups and the 7 colors of custom groups in the official guide (Release 24-2026.07.31), as presets. Lines are 1.25 pt (about 1.67 px) without fill, and names are 12 pt (16 px) Arial, black (white on dark backgrounds). Availability Zone has two types, a tree group and an overlay group
- Icon nodes: a type for each icon, made from base types for services and resources (Palette)
- Arrows: three link types, with open arrows at one end, at both ends and at none. Lines are 1.25 pt and black (#8FA7C4 on dark backgrounds)
- Examples of nesting rules (a subnet only inside a VPC, and so on)

### Documentation and images

- The documentation explains how to download the official package and read it in the Example
- No official icons are in the repository, the Example or the images of the documentation

## 🌐 Languages

The code of the core, the components and JS knows no language. Texts are in language data (`DiagramTexts`), registered in `DiagramCultures` of the core. English and Japanese are built in and need no registration.

- Texts follow `CurrentUICulture`, looked up from the culture to its parents (`ja-JP`, `ja`, the empty name). Items not set and empty items do not count, and items found nowhere are English
- The built-in languages are English under the empty name and Japanese under `ja`. Japanese uses only the public API and is a model for other languages. The app adds languages in the same form, and can replace the built-in texts
- The registration is decided before the first diagram is created
- Issues and rejection reasons have a kind and arguments. Their texts are format strings filled with the arguments (display names of types, counts and so on). Reasons returned by app rules use the text of the app as it is
- The view sends the texts of the current language to JS as data (for rejection reasons while dragging)
- The display names of types and categories, and the default texts of label slots, are passed by the app in the schema. The app makes the values for each language

| Language data | Content |
|---|---|
| Rejection reasons | Connection rules (Types and rules), where an element can be placed (Palette), commits (Commit and validation) |
| Issues | The whole-diagram check (Commit and validation), what loading fixed and what sanitizing removed (Saving and export) |
| Commands | The display names of the built-in commands (Commands and keys) |
| Palette and type picker | The placeholder of the search box, the text when nothing matches |
| Export | The reasons an export failed |

## ♿ Accessibility

The first version has no screen reader support and no keyboard movement between elements.

- In the first version: the command keys (Commands and keys), the type picker to search and place by keys (Palette), the tooltips of elements, ports and badges (`<title>`), and labels as SVG text
- Planned: moving between and selecting elements with keys, ARIA and screen readers, a visible focus, high contrast and reduced motion
- Later, when needed: moving, connecting and creating elements with keys, and other keyboard and reading features

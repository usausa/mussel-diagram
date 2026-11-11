const SVG_NS = 'http://www.w3.org/2000/svg';
const LINE_HEIGHT = 1.2;
const ELLIPSIS = '…';
const NO_LINE_START = new Set([...'、。，．,.:;!?)]}」』）】〉》〕｝・ー…‥ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ']);
const LABEL_ATTRIBUTES = ['data-label', 'data-width', 'data-lines', 'data-valign', 'x'];

const segmenters = new Map();

function segmentsOf(text, granularity) {
    if ((typeof Intl === 'undefined') || (typeof Intl.Segmenter !== 'function')) {
        return granularity === 'word' ? text.split(/(\s+)/).filter((s) => s.length > 0) : [...text];
    }
    let segmenter = segmenters.get(granularity);
    if (!segmenter) {
        segmenter = new Intl.Segmenter(undefined, { granularity });
        segmenters.set(granularity, segmenter);
    }
    return [...segmenter.segment(text)].map((s) => s.segment);
}

function isSpace(text) {
    return /^\s+$/.test(text);
}

function wrapParagraph(text, width, measure, lines) {
    let line = '';
    const push = () => {
        lines.push(line.trimEnd());
        line = '';
    };
    for (const segment of segmentsOf(text, 'word')) {
        if ((line === '') && isSpace(segment)) {
            continue;
        }
        const candidate = line + segment;
        if (measure(candidate.trimEnd()) <= width) {
            line = candidate;
            continue;
        }
        if ((line !== '') && NO_LINE_START.has(segment[0])) {
            line = candidate;
            continue;
        }
        if (line !== '') {
            push();
            if (isSpace(segment)) {
                continue;
            }
        }
        if (measure(segment) <= width) {
            line = segment;
            continue;
        }
        for (const grapheme of segmentsOf(segment, 'grapheme')) {
            const next = line + grapheme;
            if ((line !== '') && (measure(next) > width) && !NO_LINE_START.has(grapheme)) {
                push();
                line = grapheme;
            } else {
                line = next;
            }
        }
    }
    push();
}

function fitWithEllipsis(line, width, measure) {
    let graphemes = segmentsOf(line, 'grapheme');
    while ((graphemes.length > 0) && (measure(graphemes.join('') + ELLIPSIS) > width)) {
        graphemes = graphemes.slice(0, -1);
    }
    return graphemes.join('').trimEnd() + ELLIPSIS;
}

export function wrapLabel(text, width, maxLines, measure) {
    const lines = [];
    for (const paragraph of String(text).split(/\r?\n/)) {
        wrapParagraph(paragraph, width, measure, lines);
    }
    if ((maxLines > 0) && (lines.length > maxLines)) {
        const kept = lines.slice(0, maxLines);
        kept[maxLines - 1] = fitWithEllipsis(kept[maxLines - 1], width, measure);
        return { lines: kept, truncated: true };
    }
    return { lines, truncated: false };
}

export function firstLineOffset(lineCount, verticalAlign) {
    if (verticalAlign === 'top') {
        return 0.8;
    }
    if (verticalAlign === 'bottom') {
        return -0.2 - ((lineCount - 1) * LINE_HEIGHT);
    }
    return 0.35 - (((lineCount - 1) * LINE_HEIGHT) / 2);
}

class LabelWriter {
    constructor(svg) {
        this.svg = svg;
        this.context = document.createElement('canvas').getContext('2d');
        this.pending = new Set();
        this.observer = new MutationObserver((records) => this.observe(records));
        this.observer.observe(svg, { subtree: true, childList: true, attributes: true, attributeFilter: LABEL_ATTRIBUTES });
        this.refresh = () => this.writeAll(true);
        document.fonts?.addEventListener('loadingdone', this.refresh);
        this.writeAll(false);
    }

    observe(records) {
        for (const record of records) {
            if (record.type === 'attributes') {
                if (record.target.classList?.contains('mussel-diagram-label')) {
                    this.pending.add(record.target);
                }
                continue;
            }
            for (const node of record.addedNodes) {
                if (node.nodeType !== Node.ELEMENT_NODE) {
                    continue;
                }
                if (node.classList.contains('mussel-diagram-label')) {
                    this.pending.add(node);
                }
                for (const label of node.querySelectorAll('.mussel-diagram-label')) {
                    this.pending.add(label);
                }
            }
        }
        for (const label of this.pending) {
            this.write(label, false);
        }
        this.pending.clear();
    }

    writeAll(force) {
        for (const label of this.svg.querySelectorAll('.mussel-diagram-label')) {
            this.write(label, force);
        }
    }

    write(label, force) {
        const key = LABEL_ATTRIBUTES.map((name) => label.getAttribute(name)).join('\u0001');
        if (!force && (label.musselLabelKey === key) && label.hasChildNodes()) {
            return;
        }
        label.musselLabelKey = key;
        const style = getComputedStyle(label);
        const font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
        const measure = (text) => {
            this.context.font = font;
            return this.context.measureText(text).width;
        };
        const text = label.getAttribute('data-label') ?? '';
        const width = Number(label.getAttribute('data-width')) || Number.POSITIVE_INFINITY;
        const maxLines = Number(label.getAttribute('data-lines')) || 0;
        const x = label.getAttribute('x') ?? '0';
        const { lines, truncated } = wrapLabel(text, width, maxLines, measure);
        const first = firstLineOffset(lines.length, label.getAttribute('data-valign'));
        const nodes = lines.map((line, i) => {
            const span = document.createElementNS(SVG_NS, 'tspan');
            span.setAttribute('x', x);
            span.setAttribute('dy', `${i === 0 ? first : LINE_HEIGHT}em`);
            span.textContent = line;
            return span;
        });
        if (truncated) {
            const title = document.createElementNS(SVG_NS, 'title');
            title.textContent = text;
            nodes.unshift(title);
        }
        label.replaceChildren(...nodes);
    }

    dispose() {
        this.observer.disconnect();
        document.fonts?.removeEventListener('loadingdone', this.refresh);
    }
}

const SETTLE_DELAY = 180;
const WHEEL_ZOOM_RATE = 0.0015;
const MOUSE_THRESHOLD = 4;
const TOUCH_THRESHOLD = 8;
const REASON_DURATION = 3000;
const SELECTION_PADDING = 4;
const ELEMENT_SELECTOR = '.mussel-diagram-node, .mussel-diagram-group, .mussel-diagram-link';
const NO_DRAG_SELECTOR = 'input, textarea, select, button, [contenteditable], .mussel-diagram-nodrag';

export function zoomAround(view, factor, point) {
    return {
        zoom: view.zoom * factor,
        x: point.x - ((point.x - view.x) * factor),
        y: point.y - ((point.y - view.y) * factor),
    };
}

export function limitFactor(zoom, factor, minZoom, maxZoom) {
    const target = Math.min(maxZoom, Math.max(minZoom, zoom * factor));
    return target / zoom;
}

export function fitView(bounds, width, height, margin, minZoom, maxZoom) {
    if ((bounds.width <= 0) || (bounds.height <= 0) || (width <= 0) || (height <= 0)) {
        return { zoom: 1, x: margin - bounds.x, y: margin - bounds.y };
    }
    const fit = Math.min((width - (margin * 2)) / bounds.width, (height - (margin * 2)) / bounds.height);
    const zoom = Math.min(maxZoom, Math.max(minZoom, Math.floor(fit * 100) / 100));
    return {
        zoom,
        x: ((width - (bounds.width * zoom)) / 2) - (bounds.x * zoom),
        y: ((height - (bounds.height * zoom)) / 2) - (bounds.y * zoom),
    };
}

export function roundZoom(view, point) {
    const zoom = Math.round(view.zoom * 100) / 100;
    return zoomAround(view, zoom / view.zoom, point);
}

export function wheelDelta(event, pageHeight) {
    const scale = event.deltaMode === 1 ? 16 : (event.deltaMode === 2 ? pageHeight : 1);
    return { x: event.deltaX * scale, y: event.deltaY * scale };
}

export function portPosition(box, side, ratio, offset) {
    switch (side) {
        case 'top':
            return { x: box.x + (box.w * ratio), y: box.y - offset };
        case 'right':
            return { x: box.x + box.w + offset, y: box.y + (box.h * ratio) };
        case 'bottom':
            return { x: box.x + (box.w * ratio), y: box.y + box.h + offset };
        default:
            return { x: box.x - offset, y: box.y + (box.h * ratio) };
    }
}

function centerOf(box) {
    return { x: box.x + (box.w / 2), y: box.y + (box.h / 2) };
}

export function anchorOf(box, port, toward) {
    if (port) {
        return { ...portPosition(box, port.side, port.ratio, port.offset), side: port.side };
    }
    const center = centerOf(box);
    const dx = toward.x - center.x;
    const dy = toward.y - center.y;
    const side = (Math.abs(dx) * box.h) >= (Math.abs(dy) * box.w)
        ? (dx >= 0 ? 'right' : 'left')
        : (dy >= 0 ? 'bottom' : 'top');
    return { ...portPosition(box, side, 0.5, 0), side };
}

function isHorizontal(side) {
    return (side === 'left') || (side === 'right');
}

function stepOut(point, side, length) {
    switch (side) {
        case 'top':
            return { x: point.x, y: point.y - length };
        case 'right':
            return { x: point.x + length, y: point.y };
        case 'bottom':
            return { x: point.x, y: point.y + length };
        default:
            return { x: point.x - length, y: point.y };
    }
}

function simplify(points) {
    const result = [];
    for (const point of points) {
        const last = result[result.length - 1];
        if (last && (last.x === point.x) && (last.y === point.y)) {
            continue;
        }
        if (result.length >= 2) {
            const before = result[result.length - 2];
            if (((before.x === last.x) && (last.x === point.x)) || ((before.y === last.y) && (last.y === point.y))) {
                result[result.length - 1] = point;
                continue;
            }
        }
        result.push(point);
    }
    return result;
}

export function orthogonalRoute(start, end, stub) {
    const startOut = stepOut(start, start.side, stub);
    const endOut = stepOut(end, end.side, stub);
    const points = [{ x: start.x, y: start.y }, startOut];
    if (isHorizontal(start.side) && isHorizontal(end.side)) {
        const middle = (startOut.x + endOut.x) / 2;
        points.push({ x: middle, y: startOut.y }, { x: middle, y: endOut.y });
    } else if (!isHorizontal(start.side) && !isHorizontal(end.side)) {
        const middle = (startOut.y + endOut.y) / 2;
        points.push({ x: startOut.x, y: middle }, { x: endOut.x, y: middle });
    } else if (isHorizontal(start.side)) {
        points.push({ x: endOut.x, y: startOut.y });
    } else {
        points.push({ x: startOut.x, y: endOut.y });
    }
    points.push(endOut, { x: end.x, y: end.y });
    return simplify(points);
}

function findPort(box, id) {
    return id ? (box.ports ?? []).find((p) => p.id === id) : undefined;
}

export function routeLink(link, source, target) {
    const sourcePort = findPort(source, link.sourcePort);
    const targetPort = findPort(target, link.targetPort);
    const sourceToward = targetPort ? portPosition(target, targetPort.side, targetPort.ratio, targetPort.offset) : centerOf(target);
    const targetToward = sourcePort ? portPosition(source, sourcePort.side, sourcePort.ratio, sourcePort.offset) : centerOf(source);
    const start = anchorOf(source, sourcePort, sourceToward);
    const end = anchorOf(target, targetPort, targetToward);
    return link.straight
        ? [{ x: start.x, y: start.y }, { x: end.x, y: end.y }]
        : orthogonalRoute(start, end, link.stub);
}

function formatNumber(value) {
    return String(Math.round(value * 100) / 100);
}

export function toPath(points) {
    return points.map((p, i) => `${i === 0 ? 'M' : 'L'}${formatNumber(p.x)},${formatNumber(p.y)}`).join('');
}

function pushTo(map, key, value) {
    const list = map.get(key);
    if (list) {
        list.push(value);
    } else {
        map.set(key, [value]);
    }
}

export class DiagramTable {
    constructor(data) {
        this.set(data);
    }

    set(data) {
        this.version = data.version;
        this.boxes = new Map(data.boxes.map((b) => [b.id, b]));
        this.links = new Map(data.links.map((l) => [l.id, l]));
        this.index();
    }

    apply(changes) {
        this.version = Math.max(this.version, changes.version);
        for (const id of changes.removed) {
            this.boxes.delete(id);
            this.links.delete(id);
        }
        for (const box of changes.boxes) {
            this.boxes.set(box.id, box);
        }
        for (const link of changes.links) {
            this.links.set(link.id, link);
        }
        this.index();
    }

    index() {
        this.children = new Map();
        this.linksByBox = new Map();
        for (const box of this.boxes.values()) {
            if (box.parent) {
                pushTo(this.children, box.parent, box.id);
            }
        }
        for (const link of this.links.values()) {
            pushTo(this.linksByBox, link.source, link);
            if (link.target !== link.source) {
                pushTo(this.linksByBox, link.target, link);
            }
        }
    }

    descendants(id) {
        const result = [];
        const stack = [id];
        while (stack.length > 0) {
            for (const child of this.children.get(stack.pop()) ?? []) {
                result.push(child);
                stack.push(child);
            }
        }
        return result;
    }

    ancestors(id) {
        const result = [];
        let current = this.boxes.get(id)?.parent;
        while (current && (result.length <= this.boxes.size)) {
            result.push(current);
            current = this.boxes.get(current)?.parent;
        }
        return result;
    }

    movingSet(roots) {
        const result = new Set();
        for (const id of roots) {
            const box = this.boxes.get(id);
            const starts = box?.kind === 'overlay' ? (box.members ?? []) : [id];
            for (const start of starts) {
                result.add(start);
                for (const descendant of this.descendants(start)) {
                    result.add(descendant);
                }
            }
        }
        return result;
    }

    linksOf(ids) {
        const result = new Set();
        for (const id of ids) {
            for (const link of this.linksByBox.get(id) ?? []) {
                result.add(link);
            }
        }
        return result;
    }
}

class DiagramController {
    constructor(root, dotnet, options, table) {
        this.root = root;
        this.dotnet = dotnet;
        this.options = options;
        this.table = new DiagramTable(table);
        this.stage = root.querySelector('.mussel-diagram-stage');
        this.canvas = root.querySelector('.mussel-diagram-canvas');
        this.viewports = [...root.querySelectorAll('.mussel-diagram-viewport')];
        this.overlay = root.querySelector('.mussel-diagram-overlay > .mussel-diagram-viewport');
        this.view = { x: 0, y: 0, zoom: 1 };
        this.gesture = null;
        this.pan = null;
        this.press = null;
        this.drag = null;
        this.selection = new Set();
        this.marked = [];
        this.commits = Promise.resolve();
        this.pending = [];
        this.lastVersion = this.table.version;
        this.rejections = 0;
        this.waiting = false;
        this.reason = null;
        this.spaceDown = false;
        this.settleTimer = 0;
        this.frameRequest = 0;
        this.size = { width: root.clientWidth, height: root.clientHeight };
        this.labels = new LabelWriter(this.canvas);
        this.listeners = [];
        this.listen(root, 'wheel', (e) => this.onWheel(e), { passive: false });
        this.listen(root, 'pointerdown', (e) => this.onPointerDown(e));
        this.listen(root, 'pointermove', (e) => this.onPointerMove(e));
        this.listen(root, 'pointerup', (e) => this.onPointerUp(e));
        this.listen(root, 'pointercancel', (e) => this.onPointerCancel(e));
        this.listen(root, 'lostpointercapture', (e) => this.onPointerCancel(e));
        this.listen(root, 'keydown', (e) => this.onKeyDown(e));
        this.listen(root, 'keyup', (e) => this.onKeyUp(e));
        this.listen(root, 'blur', () => this.setSpace(false));
        this.versionObserver = new MutationObserver(() => this.settlePending());
        this.versionObserver.observe(root, { attributes: true, attributeFilter: ['data-version'] });
        this.resizeObserver = new ResizeObserver(() => this.onResize());
        this.resizeObserver.observe(root);
        this.applyView();
        root.dataset.attached = '';
        this.notifyViewport();
    }

    listen(target, type, handler, options) {
        target.addEventListener(type, handler, options);
        this.listeners.push(() => target.removeEventListener(type, handler, options));
    }

    setOptions(options) {
        this.options = options;
    }

    setTable(table) {
        this.table.set(table);
        this.pruneSelection();
    }

    applyChanges(changes) {
        this.table.apply(changes);
        this.pruneSelection();
    }

    point(event) {
        const rect = this.root.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    element(id) {
        return this.canvas.querySelector(`[data-id="${CSS.escape(id)}"]`);
    }

    applyView() {
        const { x, y, zoom } = this.view;
        const transform = `translate(${x},${y}) scale(${zoom})`;
        for (const viewport of this.viewports) {
            viewport.setAttribute('transform', transform);
        }
        this.drawSelection();
    }

    notifyViewport() {
        const { x, y, zoom } = this.view;
        this.dotnet?.invokeMethodAsync('OnViewportChanged', zoom, x, y, this.size.width, this.size.height).catch(() => {});
    }

    setView(zoom, centerX, centerY) {
        this.foldGesture();
        const limited = Math.min(this.options.maxZoom, Math.max(this.options.minZoom, zoom));
        this.view = { zoom: limited, x: (this.size.width / 2) - (centerX * limited), y: (this.size.height / 2) - (centerY * limited) };
        this.applyView();
        this.notifyViewport();
    }

    fit(bounds, margin) {
        this.foldGesture();
        this.view = fitView(bounds, this.size.width, this.size.height, margin, this.options.minZoom, this.options.maxZoom);
        this.applyView();
        this.notifyViewport();
    }

    onWheel(event) {
        if (event.target.closest?.('.mussel-diagram-nowheel')) {
            return;
        }
        event.preventDefault();
        const delta = wheelDelta(event, this.size.height);
        const zooming = event.ctrlKey || event.altKey || ((this.options.wheelMode === 'zoom') && !event.shiftKey);
        if (zooming) {
            if (this.options.zoomEnabled) {
                this.zoomBy(Math.exp(-delta.y * WHEEL_ZOOM_RATE), this.point(event));
            }
        } else if (this.options.panEnabled) {
            this.foldGesture();
            const dx = event.shiftKey && (delta.x === 0) ? delta.y : delta.x;
            const dy = event.shiftKey && (delta.x === 0) ? 0 : delta.y;
            this.view = { ...this.view, x: this.view.x - dx, y: this.view.y - dy };
            this.applyView();
        }
        this.scheduleSettle();
    }

    zoomBy(factor, point) {
        const gesture = this.gesture ?? { k: 1, x: 0, y: 0 };
        const allowed = limitFactor(this.view.zoom * gesture.k, factor, this.options.minZoom, this.options.maxZoom);
        this.gesture = {
            k: gesture.k * allowed,
            x: point.x - ((point.x - gesture.x) * allowed),
            y: point.y - ((point.y - gesture.y) * allowed),
            point,
        };
        this.stage.style.transform = `matrix(${this.gesture.k},0,0,${this.gesture.k},${this.gesture.x},${this.gesture.y})`;
    }

    foldGesture() {
        if (!this.gesture) {
            return;
        }
        const { k, x, y, point } = this.gesture;
        this.gesture = null;
        this.view = roundZoom({ zoom: this.view.zoom * k, x: (this.view.x * k) + x, y: (this.view.y * k) + y }, point);
        this.stage.style.transform = '';
        this.applyView();
    }

    scheduleSettle() {
        clearTimeout(this.settleTimer);
        this.settleTimer = setTimeout(() => this.settle(), SETTLE_DELAY);
    }

    settle() {
        clearTimeout(this.settleTimer);
        this.foldGesture();
        this.notifyViewport();
    }

    hit(event) {
        if (!this.canvas.contains(event.target)) {
            return null;
        }
        const target = event.target.closest(ELEMENT_SELECTOR);
        if (!target || !this.canvas.contains(target)) {
            return null;
        }
        const id = target.getAttribute('data-id');
        if (this.table.boxes.has(id)) {
            return { id, box: true };
        }
        return this.table.links.has(id) ? { id, box: false } : null;
    }

    onPointerDown(event) {
        this.hideReason();
        this.root.focus({ preventScroll: true });
        const panButton = (event.button === 1) || ((event.button === 0) && this.spaceDown);
        if (panButton) {
            if (this.options.panEnabled) {
                event.preventDefault();
                this.foldGesture();
                this.root.setPointerCapture(event.pointerId);
                this.pan = { pointerId: event.pointerId, x: event.clientX, y: event.clientY };
                this.root.classList.add('mussel-diagram-panning');
            }
            return;
        }
        if ((event.button !== 0) || this.press || this.drag || event.target.closest?.(NO_DRAG_SELECTOR)) {
            return;
        }
        this.root.setPointerCapture(event.pointerId);
        this.press = {
            pointerId: event.pointerId,
            hit: this.hit(event),
            x: event.clientX,
            y: event.clientY,
            toggle: event.shiftKey || event.ctrlKey || event.metaKey,
            threshold: event.pointerType === 'touch' ? TOUCH_THRESHOLD : MOUSE_THRESHOLD,
        };
    }

    onPointerMove(event) {
        if (this.pan?.pointerId === event.pointerId) {
            this.view = { ...this.view, x: this.view.x + (event.clientX - this.pan.x), y: this.view.y + (event.clientY - this.pan.y) };
            this.pan.x = event.clientX;
            this.pan.y = event.clientY;
            this.applyView();
            return;
        }
        if (this.press?.pointerId !== event.pointerId) {
            return;
        }
        if (!this.drag) {
            const distance = Math.hypot(event.clientX - this.press.x, event.clientY - this.press.y);
            if ((distance < this.press.threshold) || !this.startDrag(this.press)) {
                return;
            }
        }
        this.drag.dx = (event.clientX - this.press.x) / this.view.zoom;
        this.drag.dy = (event.clientY - this.press.y) / this.view.zoom;
        this.drag.point = this.point(event);
        if (!this.frameRequest) {
            this.frameRequest = requestAnimationFrame(() => {
                this.frameRequest = 0;
                this.renderDrag();
            });
        }
    }

    onPointerUp(event) {
        if (this.pan?.pointerId === event.pointerId) {
            this.pan = null;
            this.root.classList.remove('mussel-diagram-panning');
            this.settle();
            return;
        }
        if (this.press?.pointerId !== event.pointerId) {
            return;
        }
        const press = this.press;
        this.press = null;
        if (this.drag) {
            this.finishDrag();
        } else {
            this.click(press);
        }
    }

    onPointerCancel(event) {
        if (this.pan?.pointerId === event.pointerId) {
            this.pan = null;
            this.root.classList.remove('mussel-diagram-panning');
            this.settle();
        }
        if (this.press?.pointerId === event.pointerId) {
            this.press = null;
            this.cancelDrag();
        }
    }

    click(press) {
        if (!this.options.selectionEnabled) {
            return;
        }
        const hit = press.hit;
        const selectable = hit && (hit.box ? this.table.boxes.get(hit.id)?.selectable : this.table.links.get(hit.id)?.selectable);
        if (!hit || !selectable) {
            if (!press.toggle) {
                this.setSelection([]);
            }
            return;
        }
        if (press.toggle) {
            const next = new Set(this.selection);
            if (next.has(hit.id)) {
                next.delete(hit.id);
            } else {
                next.add(hit.id);
            }
            this.setSelection([...next]);
        } else {
            this.setSelection([hit.id]);
        }
    }

    select(ids) {
        this.selection = new Set(ids.filter((id) => this.table.boxes.has(id) || this.table.links.has(id)));
        this.drawSelection();
    }

    setSelection(ids) {
        const next = new Set(ids);
        if ((next.size === this.selection.size) && [...next].every((id) => this.selection.has(id))) {
            return;
        }
        this.selection = next;
        this.drawSelection();
        this.dotnet?.invokeMethodAsync('OnSelectionChanged', [...next]).catch(() => {});
    }

    pruneSelection() {
        const kept = [...this.selection].filter((id) => this.table.boxes.has(id) || this.table.links.has(id));
        if (kept.length !== this.selection.size) {
            this.selection = new Set(kept);
        }
        this.drawSelection();
    }

    markSelection() {
        for (const element of this.marked) {
            element.removeAttribute('data-selected');
        }
        this.marked = [];
        for (const id of this.selection) {
            const element = this.element(id);
            if (element) {
                element.setAttribute('data-selected', '');
                this.marked.push(element);
            }
        }
    }

    drawSelection() {
        if (!this.overlay) {
            return;
        }
        if (!this.drag) {
            this.markSelection();
        }
        const pad = SELECTION_PADDING / this.view.zoom;
        const frames = [];
        for (const id of this.selection) {
            const box = this.table.boxes.get(id);
            if (!box || (box.kind === 'overlay')) {
                continue;
            }
            const moved = this.drag?.moving.has(id) && this.drag.live;
            const rect = document.createElementNS(SVG_NS, 'rect');
            rect.setAttribute('class', 'mussel-diagram-selection');
            rect.setAttribute('x', String((box.x + (moved ? this.drag.dx : 0)) - pad));
            rect.setAttribute('y', String((box.y + (moved ? this.drag.dy : 0)) - pad));
            rect.setAttribute('width', String(box.w + (pad * 2)));
            rect.setAttribute('height', String(box.h + (pad * 2)));
            frames.push(rect);
        }
        if (this.drag && !this.drag.live) {
            const { bounds } = this.drag;
            const frame = document.createElementNS(SVG_NS, 'rect');
            frame.setAttribute('class', 'mussel-diagram-drag-frame');
            frame.setAttribute('x', String(bounds.x + this.drag.dx));
            frame.setAttribute('y', String(bounds.y + this.drag.dy));
            frame.setAttribute('width', String(bounds.w));
            frame.setAttribute('height', String(bounds.h));
            frames.push(frame);
        }
        this.overlay.replaceChildren(...frames);
    }

    startDrag(press) {
        if (!press.hit?.box || !this.options.moveEnabled || this.waiting) {
            return false;
        }
        if (!this.selection.has(press.hit.id)) {
            this.setSelection([press.hit.id]);
        }
        const roots = [...this.selection]
            .map((id) => this.table.boxes.get(id))
            .filter((box) => box && box.movable && !this.table.ancestors(box.id).some((a) => this.selection.has(a)))
            .map((box) => box.id);
        if (roots.length === 0) {
            return false;
        }
        const moving = this.table.movingSet(roots);
        const live = moving.size <= this.options.livePreviewLimit;
        const links = this.table.linksOf(moving);
        const boxes = [...moving].map((id) => this.table.boxes.get(id)).filter((b) => b && (b.kind !== 'overlay'));
        const left = Math.min(...boxes.map((b) => b.x));
        const top = Math.min(...boxes.map((b) => b.y));
        const bounds = { x: left, y: top, w: Math.max(...boxes.map((b) => b.x + b.w)) - left, h: Math.max(...boxes.map((b) => b.y + b.h)) - top };
        this.drag = { roots, moving, live, links, bounds, dx: 0, dy: 0, point: null, saved: new Map(), base: this.table.version, queued: this.pending.length > 0, elements: new Map(), paths: new Map() };
        for (const id of moving) {
            const element = this.element(id);
            if (element) {
                this.drag.elements.set(id, element);
                this.save(element, 'transform');
            }
        }
        for (const link of links) {
            const paths = [...(this.element(link.id)?.querySelectorAll('path') ?? [])];
            this.drag.paths.set(link.id, paths);
            for (const path of paths) {
                this.save(path, 'd');
            }
        }
        this.root.classList.add('mussel-diagram-dragging');
        return true;
    }

    save(element, attribute) {
        if (element && !this.drag.saved.has(element)) {
            this.drag.saved.set(element, { attribute, value: element.getAttribute(attribute) });
        }
    }

    renderDrag() {
        const drag = this.drag;
        if (!drag) {
            return;
        }
        if (drag.live) {
            this.placeMoved(drag);
        }
        this.drawSelection();
    }

    placeMoved(drag) {
        const moved = (id) => {
            const box = this.table.boxes.get(id);
            return drag.moving.has(id) ? { ...box, x: box.x + drag.dx, y: box.y + drag.dy } : box;
        };
        for (const [id, element] of drag.elements) {
            const box = this.table.boxes.get(id);
            if (box && (box.kind !== 'overlay')) {
                element.setAttribute('transform', `translate(${formatNumber(box.x + drag.dx)},${formatNumber(box.y + drag.dy)})`);
            }
        }
        for (const link of drag.links) {
            const source = moved(link.source);
            const target = moved(link.target);
            const paths = drag.paths.get(link.id) ?? [];
            if (!source || !target || (paths.length === 0)) {
                continue;
            }
            const path = toPath(routeLink(link, source, target));
            for (const line of paths) {
                line.setAttribute('d', path);
            }
        }
    }

    finishDrag() {
        const drag = this.drag;
        if (!drag) {
            return;
        }
        cancelAnimationFrame(this.frameRequest);
        this.frameRequest = 0;
        this.root.classList.remove('mussel-diagram-dragging');
        if ((drag.dx === 0) && (drag.dy === 0)) {
            this.drag = null;
            this.drawSelection();
            return;
        }
        this.placeMoved(drag);
        const previous = new Map();
        for (const id of drag.moving) {
            const box = this.table.boxes.get(id);
            if (box) {
                previous.set(id, box);
                this.table.boxes.set(id, { ...box, x: box.x + drag.dx, y: box.y + drag.dy });
            }
        }
        this.drag = null;
        this.drawSelection();
        const entry = { saved: drag.saved, previous, version: null, elements: [...drag.saved.keys()] };
        this.pending.push(entry);
        if (!this.options.optimistic) {
            this.waiting = true;
            for (const element of entry.elements) {
                element.setAttribute('data-pending', '');
            }
        }
        this.commits = this.commits.then(() => this.sendMove(entry, drag));
    }

    async sendMove(entry, drag) {
        if (!this.pending.includes(entry)) {
            return;
        }
        let result = null;
        try {
            const base = drag.queued ? Math.max(drag.base, this.lastVersion) : drag.base;
            result = await this.dotnet.invokeMethodAsync('CommitMoveAsync', drag.roots, drag.dx, drag.dy, base, this.rejections);
        } catch {
            result = null;
        }
        for (const element of entry.elements) {
            element.removeAttribute('data-pending');
        }
        this.waiting = false;
        if (result) {
            this.rejections = result.rejections;
        }
        if (result?.accepted) {
            entry.version = result.version;
            this.lastVersion = Math.max(this.lastVersion, result.version);
            this.settlePending();
            return;
        }
        this.revertFrom(entry);
        if (result?.reason) {
            this.showReason(result.reason, drag.point);
        }
    }

    revertFrom(entry) {
        const index = this.pending.indexOf(entry);
        if (index < 0) {
            return;
        }
        const reverted = this.pending.splice(index);
        for (const item of reverted.reverse()) {
            for (const [element, saved] of item.saved) {
                if (saved.value === null) {
                    element.removeAttribute(saved.attribute);
                } else {
                    element.setAttribute(saved.attribute, saved.value);
                }
            }
            for (const [id, box] of item.previous) {
                if (this.table.boxes.has(id)) {
                    this.table.boxes.set(id, box);
                }
            }
        }
        this.drawSelection();
    }

    settlePending() {
        const rendered = Number(this.root.dataset.version);
        this.pending = this.pending.filter((entry) => (entry.version === null) || (rendered < entry.version));
    }

    cancelDrag() {
        const drag = this.drag;
        if (!drag) {
            return;
        }
        cancelAnimationFrame(this.frameRequest);
        this.frameRequest = 0;
        for (const [element, saved] of drag.saved) {
            if (saved.value === null) {
                element.removeAttribute(saved.attribute);
            } else {
                element.setAttribute(saved.attribute, saved.value);
            }
        }
        this.drag = null;
        this.root.classList.remove('mussel-diagram-dragging');
        this.drawSelection();
    }

    showReason(text, point) {
        this.hideReason();
        const reason = document.createElement('div');
        reason.className = 'mussel-diagram-reason';
        reason.textContent = text;
        const at = point ?? { x: this.size.width / 2, y: this.size.height / 2 };
        reason.style.left = `${at.x}px`;
        reason.style.top = `${at.y}px`;
        this.root.append(reason);
        this.reason = { element: reason, timer: setTimeout(() => this.hideReason(), REASON_DURATION) };
    }

    hideReason() {
        if (!this.reason) {
            return;
        }
        clearTimeout(this.reason.timer);
        this.reason.element.remove();
        this.reason = null;
    }

    setSpace(down) {
        this.spaceDown = down;
        this.root.classList.toggle('mussel-diagram-space', down);
    }

    onKeyDown(event) {
        if ((event.code === 'Space') && !event.repeat && this.options.panEnabled) {
            event.preventDefault();
            this.setSpace(true);
            return;
        }
        if (event.key === 'Escape') {
            if (this.drag) {
                this.press = null;
                this.cancelDrag();
            } else if (this.options.selectionEnabled) {
                this.setSelection([]);
            }
        }
    }

    onKeyUp(event) {
        if (event.code === 'Space') {
            this.setSpace(false);
        }
    }

    onResize() {
        const width = this.root.clientWidth;
        const height = this.root.clientHeight;
        if ((width === this.size.width) && (height === this.size.height)) {
            return;
        }
        this.view = { ...this.view, x: this.view.x + ((width - this.size.width) / 2), y: this.view.y + ((height - this.size.height) / 2) };
        this.size = { width, height };
        this.applyView();
        this.notifyViewport();
    }

    dispose() {
        clearTimeout(this.settleTimer);
        cancelAnimationFrame(this.frameRequest);
        this.hideReason();
        this.versionObserver.disconnect();
        this.resizeObserver.disconnect();
        for (const remove of this.listeners) {
            remove();
        }
        this.labels.dispose();
        this.stage.style.transform = '';
        delete this.root.dataset.attached;
    }
}

export function attach(root, dotnet, options, table) {
    return new DiagramController(root, dotnet, options, table);
}

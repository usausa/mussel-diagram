const SVG_NS = 'http://www.w3.org/2000/svg';
const LINE_HEIGHT = 1.2;
const ELLIPSIS = '…';
const NO_LINE_START = new Set([...'、。，．,.:;!?)]}」』）】〉》〕｝・ー…‥！？：；］”’ぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ']);
const LABEL_ATTRIBUTES = ['data-label', 'data-width', 'data-lines', 'data-valign', 'data-stack', 'style', 'x'];

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

export function stackOffsets(labels) {
    const heights = labels.map((label) => label.lines * LINE_HEIGHT * label.size);
    const total = heights.reduce((sum, h) => sum + h, 0);
    let before = 0;
    return labels.map((label, i) => {
        const after = total - before - heights[i];
        const dy = label.valign === 'top'
            ? before + (0.8 * label.size)
            : (label.valign === 'bottom' ? -after - (0.2 * label.size) - ((label.lines - 1) * LINE_HEIGHT * label.size) : (-total / 2) + before + (0.95 * label.size));
        before += heights[i];
        return dy;
    });
}

class LabelWriter {
    constructor(svg) {
        this.svg = svg;
        this.tooltips = true;
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
        if (truncated && this.tooltips && !label.parentElement?.querySelector(':scope > title')) {
            const title = document.createElementNS(SVG_NS, 'title');
            title.textContent = text;
            nodes.unshift(title);
        }
        label.replaceChildren(...nodes);
        label.musselLines = lines.length;
        label.musselFontSize = parseFloat(style.fontSize) || 12;
        if (label.hasAttribute('data-stack')) {
            this.stack(label);
        }
    }

    stack(label) {
        const name = label.getAttribute('data-stack');
        const members = [...(label.parentElement?.children ?? [])].filter((e) => e.classList?.contains('mussel-diagram-label') && (e.getAttribute('data-stack') === name));
        if (members.some((m) => m.musselLines === undefined)) {
            return;
        }
        const offsets = stackOffsets(members.map((m) => ({ lines: m.musselLines, size: m.musselFontSize, valign: m.getAttribute('data-valign') })));
        members.forEach((member, i) => member.querySelector(':scope > tspan')?.setAttribute('dy', `${formatNumber(offsets[i])}px`));
    }

    dispose() {
        this.observer.disconnect();
        document.fonts?.removeEventListener('loadingdone', this.refresh);
    }
}

const BADGE_SIZE = 16;
const BADGE_GAP = 4;

class BadgeWriter {
    constructor(layer) {
        this.layer = layer;
        this.context = document.createElement('canvas').getContext('2d');
        this.observer = new MutationObserver((records) => this.observe(records));
        this.observer.observe(layer, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ['data-w', 'data-h'] });
        this.refresh = () => this.writeAll();
        document.fonts?.addEventListener('loadingdone', this.refresh);
        this.writeAll();
    }

    observe(records) {
        const sets = new Set();
        for (const record of records) {
            const target = record.target.nodeType === Node.ELEMENT_NODE ? record.target : record.target.parentElement;
            const set = target?.closest?.('.mussel-diagram-badge-set');
            if (set) {
                sets.add(set);
            }
            for (const node of record.addedNodes) {
                if ((node.nodeType === Node.ELEMENT_NODE) && node.classList.contains('mussel-diagram-badge-set')) {
                    sets.add(node);
                }
            }
        }
        for (const set of sets) {
            if (set.isConnected) {
                this.write(set);
            }
        }
    }

    writeAll() {
        for (const set of this.layer.querySelectorAll(':scope > .mussel-diagram-badge-set')) {
            this.write(set);
        }
    }

    write(set) {
        const w = Number(set.getAttribute('data-w')) || 0;
        const h = Number(set.getAttribute('data-h')) || 0;
        const link = set.hasAttribute('data-link');
        const corners = new Map();
        for (const badge of set.querySelectorAll(':scope > .mussel-diagram-badge')) {
            const corner = link ? 'link' : (badge.getAttribute('data-corner') ?? 'top-right');
            let list = corners.get(corner);
            if (!list) {
                list = [];
                corners.set(corner, list);
            }
            list.push({ badge, width: this.size(badge) });
        }
        for (const [corner, list] of corners) {
            const total = list.reduce((sum, item) => sum + item.width, 0) + (BADGE_GAP * (list.length - 1));
            const right = corner.endsWith('right');
            const y = corner.startsWith('bottom') ? h : 0;
            let x = corner === 'link' ? -total / 2 : (right ? w + (BADGE_SIZE / 2) : -(BADGE_SIZE / 2));
            for (const item of list) {
                const left = right ? x - item.width : x;
                item.badge.setAttribute('transform', `translate(${formatNumber(left)},${formatNumber(y)})`);
                x = right ? left - BADGE_GAP : left + item.width + BADGE_GAP;
            }
        }
    }

    size(badge) {
        const text = badge.querySelector(':scope > .mussel-diagram-badge-text');
        let width = BADGE_SIZE;
        if (text) {
            const style = getComputedStyle(text);
            this.context.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
            const start = Number(text.getAttribute('x')) || 4;
            width = Math.max(BADGE_SIZE, Math.ceil(start + this.context.measureText(text.textContent ?? '').width + 4));
        }
        badge.querySelector(':scope > .mussel-diagram-badge-frame')?.setAttribute('width', String(width));
        return width;
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
const LONG_PRESS_DELAY = 250;
const REASON_DURATION = 3000;
const SELECTION_PADDING = 4;
const HANDLE_SIZE = 8;
const PORT_RADIUS = 4;
const ARROW_DELAY = 140;
const ARROW_OFFSET = 16;
const ARROW_SIZE = 7;
const EDGE_ZONE = 40;
const EDGE_SPEED = 16;
const GRID_MINIMUM = 8;
const MIN_SIZE = 10;
const GUIDE_EPSILON = 0.01;
const ELEMENT_SELECTOR = '.mussel-diagram-node, .mussel-diagram-group, .mussel-diagram-link';
const NO_DRAG_SELECTOR = 'input, textarea, select, button, [contenteditable], .mussel-diagram-nodrag';
const HANDLES = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w'];
const SIDES = ['top', 'right', 'bottom', 'left'];
const DEFAULT_LINK_TYPE = { allowSelf: false, allowDuplicate: false, allowCrossParent: true, allowCycles: true, form: 'orthogonal', stub: 20, sourceCut: 0, targetCut: 10 };
const TYPING_KEYS = new Set(['Ctrl+Z', 'Ctrl+Y', 'Ctrl+Shift+Z', 'Ctrl+A', 'Ctrl+C', 'Ctrl+X', 'Ctrl+V']);
const ZOOM_STEP = 1.2;
const controllers = new WeakMap();

export function keyName(event, mac) {
    let key = event.key;
    if (!key || ['Control', 'Shift', 'Alt', 'Meta', 'Unidentified', 'Process', 'Dead'].includes(key)) {
        return null;
    }
    const ctrl = mac ? event.metaKey : event.ctrlKey;
    if ((key === '+') || (ctrl && (key === '='))) {
        key = 'Plus';
    } else if ((key === '-') || (ctrl && (key === '_'))) {
        key = 'Minus';
    } else if (key === ' ') {
        key = 'Space';
    } else if (key.length === 1) {
        if (event.altKey && /^Key[A-Z]$/.test(event.code ?? '')) {
            key = event.code.slice(3);
        } else if (event.altKey && /^Digit[0-9]$/.test(event.code ?? '')) {
            key = event.code.slice(5);
        } else {
            key = key.toUpperCase();
        }
    }
    const parts = [];
    if (ctrl) {
        parts.push('Ctrl');
    }
    if (event.altKey) {
        parts.push('Alt');
    }
    if (event.shiftKey && (/^[A-Z]$/.test(key) || ((key.length > 1) && (key !== 'Plus') && (key !== 'Minus')))) {
        parts.push('Shift');
    }
    parts.push(key);
    return parts.join('+');
}

export function nextZoom(zoom, direction, minZoom, maxZoom) {
    let next = direction > 0 ? zoom * ZOOM_STEP : zoom / ZOOM_STEP;
    next = Math.round(next * 20) / 20;
    if (next === zoom) {
        next = zoom + (direction * 0.05);
    }
    if (((zoom < 1) && (next > 1)) || ((zoom > 1) && (next < 1))) {
        next = 1;
    }
    return Math.min(Math.max(next, minZoom), maxZoom);
}

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

export function portPosition(box, side, ratio, offset, along) {
    const fixed = (along ?? null) !== null;
    switch (side) {
        case 'top':
            return { x: box.x + (fixed ? along : box.w * ratio), y: box.y - offset };
        case 'right':
            return { x: box.x + box.w + offset, y: box.y + (fixed ? along : box.h * ratio) };
        case 'bottom':
            return { x: box.x + (fixed ? along : box.w * ratio), y: box.y + box.h + offset };
        default:
            return { x: box.x - offset, y: box.y + (fixed ? along : box.h * ratio) };
    }
}

function centerOf(box) {
    return { x: box.x + (box.w / 2), y: box.y + (box.h / 2) };
}

function contains(box, point) {
    return (point.x >= box.x) && (point.x <= box.x + box.w) && (point.y >= box.y) && (point.y <= box.y + box.h);
}

export function anchorOf(box, port, toward) {
    if (port) {
        return { ...portPosition(box, port.side, port.ratio, port.offset, port.along), side: port.side };
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

export function gridMiddle(a, b, grid) {
    const middle = (a + b) / 2;
    if (!(grid > 0)) {
        return middle;
    }
    const first = Math.ceil(Math.min(a, b) / grid) * grid;
    const last = Math.floor(Math.max(a, b) / grid) * grid;
    return first > last ? middle : Math.min(Math.max(Math.round(middle / grid) * grid, first), last);
}

export function orthogonalRoute(start, end, stub, grid = 0) {
    const startOut = stepOut(start, start.side, stub);
    const endOut = stepOut(end, end.side, stub);
    const points = [{ x: start.x, y: start.y }, startOut];
    if (isHorizontal(start.side) && isHorizontal(end.side)) {
        const middle = gridMiddle(startOut.x, endOut.x, grid);
        points.push({ x: middle, y: startOut.y }, { x: middle, y: endOut.y });
    } else if (!isHorizontal(start.side) && !isHorizontal(end.side)) {
        const middle = gridMiddle(startOut.y, endOut.y, grid);
        points.push({ x: startOut.x, y: middle }, { x: endOut.x, y: middle });
    } else if (isHorizontal(start.side)) {
        points.push({ x: endOut.x, y: startOut.y });
    } else {
        points.push({ x: startOut.x, y: endOut.y });
    }
    points.push(endOut, { x: end.x, y: end.y });
    return simplify(points);
}

export function curveRoute(start, end, stub) {
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const reach = Math.max(stub, Math.sqrt((dx * dx) + (dy * dy)) / 2);
    return [{ x: start.x, y: start.y }, stepOut(start, start.side, reach), stepOut(end, end.side, reach), { x: end.x, y: end.y }];
}

export function routeBetween(start, end, form, stub, grid = 0) {
    if (form === 'straight') {
        return [{ x: start.x, y: start.y }, { x: end.x, y: end.y }];
    }
    return form === 'curved' ? curveRoute(start, end, stub) : orthogonalRoute(start, end, stub, grid);
}

function findPort(box, id) {
    return id ? (box?.ports ?? []).find((p) => p.id === id) : undefined;
}

export function linkAnchors(link, source, target) {
    const sourcePort = findPort(source, link.sourcePort);
    const targetPort = findPort(target, link.targetPort);
    const sourceToward = targetPort ? portPosition(target, targetPort.side, targetPort.ratio, targetPort.offset, targetPort.along) : centerOf(target);
    const targetToward = sourcePort ? portPosition(source, sourcePort.side, sourcePort.ratio, sourcePort.offset, sourcePort.along) : centerOf(source);
    return { start: anchorOf(source, sourcePort, sourceToward), end: anchorOf(target, targetPort, targetToward) };
}

export function routeLink(link, source, target, grid = 0) {
    const { start, end } = linkAnchors(link, source, target);
    return routeBetween(start, end, link.form ?? 'orthogonal', link.stub ?? DEFAULT_LINK_TYPE.stub, grid);
}

export function formatNumber(value) {
    return String(Math.round(value * 100) / 100);
}

function cutPoint(end, toward, length) {
    if (!(length > 0)) {
        return end;
    }
    const dx = toward.x - end.x;
    const dy = toward.y - end.y;
    const distance = Math.sqrt((dx * dx) + (dy * dy));
    if (distance <= 0) {
        return end;
    }
    const ratio = Math.min(length, distance) / distance;
    return { x: end.x + (dx * ratio), y: end.y + (dy * ratio) };
}

export function toPath(points, form = 'orthogonal', sourceCut = 0, targetCut = 0) {
    if (points.length < 2) {
        return '';
    }
    const text = (point) => `${formatNumber(point.x)},${formatNumber(point.y)}`;
    const first = cutPoint(points[0], points[1], sourceCut);
    const last = cutPoint(points[points.length - 1], points[points.length - 2], targetCut);
    if ((form === 'curved') && (points.length === 4)) {
        return `M${text(first)}C${text(points[1])} ${text(points[2])} ${text(last)}`;
    }
    let path = `M${text(first)}`;
    for (let i = 1; i < points.length - 1; i++) {
        path += `L${text(points[i])}`;
    }
    return `${path}L${text(last)}`;
}

export function collectLines(boxes) {
    const x = [];
    const y = [];
    for (const box of boxes) {
        x.push({ value: box.x, box }, { value: box.x + (box.w / 2), box }, { value: box.x + box.w, box });
        y.push({ value: box.y, box }, { value: box.y + (box.h / 2), box }, { value: box.y + box.h, box });
    }
    x.sort((a, b) => a.value - b.value);
    y.sort((a, b) => a.value - b.value);
    return { x, y };
}

function lowerBound(lines, value) {
    let low = 0;
    let high = lines.length;
    while (low < high) {
        const middle = (low + high) >> 1;
        if (lines[middle].value < value) {
            low = middle + 1;
        } else {
            high = middle;
        }
    }
    return low;
}

export function nearestLine(lines, value, tolerance) {
    const index = lowerBound(lines, value);
    let best = null;
    for (const line of [lines[index - 1], lines[index]]) {
        if (line && (Math.abs(line.value - value) <= tolerance) && (!best || (Math.abs(line.value - value) < Math.abs(best.value - value)))) {
            best = line;
        }
    }
    return best;
}

export function snapAxis(lines, values, tolerance) {
    let best = null;
    for (const value of values) {
        const line = nearestLine(lines, value, tolerance);
        if (line && (!best || (Math.abs(line.value - value) < Math.abs(best.delta)))) {
            best = { delta: line.value - value, value: line.value };
        }
    }
    return best;
}

export function matchingLines(lines, values) {
    const result = [];
    for (const value of values) {
        for (let i = lowerBound(lines, value - GUIDE_EPSILON); (i < lines.length) && (lines[i].value <= value + GUIDE_EPSILON); i++) {
            result.push(lines[i]);
        }
    }
    return result;
}

export function gridSnap(value, size) {
    return size > 0 ? Math.round(value / size) * size : value;
}

export function gridAnchor(bounds, centered) {
    return centered ? { x: bounds.x + (bounds.w / 2), y: bounds.y + (bounds.h / 2) } : { x: bounds.x, y: bounds.y };
}

export function resizeEdges(box, handle, dx, dy) {
    return {
        left: box.x + (handle.includes('w') ? dx : 0),
        top: box.y + (handle.includes('n') ? dy : 0),
        right: box.x + box.w + (handle.includes('e') ? dx : 0),
        bottom: box.y + box.h + (handle.includes('s') ? dy : 0),
    };
}

export function limitResize(edges, handle, limits) {
    let { left, top, right, bottom } = edges;
    const inner = limits.inner;
    if (handle.includes('w')) {
        left = Math.min(left, right - limits.minW, inner ? inner.left : Number.POSITIVE_INFINITY);
    }
    if (handle.includes('e')) {
        right = Math.max(right, left + limits.minW, inner ? inner.right : Number.NEGATIVE_INFINITY);
    }
    if (handle.includes('n')) {
        top = Math.min(top, bottom - limits.minH, inner ? inner.top : Number.POSITIVE_INFINITY);
    }
    if (handle.includes('s')) {
        bottom = Math.max(bottom, top + limits.minH, inner ? inner.bottom : Number.NEGATIVE_INFINITY);
    }
    return { x: left, y: top, w: right - left, h: bottom - top };
}

function resolvePoint(box, portId) {
    if (!box || !box.registered) {
        return null;
    }
    if (portId) {
        const port = findPort(box, portId);
        return port ? { box, port } : null;
    }
    return box.whole ? { box, port: null } : null;
}

function linksAt(table, element, portId, exceptLinkId) {
    let count = 0;
    for (const link of table.linksByBox.get(element) ?? []) {
        if (link.id === exceptLinkId) {
            continue;
        }
        if (((link.source === element) && ((link.sourcePort ?? null) === portId)) || ((link.target === element) && ((link.targetPort ?? null) === portId))) {
            count++;
        }
    }
    return count;
}

function accepts(port, type) {
    return !port || ((!port.accepts || port.accepts.includes(type)) && (!port.rejects || !port.rejects.includes(type)));
}

function makesCycle(table, sourceId, targetId, linkType, exceptLinkId) {
    if (sourceId === targetId) {
        return true;
    }
    const seen = new Set([targetId]);
    const queue = [targetId];
    while (queue.length > 0) {
        const current = queue.shift();
        for (const link of table.linksByBox.get(current) ?? []) {
            if ((link.source !== current) || (link.type !== linkType) || (link.id === exceptLinkId)) {
                continue;
            }
            if (link.target === sourceId) {
                return true;
            }
            if (!seen.has(link.target)) {
                seen.add(link.target);
                queue.push(link.target);
            }
        }
    }
    return false;
}

export function checkConnection(table, schema, sourceEnd, targetEnd, linkTypeName, exceptLinkId) {
    const source = resolvePoint(table.boxes.get(sourceEnd.element), sourceEnd.port);
    const target = resolvePoint(table.boxes.get(targetEnd.element), targetEnd.port);
    if (!source || !target) {
        return 'noConnectionPoint';
    }
    if ((source.port?.direction === 'in') || (target.port?.direction === 'out')) {
        return 'direction';
    }
    if (source.port?.kind && target.port?.kind && (source.port.kind !== target.port.kind)) {
        return 'portKind';
    }
    if (!source.box.connectable || !target.box.connectable) {
        return 'permission';
    }
    const sourcePort = sourceEnd.port ?? null;
    const targetPort = targetEnd.port ?? null;
    if (((source.port?.max ?? null) !== null) && (linksAt(table, sourceEnd.element, sourcePort, exceptLinkId) >= source.port.max)) {
        return 'limit';
    }
    if (((target.port?.max ?? null) !== null) && (linksAt(table, targetEnd.element, targetPort, exceptLinkId) >= target.port.max)) {
        return 'limit';
    }
    const type = schema.linkTypes.get(linkTypeName) ?? DEFAULT_LINK_TYPE;
    if (!type.allowSelf && (sourceEnd.element === targetEnd.element)) {
        return 'selfOrDuplicate';
    }
    if (!type.allowDuplicate) {
        for (const link of table.linksByBox.get(sourceEnd.element) ?? []) {
            if ((link.id !== exceptLinkId) && (link.source === sourceEnd.element) && ((link.sourcePort ?? null) === sourcePort) &&
                (link.target === targetEnd.element) && ((link.targetPort ?? null) === targetPort)) {
                return 'selfOrDuplicate';
            }
        }
    }
    if (!accepts(source.port, target.box.type) || !accepts(target.port, source.box.type)) {
        return 'peerType';
    }
    if (!type.allowCrossParent && ((source.box.parent ?? null) !== (target.box.parent ?? null))) {
        return 'crossParent';
    }
    if (!type.allowCycles && makesCycle(table, sourceEnd.element, targetEnd.element, linkTypeName, exceptLinkId)) {
        return 'cycle';
    }
    return null;
}

export function canNest(schema, typeName, parent) {
    const type = schema.types.get(typeName);
    if (!parent) {
        return !type || (type.topLevel !== false);
    }
    if (!type || (parent.kind !== 'group') || parent.locked || !parent.registered) {
        return false;
    }
    const parentType = schema.types.get(parent.type);
    return (!parentType?.children || parentType.children.includes(typeName)) && (!type.parents || type.parents.includes(parent.type));
}

export function searchTypes(types, query) {
    const text = query.trim().toLowerCase();
    const words = text.split(/\s+/).filter((w) => w.length > 0);
    if (words.length === 0) {
        return types;
    }
    const ranked = [];
    types.forEach((type, order) => {
        const haystack = [type.label, type.name, type.categoryLabel ?? type.category ?? '', ...(type.search ?? [])].join('\u0001').toLowerCase();
        if (words.every((word) => haystack.includes(word))) {
            const label = type.label.toLowerCase();
            ranked.push({ type, order, rank: label === text ? 0 : (label.startsWith(text) ? 1 : 2) });
        }
    });
    return ranked.sort((a, b) => (a.rank - b.rank) || (a.order - b.order)).map((x) => x.type);
}

export function placeBeside(box, side, size, spacing, along) {
    switch (side) {
        case 'top':
            return { x: (along ?? centerOf(box).x) - (size.w / 2), y: box.y - spacing - size.h, w: size.w, h: size.h };
        case 'bottom':
            return { x: (along ?? centerOf(box).x) - (size.w / 2), y: box.y + box.h + spacing, w: size.w, h: size.h };
        case 'left':
            return { x: box.x - spacing - size.w, y: (along ?? centerOf(box).y) - (size.h / 2), w: size.w, h: size.h };
        default:
            return { x: box.x + box.w + spacing, y: (along ?? centerOf(box).y) - (size.h / 2), w: size.w, h: size.h };
    }
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

    countOfType(type) {
        let count = 0;
        for (const box of this.boxes.values()) {
            if (box.registered && (box.type === type)) {
                count++;
            }
        }
        return count;
    }

    innerBounds(id, padding) {
        const children = (this.children.get(id) ?? []).map((child) => this.boxes.get(child)).filter((b) => b);
        if (children.length === 0) {
            return null;
        }
        const [left, top, right, bottom] = padding ?? [0, 0, 0, 0];
        return {
            left: Math.min(...children.map((b) => b.x)) - left,
            top: Math.min(...children.map((b) => b.y)) - top,
            right: Math.max(...children.map((b) => b.x + b.w)) + right,
            bottom: Math.max(...children.map((b) => b.y + b.h)) + bottom,
        };
    }
}

function svg(name, attributes) {
    const element = document.createElementNS(SVG_NS, name);
    for (const [key, value] of Object.entries(attributes ?? {})) {
        element.setAttribute(key, String(value));
    }
    return element;
}

function shapeElement(type, bounds, attributes) {
    const { x, y, w, h } = bounds;
    switch (type?.shape) {
        case 'ellipse':
            return svg('ellipse', { ...attributes, cx: x + (w / 2), cy: y + (h / 2), rx: w / 2, ry: h / 2 });
        case 'diamond':
            return svg('polygon', { ...attributes, points: `${x + (w / 2)},${y} ${x + w},${y + (h / 2)} ${x + (w / 2)},${y + h} ${x},${y + (h / 2)}` });
        case 'path':
            if (type.path) {
                return svg('path', { ...attributes, class: `${attributes.class ?? ''} mussel-diagram-shape-path`.trim(), d: type.path, transform: `translate(${x} ${y}) scale(${w} ${h})` });
            }
            break;
        default:
            break;
    }
    return svg('rect', { ...attributes, x, y, width: w, height: h });
}

function routeMidpoint(points) {
    if (points.length === 0) {
        return { x: 0, y: 0 };
    }
    let total = 0;
    for (let i = 1; i < points.length; i++) {
        total += Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y);
    }
    let half = total / 2;
    for (let i = 1; i < points.length; i++) {
        const length = Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y);
        if ((length > 0) && (half <= length)) {
            const t = half / length;
            return { x: points[i - 1].x + ((points[i].x - points[i - 1].x) * t), y: points[i - 1].y + ((points[i].y - points[i - 1].y) * t) };
        }
        half -= length;
    }
    return points[points.length - 1];
}

function restore(saved) {
    for (const [element, value] of saved) {
        for (const [attribute, old] of value) {
            if (old === null) {
                element.removeAttribute(attribute);
            } else {
                element.setAttribute(attribute, old);
            }
        }
    }
}

const EXPORT_PROPERTIES = [
    ['fill', true, 'rgb(0, 0, 0)'],
    ['fill-opacity', true, '1'],
    ['fill-rule', true, 'nonzero'],
    ['stroke', true, 'none'],
    ['stroke-width', true, '1px'],
    ['stroke-dasharray', true, 'none'],
    ['stroke-dashoffset', true, '0px'],
    ['stroke-opacity', true, '1'],
    ['stroke-linecap', true, 'butt'],
    ['stroke-linejoin', true, 'miter'],
    ['font-family', true, null],
    ['font-size', true, null],
    ['font-weight', true, '400'],
    ['font-style', true, 'normal'],
    ['letter-spacing', true, 'normal'],
    ['text-anchor', true, 'start'],
    ['dominant-baseline', true, 'auto'],
    ['visibility', true, 'visible'],
    ['color', true, null],
    ['opacity', false, '1'],
    ['vector-effect', false, 'none'],
];
const EXPORT_HTML_PROPERTIES = ['display', 'box-sizing', 'width', 'height', 'margin', 'padding', 'border', 'border-radius', 'background-color', 'color', 'font-family', 'font-size', 'font-weight', 'font-style', 'line-height', 'text-align', 'white-space', 'overflow', 'opacity'];
const EXPORT_KEPT_ATTRIBUTES = new Set(['data-id', 'data-type']);
const EXPORT_DEFINITIONS = 'defs, marker, symbol, clipPath, mask, pattern, linearGradient, radialGradient, filter';
const EXPORT_MARKS = ['data-selected', 'data-pending', 'data-candidate', 'data-drop-target', 'data-not-allowed', 'data-editing', 'data-split-target', 'data-reconnecting'];

function normalizeFamily(name) {
    return name.trim().replace(/^['"]|['"]$/g, '').toLowerCase();
}

function localUrl(value) {
    return value.replace(/url\(\s*(['"]?)[^#'")]*#([^'")]+)\1\s*\)/g, 'url(#$2)');
}

function renameIds(root, suffix) {
    const names = new Map();
    for (const element of root.querySelectorAll('[id]')) {
        const id = element.getAttribute('id');
        names.set(id, `${id}${suffix}`);
        element.setAttribute('id', `${id}${suffix}`);
    }
    if (names.size === 0) {
        return;
    }
    for (const element of [root, ...root.querySelectorAll('*')]) {
        for (const attribute of [...element.attributes]) {
            const value = attribute.value;
            if (!value.includes('#')) {
                continue;
            }
            let next = value.replace(/url\(\s*(['"]?)#([^'")\s]+)\1\s*\)/g, (whole, quote, id) => (names.has(id) ? `url(#${names.get(id)})` : whole));
            if (((attribute.name === 'href') || (attribute.name === 'xlink:href')) && next.startsWith('#') && names.has(next.slice(1))) {
                next = `#${names.get(next.slice(1))}`;
            }
            if (next !== value) {
                element.setAttribute(attribute.name, next);
            }
        }
    }
}

function inlineIcons(clone) {
    const symbols = new Map([...clone.querySelectorAll('symbol[id]')].map((symbol) => [symbol.getAttribute('id'), symbol]));
    let copy = 0;
    for (const use of [...clone.querySelectorAll('use')]) {
        const href = use.getAttribute('href') ?? use.getAttribute('xlink:href') ?? '';
        const symbol = href.startsWith('#') ? symbols.get(href.slice(1)) : null;
        if (!symbol) {
            use.remove();
            continue;
        }
        const inner = document.createElementNS(SVG_NS, 'svg');
        for (const name of ['x', 'y', 'width', 'height', 'class']) {
            const value = use.getAttribute(name);
            if (value !== null) {
                inner.setAttribute(name, value);
            }
        }
        for (const name of ['viewBox', 'preserveAspectRatio']) {
            const value = symbol.getAttribute(name);
            if (value !== null) {
                inner.setAttribute(name, value);
            }
        }
        for (const child of symbol.childNodes) {
            inner.append(child.cloneNode(true));
        }
        copy++;
        renameIds(inner, `-e${copy}`);
        use.replaceWith(inner);
    }
    for (const symbol of symbols.values()) {
        symbol.remove();
    }
}

async function readDataUrl(url, base, cache) {
    const key = new URL(url, base).href;
    if (!cache.has(key)) {
        cache.set(key, (async () => {
            try {
                const response = await fetch(key, { mode: 'cors', credentials: 'same-origin' });
                if (!response.ok) {
                    return null;
                }
                const blob = await response.blob();
                return await new Promise((resolve) => {
                    const reader = new FileReader();
                    reader.onload = () => resolve(reader.result);
                    reader.onerror = () => resolve(null);
                    reader.readAsDataURL(blob);
                });
            } catch {
                return null;
            }
        })());
    }
    return cache.get(key);
}

async function embedImages(clone, cache) {
    const missing = new Map();
    for (const image of [...clone.querySelectorAll('image')]) {
        const href = image.getAttribute('href') ?? image.getAttribute('xlink:href') ?? '';
        if (!href || href.startsWith('data:')) {
            continue;
        }
        const data = await readDataUrl(href, document.baseURI, cache);
        if (data) {
            image.removeAttribute('xlink:href');
            image.setAttribute('href', data);
            continue;
        }
        const id = image.closest('[data-id]')?.getAttribute('data-id') ?? image.closest('[data-for]')?.getAttribute('data-for');
        let ids = missing.get(href);
        if (!ids) {
            ids = new Set();
            missing.set(href, ids);
        }
        if (id) {
            ids.add(id);
        }
        image.remove();
    }
    return [...missing].map(([url, ids]) => ({ url, ids: [...ids] }));
}

function inlineStyles(root) {
    const families = new Set();
    const entries = [];
    const walk = (element, parent) => {
        const style = getComputedStyle(element);
        const definition = element.closest(EXPORT_DEFINITIONS);
        if ((style.display === 'none') && !definition) {
            entries.push({ element, remove: true });
            return;
        }
        const html = element.namespaceURI !== SVG_NS;
        const values = new Map();
        const attributes = [];
        if (html) {
            attributes.push(['style', EXPORT_HTML_PROPERTIES.map((name) => `${name}:${style.getPropertyValue(name)}`).join(';')]);
        } else {
            for (const [name, inherited, initial] of EXPORT_PROPERTIES) {
                const value = localUrl(style.getPropertyValue(name));
                values.set(name, value);
                const base = inherited ? (parent?.get(name) ?? initial) : initial;
                if (value !== base) {
                    attributes.push([name, value]);
                }
            }
        }
        if (html || (element.localName === 'text')) {
            for (const family of style.fontFamily.split(',')) {
                families.add(normalizeFamily(family));
            }
        }
        entries.push({ element, attributes });
        for (const child of element.children) {
            walk(child, html ? parent : values);
        }
    };
    walk(root, null);
    for (const entry of entries) {
        const element = entry.element;
        if (entry.remove) {
            element.remove();
            continue;
        }
        for (const attribute of [...element.attributes]) {
            const name = attribute.name;
            if ((name === 'class') || (name === 'style') || (name.startsWith('data-') && !EXPORT_KEPT_ATTRIBUTES.has(name))) {
                element.removeAttribute(name);
            }
        }
        for (const [name, value] of entry.attributes) {
            element.setAttribute(name, value);
        }
    }
    return families;
}

async function fontFaceRules() {
    const result = [];
    const visit = (rules, base) => {
        for (const rule of rules) {
            if (rule instanceof CSSFontFaceRule) {
                result.push({ rule, base });
            } else if (rule instanceof CSSImportRule) {
                try {
                    if (rule.styleSheet) {
                        visit(rule.styleSheet.cssRules, rule.styleSheet.href ?? base);
                    }
                } catch {
                }
            } else if (rule.cssRules) {
                visit(rule.cssRules, base);
            }
        }
    };
    for (const sheet of document.styleSheets) {
        const base = sheet.href ?? document.baseURI;
        let rules = null;
        try {
            rules = sheet.cssRules;
        } catch {
            rules = null;
        }
        if (rules) {
            visit(rules, base);
            continue;
        }
        if (!sheet.href) {
            continue;
        }
        try {
            const response = await fetch(sheet.href, { mode: 'cors' });
            if (response.ok) {
                const parsed = new CSSStyleSheet();
                parsed.replaceSync((await response.text()).replace(/@import[^;]*;/g, ''));
                visit(parsed.cssRules, sheet.href);
            }
        } catch {
        }
    }
    return result;
}

async function embedFonts(families, cache) {
    if (families.size === 0) {
        return '';
    }
    const ranges = (value) => (value || 'U+0-10FFFF').replace(/\s+/g, '').toLowerCase();
    const loaded = new Set();
    for (const face of document.fonts ?? []) {
        if (face.status === 'loaded') {
            loaded.add(`${normalizeFamily(face.family)}|${ranges(face.unicodeRange)}`);
        }
    }
    const faces = [];
    for (const { rule, base } of await fontFaceRules()) {
        const family = rule.style.getPropertyValue('font-family');
        const range = rule.style.getPropertyValue('unicode-range');
        if (!families.has(normalizeFamily(family)) || !loaded.has(`${normalizeFamily(family)}|${ranges(range)}`)) {
            continue;
        }
        const sources = [...rule.style.getPropertyValue('src').matchAll(/url\(\s*(['"]?)(.*?)\1\s*\)\s*(?:format\(\s*(['"]?)(.*?)\3\s*\))?/g)]
            .map((match) => ({ url: match[2], format: match[4] ?? '' }));
        const source = sources.find((x) => x.format === 'woff2') ?? sources[0];
        const data = source ? await readDataUrl(source.url, base, cache) : null;
        if (!data) {
            continue;
        }
        const descriptors = ['font-weight', 'font-style', 'font-stretch', 'unicode-range']
            .map((name) => [name, rule.style.getPropertyValue(name)])
            .filter(([, value]) => value)
            .map(([name, value]) => `${name}:${value};`)
            .join('');
        faces.push(`@font-face{font-family:${family};src:url(${data})${source.format ? ` format("${source.format}")` : ''};${descriptors}}`);
    }
    return faces.join('');
}

async function rasterize(text, width, height, options) {
    const scale = options.scale > 0 ? options.scale : 1;
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(width * scale));
    canvas.height = Math.max(1, Math.round(height * scale));
    const context = canvas.getContext('2d');
    if (!context || (canvas.width !== Math.max(1, Math.round(width * scale))) || (canvas.height !== Math.max(1, Math.round(height * scale)))) {
        return { tooLarge: true };
    }
    const x = canvas.width - 1;
    const y = canvas.height - 1;
    context.fillStyle = '#010203';
    context.fillRect(x, y, 1, 1);
    const probe = context.getImageData(x, y, 1, 1).data;
    context.clearRect(x, y, 1, 1);
    if ((probe[0] !== 1) || (probe[1] !== 2) || (probe[2] !== 3)) {
        return { tooLarge: true };
    }
    const url = URL.createObjectURL(new Blob([text], { type: 'image/svg+xml' }));
    try {
        const image = new Image();
        image.src = url;
        await image.decode();
        context.drawImage(image, 0, 0, canvas.width, canvas.height);
    } finally {
        URL.revokeObjectURL(url);
    }
    const blob = await new Promise((resolve) => canvas.toBlob(resolve, `image/${options.format}`, options.quality));
    return blob ? { blob } : { tooLarge: true };
}

class DiagramController {
    constructor(root, dotnet, options, table, schema) {
        this.root = root;
        this.dotnet = dotnet;
        this.options = options;
        this.table = new DiagramTable(table);
        this.setSchema(schema);
        this.stage = root.querySelector('.mussel-diagram-stage');
        this.canvas = root.querySelector('.mussel-diagram-canvas');
        this.applyOptionClasses();
        this.viewports = [...root.querySelectorAll('.mussel-diagram-viewport')];
        this.overlay = root.querySelector('.mussel-diagram-overlay > .mussel-diagram-viewport');
        this.gridLayer = root.querySelector('.mussel-diagram-grid');
        this.uid = Math.random().toString(36).slice(2, 10);
        this.layers = {};
        for (const name of ['candidates', 'guides', 'selection', 'pending', 'preview', 'handles']) {
            const layer = svg('g', { class: `mussel-diagram-layer-${name}` });
            this.overlay?.append(layer);
            this.layers[name] = layer;
        }
        this.view = { x: 0, y: 0, zoom: 1 };
        this.gesture = null;
        this.pan = null;
        this.press = null;
        this.drag = null;
        this.hover = null;
        this.picker = null;
        this.marks = [];
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
        this.drawnZoom = null;
        this.gridKey = '';
        this.size = { width: root.clientWidth, height: root.clientHeight };
        this.labels = new LabelWriter(this.canvas);
        this.labels.tooltips = options.tooltipEnabled !== false;
        this.badgeLayer = root.querySelector('.mussel-diagram-badges');
        this.badges = this.badgeLayer ? new BadgeWriter(this.badgeLayer) : null;
        this.commandMap = new Map();
        this.editor = null;
        this.external = null;
        this.externalFrame = 0;
        this.paletteTypes = null;
        this.selectWhenAdded = null;
        this.lastPointer = null;
        this.mac = /Mac|iPhone|iPad/.test(navigator.platform ?? '');
        this.inFlight = new Set();
        this.exports = new Map();
        this.touches = new Map();
        this.pinch = null;
        this.commandsInFlight = new Set();
        this.commandChain = Promise.resolve();
        this.listeners = [];
        this.listen(root, 'wheel', (e) => this.onWheel(e), { passive: false });
        this.listen(root, 'pointerdown', (e) => this.onPointerDown(e));
        this.listen(root, 'pointermove', (e) => this.onPointerMove(e));
        this.listen(root, 'pointerup', (e) => this.onPointerUp(e));
        this.listen(root, 'pointercancel', (e) => this.onPointerCancel(e));
        this.listen(root, 'lostpointercapture', (e) => this.onPointerCancel(e));
        this.listen(root, 'pointerleave', (e) => {
            if (e.pointerType !== 'touch') {
                this.lastPointer = null;
                this.clearHover();
            }
        });
        this.listen(root, 'keydown', (e) => this.onKeyDown(e));
        this.listen(root, 'keyup', (e) => this.onKeyUp(e));
        this.listen(root, 'blur', () => this.setSpace(false));
        this.listen(root, 'dblclick', (e) => this.onDoubleClick(e));
        this.listen(root, 'contextmenu', (e) => this.onContextMenu(e));
        this.versionObserver = new MutationObserver(() => this.settlePending());
        this.versionObserver.observe(root, { attributes: true, attributeFilter: ['data-version'] });
        this.resizeObserver = new ResizeObserver(() => this.onResize());
        this.resizeObserver.observe(root);
        this.applyView();
        this.notifyViewport();
        controllers.set(root, this);
    }

    ready() {
        this.root.dataset.attached = '';
    }

    listen(target, type, handler, options) {
        target.addEventListener(type, handler, options);
        this.listeners.push(() => target.removeEventListener(type, handler, options));
    }

    setOptions(options) {
        const tooltips = options.tooltipEnabled !== false;
        if (this.labels.tooltips !== tooltips) {
            this.labels.tooltips = tooltips;
            this.labels.writeAll(true);
        }
        this.options = options;
        this.applyOptionClasses();
        this.gridKey = '';
        this.updateGrid();
        this.drawSelection();
    }

    applyOptionClasses() {
        const options = this.options;
        this.canvas?.style.setProperty('--mussel-diagram-link-hit-width', String(options.linkHitWidth ?? 10));
        this.canvas?.classList.toggle('mussel-diagram-no-move', !options.moveEnabled);
        this.canvas?.classList.toggle('mussel-diagram-no-connect', !options.connectEnabled);
        this.canvas?.classList.toggle('mussel-diagram-pan-drag', (options.backgroundDrag === 'pan') && options.panEnabled);
    }

    setNotAllowed(on) {
        this.root.classList.toggle('mussel-diagram-not-allowed', on);
    }

    setSchema(schema) {
        const first = (list) => {
            const map = new Map();
            for (const item of list) {
                if (!map.has(item.name)) {
                    map.set(item.name, item);
                }
            }
            return map;
        };
        this.schema = {
            list: schema.types,
            types: first(schema.types),
            linkTypes: first(schema.linkTypes),
            order: new Map((schema.order ?? []).map((name, i) => [name, i])),
            defaultLinkType: schema.defaultLinkType ?? schema.linkTypes[0]?.name ?? null,
            reasons: schema.reasons ?? {},
            texts: schema.texts ?? { searchPlaceholder: '', noMatches: '' },
        };
        this.wantedIcons = new Set();
    }

    wantIcons(ids) {
        const fresh = ids.filter((id) => id && !this.wantedIcons.has(id));
        if ((fresh.length === 0) || !this.dotnet) {
            return;
        }
        for (const id of fresh) {
            this.wantedIcons.add(id);
        }
        this.dotnet.invokeMethodAsync('WantIcons', fresh).catch(() => {});
    }

    setTable(table) {
        this.table.set(table);
        this.pruneSelection();
        this.afterTableChange();
    }

    applyChanges(changes) {
        this.table.apply(changes);
        this.pruneSelection();
        this.afterTableChange();
    }

    afterTableChange() {
        if (this.editor && !this.table.boxes.has(this.editor.id)) {
            this.closeEditor();
        }
        if (this.awaitedSelection?.size) {
            let added = false;
            for (const id of [...this.awaitedSelection]) {
                if (this.table.boxes.has(id) || this.table.links.has(id)) {
                    this.awaitedSelection.delete(id);
                    this.selection.add(id);
                    added = true;
                }
            }
            if (added) {
                this.drawSelection();
            }
        }
        const id = this.selectWhenAdded;
        if (id && (this.table.boxes.has(id) || this.table.links.has(id))) {
            this.selectWhenAdded = null;
            this.setSelection([id]);
        }
    }

    reasonText(kind) {
        return this.schema.reasons[kind] ?? kind;
    }

    point(event) {
        const rect = this.root.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    toWorld(point) {
        return { x: (point.x - this.view.x) / this.view.zoom, y: (point.y - this.view.y) / this.view.zoom };
    }

    visibleArea() {
        const { x, y, zoom } = this.view;
        return { x: -x / zoom, y: -y / zoom, w: this.size.width / zoom, h: this.size.height / zoom };
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
        this.updateGrid();
        if (this.drawnZoom !== zoom) {
            this.drawSelection();
        }
        this.placeEditor();
    }

    updateGrid() {
        if (!this.gridLayer) {
            return;
        }
        if (!this.options.gridVisible) {
            if (this.gridKey !== 'off') {
                this.gridLayer.replaceChildren();
                this.gridKey = 'off';
            }
            return;
        }
        const zoom = this.view.zoom;
        let spacing = this.options.gridSize > 0 ? this.options.gridSize : 10;
        while (spacing * zoom < GRID_MINIMUM) {
            spacing *= 2;
        }
        const key = `${this.options.gridStyle}:${spacing}:${zoom}`;
        if (key !== this.gridKey) {
            this.gridKey = key;
            const id = `mussel-diagram-${this.uid}-grid`;
            const size = spacing * 4;
            const pattern = svg('pattern', { id, width: size, height: size, patternUnits: 'userSpaceOnUse' });
            for (let i = 0; i < 4; i++) {
                for (let j = 0; (j < 4) && (this.options.gridStyle !== 'lines'); j++) {
                    pattern.append(svg('circle', { class: (i === 0) && (j === 0) ? 'mussel-diagram-grid-major' : 'mussel-diagram-grid-minor', cx: i * spacing, cy: j * spacing, r: 1 / zoom }));
                }
                if (this.options.gridStyle === 'lines') {
                    const major = i === 0 ? 'mussel-diagram-grid-major' : 'mussel-diagram-grid-minor';
                    pattern.append(svg('path', { class: major, d: `M${i * spacing},0V${size}M0,${i * spacing}H${size}`, 'stroke-width': 1 / zoom }));
                }
            }
            this.gridRect = svg('rect', { class: 'mussel-diagram-grid-fill', fill: `url(#${id})` });
            this.gridLayer.replaceChildren(pattern, this.gridRect);
        }
        const area = this.visibleArea();
        this.gridRect.setAttribute('x', String(area.x));
        this.gridRect.setAttribute('y', String(area.y));
        this.gridRect.setAttribute('width', String(area.w));
        this.gridRect.setAttribute('height', String(area.h));
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
        this.view = fitView(this.drawnBounds(bounds), this.size.width, this.size.height, margin, this.options.minZoom, this.options.maxZoom);
        this.applyView();
        this.notifyViewport();
    }

    drawnBounds(bounds) {
        let left = bounds.x;
        let top = bounds.y;
        let right = bounds.x + bounds.width;
        let bottom = bounds.y + bounds.height;
        const layers = this.canvas?.querySelectorAll(':scope > .mussel-diagram-viewport > :is(.mussel-diagram-groups, .mussel-diagram-overlay-groups, .mussel-diagram-links, .mussel-diagram-nodes, .mussel-diagram-badges)') ?? [];
        for (const layer of layers) {
            const box = layer.getBBox();
            if ((box.width > 0) || (box.height > 0)) {
                left = Math.min(left, box.x);
                top = Math.min(top, box.y);
                right = Math.max(right, box.x + box.width);
                bottom = Math.max(bottom, box.y + box.height);
            }
        }
        return { x: left, y: top, width: right - left, height: bottom - top };
    }

    onWheel(event) {
        if (event.target.closest?.('.mussel-diagram-nowheel, .mussel-diagram-picker')) {
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
        const badge = event.target.closest('.mussel-diagram-badge');
        if (badge) {
            const id = badge.parentElement?.getAttribute('data-for');
            const box = this.table.boxes.has(id);
            if (!box && !this.table.links.has(id)) {
                return null;
            }
            return badge.hasAttribute('data-clickable') ? { kind: 'badge', id, index: Number(badge.getAttribute('data-index')) } : { kind: 'element', id, box };
        }
        const target = event.target.closest(ELEMENT_SELECTOR);
        if (!target || !this.canvas.contains(target)) {
            return null;
        }
        const id = target.getAttribute('data-id');
        if (this.table.boxes.has(id)) {
            return { kind: 'element', id, box: true };
        }
        return this.table.links.has(id) ? { kind: 'element', id, box: false } : null;
    }

    pressTarget(event) {
        const target = event.target;
        if (this.overlay?.contains(target)) {
            const handle = target.closest('[data-handle]');
            if (handle) {
                return { kind: 'handle', id: handle.getAttribute('data-id'), handle: handle.getAttribute('data-handle') };
            }
            const end = target.closest('[data-end]');
            if (end) {
                return { kind: 'end', id: end.getAttribute('data-id'), side: end.getAttribute('data-end') };
            }
            const arrow = target.closest('[data-arrow]');
            if (arrow) {
                return { kind: 'arrow', id: arrow.getAttribute('data-id'), side: arrow.getAttribute('data-arrow') };
            }
        }
        const port = target.closest?.('.mussel-diagram-port');
        if (port && this.canvas.contains(port) && this.options.connectEnabled) {
            const id = port.closest(ELEMENT_SELECTOR)?.getAttribute('data-id');
            const box = this.table.boxes.get(id);
            const definition = findPort(box, port.getAttribute('data-port'));
            const full = ((definition?.max ?? null) !== null) && (linksAt(this.table, id, definition.id, null) >= definition.max);
            if (box?.connectable && definition && (definition.direction !== 'in') && !full) {
                return { kind: 'port', id, port: definition.id };
            }
        }
        return this.hit(event) ?? { kind: 'background' };
    }

    onPointerDown(event) {
        this.hideReason();
        if (this.picker) {
            if (this.picker.element.contains(event.target)) {
                return;
            }
            this.closePicker();
        }
        this.root.focus({ preventScroll: true });
        if (event.pointerType === 'touch') {
            this.touches.set(event.pointerId, { x: event.clientX, y: event.clientY });
            if (this.touches.size > 1) {
                this.startPinch(event);
                return;
            }
        }
        if (event.target.closest?.(NO_DRAG_SELECTOR)) {
            return;
        }
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
        if ((event.button !== 0) || this.press || this.drag) {
            return;
        }
        this.root.setPointerCapture(event.pointerId);
        const point = this.point(event);
        this.press = {
            pointerId: event.pointerId,
            hit: this.pressTarget(event),
            x: event.clientX,
            y: event.clientY,
            point,
            world: this.toWorld(point),
            toggle: event.shiftKey || event.ctrlKey || event.metaKey,
            shift: event.shiftKey,
            keys: { shift: event.shiftKey, ctrl: event.ctrlKey, alt: event.altKey, meta: event.metaKey },
            threshold: event.pointerType === 'touch' ? TOUCH_THRESHOLD : MOUSE_THRESHOLD,
            touch: event.pointerType === 'touch',
        };
        if (this.press.touch) {
            const press = this.press;
            press.timer = setTimeout(() => this.longPress(press), LONG_PRESS_DELAY);
        }
    }

    onPointerMove(event) {
        this.lastPointer = this.point(event);
        if (this.touches.has(event.pointerId)) {
            this.touches.set(event.pointerId, { x: event.clientX, y: event.clientY });
            if (this.pinch) {
                this.updatePinch();
                return;
            }
        }
        if (this.pan?.pointerId === event.pointerId) {
            this.panTo(event);
            return;
        }
        if (this.press?.pointerId !== event.pointerId) {
            if (!this.press && !this.drag && !this.pan) {
                this.updateHover(event);
            }
            return;
        }
        if (!this.drag) {
            const press = this.press;
            const distance = Math.hypot(event.clientX - press.x, event.clientY - press.y);
            if (distance < press.threshold) {
                return;
            }
            clearTimeout(press.timer);
            if (press.consumed) {
                return;
            }
            if (press.touch && (press.hit.kind === 'background') && !press.long) {
                this.press = null;
                this.startPan(event);
                return;
            }
            if (!press.touch && (press.hit.kind === 'background') && (this.options.backgroundDrag !== 'boxSelect')) {
                this.press = null;
                if ((this.options.backgroundDrag === 'pan') && this.startPan(event, press)) {
                    this.panTo(event);
                }
                return;
            }
            if (!this.startDrag(press)) {
                return;
            }
            this.clearHover();
        }
        this.drag.point = this.point(event);
        this.drag.alt = event.altKey;
        this.requestFrame();
    }

    onPointerUp(event) {
        if (this.touches.delete(event.pointerId) && this.pinch) {
            if (this.touches.size < 2) {
                this.endPinch();
            }
            return;
        }
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
        clearTimeout(press.timer);
        if (this.drag) {
            this.drag.point = this.point(event);
            this.drag.alt = event.altKey;
            this.finishDrag();
        } else if (!press.consumed) {
            this.click(press);
            this.notifyClick(press, false);
        }
    }

    onPointerCancel(event) {
        if (this.touches.delete(event.pointerId) && this.pinch) {
            if (this.touches.size < 2) {
                this.endPinch();
            }
            return;
        }
        if (this.pan?.pointerId === event.pointerId) {
            this.pan = null;
            this.root.classList.remove('mussel-diagram-panning');
            this.settle();
        }
        if (this.press?.pointerId === event.pointerId) {
            clearTimeout(this.press.timer);
            this.press = null;
            this.cancelDrag();
        }
    }

    startPan(event, from = null) {
        if (!this.options.panEnabled) {
            return false;
        }
        this.foldGesture();
        this.pan = { pointerId: event.pointerId, x: from?.x ?? event.clientX, y: from?.y ?? event.clientY };
        this.root.classList.add('mussel-diagram-panning');
        return true;
    }

    panTo(event) {
        this.view = { ...this.view, x: this.view.x + (event.clientX - this.pan.x), y: this.view.y + (event.clientY - this.pan.y) };
        this.pan.x = event.clientX;
        this.pan.y = event.clientY;
        this.applyView();
    }

    longPress(press) {
        if ((this.press !== press) || this.drag) {
            return;
        }
        press.long = true;
        const hit = press.hit;
        if ((hit.kind !== 'element') || !this.options.selectionEnabled) {
            return;
        }
        const selectable = hit.box ? this.table.boxes.get(hit.id)?.selectable : this.table.links.get(hit.id)?.selectable;
        if (!selectable) {
            return;
        }
        press.consumed = true;
        const next = new Set(this.selection);
        if (next.has(hit.id)) {
            next.delete(hit.id);
        } else {
            next.add(hit.id);
        }
        this.setSelection([...next]);
    }

    startPinch(event) {
        if (this.pinch) {
            return;
        }
        this.root.setPointerCapture(event.pointerId);
        if (this.press) {
            clearTimeout(this.press.timer);
            this.press = null;
        }
        if (this.drag) {
            this.cancelDrag();
        }
        if (this.pan) {
            this.pan = null;
            this.root.classList.remove('mussel-diagram-panning');
        }
        this.foldGesture();
        const [first, second] = [...this.touches.values()];
        const rect = this.root.getBoundingClientRect();
        this.pinch = {
            ids: [...this.touches.keys()].slice(0, 2),
            distance: Math.max(1, Math.hypot(second.x - first.x, second.y - first.y)),
            mid: { x: ((first.x + second.x) / 2) - rect.left, y: ((first.y + second.y) / 2) - rect.top },
            rect,
        };
    }

    updatePinch() {
        const pinch = this.pinch;
        const first = this.touches.get(pinch.ids[0]);
        const second = this.touches.get(pinch.ids[1]);
        if (!first || !second) {
            return;
        }
        const mid = { x: ((first.x + second.x) / 2) - pinch.rect.left, y: ((first.y + second.y) / 2) - pinch.rect.top };
        const factor = Math.hypot(second.x - first.x, second.y - first.y) / pinch.distance;
        const k = this.options.zoomEnabled ? limitFactor(this.view.zoom, factor, this.options.minZoom, this.options.maxZoom) : 1;
        const to = this.options.panEnabled ? mid : pinch.mid;
        this.gesture = { k, x: to.x - (pinch.mid.x * k), y: to.y - (pinch.mid.y * k), point: to };
        this.stage.style.transform = `matrix(${k},0,0,${k},${this.gesture.x},${this.gesture.y})`;
    }

    endPinch() {
        this.pinch = null;
        this.settle();
        const rest = [...this.touches][0];
        if (rest && this.options.panEnabled) {
            this.pan = { pointerId: rest[0], x: rest[1].x, y: rest[1].y };
            this.root.classList.add('mussel-diagram-panning');
        }
    }

    onContextMenu(event) {
        if (this.press?.touch || this.pinch || (this.touches.size > 0)) {
            event.preventDefault();
        }
    }

    click(press) {
        const hit = press.hit;
        if (press.touch && (hit.kind !== 'arrow')) {
            this.clearHover();
        }
        if (hit.kind === 'badge') {
            if (this.dotnet) {
                this.track(this.dotnet.invokeMethodAsync('OnBadgeClickedAsync', hit.id, hit.index).catch(() => {}));
            }
            return;
        }
        if ((hit.kind === 'port') || (hit.kind === 'arrow')) {
            const box = this.table.boxes.get(hit.id);
            const port = findPort(box, hit.port);
            const side = hit.kind === 'arrow' ? hit.side : port?.side;
            if (box && side) {
                this.openPicker({ element: hit.id, port: hit.port ?? null }, { kind: 'beside', box, side, port }, press.point);
            }
            return;
        }
        if ((hit.kind === 'handle') || (hit.kind === 'end') || !this.options.selectionEnabled) {
            return;
        }
        const selectable = (hit.kind === 'element') && (hit.box ? this.table.boxes.get(hit.id)?.selectable : this.table.links.get(hit.id)?.selectable);
        if (!selectable) {
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
            if (press.touch) {
                this.showTouchArrows(hit.id);
            }
        }
    }

    showTouchArrows(id) {
        const box = this.table.boxes.get(id);
        if (!this.options.connectEnabled || this.waiting || !box?.whole || !box.connectable || (box.kind === 'overlay')) {
            return;
        }
        this.hover = { id, shown: true, timer: 0 };
        this.drawSelection();
    }

    select(ids) {
        const known = (id) => this.table.boxes.has(id) || this.table.links.has(id);
        this.selection = new Set(ids.filter(known));
        this.awaitedSelection = new Set(ids.filter((id) => !known(id)));
        this.drawSelection();
    }

    setSelection(ids) {
        this.awaitedSelection = null;
        const next = new Set(ids);
        if ((next.size === this.selection.size) && [...next].every((id) => this.selection.has(id))) {
            return;
        }
        this.selection = next;
        this.drawSelection();
        if (this.dotnet) {
            this.track(this.dotnet.invokeMethodAsync('OnSelectionChanged', [...next]).catch(() => {}));
        }
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
        const zoom = this.view.zoom;
        this.drawnZoom = zoom;
        const drag = this.drag;
        if (!drag) {
            this.markSelection();
        }
        const pad = SELECTION_PADDING / zoom;
        const frames = [];
        const handles = [];
        for (const id of this.selection) {
            const box = this.table.boxes.get(id);
            if (!box) {
                const link = this.table.links.get(id);
                if (link && !drag && link.connectable && this.options.connectEnabled) {
                    handles.push(...this.endHandles(link));
                }
                continue;
            }
            const overlay = box.kind === 'overlay';
            const moved = (drag?.kind === 'move') && drag.live && (drag.moving.has(id) || (overlay && (box.members?.length > 0) && box.members.every((m) => drag.moving.has(m))));
            frames.push(svg('rect', {
                class: overlay ? 'mussel-diagram-selection mussel-diagram-selection-overlay' : 'mussel-diagram-selection',
                x: (box.x + (moved ? drag.dx : 0)) - pad,
                y: (box.y + (moved ? drag.dy : 0)) - pad,
                width: box.w + (pad * 2),
                height: box.h + (pad * 2),
            }));
            if (!drag && box.resizable && this.options.resizeEnabled) {
                handles.push(...this.resizeHandles(box));
            }
        }
        if ((drag?.kind === 'move') && !drag.live) {
            const { bounds } = drag;
            frames.push(svg('rect', { class: 'mussel-diagram-drag-frame', x: bounds.x + drag.dx, y: bounds.y + drag.dy, width: bounds.w, height: bounds.h }));
        }
        this.layers.selection.replaceChildren(...frames);
        this.layers.handles.replaceChildren(...handles, ...this.arrowElements());
    }

    resizeHandles(box) {
        const size = HANDLE_SIZE / this.view.zoom;
        return HANDLES.map((handle) => {
            const x = handle.includes('w') ? box.x : (handle.includes('e') ? box.x + box.w : box.x + (box.w / 2));
            const y = handle.includes('n') ? box.y : (handle.includes('s') ? box.y + box.h : box.y + (box.h / 2));
            return svg('rect', {
                class: `mussel-diagram-handle mussel-diagram-handle-${handle}`,
                'data-id': box.id,
                'data-handle': handle,
                x: x - (size / 2),
                y: y - (size / 2),
                width: size,
                height: size,
            });
        });
    }

    endHandles(link) {
        const source = this.table.boxes.get(link.source);
        const target = this.table.boxes.get(link.target);
        if (!source?.connectable || !target?.connectable) {
            return [];
        }
        const { start, end } = linkAnchors(link, source, target);
        const radius = HANDLE_SIZE / 2 / this.view.zoom;
        return [['source', start], ['target', end]].map(([side, point]) => svg('circle', {
            class: 'mussel-diagram-handle mussel-diagram-end-handle',
            'data-id': link.id,
            'data-end': side,
            cx: point.x,
            cy: point.y,
            r: radius,
        }));
    }

    arrowElements() {
        if (!this.hover?.shown) {
            return [];
        }
        const box = this.table.boxes.get(this.hover.id);
        if (!box) {
            return [];
        }
        const offset = ARROW_OFFSET / this.view.zoom;
        const size = ARROW_SIZE / this.view.zoom;
        return SIDES.map((side) => {
            const base = portPosition(box, side, 0.5, offset);
            const tip = stepOut(base, side, size);
            const across = isHorizontal(side) ? { x: 0, y: size } : { x: size, y: 0 };
            const d = `M${tip.x},${tip.y}L${base.x + across.x},${base.y + across.y}L${base.x - across.x},${base.y - across.y}Z`;
            return svg('path', { class: 'mussel-diagram-arrow', 'data-id': box.id, 'data-arrow': side, d });
        });
    }

    boxAt(point, filter, overlays) {
        let best = null;
        let bestDepth = -1;
        for (const box of this.table.boxes.values()) {
            if (((box.kind === 'overlay') && !overlays) || !contains(box, point) || (filter && !filter(box))) {
                continue;
            }
            const depth = box.kind === 'node' ? 100000 : (box.kind === 'overlay' ? 50000 : this.table.ancestors(box.id).length);
            if (depth >= bestDepth) {
                best = box;
                bestDepth = depth;
            }
        }
        return best;
    }

    hitBoxAt(point, filter, overlays) {
        const edge = (SELECTION_PADDING + 2) / this.view.zoom;
        return this.boxAt(point, (box) => {
            if (filter && !filter(box)) {
                return false;
            }
            if (box.kind === 'node') {
                return true;
            }
            const header = this.schema.types.get(box.type)?.padding?.[1] ?? 0;
            return (point.x - box.x <= edge) || (box.x + box.w - point.x <= edge) || (point.y - box.y <= Math.max(edge, header)) || (box.y + box.h - point.y <= edge);
        }, overlays);
    }

    updateHover(event) {
        if (!this.options.connectEnabled || this.waiting) {
            this.clearHover();
            return;
        }
        const world = this.toWorld(this.point(event));
        const current = this.hover ? this.table.boxes.get(this.hover.id) : null;
        if (current) {
            const margin = (ARROW_OFFSET + (ARROW_SIZE * 2)) / this.view.zoom;
            if (contains({ x: current.x - margin, y: current.y - margin, w: current.w + (margin * 2), h: current.h + (margin * 2) }, world)) {
                return;
            }
        }
        const box = this.hitBoxAt(world, (b) => b.whole && b.connectable);
        if (box?.id === this.hover?.id) {
            return;
        }
        this.clearHover();
        if (box) {
            this.hover = { id: box.id, shown: false, timer: setTimeout(() => this.showArrows(), ARROW_DELAY) };
        }
    }

    showArrows() {
        if (!this.hover || this.drag || this.press) {
            return;
        }
        this.hover.shown = true;
        this.drawSelection();
    }

    clearHover() {
        if (!this.hover) {
            return;
        }
        clearTimeout(this.hover.timer);
        const shown = this.hover.shown;
        this.hover = null;
        if (shown) {
            this.drawSelection();
        }
    }

    requestFrame() {
        if (!this.frameRequest) {
            this.frameRequest = requestAnimationFrame(() => {
                this.frameRequest = 0;
                this.renderDrag();
            });
        }
    }

    edgeSpeed(point) {
        if (!this.options.panEnabled) {
            return { x: 0, y: 0 };
        }
        const axis = (value, size) => {
            if (value < EDGE_ZONE) {
                return -EDGE_SPEED * Math.min(1, (EDGE_ZONE - value) / EDGE_ZONE);
            }
            if (value > size - EDGE_ZONE) {
                return EDGE_SPEED * Math.min(1, (value - (size - EDGE_ZONE)) / EDGE_ZONE);
            }
            return 0;
        };
        return { x: axis(point.x, this.size.width), y: axis(point.y, this.size.height) };
    }

    startDrag(press) {
        const hit = press.hit;
        const editing = hit.kind !== 'background';
        if (editing && this.waiting) {
            return false;
        }
        let started = false;
        switch (hit.kind) {
            case 'element':
                started = hit.box && this.startMove(press);
                break;
            case 'handle':
                started = this.startResize(press);
                break;
            case 'port':
                started = this.startConnect(press, { element: hit.id, port: hit.port }, null);
                break;
            case 'arrow':
                started = this.startConnect(press, { element: hit.id, port: null }, hit.side);
                break;
            case 'end':
                started = this.startReconnect(press);
                break;
            case 'badge':
                break;
            default:
                started = this.startBoxSelection(press);
                break;
        }
        if (started) {
            this.drag.point = press.point;
            this.drag.cursor = { move: 'mussel-diagram-dragging', resize: `mussel-diagram-resizing-${this.drag.handle}`, select: 'mussel-diagram-selecting' }[this.drag.kind] ?? null;
            if (this.drag.cursor) {
                this.root.classList.add(this.drag.cursor);
            }
        }
        return started;
    }

    renderDrag() {
        const drag = this.drag;
        if (!drag) {
            return;
        }
        const speed = this.edgeSpeed(drag.point);
        if ((speed.x !== 0) || (speed.y !== 0)) {
            this.view = { ...this.view, x: this.view.x - speed.x, y: this.view.y - speed.y };
            drag.panned = true;
            this.applyView();
            this.requestFrame();
        }
        drag.world = this.toWorld(drag.point);
        switch (drag.kind) {
            case 'move':
                this.updateMove(drag);
                break;
            case 'resize':
                this.updateResize(drag);
                break;
            case 'connect':
            case 'reconnect':
                this.updateConnect(drag);
                break;
            default:
                this.updateBoxSelection(drag);
                break;
        }
    }

    finishDrag() {
        const drag = this.drag;
        if (!drag) {
            return;
        }
        cancelAnimationFrame(this.frameRequest);
        this.frameRequest = 0;
        drag.world = this.toWorld(drag.point);
        switch (drag.kind) {
            case 'move':
                this.updateMove(drag);
                break;
            case 'resize':
                this.updateResize(drag);
                break;
            case 'connect':
            case 'reconnect':
                this.updateConnect(drag);
                break;
            default:
                break;
        }
        this.endDragState(drag);
        this.drag = null;
        this.clearMarks();
        this.layers.guides.replaceChildren();
        this.layers.candidates.replaceChildren();
        this.hideReason();
        switch (drag.kind) {
            case 'move':
                this.finishMove(drag);
                break;
            case 'resize':
                this.finishResize(drag);
                break;
            case 'connect':
            case 'reconnect':
                this.finishConnect(drag);
                break;
            default:
                this.finishBoxSelection(drag);
                break;
        }
        this.drawSelection();
    }

    cancelDrag() {
        const drag = this.drag;
        if (drag) {
            this.markSplit(drag, null);
        }
        if (!drag) {
            return;
        }
        cancelAnimationFrame(this.frameRequest);
        this.frameRequest = 0;
        restore(drag.saved ?? new Map());
        this.drag = null;
        this.endDragState(drag);
        this.clearMarks();
        this.layers.guides.replaceChildren();
        this.layers.candidates.replaceChildren();
        this.layers.preview.replaceChildren();
        this.hideReason();
        this.drawSelection();
    }

    endDragState(drag) {
        this.root.classList.remove('mussel-diagram-connecting');
        if (drag.cursor) {
            this.root.classList.remove(drag.cursor);
        }
        this.setNotAllowed(false);
        for (const element of drag.revealed ?? []) {
            element.removeAttribute('data-revealed');
        }
        if (drag.panned) {
            this.notifyViewport();
        }
    }

    save(saved, element, attribute) {
        if (!element) {
            return;
        }
        let entry = saved.get(element);
        if (!entry) {
            entry = new Map();
            saved.set(element, entry);
        }
        if (!entry.has(attribute)) {
            entry.set(attribute, element.getAttribute(attribute));
        }
    }

    mark(element, attribute) {
        if (element && !element.hasAttribute(attribute)) {
            element.setAttribute(attribute, '');
            this.marks.push([element, attribute]);
        }
    }

    clearMarks() {
        for (const [element, attribute] of this.marks) {
            element.removeAttribute(attribute);
        }
        this.marks = [];
    }

    snapLines(excluded) {
        if (!this.options.snapToElements) {
            return { x: [], y: [] };
        }
        const area = this.visibleArea();
        const boxes = [];
        for (const box of this.table.boxes.values()) {
            if ((box.kind === 'overlay') || excluded.has(box.id)) {
                continue;
            }
            if ((box.x + box.w < area.x) || (box.x > area.x + area.w) || (box.y + box.h < area.y) || (box.y > area.y + area.h)) {
                continue;
            }
            boxes.push(box);
        }
        return collectLines(boxes);
    }

    snapAxisOrGrid(lines, values, anchor, alt) {
        if (alt) {
            return { delta: 0, line: null };
        }
        const element = this.options.snapToElements ? snapAxis(lines, values, this.options.snapTolerance / this.view.zoom) : null;
        if (element) {
            return { delta: element.delta, line: element.value };
        }
        if (this.options.snapToGrid && (anchor !== null)) {
            return { delta: gridSnap(anchor, this.options.gridSize) - anchor, line: null };
        }
        return { delta: 0, line: null };
    }

    drawGuides(drag, rect) {
        const guides = [];
        if (!drag.alt) {
            const xs = [rect.x, rect.x + (rect.w / 2), rect.x + rect.w];
            const ys = [rect.y, rect.y + (rect.h / 2), rect.y + rect.h];
            for (const line of drag.snappedX ? matchingLines(drag.lines.x, xs) : []) {
                const top = Math.min(rect.y, line.box.y);
                const bottom = Math.max(rect.y + rect.h, line.box.y + line.box.h);
                guides.push(svg('line', { class: 'mussel-diagram-guide', x1: line.value, y1: top, x2: line.value, y2: bottom }));
            }
            for (const line of drag.snappedY ? matchingLines(drag.lines.y, ys) : []) {
                const left = Math.min(rect.x, line.box.x);
                const right = Math.max(rect.x + rect.w, line.box.x + line.box.w);
                guides.push(svg('line', { class: 'mussel-diagram-guide', x1: left, y1: line.value, x2: right, y2: line.value }));
            }
        }
        this.layers.guides.replaceChildren(...guides);
    }

    startMove(press) {
        if (!this.options.moveEnabled) {
            return false;
        }
        let ids = [...this.selection];
        if (!this.selection.has(press.hit.id)) {
            if (this.options.selectionEnabled && this.table.boxes.get(press.hit.id)?.selectable) {
                this.setSelection([press.hit.id]);
            }
            ids = [press.hit.id];
        }
        const roots = ids
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
        if (boxes.length === 0) {
            return false;
        }
        const left = Math.min(...boxes.map((b) => b.x));
        const top = Math.min(...boxes.map((b) => b.y));
        const bounds = { x: left, y: top, w: Math.max(...boxes.map((b) => b.x + b.w)) - left, h: Math.max(...boxes.map((b) => b.y + b.h)) - top };
        const pressed = this.table.boxes.get(press.hit.id);
        const anchor = pressed && moving.has(pressed.id) ? gridAnchor(pressed, pressed.kind === 'node') : gridAnchor(bounds, false);
        const excluded = new Set(moving);
        for (const id of roots) {
            for (const ancestor of this.table.ancestors(id)) {
                excluded.add(ancestor);
            }
        }
        const rootBoxes = roots.map((id) => this.table.boxes.get(id));
        const parents = new Set(rootBoxes.map((b) => b.parent ?? null));
        const types = rootBoxes.map((b) => this.schema.types.get(b.type));
        const canReparent = (parents.size === 1) && rootBoxes.every((b, i) => (b.kind !== 'overlay') && b.registered && !types[i]?.stay);
        const stays = rootBoxes.filter((b, i) => types[i]?.stay && b.parent).map((b) => ({ box: b, parent: this.table.boxes.get(b.parent) })).filter((x) => x.parent && !this.schema.types.get(x.parent.type)?.content);
        this.drag = {
            kind: 'move',
            roots,
            moving,
            live,
            links,
            bounds,
            anchor,
            lines: this.snapLines(excluded),
            canReparent,
            parent: canReparent ? [...parents][0] : undefined,
            rootTypes: rootBoxes.map((b) => b.type),
            stays,
            startWorld: press.world,
            dx: 0,
            dy: 0,
            saved: new Map(),
            base: this.table.version,
            queued: this.pending.length > 0,
            elements: new Map(),
            badgeSets: new Map(),
            paths: new Map(),
            target: undefined,
        };
        for (const id of moving) {
            const element = this.element(id);
            if (element) {
                this.drag.elements.set(id, element);
                this.save(this.drag.saved, element, 'transform');
            }
            const set = this.badgeSet(id);
            if (set) {
                this.drag.badgeSets.set(id, set);
                this.save(this.drag.saved, set, 'transform');
            }
        }
        for (const link of links) {
            this.drag.paths.set(link.id, this.linkPaths(link.id, this.drag.saved));
        }
        return true;
    }

    linkPaths(id, saved) {
        const element = this.element(id);
        const paths = { hit: element?.querySelector('.mussel-diagram-link-hit'), line: element?.querySelector('.mussel-diagram-link-line'), badges: this.badgeSet(id) };
        this.save(saved, paths.hit, 'd');
        this.save(saved, paths.line, 'd');
        this.save(saved, paths.badges, 'transform');
        return paths;
    }

    badgeSet(id) {
        return this.badgeLayer?.querySelector(`:scope > [data-for="${CSS.escape(id)}"]`) ?? null;
    }

    updateMove(drag) {
        let dx = drag.world.x - drag.startWorld.x;
        let dy = drag.world.y - drag.startWorld.y;
        const rect = { x: drag.bounds.x + dx, y: drag.bounds.y + dy, w: drag.bounds.w, h: drag.bounds.h };
        const snapX = this.snapAxisOrGrid(drag.lines.x, [rect.x, rect.x + (rect.w / 2), rect.x + rect.w], drag.anchor.x + dx, drag.alt);
        const snapY = this.snapAxisOrGrid(drag.lines.y, [rect.y, rect.y + (rect.h / 2), rect.y + rect.h], drag.anchor.y + dy, drag.alt);
        dx += snapX.delta;
        dy += snapY.delta;
        for (const { box, parent } of drag.stays) {
            const padding = this.schema.types.get(parent.type)?.padding ?? [0, 0, 0, 0];
            const minX = parent.x + padding[0] - box.x;
            const maxX = parent.x + parent.w - padding[2] - box.x - box.w;
            const minY = parent.y + padding[1] - box.y;
            const maxY = parent.y + parent.h - padding[3] - box.y - box.h;
            dx = Math.min(Math.max(dx, minX), Math.max(minX, maxX));
            dy = Math.min(Math.max(dy, minY), Math.max(minY, maxY));
        }
        drag.dx = dx;
        drag.dy = dy;
        drag.snappedX = snapX.line !== null;
        drag.snappedY = snapY.line !== null;
        if (drag.live) {
            this.placeMoved(drag);
        }
        this.updateDropTarget(drag);
        this.updateSplit(drag);
        this.drawGuides(drag, { x: drag.bounds.x + dx, y: drag.bounds.y + dy, w: drag.bounds.w, h: drag.bounds.h });
        this.drawSelection();
    }

    updateSplit(drag) {
        const root = drag.roots.length === 1 ? this.table.boxes.get(drag.roots[0]) : null;
        const rect = this.root.getBoundingClientRect();
        const client = drag.point ? { x: drag.point.x + rect.left, y: drag.point.y + rect.top } : null;
        const split = (root?.kind === 'node') && (drag.links.size === 0) && !drag.alt && client
            ? this.spliceAt(client, { ...root, x: root.x + drag.dx, y: root.y + drag.dy, parent: drag.canReparent && (drag.target !== undefined) ? drag.target : root.parent }, root.id)
            : null;
        this.markSplit(drag, split);
    }

    markSplit(state, split) {
        if (state.split?.linkId === split?.linkId) {
            return;
        }
        state.split?.element?.removeAttribute('data-split-target');
        state.split = split;
        split?.element?.setAttribute('data-split-target', '');
    }

    updateDropTarget(drag) {
        if (!drag.canReparent) {
            drag.target = undefined;
            return;
        }
        const group = this.boxAt(drag.world, (box) => (box.kind === 'group') && !drag.moving.has(box.id) && drag.rootTypes.every((type) => canNest(this.schema, type, box)));
        const target = group?.id ?? null;
        const blocked = (target === null) && (drag.parent !== null) && !drag.rootTypes.every((type) => canNest(this.schema, type, null));
        if (blocked !== !!drag.nestBlocked) {
            drag.nestBlocked = blocked;
            this.setNotAllowed(blocked);
            if (blocked) {
                this.showReason(this.reasonText('nesting'), drag.point, true);
            } else {
                this.hideReason();
            }
        }
        if (target === drag.target) {
            return;
        }
        drag.target = target;
        this.clearMarks();
        if ((target !== null) && (target !== drag.parent)) {
            this.mark(this.element(target), 'data-drop-target');
        }
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
        for (const [id, set] of drag.badgeSets) {
            const box = this.table.boxes.get(id);
            if (box && (box.kind !== 'overlay')) {
                set.setAttribute('transform', `translate(${formatNumber(box.x + drag.dx)},${formatNumber(box.y + drag.dy)})`);
            }
        }
        for (const link of drag.links) {
            this.drawLink(link, moved(link.source), moved(link.target), drag.paths.get(link.id));
        }
    }

    drawLink(link, source, target, paths) {
        if (!source || !target || !paths) {
            return;
        }
        const points = routeLink(link, source, target, this.options.gridSize);
        paths.hit?.setAttribute('d', toPath(points, link.form));
        paths.line?.setAttribute('d', toPath(points, link.form, link.sourceCut, link.targetCut));
        if (paths.badges) {
            const middle = routeMidpoint(points);
            paths.badges.setAttribute('transform', `translate(${formatNumber(middle.x)},${formatNumber(middle.y)})`);
        }
    }

    finishMove(drag) {
        if ((drag.dx === 0) && (drag.dy === 0)) {
            restore(drag.saved);
            return;
        }
        if (drag.nestBlocked) {
            restore(drag.saved);
            this.showReason(this.reasonText('nesting'), drag.point);
            return;
        }
        this.placeMoved(drag);
        const reparent = drag.canReparent && (drag.target !== undefined) && (drag.target !== drag.parent);
        const parentId = reparent ? drag.target : null;
        const previous = new Map();
        for (const id of drag.moving) {
            const box = this.table.boxes.get(id);
            if (box) {
                previous.set(id, box);
                const parent = reparent && drag.roots.includes(id) ? parentId : box.parent;
                this.table.boxes.set(id, { ...box, x: box.x + drag.dx, y: box.y + drag.dy, parent });
            }
        }
        if (reparent) {
            this.table.index();
        }
        const splitLinkId = drag.split?.linkId ?? null;
        this.markSplit(drag, null);
        this.commit(drag, { saved: drag.saved, previous }, () => this.dotnet.invokeMethodAsync('CommitMoveAsync', drag.roots, drag.dx, drag.dy, reparent, parentId, splitLinkId, this.baseOf(drag), this.rejections, [...this.selection]));
    }

    startResize(press) {
        const box = this.table.boxes.get(press.hit.id);
        if (!box?.resizable || !this.options.resizeEnabled) {
            return false;
        }
        const type = this.schema.types.get(box.type);
        const excluded = new Set([box.id, ...this.table.ancestors(box.id), ...this.table.descendants(box.id)]);
        const links = this.table.linksOf([box.id]);
        const drag = {
            kind: 'resize',
            id: box.id,
            box,
            handle: press.hit.handle,
            limits: {
                minW: type?.minW ?? MIN_SIZE,
                minH: type?.minH ?? MIN_SIZE,
                inner: box.kind === 'group' ? this.table.innerBounds(box.id, type?.padding) : null,
            },
            lines: this.snapLines(excluded),
            links,
            paths: new Map(),
            saved: new Map(),
            startWorld: press.world,
            rect: { x: box.x, y: box.y, w: box.w, h: box.h },
            base: this.table.version,
            queued: this.pending.length > 0,
        };
        for (const link of links) {
            drag.paths.set(link.id, this.linkPaths(link.id, drag.saved));
        }
        this.drag = drag;
        return true;
    }

    updateResize(drag) {
        const dx = drag.world.x - drag.startWorld.x;
        const dy = drag.world.y - drag.startWorld.y;
        const edges = resizeEdges(drag.box, drag.handle, dx, dy);
        drag.snappedX = false;
        drag.snappedY = false;
        const snapEdge = (lines, value) => this.snapAxisOrGrid(lines, [value], value, drag.alt);
        for (const [name, axis] of [['left', 'x'], ['right', 'x'], ['top', 'y'], ['bottom', 'y']]) {
            const moving = { left: 'w', right: 'e', top: 'n', bottom: 's' }[name];
            if (!drag.handle.includes(moving)) {
                continue;
            }
            const snap = snapEdge(drag.lines[axis], edges[name]);
            edges[name] += snap.delta;
            if (snap.line !== null) {
                drag[axis === 'x' ? 'snappedX' : 'snappedY'] = true;
            }
        }
        drag.rect = limitResize(edges, drag.handle, drag.limits);
        const resized = { ...drag.box, ...drag.rect };
        for (const link of drag.links) {
            const source = link.source === drag.id ? resized : this.table.boxes.get(link.source);
            const target = link.target === drag.id ? resized : this.table.boxes.get(link.target);
            this.drawLink(link, source, target, drag.paths.get(link.id));
        }
        this.drawGuides(drag, drag.rect);
        this.layers.preview.replaceChildren(svg('rect', { class: 'mussel-diagram-resize-frame', x: drag.rect.x, y: drag.rect.y, width: drag.rect.w, height: drag.rect.h }));
    }

    finishResize(drag) {
        const { rect, box } = drag;
        this.layers.preview.replaceChildren();
        if ((rect.x === box.x) && (rect.y === box.y) && (rect.w === box.w) && (rect.h === box.h)) {
            restore(drag.saved);
            return;
        }
        const frame = svg('rect', { class: 'mussel-diagram-resize-frame', x: rect.x, y: rect.y, width: rect.w, height: rect.h });
        this.layers.pending.append(frame);
        const previous = new Map([[box.id, box]]);
        this.table.boxes.set(box.id, { ...box, ...rect });
        this.commit(drag, { saved: drag.saved, previous, overlays: [frame] }, () => this.dotnet.invokeMethodAsync('CommitResizeAsync', box.id, rect.x, rect.y, rect.w, rect.h, this.baseOf(drag), this.rejections, [...this.selection]));
    }

    connectionTargets(check) {
        const area = this.visibleArea();
        const margin = Math.max(area.w, area.h) / 2;
        const targets = [];
        for (const box of this.table.boxes.values()) {
            if (!box.registered || (box.x + box.w < area.x - margin) || (box.x > area.x + area.w + margin) || (box.y + box.h < area.y - margin) || (box.y > area.y + area.h + margin)) {
                continue;
            }
            for (const port of box.ports ?? []) {
                if (!check({ element: box.id, port: port.id })) {
                    targets.push({ box, port });
                }
            }
            if (box.whole && !check({ element: box.id, port: null })) {
                targets.push({ box, port: null });
            }
        }
        return targets;
    }

    linkTypeFor(from) {
        const box = this.table.boxes.get(from.element);
        return findPort(box, from.port)?.linkType ?? this.options.linkType ?? this.schema.defaultLinkType;
    }

    startConnect(press, from, side) {
        const box = this.table.boxes.get(from.element);
        if (!this.options.connectEnabled || !box?.connectable || (!from.port && !box.whole)) {
            return false;
        }
        const linkType = this.linkTypeFor(from);
        const check = (target) => checkConnection(this.table, this.schema, from, target, linkType, null);
        this.drag = {
            kind: 'connect',
            from,
            side,
            linkType,
            check,
            targets: this.connectionTargets(check),
            target: null,
            saved: new Map(),
            base: this.table.version,
            queued: this.pending.length > 0,
        };
        this.root.classList.add('mussel-diagram-connecting');
        this.revealPorts(this.drag);
        this.drawCandidates(this.drag);
        return true;
    }

    revealPorts(drag) {
        drag.revealed = [];
        for (const target of drag.targets) {
            if (!target.port?.hidden) {
                continue;
            }
            const element = this.element(target.box.id)?.querySelector(`.mussel-diagram-port[data-port="${CSS.escape(target.port.id)}"]`);
            if (element) {
                element.setAttribute('data-revealed', '');
                drag.revealed.push(element);
            }
        }
    }

    rejectionAt(drag, point, fromEnd) {
        const exact = (PORT_RADIUS + 2) / this.view.zoom;
        for (const box of this.table.boxes.values()) {
            for (const port of box.ports ?? []) {
                if (port.hidden || ((box.id === fromEnd.element) && (port.id === (fromEnd.port ?? null)))) {
                    continue;
                }
                const position = portPosition(box, port.side, port.ratio, port.offset, port.along);
                if (Math.hypot(position.x - point.x, position.y - point.y) > exact) {
                    continue;
                }
                const reason = drag.check({ element: box.id, port: port.id });
                return reason ? { reason, element: this.element(box.id)?.querySelector(`.mussel-diagram-port[data-port="${CSS.escape(port.id)}"]`) } : null;
            }
        }
        const under = this.hitBoxAt(point, (b) => b.id !== fromEnd.element, true);
        const reason = under ? drag.check({ element: under.id, port: null }) : null;
        return reason ? { reason, element: this.element(under.id) } : null;
    }

    previewLine(linkTypeName, d) {
        const type = this.schema.linkTypes.get(linkTypeName);
        const attributes = { class: 'mussel-diagram-link-line mussel-diagram-preview-link', d };
        if (type?.style) {
            attributes.style = type.style;
        }
        if (type?.sourceMarker) {
            attributes['marker-start'] = `url(#${type.sourceMarker})`;
        }
        if (type?.targetMarker) {
            attributes['marker-end'] = `url(#${type.targetMarker})`;
        }
        return svg('path', attributes);
    }

    startReconnect(press) {
        const link = this.table.links.get(press.hit.id);
        if (!link?.connectable || !this.options.connectEnabled) {
            return false;
        }
        const side = press.hit.side;
        const fixed = side === 'source' ? { element: link.target, port: link.targetPort ?? null } : { element: link.source, port: link.sourcePort ?? null };
        const check = (end) => (side === 'source'
            ? checkConnection(this.table, this.schema, end, fixed, link.type, link.id)
            : checkConnection(this.table, this.schema, fixed, end, link.type, link.id));
        const element = this.element(link.id);
        this.drag = {
            kind: 'reconnect',
            link,
            side,
            fixed,
            linkType: link.type,
            check,
            targets: this.connectionTargets(check),
            target: null,
            element,
            saved: new Map(),
            base: this.table.version,
            queued: this.pending.length > 0,
        };
        this.mark(element, 'data-reconnecting');
        this.root.classList.add('mussel-diagram-connecting');
        this.revealPorts(this.drag);
        this.drawCandidates(this.drag);
        return true;
    }

    drawCandidates(drag) {
        const radius = (PORT_RADIUS + 2) / this.view.zoom;
        const items = drag.targets.map((target) => (target.port
            ? svg('circle', { class: 'mussel-diagram-candidate', ...this.portCenter(target), r: radius })
            : svg('rect', { class: 'mussel-diagram-candidate', x: target.box.x, y: target.box.y, width: target.box.w, height: target.box.h })));
        this.layers.candidates.replaceChildren(...items);
    }

    portCenter(target) {
        const position = portPosition(target.box, target.port.side, target.port.ratio, target.port.offset, target.port.along);
        return { cx: position.x, cy: position.y };
    }

    findTarget(drag, point) {
        const radius = this.options.connectionRadius / this.view.zoom;
        const exact = PORT_RADIUS / this.view.zoom;
        let best = null;
        let bestDistance = Number.POSITIVE_INFINITY;
        for (const target of drag.targets) {
            if (!target.port) {
                continue;
            }
            const position = portPosition(target.box, target.port.side, target.port.ratio, target.port.offset, target.port.along);
            const distance = Math.hypot(position.x - point.x, position.y - point.y);
            if (distance <= exact) {
                return target;
            }
            if ((distance <= radius) && (distance < bestDistance)) {
                best = target;
                bestDistance = distance;
            }
        }
        if (best) {
            return best;
        }
        const wholes = new Map(drag.targets.filter((t) => !t.port).map((t) => [t.box.id, t]));
        const box = this.hitBoxAt(point, (b) => wholes.has(b.id), true);
        return box ? wholes.get(box.id) : null;
    }

    updateConnect(drag) {
        const point = drag.world;
        const target = this.findTarget(drag, point);
        drag.target = target;
        this.clearMarks();
        this.hideReason();
        const fromEnd = drag.kind === 'connect' ? drag.from : drag.fixed;
        const fromBox = this.table.boxes.get(fromEnd.element);
        const fromPort = findPort(fromBox, fromEnd.port);
        let endAnchor;
        if (target) {
            const toward = fromPort ? portPosition(fromBox, fromPort.side, fromPort.ratio, fromPort.offset, fromPort.along) : centerOf(fromBox);
            endAnchor = anchorOf(target.box, target.port, toward);
            if (target.port) {
                this.mark(this.element(target.box.id)?.querySelector(`.mussel-diagram-port[data-port="${CSS.escape(target.port.id)}"]`), 'data-candidate');
            } else {
                this.mark(this.element(target.box.id), 'data-candidate');
            }
        } else {
            endAnchor = { x: point.x, y: point.y, side: 'left' };
        }
        drag.rejection = target ? null : this.rejectionAt(drag, point, fromEnd);
        this.setNotAllowed(!!drag.rejection);
        if (drag.rejection) {
            this.mark(drag.rejection.element, 'data-not-allowed');
            this.showReason(this.reasonText(drag.rejection.reason), drag.point, true);
        }
        const toward = target ? { x: endAnchor.x, y: endAnchor.y } : point;
        const startAnchor = fromPort ? anchorOf(fromBox, fromPort, toward) : anchorOf(fromBox, null, toward);
        if (!target) {
            endAnchor.side = isHorizontal(startAnchor.side) ? (point.x >= startAnchor.x ? 'left' : 'right') : (point.y >= startAnchor.y ? 'top' : 'bottom');
        }
        const type = this.schema.linkTypes.get(drag.linkType) ?? DEFAULT_LINK_TYPE;
        const sourceAnchor = (drag.kind === 'reconnect') && (drag.side === 'source') ? endAnchor : startAnchor;
        const targetAnchor = (drag.kind === 'reconnect') && (drag.side === 'source') ? startAnchor : endAnchor;
        const points = routeBetween(sourceAnchor, targetAnchor, type.form, type.stub, this.options.gridSize);
        drag.preview = this.previewLine(drag.linkType, toPath(points, type.form, type.sourceCut, type.targetCut));
        this.layers.preview.replaceChildren(drag.preview);
    }

    finishConnect(drag) {
        this.layers.preview.replaceChildren();
        const target = drag.target;
        if (!target) {
            if (drag.rejection) {
                this.showReason(this.reasonText(drag.rejection.reason), drag.point);
            } else if (drag.kind === 'connect') {
                const fromBox = this.table.boxes.get(drag.from.element);
                const under = this.hitBoxAt(drag.world, null, true);
                if (!under && fromBox) {
                    this.openPicker(drag.from, { kind: 'point', point: drag.world }, drag.point);
                }
            }
            this.clearMarks();
            return;
        }
        const end = { element: target.box.id, port: target.port?.id ?? null };
        if ((drag.kind === 'connect') && (end.element === drag.from.element) && (end.port === (drag.from.port ?? null))) {
            return;
        }
        const overlays = drag.preview ? [drag.preview] : [];
        for (const overlay of overlays) {
            this.layers.pending.append(overlay);
        }
        if (drag.kind === 'connect') {
            this.commit(drag, { saved: new Map(), previous: new Map(), overlays }, () => this.dotnet.invokeMethodAsync(
                'CommitConnectAsync', drag.from.element, drag.from.port ?? null, end.element, end.port, drag.linkType, this.baseOf(drag), this.rejections, [...this.selection]));
            return;
        }
        const link = drag.link;
        const saved = new Map();
        this.save(saved, drag.element, 'data-reconnecting');
        drag.element?.setAttribute('data-reconnecting', '');
        const previousLinks = new Map([[link.id, link]]);
        this.table.links.set(link.id, drag.side === 'source' ? { ...link, source: end.element, sourcePort: end.port } : { ...link, target: end.element, targetPort: end.port });
        this.table.index();
        this.commit(drag, { saved, previous: new Map(), previousLinks, overlays, reconnecting: drag.element }, () => this.dotnet.invokeMethodAsync(
            'CommitReconnectAsync', link.id, drag.side, end.element, end.port, this.baseOf(drag), this.rejections, [...this.selection]));
    }

    startBoxSelection(press) {
        if (!this.options.boxSelectionEnabled) {
            return false;
        }
        this.drag = { kind: 'select', startWorld: press.world, additive: press.shift, base: press.shift ? new Set(this.selection) : new Set() };
        return true;
    }

    selectionRect(drag) {
        const x = Math.min(drag.startWorld.x, drag.world.x);
        const y = Math.min(drag.startWorld.y, drag.world.y);
        return { x, y, w: Math.abs(drag.world.x - drag.startWorld.x), h: Math.abs(drag.world.y - drag.startWorld.y) };
    }

    updateBoxSelection(drag) {
        const rect = this.selectionRect(drag);
        this.layers.preview.replaceChildren(svg('rect', { class: 'mussel-diagram-box-selection', x: rect.x, y: rect.y, width: rect.w, height: rect.h }));
    }

    finishBoxSelection(drag) {
        this.layers.preview.replaceChildren();
        const rect = this.selectionRect(drag);
        const inside = (b) => (b.x >= rect.x) && (b.y >= rect.y) && (b.x + b.w <= rect.x + rect.w) && (b.y + b.h <= rect.y + rect.h);
        const next = new Set(drag.base);
        for (const box of this.table.boxes.values()) {
            if (box.selectable && inside(box)) {
                next.add(box.id);
            }
        }
        for (const link of this.table.links.values()) {
            const path = link.selectable ? this.element(link.id)?.querySelector('.mussel-diagram-link-hit') : null;
            const bounds = path?.getBBox?.();
            if (bounds && inside({ x: bounds.x, y: bounds.y, w: bounds.width, h: bounds.height })) {
                next.add(link.id);
            }
        }
        this.setSelection([...next]);
    }

    pickerTypes(from, placement) {
        const fromBox = from ? this.table.boxes.get(from.element) : null;
        const linkType = from ? this.linkTypeFor(from) : null;
        const items = [];
        for (const type of this.schema.list) {
            if (type.overlay || ((type.max ?? null) !== null && (this.table.countOfType(type.name) >= type.max))) {
                continue;
            }
            if (!from && !this.inPalette(type)) {
                continue;
            }
            const bounds = this.placementBounds(placement, type);
            const parent = this.placementParent(placement, type, bounds, fromBox);
            if (parent === undefined) {
                continue;
            }
            if (!from) {
                items.push({ type, bounds, parent, end: null });
                continue;
            }
            const virtual = {
                id: '\u0000placed',
                type: type.name,
                kind: type.group ? 'group' : 'node',
                ...bounds,
                parent,
                registered: true,
                connectable: type.connectable,
                whole: type.whole,
                ports: type.ports,
            };
            this.table.boxes.set(virtual.id, virtual);
            let end;
            try {
                end = [...(type.ports ?? []).map((p) => p.id), ...(type.whole ? [null] : [])]
                    .find((candidate) => !checkConnection(this.table, this.schema, from, { element: virtual.id, port: candidate }, linkType, null));
            } finally {
                this.table.boxes.delete(virtual.id);
            }
            if (end !== undefined) {
                items.push({ type, bounds, parent, end });
            }
        }
        const rank = (item) => this.schema.order.get(item.type.name) ?? Number.MAX_SAFE_INTEGER;
        return items.sort((a, b) => rank(a) - rank(b));
    }

    placementBounds(placement, type) {
        const size = { w: type.w, h: type.h };
        let bounds;
        if (placement.kind === 'point') {
            bounds = { x: placement.point.x - (size.w / 2), y: placement.point.y - (size.h / 2), w: size.w, h: size.h };
        } else {
            const along = placement.port ? portPosition(placement.box, placement.port.side, placement.port.ratio, placement.port.offset, placement.port.along) : null;
            bounds = placeBeside(placement.box, placement.side, size, this.options.quickAddSpacing, along ? (isHorizontal(placement.side) ? along.y : along.x) : undefined);
        }
        if (this.options.snapToGrid) {
            const anchor = gridAnchor(bounds, !type.group);
            bounds.x += gridSnap(anchor.x, this.options.gridSize) - anchor.x;
            bounds.y += gridSnap(anchor.y, this.options.gridSize) - anchor.y;
        }
        return bounds;
    }

    placementParent(placement, type, bounds, fromBox) {
        if ((placement.kind === 'beside') && fromBox) {
            const parent = fromBox.parent ? this.table.boxes.get(fromBox.parent) : null;
            if (canNest(this.schema, type.name, parent)) {
                return parent?.id ?? null;
            }
        }
        const center = centerOf(bounds);
        const group = this.boxAt(center, (box) => (box.kind === 'group') && canNest(this.schema, type.name, box));
        if (group) {
            return group.id;
        }
        return canNest(this.schema, type.name, null) ? null : undefined;
    }

    openPicker(from, placement, screenPoint) {
        this.closePicker();
        const items = this.pickerTypes(from, placement);
        if (items.length === 0) {
            return;
        }
        const element = document.createElement('div');
        element.className = 'mussel-diagram-picker mussel-diagram-nowheel';
        const search = document.createElement('input');
        search.className = 'mussel-diagram-picker-search';
        search.type = 'text';
        search.setAttribute('aria-label', this.schema.texts.searchPlaceholder);
        search.placeholder = this.schema.texts.searchPlaceholder;
        const list = document.createElement('div');
        list.className = 'mussel-diagram-picker-list';
        list.setAttribute('role', 'listbox');
        element.append(search, list);
        this.root.append(element);
        this.picker = { element, search, list, items, shown: items, active: 0, from, placement };
        const left = Math.min(Math.max(0, screenPoint.x + 8), Math.max(0, this.size.width - 220));
        const top = Math.min(Math.max(0, screenPoint.y + 8), Math.max(0, this.size.height - 240));
        element.style.left = `${left}px`;
        element.style.top = `${top}px`;
        search.addEventListener('input', () => this.filterPicker());
        search.addEventListener('keydown', (e) => this.onPickerKey(e));
        list.addEventListener('pointerdown', (e) => {
            const option = e.target.closest('[data-type]');
            if (option) {
                e.preventDefault();
                this.choosePicker(Number(option.getAttribute('data-index')));
            }
        });
        this.picker.outside = (e) => {
            if (!element.contains(e.target)) {
                this.closePicker();
            }
        };
        document.addEventListener('pointerdown', this.picker.outside, true);
        this.renderPicker();
        search.focus({ preventScroll: true });
    }

    filterPicker() {
        const picker = this.picker;
        if (this.options.hostSearch && this.dotnet) {
            const token = (picker.token ?? 0) + 1;
            picker.token = token;
            this.dotnet.invokeMethodAsync('SearchTypes', picker.search.value, picker.items.map((x) => x.type.name)).then((names) => {
                if ((this.picker !== picker) || (picker.token !== token)) {
                    return;
                }
                picker.shown = names.map((name) => picker.items.find((x) => x.type.name === name)).filter((x) => x);
                picker.active = 0;
                this.renderPicker();
            }).catch(() => {});
            return;
        }
        const types = searchTypes(picker.items.map((x) => x.type), picker.search.value);
        picker.shown = types.map((type) => picker.items.find((x) => x.type === type));
        picker.active = 0;
        this.renderPicker();
    }

    renderPicker() {
        const picker = this.picker;
        const options = picker.shown.map((item, index) => {
            const option = document.createElement('div');
            option.className = 'mussel-diagram-picker-item';
            option.setAttribute('role', 'option');
            option.setAttribute('data-type', item.type.name);
            option.setAttribute('data-index', String(index));
            option.setAttribute('aria-selected', index === picker.active ? 'true' : 'false');
            const icon = svg('svg', { class: 'mussel-diagram-picker-icon', viewBox: '0 0 24 24' });
            if (item.type.icon) {
                option.setAttribute('data-icon', item.type.icon);
                icon.append(svg('use', { href: `#${item.type.icon}`, width: 24, height: 24 }));
            } else {
                icon.append(shapeElement(item.type, { x: 3, y: 6, w: 18, h: 12 }, item.type.style ? { class: 'mussel-diagram-picker-shape', style: item.type.style } : { class: 'mussel-diagram-picker-shape' }));
            }
            const label = document.createElement('span');
            label.textContent = item.type.label;
            option.append(icon, label);
            return option;
        });
        if (options.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'mussel-diagram-picker-empty';
            empty.textContent = this.schema.texts.noMatches;
            options.push(empty);
        }
        picker.list.replaceChildren(...options);
        picker.observer ??= new IntersectionObserver((entries) => this.wantIcons(entries.filter((x) => x.isIntersecting).map((x) => x.target.getAttribute('data-icon'))), { root: picker.list });
        picker.observer.disconnect();
        for (const option of options) {
            if (option.hasAttribute('data-icon')) {
                picker.observer.observe(option);
            }
        }
        this.drawPickerPreview();
    }

    drawPickerPreview() {
        const item = this.picker?.shown[this.picker.active];
        if (!item) {
            this.layers.preview.replaceChildren();
            return;
        }
        const from = this.picker.from;
        if (!from) {
            this.layers.preview.replaceChildren(shapeElement(item.type, item.bounds, { class: 'mussel-diagram-place-frame' }));
            return;
        }
        const fromBox = this.table.boxes.get(from.element);
        const linkType = this.linkTypeFor(from);
        const type = this.schema.linkTypes.get(linkType) ?? DEFAULT_LINK_TYPE;
        const virtual = { ...item.bounds, id: '\u0000placed', ports: item.type.ports };
        const link = { sourcePort: from.port, targetPort: item.end, form: type.form, stub: type.stub };
        const points = fromBox ? routeLink(link, fromBox, virtual, this.options.gridSize) : [];
        this.layers.preview.replaceChildren(
            shapeElement(item.type, item.bounds, { class: 'mussel-diagram-place-frame' }),
            this.previewLine(linkType, toPath(points, type.form, type.sourceCut, type.targetCut)));
    }

    onPickerKey(event) {
        const picker = this.picker;
        if (!picker || event.isComposing) {
            return;
        }
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            const count = picker.shown.length;
            if (count > 0) {
                picker.active = (picker.active + (event.key === 'ArrowDown' ? 1 : count - 1)) % count;
                this.renderPicker();
            }
        } else if (event.key === 'Enter') {
            event.preventDefault();
            this.choosePicker(picker.active);
        } else if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            this.closePicker();
            this.root.focus({ preventScroll: true });
        }
    }

    choosePicker(index) {
        const picker = this.picker;
        const item = picker?.shown[index];
        if (!item || this.waiting) {
            return;
        }
        const from = picker.from;
        this.closePicker();
        const frame = shapeElement(item.type, item.bounds, { class: 'mussel-diagram-place-frame' });
        this.layers.pending.append(frame);
        const center = centerOf(item.bounds);
        const operation = { base: this.table.version, queued: this.pending.length > 0, point: { x: (center.x * this.view.zoom) + this.view.x, y: (center.y * this.view.zoom) + this.view.y } };
        if (!from) {
            this.commit(operation, { saved: new Map(), previous: new Map(), overlays: [frame], selectNew: true }, () => this.dotnet.invokeMethodAsync(
                'CommitDropAsync', item.type.name, item.bounds.x, item.bounds.y, item.bounds.w, item.bounds.h, item.parent, null, null, this.baseOf(operation), this.rejections, [...this.selection]));
            this.root.focus({ preventScroll: true });
            return;
        }
        const linkType = this.linkTypeFor(from);
        this.commit(operation, { saved: new Map(), previous: new Map(), overlays: [frame], selectNew: true }, () => this.dotnet.invokeMethodAsync(
            'CommitPlaceAsync', item.type.name, item.bounds.x, item.bounds.y, item.bounds.w, item.bounds.h, item.parent, from.element, from.port ?? null, linkType, this.baseOf(operation), this.rejections, [...this.selection]));
        this.root.focus({ preventScroll: true });
    }

    closePicker() {
        if (!this.picker) {
            return;
        }
        document.removeEventListener('pointerdown', this.picker.outside, true);
        this.picker.observer?.disconnect();
        this.picker.element.remove();
        this.picker = null;
        this.layers.preview.replaceChildren();
    }

    baseOf(operation) {
        return operation.queued ? Math.max(operation.base, this.lastVersion) : operation.base;
    }

    commit(operation, entry, call) {
        entry.version = null;
        entry.overlays = entry.overlays ?? [];
        entry.elements = [...entry.saved.keys()];
        this.pending.push(entry);
        if (!this.options.optimistic) {
            this.waiting = true;
            for (const element of entry.elements) {
                element.setAttribute('data-pending', '');
            }
            for (const overlay of entry.overlays) {
                overlay.setAttribute('data-pending', '');
            }
        }
        this.commits = this.commits.then(() => this.send(entry, operation, call));
        this.track(this.commits);
    }

    async send(entry, operation, call) {
        if (!this.pending.includes(entry)) {
            return;
        }
        let result = null;
        try {
            result = await call();
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
            if (entry.selectNew && result.elementId) {
                this.selectNew(result.elementId);
            }
            entry.accepted?.();
            return;
        }
        entry.rejected?.(result?.reason ?? null);
        this.revertFrom(entry);
        if (result?.reason) {
            this.showReason(result.reason, operation.point);
        }
    }

    revertFrom(entry) {
        const index = this.pending.indexOf(entry);
        if (index < 0) {
            return;
        }
        const reverted = this.pending.splice(index);
        let relinked = false;
        for (const item of reverted.reverse()) {
            restore(item.saved);
            for (const [id, box] of item.previous) {
                if (this.table.boxes.has(id)) {
                    this.table.boxes.set(id, box);
                }
            }
            for (const [id, link] of item.previousLinks ?? []) {
                if (this.table.links.has(id)) {
                    this.table.links.set(id, link);
                    relinked = true;
                }
            }
            for (const overlay of item.overlays) {
                overlay.remove();
            }
            for (const [node, text] of item.texts ?? []) {
                node.nodeValue = text;
            }
            item.reconnecting?.removeAttribute('data-reconnecting');
        }
        this.table.index();
        if (relinked) {
            this.table.index();
        }
        this.drawSelection();
    }

    settlePending() {
        const rendered = Number(this.root.dataset.version);
        const kept = [];
        for (const entry of this.pending) {
            if ((entry.version === null) || (rendered < entry.version)) {
                kept.push(entry);
                continue;
            }
            for (const overlay of entry.overlays) {
                overlay.remove();
            }
            entry.reconnecting?.removeAttribute('data-reconnecting');
        }
        this.pending = kept;
    }

    showReason(text, point, sticky) {
        this.hideReason();
        const reason = document.createElement('div');
        reason.className = 'mussel-diagram-reason';
        reason.textContent = text;
        const at = point ?? { x: this.size.width / 2, y: this.size.height / 2 };
        reason.style.left = `${at.x}px`;
        reason.style.top = `${at.y}px`;
        this.root.append(reason);
        this.reason = { element: reason, timer: sticky ? 0 : setTimeout(() => this.hideReason(), REASON_DURATION) };
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
        if (this.picker?.element.contains(event.target)) {
            return;
        }
        if (!event.isComposing && (event.keyCode !== 229) && (event.key !== 'Escape')) {
            const key = keyName(event, this.mac);
            const command = key ? this.commandMap.get(key) : undefined;
            if (command && !this.isTyping(event, key)) {
                if (this.drag || (this.waiting && !command.local)) {
                    event.preventDefault();
                    return;
                }
                if (command.enabled || (this.busy() && !command.local)) {
                    event.preventDefault();
                    this.runCommand(command);
                    return;
                }
            }
        }
        if (this.editor && (event.target === this.editor.input)) {
            return;
        }
        if ((event.code === 'Space') && !event.repeat && this.options.panEnabled && !this.isTyping(event, 'Space')) {
            event.preventDefault();
            this.setSpace(true);
            return;
        }
        if (event.key === 'Escape') {
            if (this.picker) {
                this.closePicker();
            } else if (this.drag) {
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

    selectNew(id) {
        if (this.table.boxes.has(id) || this.table.links.has(id)) {
            this.setSelection([id]);
        } else {
            this.selectWhenAdded = id;
        }
        this.root.focus({ preventScroll: true });
    }

    setCommands(commands) {
        this.commandMap = new Map();
        for (const command of commands) {
            for (const key of command.keys) {
                this.commandMap.set(key, command);
            }
        }
    }

    setPaletteTypes(names) {
        this.paletteTypes = names ? new Set(names) : null;
    }

    inPalette(type) {
        return type.palette && (!this.paletteTypes || this.paletteTypes.has(type.name));
    }

    isTyping(event, key) {
        const target = event.target;
        if ((target === this.root) || !target?.closest?.('input, textarea, select, button, [contenteditable]')) {
            return false;
        }
        if (/^F\d{1,2}$/.test(event.key ?? '')) {
            return false;
        }
        const plain = !event.ctrlKey && !event.metaKey && !event.altKey;
        return plain || TYPING_KEYS.has(key);
    }

    runCommand(command) {
        if (command.local) {
            switch (command.name) {
                case 'SelectAll':
                    this.selectAll();
                    return;
                case 'ClearSelection':
                    this.setSelection([]);
                    return;
                case 'ZoomIn':
                    this.zoomStep(1);
                    return;
                case 'ZoomOut':
                    this.zoomStep(-1);
                    return;
                case 'ResetZoom':
                    this.resetZoom();
                    return;
                case 'ZoomToFit':
                    this.fitAll(20);
                    return;
                case 'ZoomToSelection':
                    this.fitSelection(20);
                    return;
                default:
                    break;
            }
        }
        const selection = [...this.selection, ...(this.awaitedSelection ?? [])];
        const run = this.commandChain.then(async () => {
            await this.whenIdle();
            if (this.commandFor(command.name)?.enabled) {
                await this.dotnet?.invokeMethodAsync('ExecuteCommandAsync', command.name, selection);
            }
        }).catch(() => {});
        this.commandChain = run;
        this.track(run, true);
    }

    commandFor(name) {
        for (const command of this.commandMap.values()) {
            if (command.name === name) {
                return command;
            }
        }
        return undefined;
    }

    track(promise, command) {
        const set = command ? this.commandsInFlight : this.inFlight;
        set.add(promise);
        promise.finally(() => set.delete(promise));
        return promise;
    }

    busy() {
        return (this.inFlight.size + this.commandsInFlight.size) > 0;
    }

    async whenIdle() {
        while (this.inFlight.size > 0) {
            await Promise.allSettled([...this.inFlight]);
        }
    }

    selectAll() {
        if (!this.options.selectionEnabled) {
            return;
        }
        const ids = [];
        for (const box of this.table.boxes.values()) {
            if (box.selectable) {
                ids.push(box.id);
            }
        }
        for (const link of this.table.links.values()) {
            if (link.selectable) {
                ids.push(link.id);
            }
        }
        this.setSelection(ids);
    }

    centerPoint() {
        return { x: this.size.width / 2, y: this.size.height / 2 };
    }

    zoomStep(direction) {
        if (!this.options.zoomEnabled) {
            return;
        }
        const zoom = nextZoom(this.view.zoom, direction, this.options.minZoom, this.options.maxZoom);
        this.view = zoomAround(this.view, zoom / this.view.zoom, this.centerPoint());
        this.applyView();
        this.notifyViewport();
    }

    resetZoom() {
        if (!this.options.zoomEnabled) {
            return;
        }
        this.view = zoomAround(this.view, 1 / this.view.zoom, this.centerPoint());
        this.applyView();
        this.notifyViewport();
    }

    boundsOf(ids) {
        const boxes = ids.map((id) => this.table.boxes.get(id)).filter((b) => b && (b.kind !== 'overlay'));
        if (boxes.length === 0) {
            return null;
        }
        const left = Math.min(...boxes.map((b) => b.x));
        const top = Math.min(...boxes.map((b) => b.y));
        return { x: left, y: top, width: Math.max(...boxes.map((b) => b.x + b.w)) - left, height: Math.max(...boxes.map((b) => b.y + b.h)) - top };
    }

    fitAll(margin) {
        const bounds = this.boundsOf([...this.table.boxes.keys()]);
        if (bounds) {
            this.fit(bounds, margin);
        }
    }

    fitSelection(margin) {
        const ids = [];
        for (const id of this.selection) {
            const link = this.table.links.get(id);
            if (link) {
                ids.push(link.source, link.target);
            } else {
                ids.push(id);
            }
        }
        const bounds = this.boundsOf(ids);
        if (bounds) {
            this.fit(bounds, margin);
        }
    }

    centerOn(id) {
        const bounds = this.boundsOf([id]);
        if (!bounds) {
            return;
        }
        const zoom = this.view.zoom;
        this.view = { zoom, x: (this.size.width / 2) - ((bounds.x + (bounds.width / 2)) * zoom), y: (this.size.height / 2) - ((bounds.y + (bounds.height / 2)) * zoom) };
        this.applyView();
        this.notifyViewport();
    }

    focus() {
        this.root.focus({ preventScroll: true });
    }

    quickAdd(x, y) {
        if (!this.options.dropEnabled || this.waiting) {
            return;
        }
        let screen;
        if ((x === undefined) || (x === null)) {
            const pointer = this.lastPointer;
            screen = pointer && (pointer.x >= 0) && (pointer.y >= 0) && (pointer.x <= this.size.width) && (pointer.y <= this.size.height) ? pointer : this.centerPoint();
        } else {
            screen = { x: (x * this.view.zoom) + this.view.x, y: (y * this.view.zoom) + this.view.y };
        }
        this.openPicker(null, { kind: 'point', point: this.toWorld(screen) }, screen);
    }

    notifyClick(press, twice) {
        const kind = press.hit.kind;
        if ((kind !== 'element') && (kind !== 'background')) {
            return;
        }
        const keys = press.keys ?? {};
        this.dotnet?.invokeMethodAsync('OnClickedAsync', kind === 'element' ? press.hit.id : null, press.world.x, press.world.y, !!keys.shift, !!keys.ctrl, !!keys.alt, !!keys.meta, twice).catch(() => {});
    }

    onDoubleClick(event) {
        const under = document.elementFromPoint(event.clientX, event.clientY) ?? event.target;
        if (this.drag || this.editor || this.picker || under.closest?.(NO_DRAG_SELECTOR)) {
            return;
        }
        const hit = this.pressTarget({ target: under, clientX: event.clientX, clientY: event.clientY });
        if (hit.kind === 'element' && hit.box) {
            const slot = this.slotAt(hit.id, event);
            if (slot) {
                this.editText(hit.id, slot);
                return;
            }
        }
        const point = this.point(event);
        this.notifyClick({ hit, world: this.toWorld(point), keys: { shift: event.shiftKey, ctrl: event.ctrlKey, alt: event.altKey, meta: event.metaKey } }, true);
    }

    editableSlots(id) {
        const box = this.table.boxes.get(id);
        if (!this.options.textEditEnabled || !box?.editable) {
            return [];
        }
        return (this.schema.types.get(box.type)?.slots ?? []).filter((x) => x.editable);
    }

    slotAt(id, event) {
        const slots = this.editableSlots(id);
        if (slots.length === 0) {
            return null;
        }
        const element = this.element(id);
        for (const slot of slots) {
            const label = this.slotElement(element, slot.name);
            const rect = label?.getBoundingClientRect();
            if (rect && (rect.width > 0) && (event.clientX >= rect.left - 4) && (event.clientX <= rect.right + 4) && (event.clientY >= rect.top - 4) && (event.clientY <= rect.bottom + 4)) {
                return slot.name;
            }
        }
        const first = slots[0];
        const label = this.slotElement(element, first.name);
        const box = this.table.boxes.get(id);
        if ((box?.kind !== 'node') || !label) {
            return null;
        }
        if (box.texts) {
            return box.texts[first.name] ? null : first.name;
        }
        return !label.getAttribute('data-label') || label.hasAttribute('data-default') ? first.name : null;
    }

    slotElement(element, slot) {
        if (!element) {
            return null;
        }
        const name = CSS.escape(slot);
        return element.hasAttribute('data-template')
            ? element.querySelector(`[data-slot="${name}"]`)
            : element.querySelector(`:scope > .mussel-diagram-label[data-slot="${name}"]`);
    }

    editText(id, slot) {
        if (this.waiting || this.drag) {
            return;
        }
        const definition = this.editableSlots(id).find((x) => x.name === slot);
        const label = this.slotElement(this.element(id), slot);
        if (!definition || !label) {
            return;
        }
        this.closeEditor();
        this.hideReason();
        const texts = this.table.boxes.get(id)?.texts;
        const isDefault = texts ? !texts[slot] : label.hasAttribute('data-default');
        const text = texts ? (texts[slot] ?? '') : (isDefault ? '' : (label.getAttribute('data-label') ?? ''));
        const input = document.createElement('textarea');
        input.className = 'mussel-diagram-text-editor mussel-diagram-nowheel';
        input.value = text;
        input.placeholder = texts ? (definition.placeholder ?? '') : (isDefault ? (label.getAttribute('data-label') ?? '') : '');
        input.rows = 1;
        input.spellcheck = false;
        input.setAttribute('aria-label', slot);
        this.root.append(input);
        this.editor = { id, slot, label, input, text, maxLines: definition.maxLines, base: this.table.version, pending: false, template: !!texts };
        label.setAttribute('data-editing', '');
        input.addEventListener('keydown', (e) => this.onEditorKey(e));
        input.addEventListener('input', () => this.placeEditor());
        input.addEventListener('blur', () => {
            if (this.editor?.input === input) {
                this.finishEdit(true);
            }
        });
        this.placeEditor();
        input.focus({ preventScroll: true });
        input.select();
    }

    placeEditor() {
        const editor = this.editor;
        if (!editor) {
            return;
        }
        const box = this.table.boxes.get(editor.id);
        if (!box) {
            return;
        }
        const label = editor.label;
        const style = getComputedStyle(label);
        const fontSize = parseFloat(style.fontSize) || 12;
        if (editor.template) {
            this.placeTemplateEditor(editor, style, fontSize);
            return;
        }
        const width = Number(label.getAttribute('data-width')) || box.w;
        const anchor = label.getAttribute('text-anchor') ?? 'start';
        const valign = label.getAttribute('data-valign') ?? 'top';
        const x = Number(label.getAttribute('x')) || 0;
        const y = Number(label.getAttribute('y')) || 0;
        const lines = Math.max(1, editor.input.value.split('\n').length);
        const height = lines * fontSize * LINE_HEIGHT;
        const left = anchor === 'middle' ? x - (width / 2) : (anchor === 'end' ? x - width : x);
        const top = valign === 'middle' ? y - (height / 2) : (valign === 'bottom' ? y - height : y);
        const zoom = this.view.zoom;
        const input = editor.input;
        input.style.left = `${((box.x + left) * zoom) + this.view.x}px`;
        input.style.top = `${((box.y + top) * zoom) + this.view.y}px`;
        input.style.width = `${width * zoom}px`;
        input.style.fontSize = `${fontSize * zoom}px`;
        input.style.fontFamily = style.fontFamily;
        input.style.fontWeight = style.fontWeight;
        input.style.textAlign = anchor === 'middle' ? 'center' : (anchor === 'end' ? 'right' : 'left');
        input.style.height = 'auto';
        input.style.height = `${input.scrollHeight}px`;
    }

    placeTemplateEditor(editor, style, fontSize) {
        const zoom = this.view.zoom;
        const rect = editor.label.getBoundingClientRect();
        const root = this.root.getBoundingClientRect();
        const width = Math.max(rect.width, 80 * zoom);
        const align = (style.textAnchor === 'middle') || (style.textAlign === 'center') ? 'center' : ((style.textAnchor === 'end') || (style.textAlign === 'right') ? 'right' : 'left');
        const left = align === 'center' ? rect.left + (rect.width / 2) - (width / 2) : (align === 'right' ? rect.right - width : rect.left);
        const top = rect.height > 0 ? rect.top : rect.top - (fontSize * zoom);
        const input = editor.input;
        input.style.left = `${left - root.left}px`;
        input.style.top = `${top - root.top}px`;
        input.style.width = `${width}px`;
        input.style.fontSize = `${fontSize * zoom}px`;
        input.style.fontFamily = style.fontFamily;
        input.style.fontWeight = style.fontWeight;
        input.style.textAlign = align;
        input.style.height = 'auto';
        input.style.height = `${input.scrollHeight}px`;
    }

    onEditorKey(event) {
        const editor = this.editor;
        if (!editor || event.isComposing || (event.keyCode === 229)) {
            return;
        }
        const key = (event.key === 'Escape') || (event.key === 'Enter') ? null : keyName(event, this.mac);
        if (key && this.commandMap.has(key) && !this.isTyping(event, key)) {
            return;
        }
        event.stopPropagation();
        if (event.key === 'Escape') {
            event.preventDefault();
            this.closeEditor();
            this.root.focus({ preventScroll: true });
            return;
        }
        if (event.key !== 'Enter') {
            return;
        }
        const newline = (event.shiftKey || event.altKey) && !event.ctrlKey && !event.metaKey;
        if (newline && (editor.maxLines !== 1)) {
            if (event.altKey) {
                event.preventDefault();
                editor.input.setRangeText('\n', editor.input.selectionStart, editor.input.selectionEnd, 'end');
                this.placeEditor();
            }
            return;
        }
        event.preventDefault();
        if (!newline) {
            this.finishEdit(true);
            if (!this.editor) {
                this.root.focus({ preventScroll: true });
            }
        }
    }

    finishEdit(commit) {
        const editor = this.editor;
        if (!editor || editor.pending) {
            return;
        }
        const value = editor.maxLines === 1 ? editor.input.value.replace(/\s*\n\s*/g, ' ') : editor.input.value;
        if (!commit || (value === editor.text)) {
            this.closeEditor();
            return;
        }
        const label = editor.label;
        const box = this.table.boxes.get(editor.id);
        const point = box ? { x: ((box.x + box.w / 2) * this.view.zoom) + this.view.x, y: ((box.y + box.h / 2) * this.view.zoom) + this.view.y } : null;
        const operation = { base: editor.base, queued: this.pending.length > 0, point };
        const call = () => this.dotnet.invokeMethodAsync('CommitTextAsync', editor.id, editor.slot, value, this.baseOf(operation), this.rejections, [...this.selection]);
        if (!this.options.optimistic) {
            editor.pending = true;
            editor.input.readOnly = true;
            editor.input.setAttribute('data-pending', '');
            this.commit(operation, {
                saved: new Map(),
                previous: new Map(),
                accepted: () => {
                    if (this.editor === editor) {
                        this.closeEditor();
                    }
                },
                rejected: () => {
                    if (this.editor === editor) {
                        editor.pending = false;
                        editor.input.readOnly = false;
                        editor.input.removeAttribute('data-pending');
                        editor.base = this.table.version;
                        editor.input.focus({ preventScroll: true });
                    }
                },
            }, call);
            return;
        }
        if (editor.template) {
            const texts = this.provisionalText(label, value);
            this.closeEditor();
            this.commit(operation, { saved: new Map(), previous: new Map(), texts }, call);
            this.root.focus({ preventScroll: true });
            return;
        }
        const saved = new Map();
        this.save(saved, label, 'data-label');
        this.save(saved, label, 'data-default');
        this.closeEditor();
        if (value.length === 0) {
            label.setAttribute('data-label', '');
        } else {
            label.setAttribute('data-label', value);
            label.removeAttribute('data-default');
        }
        this.commit(operation, { saved, previous: new Map() }, call);
        this.root.focus({ preventScroll: true });
    }

    provisionalText(element, value) {
        const nodes = [...element.childNodes];
        if ((nodes.length !== 1) || (nodes[0].nodeType !== Node.TEXT_NODE)) {
            return [];
        }
        const saved = [[nodes[0], nodes[0].nodeValue]];
        nodes[0].nodeValue = value;
        return saved;
    }

    closeEditor() {
        const editor = this.editor;
        if (!editor) {
            return;
        }
        this.editor = null;
        editor.label.removeAttribute('data-editing');
        editor.input.remove();
    }

    spliceAt(clientPoint, node, exceptId) {
        const elements = document.elementsFromPoint(clientPoint.x, clientPoint.y);
        const hit = elements.find((e) => e.classList?.contains('mussel-diagram-link-hit') && this.canvas.contains(e));
        const id = hit?.closest('.mussel-diagram-link')?.getAttribute('data-id');
        const link = id ? this.table.links.get(id) : null;
        if (!link || (link.source === exceptId) || (link.target === exceptId)) {
            return null;
        }
        const type = this.schema.types.get(node.type);
        const virtual = { ...node, id: '\u0000split', registered: true, connectable: type?.connectable ?? true, whole: type?.whole ?? true, ports: type?.ports };
        const saved = this.table.boxes.get(exceptId);
        if (saved) {
            this.table.boxes.delete(exceptId);
        }
        this.table.boxes.set(virtual.id, virtual);
        try {
            const candidates = [...(type?.ports ?? []).map((p) => p.id), ...(virtual.whole || !(type?.ports?.length) ? [null] : [])];
            const into = candidates.find((port) => !checkConnection(this.table, this.schema, { element: link.source, port: link.sourcePort ?? null }, { element: virtual.id, port }, link.type, link.id));
            const out = candidates.find((port) => !checkConnection(this.table, this.schema, { element: virtual.id, port }, { element: link.target, port: link.targetPort ?? null }, link.type, link.id));
            return (into !== undefined) && (out !== undefined) ? { linkId: link.id, element: this.element(link.id) } : null;
        } finally {
            this.table.boxes.delete(virtual.id);
            if (saved) {
                this.table.boxes.set(exceptId, saved);
            }
        }
    }

    startExternal(typeName, value, item) {
        const type = this.schema.types.get(typeName);
        if (!this.options.dropEnabled || this.waiting || this.drag || !type || type.overlay) {
            return null;
        }
        this.closeEditor();
        this.closePicker();
        if (type.icon) {
            this.wantIcons([type.icon]);
        }
        const ghost = document.createElement('div');
        ghost.className = 'mussel-diagram-drag-ghost';
        const icon = item?.querySelector('svg')?.cloneNode(true);
        if (icon) {
            ghost.append(icon);
        }
        document.body.append(ghost);
        this.external = { type, value: value ?? null, ghost, lines: this.snapLines(new Set()), inside: false, point: null, client: null, alt: false, bounds: null, parent: undefined, allowed: false, split: null, snappedX: false, snappedY: false, panReady: false };
        this.root.classList.add('mussel-diagram-dropping');
        return this.external;
    }

    moveExternal(event) {
        const external = this.external;
        if (!external) {
            return;
        }
        external.client = { x: event.clientX, y: event.clientY };
        external.alt = event.altKey;
        external.ghost.style.left = `${event.clientX}px`;
        external.ghost.style.top = `${event.clientY}px`;
        const rect = this.root.getBoundingClientRect();
        const under = document.elementFromPoint(event.clientX, event.clientY);
        external.inside = (event.clientX >= rect.left) && (event.clientX <= rect.right) && (event.clientY >= rect.top) && (event.clientY <= rect.bottom) && !!under && this.root.contains(under) && !this.picker?.element.contains(under);
        external.ghost.hidden = external.inside;
        external.point = { x: event.clientX - rect.left, y: event.clientY - rect.top };
        if (!external.inside) {
            this.clearExternalMarks();
            return;
        }
        if (!external.panReady) {
            const speed = this.edgeSpeed(external.point);
            external.panReady = (speed.x === 0) && (speed.y === 0);
        }
        this.updateExternal();
        if (!this.externalFrame) {
            this.externalFrame = requestAnimationFrame(() => this.externalTick());
        }
    }

    externalTick() {
        this.externalFrame = 0;
        const external = this.external;
        if (!external?.inside || !external.panReady) {
            return;
        }
        const speed = this.edgeSpeed(external.point);
        if ((speed.x !== 0) || (speed.y !== 0)) {
            this.view = { ...this.view, x: this.view.x - speed.x, y: this.view.y - speed.y };
            external.panned = true;
            this.applyView();
            this.updateExternal();
            this.externalFrame = requestAnimationFrame(() => this.externalTick());
        }
    }

    updateExternal() {
        const external = this.external;
        const type = external.type;
        const world = this.toWorld(external.point);
        let x = world.x - (type.w / 2);
        let y = world.y - (type.h / 2);
        const anchor = gridAnchor({ x, y, w: type.w, h: type.h }, !type.group);
        const snapX = this.snapAxisOrGrid(external.lines.x, [x, x + (type.w / 2), x + type.w], anchor.x, external.alt);
        const snapY = this.snapAxisOrGrid(external.lines.y, [y, y + (type.h / 2), y + type.h], anchor.y, external.alt);
        x += snapX.delta;
        y += snapY.delta;
        external.snappedX = snapX.line !== null;
        external.snappedY = snapY.line !== null;
        external.bounds = { x, y, w: type.w, h: type.h };
        const group = this.boxAt(world, (box) => (box.kind === 'group') && !box.locked && canNest(this.schema, type.name, box));
        const parent = group ? group.id : (canNest(this.schema, type.name, null) ? null : undefined);
        const full = ((type.max ?? null) !== null) && (this.table.countOfType(type.name) >= type.max);
        external.parent = parent;
        external.allowed = !full && (parent !== undefined);
        this.clearMarks();
        this.hideReason();
        if (parent) {
            this.mark(this.element(parent), 'data-drop-target');
        }
        const split = external.allowed && !type.group && !external.alt
            ? this.spliceAt(external.client, { id: '\u0000placed', type: type.name, kind: 'node', ...external.bounds, parent }, null)
            : null;
        this.markSplit(external, split);
        this.setNotAllowed(!external.allowed);
        if (!external.allowed) {
            this.showReason(this.reasonText(full ? 'count' : 'nesting'), external.point, true);
        }
        this.drawGuides(external, external.bounds);
        this.drawExternalPreview(external);
    }

    drawExternalPreview(external) {
        const { bounds, type } = external;
        const shape = shapeElement(type, bounds, {});
        const group = svg('g', { class: external.allowed ? 'mussel-diagram-drop-preview' : 'mussel-diagram-drop-preview mussel-diagram-drop-preview-blocked' });
        group.append(shape);
        if (type.icon && (type.headerForm !== 'name-only')) {
            const size = type.group ? (type.headerIcon ?? 24) : null;
            const center = type.headerForm === 'top-center';
            group.append(svg('use', {
                href: `#${type.icon}`,
                x: center ? bounds.x + ((bounds.w - size) / 2) : bounds.x,
                y: center ? bounds.y - (size / 2) : bounds.y,
                width: size ?? bounds.w,
                height: size ?? bounds.h,
            }));
        }
        this.layers.preview.replaceChildren(group);
    }

    clearExternalMarks() {
        this.clearMarks();
        this.hideReason();
        this.setNotAllowed(false);
        this.layers.guides.replaceChildren();
        this.layers.preview.replaceChildren();
        if (this.external) {
            this.markSplit(this.external, null);
        }
    }

    dropExternal(event) {
        const external = this.external;
        if (!external) {
            return;
        }
        this.moveExternal(event);
        const { inside, allowed, bounds, parent, type, value } = external;
        const splitLinkId = external.split?.linkId ?? null;
        this.cancelExternal();
        if (!inside || !allowed || !bounds) {
            return;
        }
        const frame = shapeElement(type, bounds, { class: 'mussel-diagram-place-frame' });
        this.layers.pending.append(frame);
        const operation = { base: this.table.version, queued: this.pending.length > 0, point: external.point };
        this.commit(operation, { saved: new Map(), previous: new Map(), overlays: [frame], selectNew: true }, () => this.dotnet.invokeMethodAsync(
            'CommitDropAsync', type.name, bounds.x, bounds.y, bounds.w, bounds.h, parent, value, splitLinkId, this.baseOf(operation), this.rejections, [...this.selection]));
        this.root.focus({ preventScroll: true });
    }

    cancelExternal() {
        const external = this.external;
        if (!external) {
            return;
        }
        cancelAnimationFrame(this.externalFrame);
        this.externalFrame = 0;
        this.clearExternalMarks();
        external.ghost.remove();
        this.external = null;
        this.root.classList.remove('mussel-diagram-dropping');
        if (external.panned) {
            this.notifyViewport();
        }
    }

    async exportDiagram(options) {
        this.exportCount = (this.exportCount ?? 0) + 1;
        const id = this.exportCount;
        const result = await this.buildExport(options);
        if (result.blob) {
            this.exports.set(id, result.blob);
        }
        return JSON.stringify({ id, empty: !!result.empty, tooLarge: !!result.tooLarge, missing: result.missing ?? [] });
    }

    takeExport(id) {
        const blob = this.exports.get(id) ?? new Blob([]);
        this.exports.delete(id);
        return blob;
    }

    async buildExport(options) {
        const host = document.createElement('div');
        host.style.cssText = 'position:absolute;left:-200000px;top:0;width:10px;height:10px;overflow:hidden;opacity:0;pointer-events:none';
        const clone = this.canvas.cloneNode(true);
        host.append(clone);
        this.root.append(host);
        try {
            const viewport = clone.querySelector(':scope > .mussel-diagram-viewport');
            viewport.removeAttribute('transform');
            clone.querySelector('.mussel-diagram-grid')?.remove();
            for (const element of clone.querySelectorAll('.mussel-diagram-link-hit, title')) {
                element.remove();
            }
            if (!options.overlays) {
                clone.querySelector('.mussel-diagram-badges')?.remove();
                for (const element of clone.querySelectorAll('[class*="mussel-diagram-state-"], .mussel-diagram-error, .mussel-diagram-warning')) {
                    for (const name of [...element.classList]) {
                        if (name.startsWith('mussel-diagram-state-') || (name === 'mussel-diagram-error') || (name === 'mussel-diagram-warning')) {
                            element.classList.remove(name);
                        }
                    }
                }
                for (const element of clone.querySelectorAll('[style*="--mussel-diagram-override-"]')) {
                    for (const name of [...element.style]) {
                        if (name.startsWith('--mussel-diagram-override-')) {
                            element.style.removeProperty(name);
                        }
                    }
                }
            }
            for (const name of EXPORT_MARKS) {
                for (const element of clone.querySelectorAll(`[${name}]`)) {
                    element.removeAttribute(name);
                }
            }
            if (!viewport.querySelector(ELEMENT_SELECTOR)) {
                return { empty: true };
            }
            const bounds = viewport.getBBox();
            const cache = new Map();
            inlineIcons(clone);
            const missing = await embedImages(clone, cache);
            const background = getComputedStyle(this.root).backgroundColor;
            const families = inlineStyles(clone);
            const fonts = options.fonts ? await embedFonts(families, cache) : '';
            const margin = Math.max(0, options.margin);
            const width = Math.max(1, Math.ceil(bounds.width + (margin * 2)));
            const height = Math.max(1, Math.ceil(bounds.height + (margin * 2)));
            clone.setAttribute('width', String(width));
            clone.setAttribute('height', String(height));
            clone.setAttribute('viewBox', `0 0 ${width} ${height}`);
            viewport.setAttribute('transform', `translate(${formatNumber(margin - bounds.x)},${formatNumber(margin - bounds.y)})`);
            if (fonts) {
                let defs = clone.querySelector(':scope > defs');
                if (!defs) {
                    defs = svg('defs');
                    clone.prepend(defs);
                }
                const style = svg('style');
                style.textContent = fonts;
                defs.prepend(style);
            }
            if (options.background) {
                viewport.before(svg('rect', { width, height, fill: background }));
            }
            const text = new XMLSerializer().serializeToString(clone);
            if (options.format === 'svg') {
                return { blob: new Blob([text], { type: 'image/svg+xml' }), missing };
            }
            return { ...(await rasterize(text, width, height, options)), missing };
        } finally {
            host.remove();
        }
    }

    dispose() {
        clearTimeout(this.settleTimer);
        cancelAnimationFrame(this.frameRequest);
        this.clearHover();
        this.closePicker();
        this.hideReason();
        this.versionObserver.disconnect();
        this.resizeObserver.disconnect();
        for (const remove of this.listeners) {
            remove();
        }
        this.labels.dispose();
        this.badges?.dispose();
        this.closeEditor();
        this.cancelExternal();
        controllers.delete(this.root);
        this.stage.style.transform = '';
        delete this.root.dataset.attached;
    }
}

class DragSource {
    constructor(element, viewRoot) {
        this.element = element;
        this.viewRoot = viewRoot;
        this.armed = false;
        this.onDown = (event) => this.down(event);
        this.onTouchMove = (event) => {
            if (this.armed) {
                event.preventDefault();
            }
        };
        element.addEventListener('pointerdown', this.onDown);
        element.addEventListener('touchmove', this.onTouchMove, { passive: false });
        element.dataset.attached = '';
    }

    down(event) {
        if (event.button !== 0) {
            return;
        }
        const item = event.target.closest?.('[data-mussel-type]');
        const controller = controllers.get(this.viewRoot);
        if (!item || !this.element.contains(item) || item.hasAttribute('data-disabled') || !controller) {
            return;
        }
        const touch = event.pointerType === 'touch';
        if (!touch) {
            event.preventDefault();
        }
        const start = { x: event.clientX, y: event.clientY };
        let dragging = false;
        let armed = !touch;
        const timer = touch
            ? setTimeout(() => {
                armed = true;
                this.armed = true;
                item.setAttribute('data-armed', '');
            }, LONG_PRESS_DELAY)
            : 0;
        const block = (e) => {
            if (armed) {
                e.preventDefault();
            }
        };
        const move = (e) => {
            if (e.pointerId !== event.pointerId) {
                return;
            }
            if (!dragging) {
                const distance = Math.hypot(e.clientX - start.x, e.clientY - start.y);
                if (!armed) {
                    if (distance >= TOUCH_THRESHOLD) {
                        end();
                    }
                    return;
                }
                if (distance < (touch ? TOUCH_THRESHOLD : MOUSE_THRESHOLD)) {
                    return;
                }
                dragging = !!controller.startExternal(item.getAttribute('data-mussel-type'), item.getAttribute('data-mussel-value'), item);
                if (!dragging) {
                    end();
                    return;
                }
            }
            controller.moveExternal(e);
        };
        const up = (e) => {
            if (e.pointerId !== event.pointerId) {
                return;
            }
            if (dragging) {
                controller.dropExternal(e);
            }
            end();
        };
        const cancel = (e) => {
            if (e && (e.pointerId !== undefined) && (e.pointerId !== event.pointerId)) {
                return;
            }
            if (dragging) {
                controller.cancelExternal();
            }
            end();
        };
        const key = (e) => {
            if (e.key === 'Escape') {
                e.preventDefault();
                cancel();
            }
        };
        const second = (e) => {
            if ((e.pointerType === 'touch') && (e.pointerId !== event.pointerId)) {
                cancel();
            }
        };
        const end = () => {
            clearTimeout(timer);
            this.armed = false;
            item.removeAttribute('data-armed');
            window.removeEventListener('pointerdown', second, true);
            window.removeEventListener('pointermove', move);
            window.removeEventListener('pointerup', up);
            window.removeEventListener('pointercancel', cancel);
            window.removeEventListener('keydown', key, true);
            window.removeEventListener('contextmenu', block, true);
        };
        window.addEventListener('pointerdown', second, true);
        window.addEventListener('pointermove', move);
        window.addEventListener('pointerup', up);
        window.addEventListener('pointercancel', cancel);
        window.addEventListener('keydown', key, true);
        if (touch) {
            window.addEventListener('contextmenu', block, true);
        }
    }

    dispose() {
        this.element.removeEventListener('pointerdown', this.onDown);
        this.element.removeEventListener('touchmove', this.onTouchMove);
        delete this.element.dataset.attached;
    }
}

export function attach(root, dotnet, options, table, schema) {
    return new DiagramController(root, dotnet, options, table, schema);
}

export function attachSource(element, viewRoot) {
    return new DragSource(element, viewRoot);
}

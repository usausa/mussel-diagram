import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    wrapLabel, firstLineOffset, zoomAround, limitFactor, fitView, roundZoom, wheelDelta, wheelAction, routeLink, toPath, DiagramTable, elementBounds,
    collectLines, snapAxis, matchingLines, gridSnap, gridAnchor, gridMiddle, gridTile, resizeEdges, limitResize, checkConnection, canNest, searchTypes, placeBeside,
    keyName, nextZoom, stackOffsets, restore, restoreTexts, applyEntry, writeAttribute, changedSelection, scrollToShow, keepWritten, settleWritten, revertSaved, firstById, commandKey,
    canDrawAt,
} from '../../MusselDiagram/wwwroot/MusselDiagram.js';

const measure = (text) => [...text].length * 10;

const fakeElement = (attributes) => ({
    attributes: new Map(attributes),
    getAttribute(name) { return this.attributes.get(name) ?? null; },
    setAttribute(name, value) { this.attributes.set(name, String(value)); },
    removeAttribute(name) { this.attributes.delete(name); },
});

test('labels break at word boundaries', () => {
    assert.deepEqual(wrapLabel('Amazon EC2 instance', 100, 0, measure), { lines: ['Amazon EC2', 'instance'], truncated: false });
});

test('a word longer than the width breaks between characters', () => {
    assert.deepEqual(wrapLabel('abcdefghijklmnop', 50, 0, measure).lines, ['abcde', 'fghij', 'klmno', 'p']);
});

test('a line does not start with a closing punctuation mark', () => {
    const { lines } = wrapLabel('東京リージョン、大阪リージョン', 70, 0, measure);
    assert.ok(lines.length >= 2);
    assert.ok(lines.every((line) => !line.startsWith('、')));
    assert.equal(lines.join(''), '東京リージョン、大阪リージョン');
});

test('a line does not start with full-width punctuation or closing quotes', () => {
    for (const mark of ['！', '？', '：', '；', '］', '”', '’']) {
        assert.deepEqual(wrapLabel(`あいう${mark}えお`, 30, 0, measure).lines, [`あいう${mark}`, 'えお']);
    }
});

test('line breaks in the text are kept', () => {
    assert.deepEqual(wrapLabel('a\nb', 100, 0, measure).lines, ['a', 'b']);
    assert.deepEqual(wrapLabel('a\r\n\r\nb', 100, 0, measure).lines, ['a', '', 'b']);
});

test('lines over the limit end with an ellipsis', () => {
    assert.deepEqual(wrapLabel('Amazon EC2 instance web server', 100, 2, measure), { lines: ['Amazon EC2', 'instance…'], truncated: true });
});

test('slots in the same placement are stacked in the declared order', () => {
    const round = (values) => values.map((x) => Math.round(x * 100) / 100);
    assert.deepEqual(round(stackOffsets([{ lines: 1, size: 12, valign: 'middle' }, { lines: 1, size: 12, valign: 'middle' }])), [-3, 11.4]);
    assert.deepEqual(round(stackOffsets([{ lines: 2, size: 10, valign: 'top' }, { lines: 1, size: 12, valign: 'top' }])), [8, 33.6]);
    assert.deepEqual(round(stackOffsets([{ lines: 1, size: 10, valign: 'bottom' }, { lines: 2, size: 10, valign: 'bottom' }])), [-26, -14]);
    assert.deepEqual(round(stackOffsets([{ lines: 3, size: 12, valign: 'middle' }])), [12 * firstLineOffset(3, 'middle')].map((x) => Math.round(x * 100) / 100));
});

test('the first line is placed by the vertical alignment', () => {
    assert.equal(firstLineOffset(1, 'top'), 0.8);
    assert.equal(firstLineOffset(1, 'middle'), 0.35);
    assert.equal(firstLineOffset(2, 'bottom'), -1.4);
});

test('zooming keeps the point under the pointer still', () => {
    const view = zoomAround({ x: 100, y: 50, zoom: 1 }, 2, { x: 300, y: 250 });
    assert.deepEqual(view, { x: -100, y: -150, zoom: 2 });
    const world = { x: (300 - 100) / 1, y: (250 - 50) / 1 };
    assert.equal((world.x * view.zoom) + view.x, 300);
    assert.equal((world.y * view.zoom) + view.y, 250);
});

test('the zoom stays within its limits', () => {
    assert.equal(limitFactor(4, 4, 0.05, 8), 2);
    assert.equal(limitFactor(0.1, 0.1, 0.05, 8), 0.5);
    assert.equal(limitFactor(1, 1.5, 0.05, 8), 1.5);
});

test('fitting centers the bounds on a 1% grid', () => {
    const view = fitView({ x: 100, y: 100, width: 1000, height: 500 }, 800, 600, 20, 0.05, 8);
    assert.equal(view.zoom, 0.76);
    assert.equal(view.x, 400 - (600 * 0.76));
    assert.equal(view.y, 300 - (350 * 0.76));
});

test('wheel zoom ends on a 1% grid around the pointer', () => {
    const view = roundZoom({ x: 0, y: 0, zoom: 1.23456 }, { x: 0, y: 0 }, 0.05, 8);
    assert.equal(view.zoom, 1.23);
});

test('rounding the zoom stays inside the limits', () => {
    const point = { x: 0, y: 0 };
    assert.ok(Math.abs(roundZoom({ x: 0, y: 0, zoom: 1 / 3 }, point, 1 / 3, 8).zoom - (1 / 3)) < 1e-12);
    assert.ok(Math.abs(roundZoom({ x: 0, y: 0, zoom: 7.996 }, point, 0.05, 7.995).zoom - 7.995) < 1e-12);
});

test('zooms below 5% are not rounded to a grid', () => {
    const point = { x: 0, y: 0 };
    const tick = Math.exp(0.15);
    assert.ok(Math.abs(roundZoom({ x: 0, y: 0, zoom: 0.03 * tick }, point, 0.001, 8).zoom - (0.03 * tick)) < 1e-12);
    assert.ok(Math.abs(roundZoom({ x: 0, y: 0, zoom: 0.003 }, point, 0.001, 8).zoom - 0.003) < 1e-12);
    assert.ok(Math.abs(roundZoom({ x: 0, y: 0, zoom: 0.004 }, point, 0, 8).zoom - 0.004) < 1e-12);
    assert.ok(Math.abs(nextZoom(0.05, -1, 0.001, 8) - (0.05 / 1.2)) < 1e-12);
    assert.ok(Math.abs(nextZoom(0.02, 1, 0.001, 8) - 0.024) < 1e-12);
    assert.equal(nextZoom(0.045, 1, 0.001, 8), 0.05);
    assert.ok(Math.abs(fitView({ x: 0, y: 0, width: 304000, height: 100000 }, 800, 600, 20, 0.001, 8).zoom - 0.0025) < 1e-12);
});

test('wheel deltas in lines and pages become pixels', () => {
    assert.deepEqual(wheelDelta({ deltaMode: 0, deltaX: 3, deltaY: 5 }, 600), { x: 3, y: 5 });
    assert.deepEqual(wheelDelta({ deltaMode: 1, deltaX: 0, deltaY: 3 }, 600), { x: 0, y: 48 });
    assert.deepEqual(wheelDelta({ deltaMode: 2, deltaX: 0, deltaY: 1 }, 600), { x: 0, y: 600 });
});

test('the wheel is left to the page when the diagram does not pan or zoom', () => {
    const still = { wheelMode: 'pan', panEnabled: false, zoomEnabled: false };
    assert.equal(wheelAction({}, still), null);
    assert.equal(wheelAction({ ctrlKey: true }, still), null);
    assert.equal(wheelAction({}, { ...still, panEnabled: true }), 'pan');
    assert.equal(wheelAction({ ctrlKey: true }, { ...still, zoomEnabled: true }), 'zoom');
    assert.equal(wheelAction({}, { ...still, wheelMode: 'zoom', zoomEnabled: true }), 'zoom');
});

const box = (id, x, y, ports) => ({ id, kind: 'node', x, y, w: 100, h: 50, ports });
const service = (id, x, y) => box(id, x, y, [
    { id: 'in', side: 'left', ratio: 0.5, offset: 0 },
    { id: 'out', side: 'right', ratio: 0.5, offset: 0 },
    { id: 'top', side: 'top', ratio: 0.25, offset: 0 },
]);

test('links are routed the same way as in C#', () => {
    const link = (source, target, sourcePort, targetPort) => ({ source, target, sourcePort, targetPort, stub: 20, form: 'orthogonal' });
    assert.equal(toPath(routeLink(link('a', 'b'), box('a', 0, 0), box('b', 300, 0))), 'M100,25L300,25');
    assert.equal(toPath(routeLink(link('a', 'c'), box('a', 0, 0), box('c', 0, 300))), 'M50,50L50,300');
    assert.equal(toPath(routeLink(link('a', 'b', 'out', 'in'), service('a', 0, 0), service('b', 300, 100))), 'M100,25L200,25L200,125L300,125');
    assert.equal(toPath(routeLink(link('a', 'c', 'out', 'top'), service('a', 0, 0), service('c', 300, 400))), 'M100,25L325,25L325,400');
    assert.equal(toPath(routeLink({ ...link('a', 'b'), form: 'straight' }, box('a', 0, 0), box('b', 300, 100))), 'M100,25L300,125');
    assert.equal(toPath(routeLink({ ...link('a', 'b'), form: 'curved' }, box('a', 0, 0), box('b', 400, 0)), 'curved'), 'M100,25C250,25 250,25 400,25');
    assert.equal(toPath(routeLink(link('a', 'b'), box('a', 0, 0), box('b', 300, 0)), 'orthogonal', 10, 10), 'M110,25L290,25');
    assert.equal(toPath(routeLink(link('a', 'b'), box('a', 0, 0), box('b', 403, 100), 10)), 'M100,25L250,25L250,125L403,125');
    assert.equal(toPath(routeLink(link('a', 'b'), box('a', 0, 0), box('b', 403, 100))), 'M100,25L251.5,25L251.5,125L403,125');
});

test('a curve reaches out at most the range of boxes from its ends', () => {
    const link = { source: 'a', target: 'b', sourcePort: 'out', targetPort: 'in', stub: 20, form: 'curved' };
    const points = routeLink(link, service('a', 1e12 - 100, 0), service('b', -1e12, 1e12 - 50));
    assert.deepEqual(points.map((point) => [point.x, point.y]), [[1e12, 25], [2e12, 25], [-2e12, 1e12 - 25], [-1e12, 1e12 - 25]]);
});

test('a curve from ports that stand out stays inside the route range', () => {
    const link = { source: 'a', target: 'b', sourcePort: 'out', targetPort: 'in', stub: 20, form: 'curved' };
    const outward = (id, x, y) => box(id, x, y, [
        { id: 'in', side: 'left', ratio: 0.5, offset: 10 },
        { id: 'out', side: 'right', ratio: 0.5, offset: 10 },
    ]);
    const upright = (id, x, y) => box(id, x, y, [
        { id: 'in', side: 'top', ratio: 0.5, offset: 10 },
        { id: 'out', side: 'bottom', ratio: 0.5, offset: 10 },
    ]);
    const points = routeLink(link, outward('a', 1e12 - 100, 0), outward('b', -1e12, 1e12 - 50));
    assert.deepEqual(points.map((point) => [point.x, point.y]), [[1e12 + 10, 25], [2e12, 25], [-2e12, 1e12 - 25], [-1e12 - 10, 1e12 - 25]]);
    const vertical = routeLink(link, upright('a', 0, 1e12 - 50), upright('b', 1e12 - 100, -1e12));
    assert.deepEqual(vertical.map((point) => [point.x, point.y]), [[50, 1e12 + 10], [50, 2e12], [1e12 - 50, -2e12], [1e12 - 50, -1e12 - 10]]);
});

test('bends go to the grid line nearest the middle between the stubs', () => {
    assert.equal(gridMiddle(120, 383, 10), 250);
    assert.equal(gridMiddle(383, 120, 10), 250);
    assert.equal(gridMiddle(121, 125, 10), 123);
    assert.equal(gridMiddle(120, 383, 0), 251.5);
    assert.equal(gridMiddle(-35, -5, 10), -20);
});

test('the table knows descendants, moving sets and links', () => {
    const table = new DiagramTable({
        version: 3,
        boxes: [
            { ...box('vpc', 0, 0), kind: 'group' },
            { ...box('subnet', 10, 10), kind: 'group', parent: 'vpc' },
            { ...box('a', 20, 20), parent: 'subnet' },
            box('b', 500, 0),
            { ...box('asg', 0, 0), kind: 'overlay', members: ['b'] },
        ],
        links: [{ id: 'ab', source: 'a', target: 'b', stub: 20, form: 'orthogonal' }],
    });
    assert.deepEqual(table.descendants('vpc').sort(), ['a', 'subnet']);
    assert.deepEqual(table.ancestors('a'), ['subnet', 'vpc']);
    assert.equal(table.depth('a', new Map()), 2);
    assert.deepEqual(table.outermost(['a', 'subnet', 'b'], new Set(['subnet'])), ['subnet', 'b']);
    assert.deepEqual([...table.ancestorsOf(['a', 'subnet'])].sort(), ['subnet', 'vpc']);
    assert.deepEqual([...table.movingSet(['subnet'])].sort(), ['a', 'subnet']);
    assert.deepEqual([...table.movingSet(['asg'])], ['b', 'asg']);
    assert.deepEqual([...table.movingSet(['a'])], ['a']);
    assert.deepEqual([...table.linksOf(['a'])].map((l) => l.id), ['ab']);
    table.apply({ version: 4, boxes: [box('c', 0, 0)], links: [], removed: ['ab'] });
    assert.equal(table.version, 4);
    assert.equal(table.linksOf(['a']).size, 0);
    assert.ok(table.boxes.has('c'));
});

test('deep trees are walked once per call', { timeout: 10000 }, () => {
    const count = 50000;
    const boxes = Array.from({ length: count }, (_, i) => ({ ...box(`g${i}`, 0, 0), kind: 'group', ...(i > 0 ? { parent: `g${i - 1}` } : {}) }));
    const table = new DiagramTable({ version: 1, boxes, links: [] });
    const ids = boxes.map((b) => b.id).reverse();
    const depths = new Map();
    assert.equal(ids.reduce((max, id) => Math.max(max, table.depth(id, depths)), 0), count - 1);
    assert.deepEqual(table.outermost(ids, new Set(ids)), ['g0']);
    assert.equal(table.ancestorsOf(ids).size, count - 1);
});

test('links are framed by their ends and overlay groups by their members', () => {
    const table = new DiagramTable({
        version: 1,
        boxes: [box('a', 0, 0), box('b', 500, 100), { ...box('asg', 0, 0), kind: 'overlay', members: ['b'] }],
        links: [{ id: 'ab', source: 'a', target: 'b' }, { id: 'ag', source: 'a', target: 'asg' }],
    });
    assert.deepEqual(elementBounds(table, ['ab']), { x: 0, y: 0, width: 600, height: 150 });
    assert.deepEqual(elementBounds(table, ['ag']), { x: 0, y: 0, width: 600, height: 150 });
    assert.deepEqual(elementBounds(table, ['asg']), { x: 500, y: 100, width: 100, height: 50 });
    assert.equal(elementBounds(table, ['missing']), null);
});

test('moving edges snap to the nearest line, then to the grid', () => {
    const lines = collectLines([box('a', 100, 100), box('b', 300, 200)]);
    assert.deepEqual(snapAxis(lines.x, [96, 147, 197], 8), { delta: 3, value: 150 });
    assert.equal(snapAxis(lines.x, [50], 8), null);
    assert.equal(matchingLines(lines.x, [200, 300]).length, 2);
    assert.equal(gridSnap(14, 10), 10);
    assert.equal(gridSnap(15, 10), 20);
    assert.deepEqual(gridAnchor({ x: 3, y: 4, w: 48, h: 64 }, true), { x: 27, y: 36 });
    assert.deepEqual(gridAnchor({ x: 3, y: 4, w: 48, h: 64 }, false), { x: 3, y: 4 });
});

test('grid tiles draw a major line every interval lines, away from the cut edges of the tile', () => {
    assert.deepEqual(gridTile('lines', 10, 1, 4, 2), {
        size: 40,
        offset: -5,
        shapes: [
            { tag: 'path', class: 'mussel-diagram-grid-major', d: 'M5,0V40M0,5H40', 'stroke-width': 2 },
            { tag: 'path', class: 'mussel-diagram-grid-minor', d: 'M15,0V40M0,15H40M25,0V40M0,25H40M35,0V40M0,35H40', 'stroke-width': 1 },
        ],
    });
    assert.deepEqual(gridTile('lines', 20, 0.5, 3, 2).shapes.map((s) => s['stroke-width']), [4, 2]);
    assert.deepEqual(gridTile('lines', 10, 1, 0, 2), {
        size: 10,
        offset: -5,
        shapes: [{ tag: 'path', class: 'mussel-diagram-grid-minor', d: 'M5,0V10M0,5H10', 'stroke-width': 1 }],
    });
});

test('grid tiles of dots mark one major dot every interval', () => {
    const tile = gridTile('dots', 10, 2, 3, 4);
    assert.equal(tile.size, 30);
    assert.equal(tile.shapes.length, 9);
    assert.deepEqual(tile.shapes[0], { tag: 'circle', class: 'mussel-diagram-grid-major', cx: 5, cy: 5, r: 0.5 });
    assert.equal(tile.shapes.filter((s) => s.class === 'mussel-diagram-grid-major').length, 1);
    assert.equal(gridTile('dots', 10, 1, 0, 2).shapes.filter((s) => s.class === 'mussel-diagram-grid-major').length, 0);
});

test('resizing keeps the minimum size and the children inside', () => {
    const edges = resizeEdges({ x: 0, y: 0, w: 100, h: 100 }, 'nw', 90, 95);
    assert.deepEqual(limitResize(edges, 'nw', { minW: 20, minH: 20, inner: null }), { x: 80, y: 80, w: 20, h: 20 });
    const inner = { left: 10, top: 10, right: 70, bottom: 70 };
    assert.deepEqual(limitResize(resizeEdges({ x: 0, y: 0, w: 100, h: 100 }, 'e', -60, 0), 'e', { minW: 10, minH: 10, inner }), { x: 0, y: 0, w: 70, h: 100 });
});

test('connections follow the declared rules', () => {
    const port = (id, direction, extra) => ({ id, side: direction === 'in' ? 'left' : 'right', ratio: 0.5, offset: 0, direction, ...extra });
    const node = (id, type, ports, extra) => ({ id, type, kind: 'node', x: 0, y: 0, w: 100, h: 50, registered: true, connectable: true, whole: !ports, ports, ...extra });
    const table = new DiagramTable({
        version: 1,
        boxes: [
            node('a', 'svc', [port('in', 'in', { max: 1 }), port('out', 'out')]),
            node('b', 'svc', [port('in', 'in', { max: 1 }), port('out', 'out')]),
            node('c', 'box', null),
            node('d', 'box', null, { connectable: false }),
            node('e', 'box', null, { registered: false }),
        ],
        links: [{ id: 'ab', type: 'arrow', source: 'a', sourcePort: 'out', target: 'b', targetPort: 'in' }],
    });
    const schema = { types: new Map(), linkTypes: new Map([['arrow', { allowSelf: false, allowDuplicate: false, allowCrossParent: true, allowCycles: false }]]) };
    const check = (source, target) => checkConnection(table, schema, source, target, 'arrow', null);
    assert.equal(check({ element: 'a', port: 'in' }, { element: 'b', port: 'in' }), 'direction');
    assert.equal(check({ element: 'c', port: null }, { element: 'b', port: 'in' }), 'limit');
    assert.equal(check({ element: 'a', port: 'out' }, { element: 'b', port: 'in' }), 'limit');
    assert.equal(check({ element: 'b', port: 'out' }, { element: 'a', port: 'in' }), 'cycle');
    assert.equal(check({ element: 'c', port: null }, { element: 'd', port: null }), 'permission');
    assert.equal(check({ element: 'c', port: null }, { element: 'e', port: null }), 'noConnectionPoint');
    assert.equal(check({ element: 'c', port: null }, { element: 'c', port: null }), 'selfOrDuplicate');
    assert.equal(checkConnection(table, schema, { element: 'a', port: 'out' }, { element: 'b', port: 'in' }, 'arrow', 'ab'), null);
});

test('nesting follows the allowed parents and children', () => {
    const schema = {
        types: new Map([
            ['db', { name: 'db', parents: ['subnet'], topLevel: false }],
            ['subnet', { name: 'subnet', children: ['db'] }],
            ['vpc', { name: 'vpc' }],
            ['web', { name: 'web' }],
        ]),
    };
    const group = (type, extra) => ({ id: type, type, kind: 'group', registered: true, ...extra });
    assert.equal(canNest(schema, 'db', group('subnet')), true);
    assert.equal(canNest(schema, 'db', group('vpc')), false);
    assert.equal(canNest(schema, 'web', group('subnet')), false);
    assert.equal(canNest(schema, 'web', group('vpc', { locked: true })), false);
    assert.equal(canNest(schema, 'db', null), false);
    assert.equal(canNest(schema, 'web', null), true);
});

test('the type list is searched by words and ranked by the label', () => {
    const types = [{ name: 'a', label: 'Web server' }, { name: 'b', label: 'Server', search: ['ec2'] }, { name: 'c', label: 'Database', category: 'storage', categoryLabel: 'Storage' }];
    assert.deepEqual(searchTypes(types, 'server').map((t) => t.name), ['b', 'a']);
    assert.deepEqual(searchTypes(types, 'server\u0085').map((t) => t.name), ['b', 'a']);
    assert.deepEqual(searchTypes(types, 'ec2').map((t) => t.name), ['b']);
    assert.deepEqual(searchTypes(types, 'stor data').map((t) => t.name), ['c']);
    assert.equal(searchTypes(types, ' ').length, 3);
});

test('the type list is searched in capitals one letter at a time and split at the spaces of .NET', () => {
    const types = [{ name: 'street', label: 'ΟΔΟΣ' }, { name: 'size', label: 'GROẞ' }, { name: 'temperature', label: '\u212A' }, { name: 'server', label: 'Server' }, { name: 'road', label: 'Straße' }];
    assert.deepEqual(searchTypes(types, 'οδοσ').map((t) => t.name), ['street']);
    assert.deepEqual(searchTypes(types, 'groß').map((t) => t.name), []);
    assert.deepEqual(searchTypes(types, 'strasse').map((t) => t.name), []);
    assert.deepEqual(searchTypes(types, 'k').map((t) => t.name), []);
    assert.deepEqual(searchTypes(types, 'server\u0085').map((t) => t.name), ['server']);
    assert.deepEqual(searchTypes(types, '\uFEFFserver').map((t) => t.name), []);
});

test('keys are named with the modifiers in a fixed order', () => {
    const key = (k, extra = {}) => keyName({ key: k, code: '', ctrlKey: false, metaKey: false, altKey: false, shiftKey: false, ...extra }, false);
    assert.equal(key('z', { ctrlKey: true }), 'Ctrl+Z');
    assert.equal(key('Z', { ctrlKey: true, shiftKey: true }), 'Ctrl+Shift+Z');
    assert.equal(key('=', { ctrlKey: true }), 'Ctrl+Plus');
    assert.equal(key('+', { ctrlKey: true, shiftKey: true }), 'Ctrl+Plus');
    assert.equal(key('-', { ctrlKey: true }), 'Ctrl+Minus');
    assert.equal(key('F8', { shiftKey: true }), 'Shift+F8');
    assert.equal(key('Delete'), 'Delete');
    assert.equal(key('f', { ctrlKey: true, altKey: true, shiftKey: true, code: 'KeyF' }), 'Ctrl+Alt+Shift+F');
    assert.equal(key('Control', { ctrlKey: true }), null);
    assert.equal(keyName({ key: 'c', code: 'KeyC', ctrlKey: false, metaKey: true, altKey: false, shiftKey: false }, true), 'Ctrl+C');
    assert.equal(key('ä', { ctrlKey: true }), 'Ctrl+Ä');
    assert.equal(key('Ä', { ctrlKey: true, shiftKey: true }), 'Ctrl+Shift+Ä');
    assert.equal(key('Ж', { ctrlKey: true, shiftKey: true }), 'Ctrl+Shift+Ж');
    assert.equal(key('!', { ctrlKey: true, shiftKey: true }), 'Ctrl+!');
    assert.equal(key('ß', { ctrlKey: true }), 'Ctrl+ß');
    assert.equal(key('\u0131', { ctrlKey: true }), 'Ctrl+I');
});

test('with Alt, a letter or a digit is the key pressed and Shift is written', () => {
    const key = (k, extra = {}) => keyName({ key: k, code: '', ctrlKey: false, metaKey: false, altKey: false, shiftKey: false, ...extra }, false);
    assert.equal(key('1', { altKey: true, code: 'Digit1' }), 'Alt+1');
    assert.equal(key('!', { altKey: true, shiftKey: true, code: 'Digit1' }), 'Alt+Shift+1');
    assert.equal(key('å', { altKey: true, code: 'KeyA' }), 'Alt+A');
});

test('key names of commands match in any case', () => {
    const pressed = keyName({ key: 'Pause', code: 'Pause', ctrlKey: true, metaKey: false, altKey: false, shiftKey: false }, false);
    assert.equal(commandKey('Ctrl+pause'), commandKey(pressed));
    const sharp = keyName({ key: 'ß', code: 'Minus', ctrlKey: true, metaKey: false, altKey: false, shiftKey: false }, false);
    assert.equal(commandKey('Ctrl+ß'), commandKey(sharp));
});

test('command zoom moves in steps on a 5% grid and passes 100%', () => {
    assert.equal(nextZoom(1, 1, 0.05, 8), 1.2);
    assert.equal(nextZoom(1.2, -1, 0.05, 8), 1);
    assert.equal(nextZoom(0.9, 1, 0.05, 8), 1);
    assert.equal(nextZoom(7.5, 1, 0.05, 8), 8);
    assert.equal(nextZoom(0.05, -1, 0.05, 8), 0.05);
    assert.equal(nextZoom(0.06, 1, 0.05, 8), 0.1);
    assert.equal(nextZoom(0.09, -1, 0.05, 8), 0.05);
    assert.equal(nextZoom(0.05, 1, 0.05, 8), 0.1);
    assert.equal(nextZoom(0.1, -1, 0.05, 8), 0.05);
});

test('quick add places the element beside the connection point', () => {
    const origin = { x: 100, y: 100, w: 100, h: 50 };
    assert.deepEqual(placeBeside(origin, 'right', { w: 40, h: 20 }, 80), { x: 280, y: 115, w: 40, h: 20 });
    assert.deepEqual(placeBeside(origin, 'top', { w: 40, h: 20 }, 80, 130), { x: 110, y: 0, w: 40, h: 20 });
});

test('reverting keeps the values that changed after the commit', () => {
    const moved = fakeElement([['transform', 'translate(50,0)']]);
    const rendered = fakeElement([['transform', 'translate(80,0)']]);
    const saved = new Map([[moved, new Map([['transform', 'translate(0,0)']])], [rendered, new Map([['transform', 'translate(0,0)']])]]);
    const written = new Map([[moved, new Map([['transform', 'translate(50,0)']])], [rendered, new Map([['transform', 'translate(50,0)']])]]);

    restore(saved, written);

    assert.equal(moved.getAttribute('transform'), 'translate(0,0)');
    assert.equal(rendered.getAttribute('transform'), 'translate(80,0)');
});

test('a commit writes over the entry as it is when the pointer is released', () => {
    const table = new Map([['a', { id: 'a', x: 50, y: 0, w: 10, h: 10, locked: true }]]);

    const previous = applyEntry(table, 'a', { w: 20, h: 30 });

    assert.deepEqual(table.get('a'), { id: 'a', x: 50, y: 0, w: 20, h: 30, locked: true });
    assert.deepEqual(previous.get('a'), { id: 'a', x: 50, y: 0, w: 10, h: 10, locked: true });
    assert.equal(applyEntry(table, 'gone', { w: 1 }), null);
    assert.equal(table.has('gone'), false);
});

test('an element that a render changed is not restored in part', () => {
    const label = fakeElement([['data-label', 'Bar']]);
    const saved = new Map([[label, new Map([['data-label', 'Untitled'], ['data-default', '']])]]);
    const written = new Map([[label, new Map([['data-label', 'Foo'], ['data-default', null]])]]);

    restore(saved, written);

    assert.equal(label.getAttribute('data-label'), 'Bar');
    assert.equal(label.getAttribute('data-default'), null);
});

test('a commit still waiting keeps what it wrote over an older render, and goes back to that render', () => {
    const label = fakeElement([['data-label', 'Bar']]);
    const moved = fakeElement([['transform', 'translate(50,0)']]);
    const node = { nodeValue: 'Rendered' };
    const entry = {
        saved: new Map([[label, new Map([['data-label', 'Untitled'], ['data-default', '']])], [moved, new Map([['transform', 'translate(0,0)']])]]),
        written: new Map([[label, new Map([['data-label', 'Foo'], ['data-default', null]])], [moved, new Map([['transform', 'translate(50,0)']])]]),
        texts: [[node, 'Old']],
        writtenTexts: new Map([[node, 'New']]),
    };

    keepWritten(entry, new Set());
    const kept = [label.getAttribute('data-label'), label.getAttribute('data-default'), moved.getAttribute('transform'), node.nodeValue];
    restore(entry.saved, entry.written);

    assert.deepEqual(kept, ['Foo', null, 'translate(50,0)', 'New']);
    assert.deepEqual([label.getAttribute('data-label'), label.getAttribute('data-default'), moved.getAttribute('transform')], ['Bar', null, 'translate(0,0)']);
    assert.equal(entry.texts[0][1], 'Rendered');
});

test('a drag that started on a rejected commit goes back to where that commit started', () => {
    const moved = fakeElement([['transform', 'translate(80,0)']]);
    const rendered = fakeElement([['transform', 'translate(80,0)']]);
    const first = {
        saved: new Map([[moved, new Map([['transform', 'translate(0,0)']])], [rendered, new Map([['transform', 'translate(0,0)']])]]),
        written: new Map([[moved, new Map([['transform', 'translate(20,0)']])], [rendered, new Map([['transform', 'translate(20,0)']])]]),
    };
    const second = {
        saved: new Map([[moved, new Map([['transform', 'translate(20,0)']])]]),
        written: new Map([[moved, new Map([['transform', 'translate(50,0)']])]]),
    };
    const drag = new Map([[moved, new Map([['transform', 'translate(50,0)']])], [rendered, new Map([['transform', 'translate(30,0)']])]]);

    revertSaved(drag, second);
    revertSaved(drag, first);

    assert.equal(drag.get(moved).get('transform'), 'translate(0,0)');
    assert.equal(drag.get(rendered).get('transform'), 'translate(30,0)');
});

test('the first write of a drag keeps a render that arrived before it', () => {
    const element = fakeElement([['transform', 'translate(9,9)']]);
    const saved = new Map([[element, new Map([['transform', 'translate(0,0)']])]]);
    const written = new Map();

    writeAttribute(written, element, 'transform', 'translate(5,5)', saved);
    restore(saved, written);

    assert.equal(element.getAttribute('transform'), 'translate(9,9)');
});

test('a drag remembers what it wrote so that a cancel keeps a newer render', () => {
    const drawn = fakeElement([['d', 'M0 0']]);
    const rendered = fakeElement([['d', 'M0 0']]);
    const saved = new Map([[drawn, new Map([['d', 'M0 0']])], [rendered, new Map([['d', 'M0 0']])]]);
    const written = new Map();
    writeAttribute(written, drawn, 'd', 'M5 5', saved);
    writeAttribute(written, rendered, 'd', 'M5 5', saved);
    rendered.setAttribute('d', 'M9 9');

    restore(saved, written);

    assert.equal(drawn.getAttribute('d'), 'M0 0');
    assert.equal(rendered.getAttribute('d'), 'M9 9');
});

test('a render that arrives during a drag is what a cancel goes back to', () => {
    const element = fakeElement([['d', 'M0 0']]);
    const saved = new Map([[element, new Map([['d', 'M0 0']])]]);
    const written = new Map();
    writeAttribute(written, element, 'd', 'M5 5', saved);
    element.setAttribute('d', 'M9 9');
    writeAttribute(written, element, 'd', 'M6 6', saved);

    restore(saved, written);

    assert.equal(element.getAttribute('d'), 'M9 9');
});

test('a cancel leaves alone what the drag has not written', () => {
    const element = fakeElement([['d', 'M0 0']]);
    const saved = new Map([[element, new Map([['d', 'M0 0']])]]);
    element.setAttribute('d', 'M9 9');

    restore(saved, new Map());

    assert.equal(element.getAttribute('d'), 'M9 9');
});

test('a key with the Control key of a Mac or the Windows key has no name', () => {
    const base = { key: 'v', code: 'KeyV', ctrlKey: false, metaKey: false, altKey: false, shiftKey: false };
    assert.equal(keyName({ ...base, ctrlKey: true }, true), null);
    assert.equal(keyName({ ...base, metaKey: true }, false), null);
    assert.equal(keyName({ ...base, metaKey: true }, true), 'Ctrl+V');
});

test('a commit still waiting leaves alone what the current drag has written', () => {
    const element = fakeElement([['transform', 'translate(80,0)']]);
    const entry = {
        sent: false,
        version: null,
        saved: new Map([[element, new Map([['transform', 'translate(0,0)']])]]),
        written: new Map([[element, new Map([['transform', 'translate(50,0)']])]]),
        writtenTexts: new Map(),
    };

    settleWritten([entry], new Map([[element, new Map([['transform', 'translate(80,0)']])]]));

    assert.equal(element.getAttribute('transform'), 'translate(80,0)');
    assert.equal(entry.saved.get(element).get('transform'), 'translate(0,0)');
});

test('a commit still waiting leaves alone what a later commit has written, so both go back to the start', () => {
    const element = fakeElement([['transform', 'translate(10,10)']]);
    const node = { nodeValue: 'Second' };
    const first = {
        sent: false,
        version: null,
        saved: new Map([[element, new Map([['transform', 'translate(0,0)']])]]),
        written: new Map([[element, new Map([['transform', 'translate(10,0)']])]]),
        texts: [[node, 'Start']],
        writtenTexts: new Map([[node, 'First']]),
    };
    const second = {
        sent: false,
        version: null,
        saved: new Map([[element, new Map([['transform', 'translate(10,0)']])]]),
        written: new Map([[element, new Map([['transform', 'translate(10,10)']])]]),
        texts: [[node, 'First']],
        writtenTexts: new Map([[node, 'Second']]),
    };

    settleWritten([first, second], null);
    const kept = [element.getAttribute('transform'), node.nodeValue];
    for (const item of [second, first]) {
        restore(item.saved, item.written);
        restoreTexts(item.texts, item.writtenTexts);
    }

    assert.deepEqual(kept, ['translate(10,10)', 'Second']);
    assert.deepEqual([element.getAttribute('transform'), node.nodeValue], ['translate(0,0)', 'Start']);
});

test('a text that a rendering changed after the commit wrote it is not put back', () => {
    const node = { nodeValue: 'Rendered' };

    restoreTexts([[node, 'Start']], new Map([[node, 'Typed']]));

    assert.equal(node.nodeValue, 'Rendered');
});

test('a commit waiting for its answer leaves its elements to the renderings, even over an earlier answered one', () => {
    const element = fakeElement([['transform', 'translate(5,0)']]);
    const entry = (version, before, after) => ({
        sent: true,
        version,
        saved: new Map([[element, new Map([['transform', before]])]]),
        written: new Map([[element, new Map([['transform', after]])]]),
        texts: [],
        writtenTexts: new Map(),
    });

    settleWritten([entry(1, 'translate(0,0)', 'translate(10,0)'), entry(null, 'translate(10,0)', 'translate(20,0)')], null);

    assert.equal(element.getAttribute('transform'), 'translate(5,0)');
});

test('the commits that stay write their values back after a later one is reverted', () => {
    const element = fakeElement([['transform', 'translate(20,0)']]);
    const entry = (sent, version, before, after) => ({
        sent,
        version,
        saved: new Map([[element, new Map([['transform', before]])]]),
        written: new Map([[element, new Map([['transform', after]])]]),
        texts: [],
        writtenTexts: new Map(),
    });
    const first = entry(true, 1, 'translate(0,0)', 'translate(10,0)');
    const second = entry(false, null, 'translate(10,0)', 'translate(20,0)');
    element.setAttribute('transform', 'translate(5,0)');

    settleWritten([first, second], null);
    const kept = element.getAttribute('transform');
    restore(second.saved, second.written);
    const restored = element.getAttribute('transform');
    settleWritten([first], null);

    assert.deepEqual([kept, restored, element.getAttribute('transform')], ['translate(20,0)', 'translate(5,0)', 'translate(10,0)']);
});

test('a selection that the view already has is not sent again', () => {
    const sent = new Set(['a', 'b']);
    assert.equal(changedSelection(new Set(['b', 'a']), sent), null);
    assert.deepEqual(changedSelection(['a'], sent), ['a']);
    assert.deepEqual(changedSelection(['a', 'b', 'c'], sent), ['a', 'b', 'c']);
    assert.deepEqual(changedSelection(new Set(['a', 'b']), null), ['a', 'b']);
});

test('a list scrolls just enough to show the active row', () => {
    assert.equal(scrollToShow(0, 100, 40, 20), 0);
    assert.equal(scrollToShow(0, 100, 120, 20), 40);
    assert.equal(scrollToShow(80, 100, 40, 20), 40);
});

test('an id finds its first element, the same as a query of the element', () => {
    const element = (id) => ({ dataset: { id } });
    const first = element('a');
    const other = element('b');
    assert.deepEqual([...firstById([first, other, element('a')], 'id')], [['a', first], ['b', other]]);
});

test('a canvas that draws the probe pixel can hold the image', () => {
    const pixels = new Map();
    const context = {
        fillStyle: '',
        fillRect(x, y) { pixels.set(`${x},${y}`, this.fillStyle); },
        clearRect(x, y) { pixels.delete(`${x},${y}`); },
        getImageData(x, y) { return { data: pixels.get(`${x},${y}`) === '#010203' ? [1, 2, 3, 255] : [0, 0, 0, 0] }; },
    };
    assert.equal(canDrawAt(context, 99, 49), true);
    assert.equal(pixels.size, 0);
});

test('a canvas beyond the limits of the browser is too large, whether the probe pixel stays empty or reading it throws', () => {
    const context = (getImageData) => ({ fillStyle: '', fillRect() { }, clearRect() { }, getImageData });
    assert.equal(canDrawAt(context(() => ({ data: [0, 0, 0, 0] })), 70000, 0), false);
    assert.equal(canDrawAt(context(() => { throw new Error('NS_ERROR_FAILURE'); }), 70000, 0), false);
});

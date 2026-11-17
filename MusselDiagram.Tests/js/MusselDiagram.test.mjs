import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    wrapLabel, firstLineOffset, zoomAround, limitFactor, fitView, roundZoom, wheelDelta, wheelAction, routeLink, toPath, DiagramTable, elementBounds,
    collectLines, snapAxis, matchingLines, gridSnap, gridAnchor, gridMiddle, gridTile, resizeEdges, limitResize, checkConnection, canNest, searchTypes, placeBeside,
    keyName, nextZoom, stackOffsets, restore,
} from '../../MusselDiagram/wwwroot/MusselDiagram.js';

const measure = (text) => [...text].length * 10;

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
    const view = roundZoom({ x: 0, y: 0, zoom: 1.23456 }, { x: 0, y: 0 });
    assert.equal(view.zoom, 1.23);
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
    assert.deepEqual(searchTypes(types, 'ec2').map((t) => t.name), ['b']);
    assert.deepEqual(searchTypes(types, 'stor data').map((t) => t.name), ['c']);
    assert.equal(searchTypes(types, ' ').length, 3);
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
});

test('command zoom moves in steps on a 5% grid and passes 100%', () => {
    assert.equal(nextZoom(1, 1, 0.05, 8), 1.2);
    assert.equal(nextZoom(1.2, -1, 0.05, 8), 1);
    assert.equal(nextZoom(0.9, 1, 0.05, 8), 1);
    assert.equal(nextZoom(7.5, 1, 0.05, 8), 8);
    assert.equal(nextZoom(0.05, -1, 0.05, 8), 0.05);
});

test('quick add places the element beside the connection point', () => {
    const origin = { x: 100, y: 100, w: 100, h: 50 };
    assert.deepEqual(placeBeside(origin, 'right', { w: 40, h: 20 }, 80), { x: 280, y: 115, w: 40, h: 20 });
    assert.deepEqual(placeBeside(origin, 'top', { w: 40, h: 20 }, 80, 130), { x: 110, y: 0, w: 40, h: 20 });
});

test('reverting keeps the values that changed after the commit', () => {
    const element = (transform) => ({
        attributes: new Map([['transform', transform]]),
        getAttribute(name) { return this.attributes.get(name) ?? null; },
        setAttribute(name, value) { this.attributes.set(name, String(value)); },
        removeAttribute(name) { this.attributes.delete(name); },
    });
    const moved = element('translate(50,0)');
    const rendered = element('translate(80,0)');
    const saved = new Map([[moved, new Map([['transform', 'translate(0,0)']])], [rendered, new Map([['transform', 'translate(0,0)']])]]);
    const written = new Map([[moved, new Map([['transform', 'translate(50,0)']])], [rendered, new Map([['transform', 'translate(50,0)']])]]);

    restore(saved, written);

    assert.equal(moved.getAttribute('transform'), 'translate(0,0)');
    assert.equal(rendered.getAttribute('transform'), 'translate(80,0)');
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { wrapLabel, firstLineOffset, zoomAround, limitFactor, fitView, roundZoom, wheelDelta, routeLink, toPath, DiagramTable } from '../../MusselDiagram/wwwroot/MusselDiagram.js';

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

test('line breaks in the text are kept', () => {
    assert.deepEqual(wrapLabel('a\nb', 100, 0, measure).lines, ['a', 'b']);
    assert.deepEqual(wrapLabel('a\r\n\r\nb', 100, 0, measure).lines, ['a', '', 'b']);
});

test('lines over the limit end with an ellipsis', () => {
    assert.deepEqual(wrapLabel('Amazon EC2 instance web server', 100, 2, measure), { lines: ['Amazon EC2', 'instance…'], truncated: true });
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

const box = (id, x, y, ports) => ({ id, kind: 'node', x, y, w: 100, h: 50, ports });
const service = (id, x, y) => box(id, x, y, [
    { id: 'in', side: 'left', ratio: 0.5, offset: 0 },
    { id: 'out', side: 'right', ratio: 0.5, offset: 0 },
    { id: 'top', side: 'top', ratio: 0.25, offset: 0 },
]);

test('links are routed the same way as in C#', () => {
    const link = (source, target, sourcePort, targetPort) => ({ source, target, sourcePort, targetPort, stub: 20, straight: false });
    assert.equal(toPath(routeLink(link('a', 'b'), box('a', 0, 0), box('b', 300, 0))), 'M100,25L300,25');
    assert.equal(toPath(routeLink(link('a', 'c'), box('a', 0, 0), box('c', 0, 300))), 'M50,50L50,300');
    assert.equal(toPath(routeLink(link('a', 'b', 'out', 'in'), service('a', 0, 0), service('b', 300, 100))), 'M100,25L200,25L200,125L300,125');
    assert.equal(toPath(routeLink(link('a', 'c', 'out', 'top'), service('a', 0, 0), service('c', 300, 400))), 'M100,25L325,25L325,400');
    assert.equal(toPath(routeLink({ ...link('a', 'b'), straight: true }, box('a', 0, 0), box('b', 300, 100))), 'M100,25L300,125');
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
        links: [{ id: 'ab', source: 'a', target: 'b', stub: 20, straight: false }],
    });
    assert.deepEqual(table.descendants('vpc').sort(), ['a', 'subnet']);
    assert.deepEqual(table.ancestors('a'), ['subnet', 'vpc']);
    assert.deepEqual([...table.movingSet(['subnet'])].sort(), ['a', 'subnet']);
    assert.deepEqual([...table.movingSet(['asg'])], ['b']);
    assert.deepEqual([...table.linksOf(['a'])].map((l) => l.id), ['ab']);
    table.apply({ version: 4, boxes: [box('c', 0, 0)], links: [], removed: ['ab'] });
    assert.equal(table.version, 4);
    assert.equal(table.linksOf(['a']).size, 0);
    assert.ok(table.boxes.has('c'));
});

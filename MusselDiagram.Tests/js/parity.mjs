import { readFileSync } from 'node:fs';
import { routeLink, toPath, checkConnection, formatNumber, searchTypes, DiagramTable } from '../../MusselDiagram/wwwroot/MusselDiagram.js';

const data = JSON.parse(readFileSync(process.argv[2], 'utf8'));
const table = new DiagramTable(data.table);
const schema = {
    list: data.schema.types,
    types: new Map(data.schema.types.map((t) => [t.name, t])),
    linkTypes: new Map(data.schema.linkTypes.map((t) => [t.name, t])),
    defaultLinkType: data.schema.defaultLinkType,
    reasons: data.schema.reasons,
};
const mismatches = [];
for (const item of data.routes) {
    const { link } = item;
    const actual = toPath(routeLink(link, table.boxes.get(link.source), table.boxes.get(link.target)), link.form, link.sourceCut, link.targetCut);
    if (actual !== item.expected) {
        mismatches.push({ kind: 'route', item, actual });
    }
}
for (const item of data.rules) {
    const actual = checkConnection(table, schema, item.source, item.target, item.linkType, null);
    if ((actual ?? null) !== (item.expected ?? null)) {
        mismatches.push({ kind: 'rule', item, actual });
    }
}
for (const item of data.numbers) {
    const actual = formatNumber(item.value);
    if (actual !== item.expected) {
        mismatches.push({ kind: 'number', item, actual });
    }
}
for (const item of data.searches ?? []) {
    const actual = searchTypes(schema.list.filter((t) => item.names.includes(t.name)), item.query).map((t) => t.name);
    if (JSON.stringify(actual) !== JSON.stringify(item.expected)) {
        mismatches.push({ kind: 'search', item, actual });
    }
}
console.log(JSON.stringify({ routes: data.routes.length, rules: data.rules.length, numbers: data.numbers.length, searches: (data.searches ?? []).length, mismatches: mismatches.slice(0, 20) }));
process.exit(mismatches.length === 0 ? 0 : 1);

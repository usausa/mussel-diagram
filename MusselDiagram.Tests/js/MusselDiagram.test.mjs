import { test } from 'node:test';
import assert from 'node:assert/strict';
import { attach } from '../../MusselDiagram/wwwroot/MusselDiagram.js';

test('attach marks the root, and dispose removes the mark', () => {
    const root = { dataset: {} };

    const controller = attach(root);
    assert.equal(root.dataset.attached, '');

    controller.dispose();
    assert.equal('attached' in root.dataset, false);
});

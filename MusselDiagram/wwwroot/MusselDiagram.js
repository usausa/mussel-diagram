class DiagramController {
    constructor(root) {
        this.root = root;
        root.dataset.attached = '';
    }

    dispose() {
        delete this.root.dataset.attached;
    }
}

export function attach(root) {
    return new DiagramController(root);
}

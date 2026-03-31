window.graphInterop = {
    renderGraph: (nodes, edges) => {
        const existing = document.getElementById('graph');
        existing.innerHTML = '';

        const cy = cytoscape({
            container: existing,

            elements: [
                ...nodes.map(n => ({
                    data: {
                        id: n.id,
                        label: n.label,
                        type: n.type,
                        layer: n.layer
                    }
                })),
                ...edges.map(e => ({
                    data: {
                        id: e.id,
                        source: e.sourceId,
                        target: e.targetId,
                        relationship: e.relationship
                    }
                }))
            ],

            style: [
                {
                    selector: 'node',
                    style: {
                        'background-color': '#6366f1',
                        'label': 'data(label)',
                        'color': '#ffffff',
                        'text-valign': 'bottom',
                        'text-halign': 'center',
                        'text-margin-y': 10,
                        'font-size': '10px',
                        'text-wrap': 'wrap',
                        'text-max-width': '90px',
                        'width': 26,
                        'height': 26,
                        'border-width': 2,
                        'border-color': '#ffffff'
                    }
                },
                {
                    selector: 'edge',
                    style: {
                        'curve-style': 'bezier',
                        'line-color': '#888',
                        'target-arrow-color': '#888',
                        'target-arrow-shape': 'triangle',
                        'width': 2
                    }
                }
            ],

            layout: {
                name: 'breadthfirst',
                directed: true,
                padding: 30,
                spacingFactor: 1.4
            }
        });
    }
};
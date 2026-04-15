let currentGraph = null;
let currentLayoutName = 'breadthfirst';
let currentViewMode = 'Full';
let graphResizeObserver = null;
let graphFitTimeout = null;
let graphFitRunId = 0;
let horizontalPanBar = null;
let horizontalPanThumb = null;
let verticalPanBar = null;
let verticalPanThumb = null;

const focusClassNames = 'selected-node neighbor-node connected-edge dimmed-element flow-node flow-edge flow-dimmed-element endpoint-node endpoint-path-node endpoint-path-edge endpoint-dimmed-element hotspot-node hotspot-dimmed-element';
const graphMotion = {
    loadDuration: 360,
    focusDuration: 230,
    loadPadding: 34,
    focusPadding: 42,
    fullMinZoom: 0.18,
    focusMinZoom: 0.5,
    zoomStep: 1.1,
    panStep: 72,
    easing: 'ease-in-out'
};

const layerNames = ['Frontend', 'Api', 'Application', 'Data', 'Infrastructure', 'Workspace'];
const nodeTypeNames = ['Page', 'Component', 'Endpoint', 'Controller', 'Service', 'Repository', 'Database', 'ExternalApi', 'Route', 'Config', 'ProjectFile', 'EntryPoint', 'Project', 'Folder', 'Interface'];
const relationshipNames = ['Calls', 'Uses', 'MapsTo', 'ReadsFrom', 'WritesTo', 'Queries', 'Returns'];

const readValue = (source, camelName, fallback = '') => {
    if (!source) {
        return fallback;
    }

    const pascalName = camelName.charAt(0).toUpperCase() + camelName.slice(1);
    return source[camelName] ?? source[pascalName] ?? fallback;
};

const normalizeEnum = (value, names, fallback) => {
    if (Number.isInteger(value)) {
        return names[value] ?? fallback;
    }

    if (typeof value === 'string') {
        const trimmedValue = value.trim();
        const numericValue = Number(trimmedValue);

        if (Number.isInteger(numericValue) && trimmedValue !== '') {
            return names[numericValue] ?? fallback;
        }

        const match = names.find(name => name.toLowerCase() === trimmedValue.toLowerCase());
        return match ?? trimmedValue;
    }

    return fallback;
};

const getOption = (options, name, fallback) => {
    const pascalName = name.charAt(0).toUpperCase() + name.slice(1);
    return options?.[name] ?? options?.[pascalName] ?? fallback;
};

const isFocusView = viewMode => viewMode === 'FlowFocus' || viewMode === 'EndpointFocus';

const createLayoutOptions = (layoutName, viewMode) => {
    const focusView = isFocusView(viewMode);
    const padding = focusView ? 34 : 30;

    if (layoutName === 'cose') {
        return {
            name: 'cose',
            animate: true,
            animationDuration: focusView ? 240 : 320,
            animationEasing: graphMotion.easing,
            fit: true,
            padding,
            nodeRepulsion: focusView ? 3000 : 3900,
            idealEdgeLength: focusView ? 64 : 78,
            edgeElasticity: 150,
            gravity: focusView ? 1 : 0.86,
            componentSpacing: focusView ? 22 : 28,
            randomize: false
        };
    }

    if (layoutName === 'circle') {
        return {
            name: 'circle',
            animate: true,
            animationDuration: focusView ? 220 : 300,
            animationEasing: graphMotion.easing,
            fit: true,
            padding,
            avoidOverlap: true,
            spacingFactor: focusView ? 0.76 : 0.92
        };
    }

    return {
        name: 'breadthfirst',
        directed: true,
        animate: true,
        animationDuration: focusView ? 230 : 320,
        animationEasing: graphMotion.easing,
        fit: true,
        padding,
        spacingFactor: focusView ? 0.72 : 0.84,
        nodeDimensionsIncludeLabels: true
    };
};

const getReadableMinZoom = viewMode => isFocusView(viewMode)
    ? graphMotion.focusMinZoom
    : graphMotion.fullMinZoom;

const getGraphPanLimits = graph => {
    if (!graph || graph.elements().empty()) {
        return null;
    }

    const bounds = graph.elements().boundingBox({ includeLabels: true });
    const zoom = graph.zoom();
    const width = graph.width();
    const height = graph.height();
    const margin = 88;
    const minX = margin - bounds.x2 * zoom;
    const maxX = width - margin - bounds.x1 * zoom;
    const minY = margin - bounds.y2 * zoom;
    const maxY = height - margin - bounds.y1 * zoom;

    return { minX, maxX, minY, maxY };
};

const clampValue = (value, min, max) => {
    if (min > max) {
        return (min + max) / 2;
    }

    return Math.min(max, Math.max(min, value));
};

const updateHorizontalPanBar = () => {
    if (!currentGraph || !horizontalPanBar || !horizontalPanThumb) {
        return;
    }

    const limits = getGraphPanLimits(currentGraph);
    if (!limits || limits.minX >= limits.maxX) {
        horizontalPanThumb.style.left = '50%';
        horizontalPanBar.setAttribute('aria-valuenow', '50');
        horizontalPanBar.classList.add('is-disabled');
        return;
    }

    const pan = currentGraph.pan();
    const ratio = clampValue((limits.maxX - pan.x) / (limits.maxX - limits.minX), 0, 1);
    const value = Math.round(ratio * 100);
    horizontalPanThumb.style.left = `${value}%`;
    horizontalPanBar.setAttribute('aria-valuenow', value.toString());
    horizontalPanBar.classList.remove('is-disabled');
};

const updateVerticalPanBar = () => {
    if (!currentGraph || !verticalPanBar || !verticalPanThumb) {
        return;
    }

    const limits = getGraphPanLimits(currentGraph);
    if (!limits || limits.minY >= limits.maxY) {
        verticalPanThumb.style.top = '50%';
        verticalPanBar.setAttribute('aria-valuenow', '50');
        verticalPanBar.classList.add('is-disabled');
        return;
    }

    const pan = currentGraph.pan();
    const ratio = clampValue((limits.maxY - pan.y) / (limits.maxY - limits.minY), 0, 1);
    const value = Math.round(ratio * 100);
    verticalPanThumb.style.top = `${value}%`;
    verticalPanBar.setAttribute('aria-valuenow', value.toString());
    verticalPanBar.classList.remove('is-disabled');
};

const updatePanBars = () => {
    updateHorizontalPanBar();
    updateVerticalPanBar();
};

const setGraphPan = (graph, x, y) => {
    if (!graph) {
        return;
    }

    const limits = getGraphPanLimits(graph);
    if (!limits) {
        return;
    }

    graph.pan({
        x: clampValue(x, limits.minX, limits.maxX),
        y: clampValue(y, limits.minY, limits.maxY)
    });
    updatePanBars();
};

const canZoomFitElements = (graph, elements, zoom, padding) => {
    if (!graph || !elements || elements.empty()) {
        return false;
    }

    const bounds = elements.boundingBox({ includeLabels: true });
    const width = (bounds.w || bounds.x2 - bounds.x1) * zoom + padding * 2;
    const height = (bounds.h || bounds.y2 - bounds.y1) * zoom + padding * 2;

    return width <= graph.width() && height <= graph.height();
};

const clampReadableZoom = (graph, elements, viewMode, padding = 0) => {
    if (!graph || !elements || elements.empty()) {
        return;
    }

    const minZoom = getReadableMinZoom(viewMode);

    if (graph.zoom() < minZoom && canZoomFitElements(graph, elements, minZoom, padding)) {
        graph.zoom({
            level: minZoom,
            renderedPosition: {
                x: graph.width() / 2,
                y: graph.height() / 2
            }
        });
        graph.center(elements);
    }

    updatePanBars();
};

const animateGraphFit = (graph, elements, padding, duration, viewMode = currentViewMode) => {
    if (!graph || !elements || elements.empty()) {
        return;
    }

    if (typeof graph.stop === 'function') {
        graph.stop();
    }

    graph.resize();
    graph.animate({
        fit: {
            eles: elements,
            padding
        }
    }, {
        duration,
        easing: graphMotion.easing,
        complete: () => {
            graph.center(elements);
            clampReadableZoom(graph, elements, viewMode, padding);
            updatePanBars();
        }
    });

    window.setTimeout(() => {
        graph.center(elements);
        clampReadableZoom(graph, elements, viewMode, padding);
        updatePanBars();
    }, duration + 40);
};

const fitLoadedGraph = (graph, viewMode, duration = graphMotion.loadDuration) => {
    const padding = isFocusView(viewMode) ? graphMotion.focusPadding : graphMotion.loadPadding;
    animateGraphFit(graph, graph.elements(), padding, duration, viewMode);
};

const fitGraphNow = (graph, viewMode, duration = graphMotion.loadDuration) => {
    if (!graph || (typeof graph.destroyed === 'function' && graph.destroyed())) {
        return;
    }

    graph.resize();
    fitLoadedGraph(graph, viewMode, duration);
};

const runPostLayoutFit = (graph, viewMode) => {
    const fitRunId = ++graphFitRunId;

    [20, 160, 320].forEach((delay, index) => {
        window.setTimeout(() => {
            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => {
                    if (fitRunId !== graphFitRunId || graph !== currentGraph) {
                        return;
                    }

                    fitGraphNow(
                        graph,
                        viewMode,
                        index === 0 ? graphMotion.loadDuration : Math.round(graphMotion.loadDuration / 2));
                });
            });
        }, delay);
    });
};

const scheduleGraphFit = (graph, viewMode, delay = 0) => {
    if (graphFitTimeout) {
        window.clearTimeout(graphFitTimeout);
    }

    const fitRunId = ++graphFitRunId;
    graphFitTimeout = window.setTimeout(() => {
        if (fitRunId !== graphFitRunId) {
            return;
        }

        fitGraphNow(graph, viewMode);
    }, delay);
};

const cancelPendingGraphFits = () => {
    graphFitRunId++;

    if (graphFitTimeout) {
        window.clearTimeout(graphFitTimeout);
        graphFitTimeout = null;
    }
};

const clearFocusClasses = graph => {
    graph?.elements().removeClass(focusClassNames);
};

const disconnectResizeObserver = () => {
    if (graphResizeObserver) {
        graphResizeObserver.disconnect();
        graphResizeObserver = null;
    }
};

const attachResizeObserver = element => {
    disconnectResizeObserver();

    if (!element || typeof ResizeObserver === 'undefined') {
        return;
    }

    graphResizeObserver = new ResizeObserver(() => {
        if (!currentGraph) {
            return;
        }

        currentGraph.resize();
        window.requestAnimationFrame(() => scheduleGraphFit(currentGraph, currentViewMode, 80));
    });
    graphResizeObserver.observe(element);
};

const attachWheelNavigation = element => {
    if (!element) {
        return;
    }

    element.onwheel = event => {
        if (!currentGraph) {
            return;
        }

        event.preventDefault();
        const pan = currentGraph.pan();
        const horizontalDelta = event.shiftKey ? event.deltaY : event.deltaX;

        setGraphPan(
            currentGraph,
            pan.x - horizontalDelta,
            pan.y - event.deltaY);
    };
};

const setHorizontalPanFromRatio = ratio => {
    if (!currentGraph) {
        return;
    }

    const limits = getGraphPanLimits(currentGraph);
    if (!limits) {
        return;
    }

    const x = limits.maxX - clampValue(ratio, 0, 1) * (limits.maxX - limits.minX);
    setGraphPan(currentGraph, x, currentGraph.pan().y);
};

const setVerticalPanFromRatio = ratio => {
    if (!currentGraph) {
        return;
    }

    const limits = getGraphPanLimits(currentGraph);
    if (!limits) {
        return;
    }

    const y = limits.maxY - clampValue(ratio, 0, 1) * (limits.maxY - limits.minY);
    setGraphPan(currentGraph, currentGraph.pan().x, y);
};

const attachHorizontalPanBar = () => {
    horizontalPanBar = document.getElementById('graph-horizontal-pan');
    horizontalPanThumb = horizontalPanBar?.querySelector('.graph-pan-thumb') ?? null;

    if (!horizontalPanBar) {
        return;
    }

    const updateFromClientX = clientX => {
        const bounds = horizontalPanBar.getBoundingClientRect();
        const ratio = bounds.width <= 0
            ? 0.5
            : (clientX - bounds.left) / bounds.width;
        setHorizontalPanFromRatio(ratio);
    };

    horizontalPanBar.onpointerdown = event => {
        horizontalPanBar.setPointerCapture?.(event.pointerId);
        updateFromClientX(event.clientX);
    };

    horizontalPanBar.onpointermove = event => {
        if (event.buttons !== 1) {
            return;
        }

        updateFromClientX(event.clientX);
    };

    horizontalPanBar.onkeydown = event => {
        if (!currentGraph) {
            return;
        }

        const pan = currentGraph.pan();

        if (event.key === 'ArrowLeft') {
            event.preventDefault();
            setGraphPan(currentGraph, pan.x + graphMotion.panStep, pan.y);
        }

        if (event.key === 'ArrowRight') {
            event.preventDefault();
            setGraphPan(currentGraph, pan.x - graphMotion.panStep, pan.y);
        }
    };

    updatePanBars();
};

const attachVerticalPanBar = () => {
    verticalPanBar = document.getElementById('graph-vertical-pan');
    verticalPanThumb = verticalPanBar?.querySelector('.graph-pan-thumb') ?? null;

    if (!verticalPanBar) {
        return;
    }

    const updateFromClientY = clientY => {
        const bounds = verticalPanBar.getBoundingClientRect();
        const ratio = bounds.height <= 0
            ? 0.5
            : (clientY - bounds.top) / bounds.height;
        setVerticalPanFromRatio(ratio);
    };

    verticalPanBar.onpointerdown = event => {
        verticalPanBar.setPointerCapture?.(event.pointerId);
        updateFromClientY(event.clientY);
    };

    verticalPanBar.onpointermove = event => {
        if (event.buttons !== 1) {
            return;
        }

        updateFromClientY(event.clientY);
    };

    verticalPanBar.onkeydown = event => {
        if (!currentGraph) {
            return;
        }

        const pan = currentGraph.pan();

        if (event.key === 'ArrowUp') {
            event.preventDefault();
            setGraphPan(currentGraph, pan.x, pan.y + graphMotion.panStep);
        }

        if (event.key === 'ArrowDown') {
            event.preventDefault();
            setGraphPan(currentGraph, pan.x, pan.y - graphMotion.panStep);
        }
    };

    updatePanBars();
};

const zoomGraph = factor => {
    if (!currentGraph) {
        return;
    }

    const nextZoom = clampValue(
        currentGraph.zoom() * factor,
        currentGraph.minZoom(),
        currentGraph.maxZoom());

    currentGraph.zoom({
        level: nextZoom,
        renderedPosition: {
            x: currentGraph.width() / 2,
            y: currentGraph.height() / 2
        }
    });
    setGraphPan(currentGraph, currentGraph.pan().x, currentGraph.pan().y);
};

const truncateLabel = (label, maxCharacters) => {
    if (label.length <= maxCharacters) {
        return label;
    }

    return `${label.slice(0, Math.max(maxCharacters - 3, 1))}...`;
};

const getNodeDisplayMetrics = (label, type) => {
    const maxCharacters = type === 'Endpoint'
        ? 58
        : type === 'Project' || type === 'Folder' || type === 'Config' || type === 'ProjectFile'
            ? 36
            : 46;
    const displayLabel = truncateLabel(label, maxCharacters);
    const minWidth = type === 'Endpoint'
        ? 190
        : type === 'Controller' || type === 'Route' || type === 'Page'
            ? 166
            : type === 'Project' || type === 'Folder' || type === 'Config' || type === 'ProjectFile'
                ? 122
                : 148;
    const maxWidth = type === 'Endpoint' ? 370 : 312;
    const characterWidth = type === 'Endpoint' ? 7.6 : 7.15;
    const horizontalPadding = type === 'Endpoint' ? 58 : 46;
    const width = Math.ceil(clampValue(displayLabel.length * characterWidth + horizontalPadding, minWidth, maxWidth));
    const height = type === 'Endpoint'
        ? 58
        : type === 'Project' || type === 'Folder' || type === 'Config' || type === 'ProjectFile'
            ? 40
            : 48;

    return {
        displayLabel,
        width,
        height,
        textWidth: Math.max(width - 26, 72)
    };
};

const normalizeNode = node => {
    const layer = normalizeEnum(readValue(node, 'layer'), layerNames, 'Unknown');
    const type = normalizeEnum(readValue(node, 'type'), nodeTypeNames, 'Unknown');
    const label = String(readValue(node, 'label') || readValue(node, 'id') || 'Unknown');
    const displayMetrics = getNodeDisplayMetrics(label, type);

    return {
        data: {
            id: readValue(node, 'id'),
            label,
            displayLabel: displayMetrics.displayLabel,
            width: displayMetrics.width,
            height: displayMetrics.height,
            textWidth: displayMetrics.textWidth,
            type,
            layer
        }
    };
};

const normalizeEdge = edge => {
    const relationship = normalizeEnum(readValue(edge, 'relationship'), relationshipNames, '');

    return {
        data: {
            id: readValue(edge, 'id'),
            source: readValue(edge, 'sourceId'),
            target: readValue(edge, 'targetId'),
            relationship
        }
    };
};

const graphStyle = [
    {
        selector: 'node',
        style: {
            'shape': 'round-rectangle',
            'background-color': '#071525',
            'label': 'data(displayLabel)',
            'color': '#f8fbff',
            'text-valign': 'center',
            'text-halign': 'center',
            'font-family': 'Inter, Segoe UI, sans-serif',
            'font-size': '10.8px',
            'font-weight': 760,
            'text-wrap': 'none',
            'text-max-width': 'data(textWidth)',
            'text-outline-color': '#020611',
            'text-outline-width': 2.4,
            'width': 'data(width)',
            'height': 'data(height)',
            'border-width': 2,
            'border-color': '#22d3ee',
            'underlay-color': '#67e8f9',
            'underlay-opacity': 0.13,
            'underlay-padding': 10,
            'overlay-opacity': 0,
            'transition-property': 'background-color, border-color, border-width, opacity, text-opacity, width, height, underlay-opacity',
            'transition-duration': '180ms',
            'transition-timing-function': 'ease-in-out'
        }
    },
    {
        selector: 'node[layer = "Frontend"]',
        style: {
            'background-color': '#061a32',
            'border-color': '#22d3ee',
            'underlay-color': '#38bdf8'
        }
    },
    {
        selector: 'node[layer = "Api"]',
        style: {
            'background-color': '#1b1035',
            'border-color': '#d946ef',
            'underlay-color': '#d946ef'
        }
    },
    {
        selector: 'node[layer = "Application"]',
        style: {
            'background-color': '#091f2b',
            'border-color': '#8b5cf6',
            'underlay-color': '#5eead4'
        }
    },
    {
        selector: 'node[layer = "Data"]',
        style: {
            'background-color': '#191927',
            'border-color': '#f59e0b',
            'underlay-color': '#f59e0b'
        }
    },
    {
        selector: 'node[layer = "Infrastructure"]',
        style: {
            'background-color': '#101827',
            'border-color': '#818cf8',
            'underlay-color': '#818cf8'
        }
    },
    {
        selector: 'node[type = "Endpoint"]',
        style: {
            'shape': 'round-rectangle',
            'background-color': '#071c2b',
            'border-color': '#67e8f9',
            'border-width': 3,
            'underlay-color': '#d946ef',
            'underlay-opacity': 0.22,
            'font-size': '11px',
            'text-outline-width': 2.8
        }
    },
    {
        selector: 'node[type = "Controller"], node[type = "Route"], node[type = "Page"]',
        style: {
            'border-width': 3
        }
    },
    {
        selector: 'node[type = "Service"], node[type = "Analyzer"], node[type = "Scanner"], node[type = "Detector"], node[type = "Simplifier"]',
        style: {
            'background-color': '#081d22',
            'border-color': '#5eead4',
            'underlay-color': '#22d3ee'
        }
    },
    {
        selector: 'node[type = "Repository"], node[type = "Database"]',
        style: {
            'shape': 'round-rectangle',
            'background-color': '#191829',
            'border-color': '#f59e0b'
        }
    },
    {
        selector: 'node[type = "Project"], node[type = "Folder"], node[type = "ProjectFile"], node[type = "Config"]',
        style: {
            'font-size': '10px',
            'background-color': '#0c101b',
            'border-color': '#475569',
            'border-style': 'dashed',
            'underlay-opacity': 0.03,
            'opacity': 0.78
        }
    },
    {
        selector: 'edge',
        style: {
            'curve-style': 'taxi',
            'taxi-direction': 'downward',
            'taxi-turn': 10,
            'taxi-turn-min-distance': 10,
            'line-color': '#3f5570',
            'target-arrow-color': '#67e8f9',
            'target-arrow-shape': 'triangle',
            'arrow-scale': 0.95,
            'width': 2,
            'label': 'data(relationship)',
            'font-family': 'Inter, Segoe UI, sans-serif',
            'font-size': '8.5px',
            'font-weight': 650,
            'color': '#d8f7ff',
            'text-background-color': '#030712',
            'text-background-opacity': 0.9,
            'text-background-padding': 3,
            'text-rotation': 'autorotate',
            'text-margin-y': -8,
            'opacity': 0.82,
            'transition-property': 'line-color, target-arrow-color, width, opacity, text-opacity, color',
            'transition-duration': '180ms',
            'transition-timing-function': 'ease-in-out'
        }
    },
    {
        selector: 'edge[relationship = "Uses"], edge[relationship = "Calls"]',
        style: {
            'line-color': '#a855f7',
            'target-arrow-color': '#d946ef',
            'color': '#e9d5ff'
        }
    },
    {
        selector: 'edge[relationship = "MapsTo"]',
        style: {
            'line-color': '#22d3ee',
            'target-arrow-color': '#d946ef',
            'width': 3.2,
            'color': '#cffafe'
        }
    },
    {
        selector: 'edge[relationship = "ReadsFrom"], edge[relationship = "WritesTo"], edge[relationship = "Queries"]',
        style: {
            'line-color': '#facc15',
            'target-arrow-color': '#fde68a',
            'color': '#fef3c7'
        }
    },
    {
        selector: 'edge[relationship = "Contains"], edge[relationship = "ContainsConfig"], edge[relationship = "ContainsProjectFile"], edge[relationship = "HasEntryPoint"]',
        style: {
            'curve-style': 'bezier',
            'line-style': 'dashed',
            'line-color': '#334155',
            'target-arrow-color': '#475569',
            'width': 1.25,
            'color': '#94a3b8',
            'opacity': 0.42,
            'text-opacity': 0.45
        }
    },
    {
        selector: 'node.selected-node',
        style: {
            'border-color': '#ffffff',
            'border-width': 4,
            'underlay-color': '#67e8f9',
            'underlay-opacity': 0.34,
            'underlay-padding': 14,
            'opacity': 1,
            'z-index': 30
        }
    },
    {
        selector: 'node.neighbor-node',
        style: {
            'border-color': '#c084fc',
            'border-width': 3,
            'underlay-opacity': 0.22,
            'opacity': 1,
            'z-index': 18
        }
    },
    {
        selector: 'edge.connected-edge',
        style: {
            'line-color': '#67e8f9',
            'target-arrow-color': '#f0abfc',
            'width': 4,
            'color': '#f8fafc',
            'opacity': 1,
            'text-opacity': 1,
            'z-index': 16
        }
    },
    {
        selector: '.dimmed-element',
        style: {
            'opacity': 0.26,
            'text-opacity': 0.38
        }
    },
    {
        selector: 'node.flow-node',
        style: {
            'border-color': '#d946ef',
            'border-width': 3,
            'underlay-color': '#d946ef',
            'underlay-opacity': 0.24,
            'opacity': 1,
            'z-index': 20
        }
    },
    {
        selector: 'edge.flow-edge',
        style: {
            'line-color': '#d946ef',
            'target-arrow-color': '#67e8f9',
            'width': 3,
            'color': '#f5d0fe',
            'opacity': 1,
            'text-opacity': 1,
            'z-index': 14
        }
    },
    {
        selector: '.flow-dimmed-element',
        style: {
            'opacity': 0.2,
            'text-opacity': 0.35
        }
    },
    {
        selector: 'node.endpoint-path-node',
        style: {
            'border-color': '#67e8f9',
            'border-width': 3,
            'underlay-color': '#67e8f9',
            'underlay-opacity': 0.24,
            'opacity': 1,
            'z-index': 20
        }
    },
    {
        selector: 'node.endpoint-node',
        style: {
            'border-color': '#f8fafc',
            'border-width': 5,
            'underlay-color': '#d946ef',
            'underlay-opacity': 0.36,
            'underlay-padding': 16,
            'opacity': 1,
            'z-index': 32
        }
    },
    {
        selector: 'edge.endpoint-path-edge',
        style: {
            'line-color': '#22d3ee',
            'target-arrow-color': '#d946ef',
            'width': 4,
            'color': '#cffafe',
            'opacity': 1,
            'text-opacity': 1,
            'z-index': 16
        }
    },
    {
        selector: '.endpoint-dimmed-element',
        style: {
            'opacity': 0.2,
            'text-opacity': 0.34
        }
    },
    {
        selector: 'node.hotspot-node',
        style: {
            'border-color': '#facc15',
            'border-width': 4,
            'underlay-color': '#facc15',
            'underlay-opacity': 0.24,
            'opacity': 1,
            'z-index': 24
        }
    },
    {
        selector: '.hotspot-dimmed-element',
        style: {
            'opacity': 0.3,
            'text-opacity': 0.44
        }
    }
];

const applySelectionStyle = node => {
    const graph = node.cy();
    cancelPendingGraphFits();
    clearFocusClasses(graph);

    const connectedEdges = node.connectedEdges();
    const neighborNodes = connectedEdges.connectedNodes().difference(node);
    const focusedElements = node.union(neighborNodes).union(connectedEdges);

    node.addClass('selected-node');
    neighborNodes.addClass('neighbor-node');
    connectedEdges.addClass('connected-edge');

    graph.nodes().difference(node.union(neighborNodes)).addClass('dimmed-element');
    graph.edges().difference(connectedEdges).addClass('dimmed-element');

    animateGraphFit(graph, focusedElements, graphMotion.focusPadding, graphMotion.focusDuration, currentViewMode);
};

const attachFullscreenListener = () => {
    if (window.graphInteropFullscreenListenerAttached) {
        return;
    }

    document.addEventListener('fullscreenchange', () => {
        if (!currentGraph) {
            return;
        }

        currentGraph.resize();
        scheduleGraphFit(currentGraph, currentViewMode, 120);
    });

    window.graphInteropFullscreenListenerAttached = true;
};

window.graphInterop = {
    renderGraph: (nodes = [], edges = [], dotNetHelper, options = {}) => {
        const existing = document.getElementById('graph');

        if (!existing) {
            return;
        }

        if (currentGraph) {
            currentGraph.destroy();
            currentGraph = null;
        }
        disconnectResizeObserver();

        existing.innerHTML = '';

        const viewMode = getOption(options, 'viewMode', 'Full');
        const layoutName = getOption(options, 'layout', 'breadthfirst');
        currentLayoutName = layoutName;
        currentViewMode = viewMode;

        const cy = cytoscape({
            container: existing,
            elements: [
                ...nodes.map(normalizeNode),
                ...edges.map(normalizeEdge)
            ],
            style: graphStyle,
            layout: createLayoutOptions(layoutName, viewMode),
            minZoom: 0.12,
            maxZoom: 2.4,
            wheelSensitivity: 0.08,
            userZoomingEnabled: false,
            userPanningEnabled: false,
            boxSelectionEnabled: false,
            autoungrabify: true
        });

        currentGraph = cy;
        attachFullscreenListener();
        attachResizeObserver(existing);
        attachWheelNavigation(existing);
        attachHorizontalPanBar();
        attachVerticalPanBar();

        cy.one('layoutstop', () => {
            runPostLayoutFit(cy, viewMode);
        });
        cy.ready(() => runPostLayoutFit(cy, viewMode));
        runPostLayoutFit(cy, viewMode);

        cy.on('tap', 'node', event => {
            const nodeId = event.target.id();
            applySelectionStyle(event.target);
            console.log('Clicked node:', nodeId);
            dotNetHelper?.invokeMethodAsync('OnNodeSelected', nodeId);
        });

        cy.on('tap', event => {
            if (event.target !== cy) {
                return;
            }

            cancelPendingGraphFits();
            clearFocusClasses(cy);
            fitLoadedGraph(cy, currentViewMode, graphMotion.focusDuration);
            dotNetHelper?.invokeMethodAsync('OnGraphBackgroundSelected');
        });
    },

    fitGraph: () => {
        if (!currentGraph) {
            return;
        }

        scheduleGraphFit(currentGraph, currentViewMode);
    },

    zoomIn: () => {
        zoomGraph(graphMotion.zoomStep);
    },

    zoomOut: () => {
        zoomGraph(1 / graphMotion.zoomStep);
    },

    resetView: () => {
        if (!currentGraph) {
            return;
        }

        clearFocusClasses(currentGraph);
        const layout = currentGraph.layout(createLayoutOptions(currentLayoutName, currentViewMode));
        currentGraph.one('layoutstop', () => scheduleGraphFit(currentGraph, currentViewMode, 40));
        layout.run();
    },

    toggleFullscreen: async () => {
        const viewport = document.querySelector('.graph-viewport') ?? document.getElementById('graph');

        if (!viewport) {
            return;
        }

        if (!document.fullscreenElement) {
            await viewport.requestFullscreen?.();
        } else {
            await document.exitFullscreen?.();
        }
    },

    highlightFlow: nodeIds => {
        if (!currentGraph || !Array.isArray(nodeIds)) {
            return;
        }

        clearFocusClasses(currentGraph);
        cancelPendingGraphFits();

        const flowNodeIds = new Set(nodeIds);
        const flowNodes = currentGraph.nodes().filter(node => flowNodeIds.has(node.id()));
        const flowEdges = currentGraph.edges().filter(edge =>
            flowNodeIds.has(edge.source().id()) && flowNodeIds.has(edge.target().id()));

        flowNodes.addClass('flow-node');
        flowEdges.addClass('flow-edge');

        currentGraph.nodes().difference(flowNodes).addClass('flow-dimmed-element');
        currentGraph.edges().difference(flowEdges).addClass('flow-dimmed-element');

        animateGraphFit(currentGraph, flowNodes.union(flowEdges), graphMotion.focusPadding, graphMotion.focusDuration, 'FlowFocus');
    },

    highlightEndpointPath: (endpointNodeId, pathNodeIds) => {
        if (!currentGraph || !endpointNodeId || !Array.isArray(pathNodeIds)) {
            return;
        }

        clearFocusClasses(currentGraph);
        cancelPendingGraphFits();

        const pathNodeIdSet = new Set(pathNodeIds);
        const pathNodes = currentGraph.nodes().filter(node => pathNodeIdSet.has(node.id()));
        const pathEdges = currentGraph.edges().filter(edge =>
            pathNodeIdSet.has(edge.source().id()) && pathNodeIdSet.has(edge.target().id()));
        const endpointNode = currentGraph.getElementById(endpointNodeId);

        pathNodes.addClass('endpoint-path-node');
        endpointNode.addClass('endpoint-node');
        pathEdges.addClass('endpoint-path-edge');

        currentGraph.nodes().difference(pathNodes).addClass('endpoint-dimmed-element');
        currentGraph.edges().difference(pathEdges).addClass('endpoint-dimmed-element');

        animateGraphFit(currentGraph, pathNodes.union(pathEdges), graphMotion.focusPadding, graphMotion.focusDuration, 'EndpointFocus');
    },

    highlightHotspots: nodeIds => {
        if (!currentGraph || !Array.isArray(nodeIds)) {
            return;
        }

        clearFocusClasses(currentGraph);
        cancelPendingGraphFits();

        const hotspotNodeIds = new Set(nodeIds);
        const hotspotNodes = currentGraph.nodes().filter(node => hotspotNodeIds.has(node.id()));

        if (hotspotNodes.empty()) {
            return;
        }

        hotspotNodes.addClass('hotspot-node');
        currentGraph.nodes().difference(hotspotNodes).addClass('hotspot-dimmed-element');

        animateGraphFit(currentGraph, hotspotNodes, graphMotion.focusPadding, graphMotion.focusDuration, 'EndpointFocus');
    }
};

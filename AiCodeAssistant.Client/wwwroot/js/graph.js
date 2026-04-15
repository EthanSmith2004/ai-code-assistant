let currentGraph = null;
let currentLayoutName = 'breadthfirst';
let currentViewMode = 'Full';
let graphResizeObserver = null;
let graphFitTimeout = null;
let graphFitFrame = null;
let graphFitRunId = 0;
let graphLayoutRunId = 0;
let graphLayoutReady = false;
let horizontalPanBar = null;
let horizontalPanThumb = null;
let verticalPanBar = null;
let verticalPanThumb = null;

const focusClassNames = 'selected-node neighbor-node connected-edge dimmed-element flow-node flow-edge flow-dimmed-element endpoint-node endpoint-path-node endpoint-path-edge endpoint-dimmed-element hotspot-node hotspot-dimmed-element';
const graphMotion = {
    loadDuration: 320,
    focusDuration: 230,
    loadPadding: 52,
    focusPadding: 46,
    fullMinZoom: 0.28,
    focusMinZoom: 0.5,
    zoomStep: 1.1,
    panStep: 72,
    layoutSettleDelay: 48,
    fitRetryDelay: 72,
    maxFitAttempts: 6,
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
const normalizeLayoutName = layoutName => layoutName === 'cose' ? 'cose' : 'breadthfirst';

const createLayoutOptions = (layoutName, viewMode) => {
    const focusView = isFocusView(viewMode);
    const padding = focusView ? 34 : 46;

    if (layoutName === 'cose') {
        return {
            name: 'cose',
            animate: true,
            animationDuration: focusView ? 240 : 320,
            animationEasing: graphMotion.easing,
            fit: false,
            padding,
            nodeRepulsion: focusView ? 3200 : 4700,
            idealEdgeLength: focusView ? 72 : 98,
            edgeElasticity: 150,
            gravity: focusView ? 1 : 0.86,
            componentSpacing: focusView ? 28 : 44,
            randomize: false
        };
    }

    return {
        name: 'breadthfirst',
        directed: true,
        animate: true,
        animationDuration: focusView ? 230 : 320,
        animationEasing: graphMotion.easing,
        fit: false,
        padding,
        spacingFactor: focusView ? 0.82 : 1.06,
        nodeDimensionsIncludeLabels: true
    };
};

const getReadableMinZoom = viewMode => isFocusView(viewMode)
    ? graphMotion.focusMinZoom
    : graphMotion.fullMinZoom;

const isGraphDestroyed = graph => !graph || (typeof graph.destroyed === 'function' && graph.destroyed());

const isGraphReadyForFit = graph => !isGraphDestroyed(graph)
    && !graph.elements().empty()
    && graph.width() > 0
    && graph.height() > 0;

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

const finalizeGraphFit = (graph, elements, viewMode, padding) => {
    if (isGraphDestroyed(graph) || !elements || elements.empty()) {
        return;
    }

    graph.center(elements);
    clampReadableZoom(graph, elements, viewMode, padding);
    updatePanBars();
};

const animateGraphFit = (graph, elements, padding, duration, viewMode = currentViewMode) => {
    if (isGraphDestroyed(graph) || !elements || elements.empty()) {
        return;
    }

    if (typeof graph.stop === 'function') {
        graph.stop();
    }

    graph.resize();

    if (duration <= 0) {
        graph.fit(elements, padding);
        finalizeGraphFit(graph, elements, viewMode, padding);
        return;
    }

    graph.animate({
        fit: {
            eles: elements,
            padding
        }
    }, {
        duration,
        easing: graphMotion.easing,
        complete: () => {
            window.requestAnimationFrame(() => finalizeGraphFit(graph, elements, viewMode, padding));
        }
    });
};

const fitLoadedGraph = (graph, viewMode, duration = graphMotion.loadDuration) => {
    const padding = isFocusView(viewMode) ? graphMotion.focusPadding : graphMotion.loadPadding;
    animateGraphFit(graph, graph.elements(), padding, duration, viewMode);
};

const fitGraphNow = (graph, viewMode, duration = graphMotion.loadDuration) => {
    if (isGraphDestroyed(graph)) {
        return;
    }

    graph.resize();
    if (!isGraphReadyForFit(graph)) {
        return;
    }

    fitLoadedGraph(graph, viewMode, duration);
};

const runGraphLayoutAndFit = (graph, layoutName, viewMode) => {
    if (isGraphDestroyed(graph) || graph.elements().empty()) {
        graphLayoutReady = true;
        updatePanBars();
        return;
    }

    graphLayoutReady = false;
    const layoutRunId = ++graphLayoutRunId;
    const layout = graph.layout(createLayoutOptions(normalizeLayoutName(layoutName), viewMode));

    graph.one('layoutstop', () => {
        if (layoutRunId !== graphLayoutRunId || graph !== currentGraph || isGraphDestroyed(graph)) {
            return;
        }

        graphLayoutReady = true;
        scheduleGraphFit(graph, viewMode, graphMotion.layoutSettleDelay);
    });

    layout.run();
};

const scheduleGraphFit = (graph, viewMode, delay = 0, duration = graphMotion.loadDuration, attempts = 0) => {
    if (graphFitTimeout) {
        window.clearTimeout(graphFitTimeout);
        graphFitTimeout = null;
    }

    if (graphFitFrame) {
        window.cancelAnimationFrame(graphFitFrame);
        graphFitFrame = null;
    }

    const fitRunId = ++graphFitRunId;
    graphFitTimeout = window.setTimeout(() => {
        graphFitTimeout = null;

        if (fitRunId !== graphFitRunId || graph !== currentGraph || isGraphDestroyed(graph)) {
            return;
        }

        graph.resize();
        graphFitFrame = window.requestAnimationFrame(() => {
            graphFitFrame = null;

            if (fitRunId !== graphFitRunId || graph !== currentGraph || isGraphDestroyed(graph)) {
                return;
            }

            graph.resize();

            if (!isGraphReadyForFit(graph)) {
                if (attempts < graphMotion.maxFitAttempts) {
                    scheduleGraphFit(graph, viewMode, graphMotion.fitRetryDelay, duration, attempts + 1);
                }

                return;
            }

            fitGraphNow(graph, viewMode, duration);
        });
    }, delay);
};

const cancelPendingGraphFits = () => {
    graphFitRunId++;

    if (graphFitTimeout) {
        window.clearTimeout(graphFitTimeout);
        graphFitTimeout = null;
    }

    if (graphFitFrame) {
        window.cancelAnimationFrame(graphFitFrame);
        graphFitFrame = null;
    }

    graphLayoutRunId++;
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
        if (!currentGraph || !graphLayoutReady) {
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
            'background-color': '#111827',
            'label': 'data(displayLabel)',
            'color': '#f8fbff',
            'text-valign': 'center',
            'text-halign': 'center',
            'font-family': 'Inter, Segoe UI, sans-serif',
            'font-size': '11.2px',
            'font-weight': 760,
            'text-wrap': 'none',
            'text-max-width': 'data(textWidth)',
            'text-outline-color': '#05070b',
            'text-outline-width': 2.6,
            'min-zoomed-font-size': 7,
            'width': 'data(width)',
            'height': 'data(height)',
            'border-width': 2,
            'border-color': '#94a3b8',
            'underlay-color': '#93c5fd',
            'underlay-opacity': 0.1,
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
            'background-color': '#102033',
            'border-color': '#60a5fa',
            'underlay-color': '#60a5fa'
        }
    },
    {
        selector: 'node[layer = "Api"]',
        style: {
            'background-color': '#10251f',
            'border-color': '#34d399',
            'underlay-color': '#34d399'
        }
    },
    {
        selector: 'node[layer = "Application"]',
        style: {
            'background-color': '#172018',
            'border-color': '#a3e635',
            'underlay-color': '#86efac'
        }
    },
    {
        selector: 'node[layer = "Data"]',
        style: {
            'background-color': '#211b12',
            'border-color': '#fbbf24',
            'underlay-color': '#fbbf24'
        }
    },
    {
        selector: 'node[layer = "Infrastructure"]',
        style: {
            'background-color': '#171923',
            'border-color': '#cbd5e1',
            'underlay-color': '#94a3b8'
        }
    },
    {
        selector: 'node[type = "Endpoint"]',
        style: {
            'shape': 'round-rectangle',
            'background-color': '#111c2a',
            'border-color': '#bfdbfe',
            'border-width': 3,
            'underlay-color': '#60a5fa',
            'underlay-opacity': 0.18,
            'font-size': '11.4px',
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
            'background-color': '#10251f',
            'border-color': '#34d399',
            'underlay-color': '#34d399'
        }
    },
    {
        selector: 'node[type = "Repository"], node[type = "Database"]',
        style: {
            'shape': 'round-rectangle',
            'background-color': '#211b12',
            'border-color': '#fbbf24'
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
            'line-color': '#4b5563',
            'target-arrow-color': '#94a3b8',
            'target-arrow-shape': 'triangle',
            'arrow-scale': 0.95,
            'width': 2,
            'label': 'data(relationship)',
            'font-family': 'Inter, Segoe UI, sans-serif',
            'font-size': '8.5px',
            'font-weight': 650,
            'color': '#dbeafe',
            'text-background-color': '#05070b',
            'text-background-opacity': 0.9,
            'text-background-padding': 3,
            'text-rotation': 'autorotate',
            'text-margin-y': -8,
            'opacity': 0.76,
            'text-opacity': 0.78,
            'min-zoomed-font-size': 6,
            'transition-property': 'line-color, target-arrow-color, width, opacity, text-opacity, color',
            'transition-duration': '180ms',
            'transition-timing-function': 'ease-in-out'
        }
    },
    {
        selector: 'edge[relationship = "Uses"], edge[relationship = "Calls"]',
        style: {
            'line-color': '#818cf8',
            'target-arrow-color': '#a5b4fc',
            'color': '#e0e7ff'
        }
    },
    {
        selector: 'edge[relationship = "MapsTo"]',
        style: {
            'line-color': '#2dd4bf',
            'target-arrow-color': '#60a5fa',
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
            'underlay-color': '#93c5fd',
            'underlay-opacity': 0.3,
            'underlay-padding': 14,
            'opacity': 1,
            'z-index': 30
        }
    },
    {
        selector: 'node.neighbor-node',
        style: {
            'border-color': '#86efac',
            'border-width': 3,
            'underlay-opacity': 0.22,
            'opacity': 1,
            'z-index': 18
        }
    },
    {
        selector: 'edge.connected-edge',
        style: {
            'line-color': '#93c5fd',
            'target-arrow-color': '#bfdbfe',
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
            'border-color': '#34d399',
            'border-width': 3,
            'underlay-color': '#34d399',
            'underlay-opacity': 0.24,
            'opacity': 1,
            'z-index': 20
        }
    },
    {
        selector: 'edge.flow-edge',
        style: {
            'line-color': '#34d399',
            'target-arrow-color': '#93c5fd',
            'width': 3,
            'color': '#d1fae5',
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
            'border-color': '#93c5fd',
            'border-width': 3,
            'underlay-color': '#93c5fd',
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
            'underlay-color': '#60a5fa',
            'underlay-opacity': 0.3,
            'underlay-padding': 16,
            'opacity': 1,
            'z-index': 32
        }
    },
    {
        selector: 'edge.endpoint-path-edge',
        style: {
            'line-color': '#60a5fa',
            'target-arrow-color': '#93c5fd',
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

        cancelPendingGraphFits();
        graphLayoutReady = false;

        if (currentGraph) {
            currentGraph.destroy();
            currentGraph = null;
        }
        disconnectResizeObserver();

        existing.innerHTML = '';

        const viewMode = getOption(options, 'viewMode', 'Full');
        const layoutName = normalizeLayoutName(getOption(options, 'layout', 'breadthfirst'));
        currentLayoutName = layoutName;
        currentViewMode = viewMode;

        const cy = cytoscape({
            container: existing,
            elements: [
                ...nodes.map(normalizeNode),
                ...edges.map(normalizeEdge)
            ],
            style: graphStyle,
            layout: {
                name: 'preset'
            },
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

        cy.ready(() => runGraphLayoutAndFit(cy, layoutName, viewMode));

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

        cancelPendingGraphFits();
        clearFocusClasses(currentGraph);
        runGraphLayoutAndFit(currentGraph, currentLayoutName, currentViewMode);
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

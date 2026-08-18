// BlazorMemory — force-directed memory graph
// Vanilla JS spring physics, no external libraries.
// Usage: MemoryGraph.init(dotnetRef, svgId, nodes, edges)
//        MemoryGraph.destroy(svgId)

window.MemoryGraph = (() => {
    const sims = {};

    // ── Public API ────────────────────────────────────────────────────────────

    function init(dotnetRef, svgId, nodes, edges) {
        // Destroy any previous simulation for this SVG (e.g. after RefreshAsync)
        if (sims[svgId]) sims[svgId].stop();

        const nodesArr = Array.from(nodes);
        const edgesArr = Array.from(edges);

        if (nodesArr.length === 0) return;

        // Simulation state: positions and velocities in viewBox space (800 × 500)
        const W = 800, H = 500;
        const state = nodesArr.map(n => ({
            id: n.id,
            x:  n.x,
            y:  n.y,
            vx: 0,
            vy: 0,
            r:  n.radius
        }));

        // Build index for fast lookup
        const idx = {};
        state.forEach((n, i) => idx[n.id] = i);

        const links = edgesArr
            .map(e => ({ si: idx[e.sourceId], ti: idx[e.targetId], sid: e.sourceId, tid: e.targetId }))
            .filter(e => e.si !== undefined && e.ti !== undefined);

        // Attach click listeners to node groups
        nodesArr.forEach(n => {
            const el = document.getElementById('node-' + n.id);
            if (el) {
                el.addEventListener('click', () => {
                    dotnetRef.invokeMethodAsync('OnNodeClicked', n.id);
                });
            }
        });

        let stopped = false;
        let raf;

        function tick() {
            if (stopped) return;

            // ── Forces ────────────────────────────────────────────────────────

            // Reset accumulated force
            for (const n of state) { n.fx = 0; n.fy = 0; }

            // Coulomb repulsion between every pair of nodes
            for (let i = 0; i < state.length; i++) {
                for (let j = i + 1; j < state.length; j++) {
                    const a = state[i], b = state[j];
                    let dx = b.x - a.x || 0.01;
                    let dy = b.y - a.y || 0.01;
                    const d2 = dx * dx + dy * dy;
                    const d  = Math.sqrt(d2);
                    const f  = 6000 / d2;
                    const fx = f * dx / d, fy = f * dy / d;
                    a.fx -= fx; a.fy -= fy;
                    b.fx += fx; b.fy += fy;
                }
            }

            // Hooke spring attraction along edges
            const REST_LEN = 120, SPRING_K = 0.03;
            for (const { si, ti } of links) {
                const a = state[si], b = state[ti];
                const dx = b.x - a.x, dy = b.y - a.y;
                const d  = Math.sqrt(dx * dx + dy * dy) || 1;
                const f  = SPRING_K * (d - REST_LEN);
                const fx = f * dx / d, fy = f * dy / d;
                a.fx += fx; a.fy += fy;
                b.fx -= fx; b.fy -= fy;
            }

            // Weak gravity toward canvas centre
            const GRAVITY = 0.003;
            for (const n of state) {
                n.fx += GRAVITY * (W / 2 - n.x);
                n.fy += GRAVITY * (H / 2 - n.y);
            }

            // ── Integrate ─────────────────────────────────────────────────────

            const DAMP = 0.82;
            let ke = 0;

            for (const n of state) {
                n.vx = (n.vx + n.fx) * DAMP;
                n.vy = (n.vy + n.fy) * DAMP;
                n.x  = Math.max(n.r + 4, Math.min(W - n.r - 4, n.x + n.vx));
                n.y  = Math.max(n.r + 4, Math.min(H - n.r - 4, n.y + n.vy));
                ke  += n.vx * n.vx + n.vy * n.vy;
            }

            // ── Update SVG DOM ────────────────────────────────────────────────

            for (const n of state) {
                const g = document.getElementById('node-' + n.id);
                if (g) g.setAttribute('transform', `translate(${n.x.toFixed(1)},${n.y.toFixed(1)})`);
            }

            for (const { si, ti, sid, tid } of links) {
                const line = document.getElementById('edge-' + sid + '-' + tid);
                if (!line) continue;
                const a = state[si], b = state[ti];
                line.setAttribute('x1', a.x.toFixed(1));
                line.setAttribute('y1', a.y.toFixed(1));
                line.setAttribute('x2', b.x.toFixed(1));
                line.setAttribute('y2', b.y.toFixed(1));
            }

            // Stop simulation once kinetic energy is negligible
            if (ke < 0.4) { stopped = true; return; }

            raf = requestAnimationFrame(tick);
        }

        raf = requestAnimationFrame(tick);
        sims[svgId] = { stop: () => { stopped = true; if (raf) cancelAnimationFrame(raf); } };
    }

    function destroy(svgId) {
        if (sims[svgId]) {
            sims[svgId].stop();
            delete sims[svgId];
        }
    }

    return { init, destroy };
})();

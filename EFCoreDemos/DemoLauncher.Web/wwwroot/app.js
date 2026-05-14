(() => {
    const state = {
        demos: [],
        selectedIndex: 0,
        running: false,
        es: null,
        timer: null,
        startedAt: 0,
        fontSize: 14
    };

    const $grid = document.getElementById('grid');
    const $term = document.getElementById('terminal');
    const $title = document.getElementById('terminal-title');
    const $pill = document.getElementById('status-pill');
    const $elapsed = document.getElementById('elapsed');
    const $btnStop = document.getElementById('btn-stop');
    const $btnDeck = document.getElementById('btn-deck');
    const $btnClear = document.getElementById('btn-clear');
    const $btnFontUp = document.getElementById('btn-font-up');
    const $btnFontDown = document.getElementById('btn-font-down');

    init();

    async function init() {
        if (new URLSearchParams(location.search).get('mode') === 'deck') {
            document.body.classList.add('deck');
        }

        try {
            const r = await fetch('/api/demos');
            const j = await r.json();
            state.demos = j.demos || [];
        } catch (e) {
            appendLine('failed to load /api/demos: ' + e.message, 'stderr');
            return;
        }

        renderGrid();
        select(0);

        $btnStop.addEventListener('click', stop);
        $btnDeck.addEventListener('click', toggleDeck);
        $btnClear.addEventListener('click', clearTerm);
        $btnFontUp.addEventListener('click', () => bumpFont(+1));
        $btnFontDown.addEventListener('click', () => bumpFont(-1));

        document.addEventListener('keydown', onKey);
    }

    function renderGrid() {
        $grid.innerHTML = '';
        state.demos.forEach((d, i) => {
            const tile = document.createElement('div');
            tile.className = 'tile';
            tile.style.setProperty('--block-color', `var(--block-${d.block})`);
            tile.dataset.index = String(i);

            const keyHint = i < 9 ? `<span class="tile-key">${i + 1}</span>` : '';

            tile.innerHTML = `
                <div class="tile-head">
                    <div class="tile-num">${d.insight}</div>
                    <div class="tile-title" style="flex:1">${escapeHtml(d.title)}</div>
                    ${keyHint}
                </div>
                <div class="tile-blurb">${escapeHtml(d.blurb)}</div>
                <div class="tile-actions">
                    <button class="btn btn-run" data-action="run">Run</button>
                    <button class="btn btn-benchmark ${d.supportsBenchmark ? '' : 'disabled'}" data-action="benchmark">Benchmark</button>
                </div>
            `;

            tile.addEventListener('click', e => {
                const btn = e.target.closest('button');
                if (btn) {
                    e.stopPropagation();
                    const action = btn.dataset.action;
                    select(i);
                    if (action === 'run') run(d, 'run');
                    else if (action === 'benchmark' && d.supportsBenchmark) run(d, 'benchmark');
                } else {
                    select(i);
                }
            });

            $grid.appendChild(tile);
        });
    }

    function select(i) {
        if (i < 0 || i >= state.demos.length) return;
        state.selectedIndex = i;
        document.querySelectorAll('.tile').forEach((el, idx) => {
            el.classList.toggle('selected', idx === i);
        });
    }

    async function run(demo, mode) {
        await stop();
        clearTerm();
        state.running = true;
        state.startedAt = Date.now();
        startTimer();

        $title.textContent = `[${mode}] ${demo.id}`;
        $pill.textContent = mode === 'benchmark' ? 'benchmark' : 'running';
        $pill.className = 'pill running';
        $btnStop.disabled = false;

        document.querySelectorAll('.tile').forEach((el, idx) => {
            el.classList.toggle('running', idx === state.selectedIndex);
        });

        appendLine(`-> ${demo.title} (${mode})`, 'cmd');

        const url = `/api/run/${encodeURIComponent(demo.id)}?mode=${encodeURIComponent(mode)}`;
        state.es = new EventSource(url);

        state.es.addEventListener('stdout', e => appendLine(e.data));
        state.es.addEventListener('stderr', e => appendLine(e.data, 'stderr'));
        state.es.addEventListener('info',   e => appendLine(e.data, 'info'));
        state.es.addEventListener('exit',   e => onExit(parseInt(e.data, 10)));

        state.es.onerror = () => {
            if (state.running) {
                // Connection dropped without an exit event -- shut down gracefully.
                onExit(-1);
            }
        };
    }

    async function stop() {
        if (state.es) {
            try { state.es.close(); } catch {}
            state.es = null;
        }
        if (state.running) {
            try { await fetch('/api/stop', { method: 'POST' }); } catch {}
        }
        state.running = false;
        stopTimer();
        $btnStop.disabled = true;
        document.querySelectorAll('.tile').forEach(el => el.classList.remove('running'));
    }

    function onExit(code) {
        if (state.es) {
            try { state.es.close(); } catch {}
            state.es = null;
        }
        state.running = false;
        stopTimer();

        const ok = code === 0;
        $pill.textContent = ok ? 'done' : 'error';
        $pill.className = 'pill ' + (ok ? 'done' : 'error');
        $btnStop.disabled = true;
        document.querySelectorAll('.tile').forEach(el => el.classList.remove('running'));

        const line = document.createElement('span');
        line.className = 'line line-exit' + (ok ? '' : ' fail');
        line.textContent = ok ? '\n[done]' : `\n[exit ${code}]`;
        $term.appendChild(line);
        scrollTermToBottom();
    }

    function appendLine(text, kind) {
        const line = document.createElement('span');
        line.className = 'line' + (kind ? ' line-' + kind : '');
        line.textContent = (text ?? '') + '\n';
        $term.appendChild(line);
        scrollTermToBottom();
    }

    function clearTerm() {
        $term.innerHTML = '';
    }

    function scrollTermToBottom() {
        $term.scrollTop = $term.scrollHeight;
    }

    function startTimer() {
        stopTimer();
        $elapsed.textContent = '00:00';
        state.timer = setInterval(() => {
            const s = Math.floor((Date.now() - state.startedAt) / 1000);
            const mm = String(Math.floor(s / 60)).padStart(2, '0');
            const ss = String(s % 60).padStart(2, '0');
            $elapsed.textContent = `${mm}:${ss}`;
        }, 250);
    }

    function stopTimer() {
        if (state.timer) { clearInterval(state.timer); state.timer = null; }
    }

    function toggleDeck() {
        document.body.classList.toggle('deck');
    }

    function bumpFont(delta) {
        state.fontSize = Math.max(10, Math.min(36, state.fontSize + delta * 2));
        document.documentElement.style.setProperty('--term-size', state.fontSize + 'px');
    }

    function onKey(e) {
        const tag = (e.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea') return;

        if (e.key === 'Escape') { stop(); e.preventDefault(); return; }
        if (e.key === 'f' || e.key === 'F') { toggleDeck(); e.preventDefault(); return; }
        if (e.key === 'c' || e.key === 'C') { clearTerm(); e.preventDefault(); return; }

        if (/^[1-9]$/.test(e.key)) {
            select(parseInt(e.key, 10) - 1);
            e.preventDefault(); return;
        }
        if (e.key === 'Enter') {
            const d = state.demos[state.selectedIndex];
            if (d) run(d, 'run');
            e.preventDefault(); return;
        }
        if (e.key === 'b' || e.key === 'B') {
            const d = state.demos[state.selectedIndex];
            if (d && d.supportsBenchmark) run(d, 'benchmark');
            e.preventDefault(); return;
        }
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }
})();

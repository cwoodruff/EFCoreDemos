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
    const $info = document.getElementById('info');
    const $infoTitle = document.getElementById('info-title');
    const $infoChips = document.getElementById('info-chips');
    const $infoBody = document.getElementById('info-body');
    const $infoClose = document.getElementById('info-close');

    const SOURCE_ROOT = 'https://github.com/cwoodruff/EFCoreDemos/tree/main/EFCoreDemos/';
    const BLOCK_NAMES = {
        Tracking: 'Tracking',
        QueryShape: 'Query Shape',
        PowerTools: 'Power Tools',
        Architecture: 'Architecture',
        Ops: 'Ops'
    };

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
        $infoClose.addEventListener('click', () => $info.close());
        // Click on the backdrop (outside the panel) closes it too.
        $info.addEventListener('click', e => { if (e.target === $info) $info.close(); });

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
                    <button class="info-btn" data-action="info" title="About this demo" aria-label="About ${escapeHtml(d.title)}">i</button>
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
                    if (action === 'info') openInfo(d);
                    else if (action === 'run') run(d, 'run');
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
        state.es.addEventListener('exit',   e => onExit(parseInt(e.data, 10), demo));

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

    function onExit(code, demo) {
        if (state.es) {
            try { state.es.close(); } catch {}
            state.es = null;
        }
        state.running = false;
        stopTimer();

        const ok = code === 0;
        // dotnet test exits 1 when tests fail - for the testing-strategies demo, failing tests are the point.
        const expected = !ok && code > 0 && demo && demo.specialCase === 'dotnet-test';
        const status = ok ? 'done' : expected ? 'expected' : 'error';
        $pill.textContent = status;
        $pill.className = 'pill ' + status;
        $btnStop.disabled = true;
        document.querySelectorAll('.tile').forEach(el => el.classList.remove('running'));

        const line = document.createElement('span');
        line.className = 'line line-exit' + (ok ? '' : expected ? ' expected' : ' fail');
        line.textContent = ok ? '\n[done]'
            : expected ? `\n[exit ${code} - expected: provider gaps found, the failing tests are the demo]`
            : `\n[exit ${code}]`;
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
        // While the info panel is open, leave keys to it: the dialog closes itself on Esc,
        // and Esc must not also stop a running demo.
        if ($info.open) return;

        if (e.key === 'i' || e.key === 'I') {
            const d = state.demos[state.selectedIndex];
            if (d) openInfo(d);
            e.preventDefault(); return;
        }

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

    // ---- Demo info panel -------------------------------------------------------------------------------
    // Every demo gets the same sections in the same order. Text comes from demos.json "info";
    // the type, run commands and source link are derived from the demo's own manifest fields.

    function demoType(d) {
        if (d.specialCase === 'dotnet-test') return 'Test suite (xUnit)';
        if (d.specialCase === 'webserver-curl') return 'Web API + curl';
        if (d.supportsBenchmark) return 'Console demo + benchmark';
        return 'Console demo';
    }

    function runCommands(d) {
        const p = d.projectPath;
        if (d.specialCase === 'dotnet-test') {
            return [
                { cmd: `dotnet test ${p} -c Release`, note: 'Runs the test suite. Failing tests are expected - they are the demo.' }
            ];
        }
        const cmds = [{ cmd: `dotnet run --project ${p} -c Release`, note: 'Run (Enter)' }];
        if (d.specialCase === 'webserver-curl') {
            const port = d.webServerPort;
            (d.webServerEndpoints || []).forEach(ep =>
                cmds.push({ cmd: `curl http://localhost:${port}${ep}`, note: 'The launcher calls each endpoint in turn' }));
        }
        if (d.supportsBenchmark) {
            cmds.push({
                cmd: `dotnet run --project ${p} -c Release -- benchmark`,
                note: 'BenchmarkDotNet harness. The launcher\'s Benchmark button (B) replays a recorded run.'
            });
        }
        return cmds;
    }

    function sourceUrl(d) {
        const folder = d.projectPath.includes('/') ? d.projectPath.slice(0, d.projectPath.lastIndexOf('/')) : d.projectPath;
        return SOURCE_ROOT + folder;
    }

    function openInfo(d) {
        const info = d.info || {};
        const missing = '<p class="info-missing">Not documented yet.</p>';
        const list = items => (items && items.length)
            ? `<ul>${items.map(x => `<li>${escapeHtml(x)}</li>`).join('')}</ul>` : missing;
        const para = text => text ? `<p>${escapeHtml(text)}</p>` : missing;
        const section = (title, html) => `<section class="info-section"><h3>${title}</h3>${html}</section>`;

        $info.style.setProperty('--block-color', `var(--block-${d.block})`);
        $infoTitle.textContent = d.title;
        $infoChips.innerHTML = [
            `<span class="chip chip-block">${escapeHtml(BLOCK_NAMES[d.block] || d.block)}</span>`,
            `<span class="chip">Insight ${escapeHtml(d.insight)}</span>`,
            `<span class="chip">${escapeHtml(demoType(d))}</span>`
        ].join('');

        const apis = (info.keyApis && info.keyApis.length)
            ? `<div class="info-apis">${info.keyApis.map(a => `<code>${escapeHtml(a)}</code>`).join('')}</div>` : missing;

        const requirements = `<dl class="info-dl">
                <dt>Database</dt><dd>${escapeHtml(info.database || 'Not documented yet.')}</dd>
                <dt>Setup</dt><dd>${escapeHtml(info.setup || 'Not documented yet.')}</dd>
            </dl>`;

        const howToRun = `<div class="info-cmds">${runCommands(d).map(c => `
                <div class="info-cmd"><code>${escapeHtml(c.cmd)}</code><span>${escapeHtml(c.note)}</span></div>`).join('')}
            </div>`;

        const links = (info.learnLinks || []).filter(l => /^https:\/\//.test(l.url || ''));
        const learn = links.length
            ? `<ul class="info-links">${links.map(l =>
                `<li><a href="${escapeHtml(l.url)}" target="_blank" rel="noopener noreferrer">${escapeHtml(l.title)}</a></li>`).join('')}</ul>`
            : missing;

        const src = sourceUrl(d);
        const source = `<p><a href="${escapeHtml(src)}" target="_blank" rel="noopener noreferrer">${escapeHtml(d.projectPath)}</a></p>`;

        $infoBody.innerHTML = [
            section('Overview', para(info.summary)),
            section('What you\'ll see', list(info.highlights)),
            section('Key APIs', apis),
            section('Takeaway', info.takeaway ? `<p class="info-takeaway">${escapeHtml(info.takeaway)}</p>` : missing),
            section('Requirements', requirements),
            section('How to run', howToRun),
            section('Learn more on Microsoft Learn', learn),
            section('Source', source)
        ].join('');

        $infoBody.scrollTop = 0;
        if (!$info.open) $info.showModal();
        $infoClose.focus();
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
})();

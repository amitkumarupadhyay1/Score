namespace Score
{
    internal static class QuizBuzzerWebAssets
    {
        public static string GetMobileHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, user-scalable=no, maximum-scale=1.0"">
    <title>QuizScore Live · Mobile Buzzer</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; -webkit-tap-highlight-color: transparent; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
            background-color: #0F172A;
            color: #F8FAFC;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: space-between;
            padding: 24px 20px;
            user-select: none;
            -webkit-user-select: none;
        }
        .header {
            text-align: center;
            margin-top: 10px;
            width: 100%;
            max-width: 420px;
        }
        .brand {
            font-size: 13px;
            font-weight: 700;
            letter-spacing: 2px;
            text-transform: uppercase;
            color: #38BDF8;
            margin-bottom: 6px;
        }
        .title {
            font-size: 24px;
            font-weight: 800;
            color: #FFFFFF;
        }
        .card {
            background-color: #1E293B;
            border: 1px solid #334155;
            border-radius: 14px;
            padding: 16px;
            width: 100%;
            max-width: 420px;
            margin: 20px 0;
        }
        .label {
            display: block;
            font-size: 11px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 1px;
            color: #94A3B8;
            margin-bottom: 8px;
        }
        select {
            width: 100%;
            height: 48px;
            font-size: 16px;
            font-weight: 600;
            color: #FFFFFF;
            background-color: #0F172A;
            border: 1.5px solid #475569;
            border-radius: 10px;
            padding: 0 14px;
            outline: none;
        }
        select:focus {
            border-color: #005A9C;
        }
        .buzzer-container {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            width: 100%;
            max-width: 420px;
            margin: 20px 0;
        }
        .buzzer-btn {
            width: 240px;
            height: 240px;
            border-radius: 50%;
            border: 6px solid #B91C1C;
            background: #DC2626;
            color: #FFFFFF;
            font-size: 28px;
            font-weight: 900;
            letter-spacing: 1.5px;
            box-shadow: 0 12px 0 #991B1B, 0 18px 25px rgba(0, 0, 0, 0.45);
            cursor: pointer;
            outline: none;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: transform 0.06s ease, box-shadow 0.06s ease, background-color 0.1s ease;
        }
        .buzzer-btn:active, .buzzer-btn.pressed {
            transform: translateY(10px);
            box-shadow: 0 2px 0 #991B1B, 0 6px 12px rgba(0, 0, 0, 0.4);
            background: #EF4444;
        }
        .buzzer-btn.buzzed-state {
            background: #059669;
            border-color: #047857;
            box-shadow: 0 6px 0 #065F46, 0 10px 20px rgba(0, 0, 0, 0.35);
        }
        .status-box {
            text-align: center;
            margin-top: 16px;
            min-height: 48px;
        }
        .status-badge {
            display: inline-block;
            font-size: 13px;
            font-weight: 700;
            padding: 6px 16px;
            border-radius: 20px;
            background-color: #334155;
            color: #E2E8F0;
        }
        .status-badge.success {
            background-color: #065F46;
            color: #34D399;
        }
        .footer {
            font-size: 11px;
            color: #64748B;
            text-align: center;
            margin-bottom: 8px;
        }
    </style>
</head>
<body>
    <div class=""header"">
        <div class=""brand"">QUIZSCORE LIVE</div>
        <div class=""title"">Contestant Buzzer</div>
    </div>

    <div class=""card"">
        <div style=""display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;"">
            <label class=""label"" for=""teamSelect"" style=""margin-bottom:0;"">Your Team</label>
            <button id=""unlockTeamBtn"" type=""button"" style=""background:none; border:none; color:#0284C7; font-size:11px; font-weight:700; cursor:pointer; display:none;"">UNLOCK / CHANGE</button>
        </div>
        <select id=""teamSelect"">
            <option value=""Team 1"">Team 1</option>
            <option value=""Team 2"">Team 2</option>
            <option value=""Team 3"">Team 3</option>
            <option value=""Team 4"">Team 4</option>
        </select>
        <div id=""teamLockNotice"" style=""display:none; font-size:11px; color:#10B981; font-weight:600; margin-top:6px;"">Locked to your team for this session</div>
    </div>

    <!-- Pounce (Secret Answer) Section -->
    <div id=""pounceCard"" class=""card"" style=""display:none; border:2px solid #D97706; background:linear-gradient(180deg, #1E293B 0%, #0F172A 100%);"">
        <div style=""display:flex; justify-content:space-between; align-items:center; margin-bottom:8px;"">
            <span class=""label"" style=""color:#F59E0B; margin-bottom:0; font-weight:800;"">CHIT POUNCE (RISK/REWARD)</span>
            <span id=""pounceBadge"" style=""font-size:10px; font-weight:800; padding:2px 8px; border-radius:10px; background:#B45309; color:#FEF3C7;"">LIVE</span>
        </div>
        <p style=""font-size:12px; color:#FDE68A; margin-bottom:10px;"">Submit secret answer directly to host. Bonus for correct, penalty for wrong.</p>
        <div style=""display:flex; flex-direction:column; gap:8px;"">
            <input type=""text"" id=""pounceInput"" placeholder=""Secret Answer..."" maxlength=""120"" style=""width:100%; height:44px; font-size:15px; font-weight:600; color:#FFFFFF; background:#0F172A; border:1.5px solid #D97706; border-radius:8px; padding:0 12px; outline:none;"" />
            <button id=""pounceBtn"" type=""button"" style=""width:100%; height:46px; background:#D97706; border:2px solid #B45309; border-radius:8px; color:#FFF; font-size:16px; font-weight:900; letter-spacing:1px; cursor:pointer; box-shadow:0 4px 10px rgba(217,119,6,0.4);"">POUNCE</button>
        </div>
        <div id=""pounceStatus"" style=""display:none; font-size:12px; font-weight:700; margin-top:10px; text-align:center; padding:8px 10px; border-radius:6px; line-height:1.4;""></div>
    </div>

    <div class=""buzzer-container"">
        <button id=""buzzerBtn"" class=""buzzer-btn"" type=""button"">BUZZ IN!</button>
        <div class=""status-box"">
            <span id=""statusBadge"" class=""status-badge"">READY TO BUZZ</span>
        </div>
    </div>

    <div class=""footer"">Connected directly to local quiz desk via Wi-Fi</div>

    <script>
        const btn = document.getElementById('buzzerBtn');
        const teamSelect = document.getElementById('teamSelect');
        const statusBadge = document.getElementById('statusBadge');
        const unlockTeamBtn = document.getElementById('unlockTeamBtn');
        const teamLockNotice = document.getElementById('teamLockNotice');
        let isBuzzed = false;
        let lastBuzzTime = 0;
        let wakeLock = null;

        // Screen Wake Lock API to prevent phone from locking/sleeping during quiz
        async function requestWakeLock() {
            try {
                if ('wakeLock' in navigator) {
                    wakeLock = await navigator.wakeLock.request('screen');
                    wakeLock.addEventListener('release', () => { wakeLock = null; });
                }
            } catch (e) {}
        }
        requestWakeLock();
        document.addEventListener('visibilitychange', () => {
            if (wakeLock === null && document.visibilityState === 'visible') {
                requestWakeLock();
            }
        });
        window.addEventListener('click', () => { if (!wakeLock) requestWakeLock(); }, { once: true });
        window.addEventListener('touchstart', () => { if (!wakeLock) requestWakeLock(); }, { once: true });

        function lockTeam(team) {
            if (!team) return;
            localStorage.setItem('quiz_team', team);
            teamSelect.disabled = true;
            if (unlockTeamBtn) unlockTeamBtn.style.display = 'inline-block';
            if (teamLockNotice) teamLockNotice.style.display = 'block';
        }

        function unlockTeam() {
            if (confirm('Change team? This will reset your current team assignment.')) {
                teamSelect.disabled = false;
                if (unlockTeamBtn) unlockTeamBtn.style.display = 'none';
                if (teamLockNotice) teamLockNotice.style.display = 'none';
                localStorage.removeItem('quiz_team');
            }
        }
        if (unlockTeamBtn) unlockTeamBtn.addEventListener('click', unlockTeam);

        // Restore chosen team from localStorage so contestants cannot hijack another team's buzz mid-round
        const savedTeam = localStorage.getItem('quiz_team');
        if (savedTeam) {
            let found = false;
            for (let i = 0; i < teamSelect.options.length; i++) {
                if (teamSelect.options[i].value === savedTeam) {
                    teamSelect.selectedIndex = i;
                    found = true;
                    break;
                }
            }
            if (!found) {
                const opt = document.createElement('option');
                opt.value = savedTeam;
                opt.textContent = savedTeam;
                opt.selected = true;
                teamSelect.appendChild(opt);
            }
            lockTeam(savedTeam);
        }

        teamSelect.addEventListener('change', () => {
            if (teamSelect.value) {
                lockTeam(teamSelect.value);
            }
            resetStatus();
        });

        // Load active team roster from host
        fetch('/api/teams')
            .then(r => r.json())
            .then(teams => {
                if (teams && teams.length > 0) {
                    const current = localStorage.getItem('quiz_team') || teamSelect.value;
                    teamSelect.innerHTML = '';
                    teams.forEach(t => {
                        const opt = document.createElement('option');
                        opt.value = t;
                        opt.textContent = t;
                        if (t === current) opt.selected = true;
                        teamSelect.appendChild(opt);
                    });
                    if (current) {
                        teamSelect.value = current;
                        lockTeam(current);
                    }
                }
            })
            .catch(() => {});

        function resetStatus() {
            isBuzzed = false;
            btn.classList.remove('buzzed-state');
            btn.textContent = 'BUZZ IN!';
            statusBadge.textContent = 'READY TO BUZZ';
            statusBadge.className = 'status-badge';
        }

        function playHapticBeep() {
            try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(880, ctx.currentTime);
                gain.gain.setValueAtTime(0.3, ctx.currentTime);
                gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.12);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start();
                osc.stop(ctx.currentTime + 0.12);
            } catch(e) {}
        }

        function sendBuzz(e) {
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }
            const now = Date.now();
            if (isBuzzed || (now - lastBuzzTime < 1200)) return;
            lastBuzzTime = now;
            isBuzzed = true;

            const team = teamSelect.value;
            if (!team) return;

            btn.classList.add('pressed');
            setTimeout(() => btn.classList.remove('pressed'), 140);
            playHapticBeep();

            btn.classList.add('buzzed-state');
            btn.textContent = 'BUZZED!';
            statusBadge.textContent = 'TRANSMITTING BUZZ FOR ' + team.toUpperCase() + '...';
            statusBadge.className = 'status-badge success';

            fetch('/api/buzz', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ team: team })
            })
            .then(r => {
                if (r.status === 423) {
                    statusBadge.textContent = 'BUZZER LOCKED BY HOST';
                    statusBadge.className = 'status-badge';
                    btn.classList.remove('buzzed-state');
                    btn.textContent = 'BUZZ IN!';
                    setTimeout(resetStatus, 2500);
                    return null;
                }
                return r.json();
            })
            .then(res => {
                if (!res) return;
                // Haptic feedback upon successful buzz
                if (navigator.vibrate) {
                    try { navigator.vibrate([80, 40, 80]); } catch(err) {}
                }
                statusBadge.textContent = 'BUZZ REGISTERED FOR ' + team.toUpperCase();
                setTimeout(resetStatus, 3000);
            })
            .catch(err => {
                statusBadge.textContent = 'NETWORK ERROR - BUZZ NOT RECEIVED';
                statusBadge.className = 'status-badge error-badge';
                btn.classList.remove('buzzed-state');
                setTimeout(resetStatus, 3500);
            });
        }

        if (window.PointerEvent) {
            btn.addEventListener('pointerdown', sendBuzz);
        } else {
            btn.addEventListener('touchstart', sendBuzz);
            btn.addEventListener('click', sendBuzz);
        }

        // Pounce (Chit Pounce Subsystem)
        const pounceCard = document.getElementById('pounceCard');
        const pounceInput = document.getElementById('pounceInput');
        const pounceBtn = document.getElementById('pounceBtn');
        const pounceStatus = document.getElementById('pounceStatus');
        let pounceSubmitted = false;

        function setPounceStatus(text, isSuccess, isError) {
            if (!pounceStatus) return;
            pounceStatus.style.display = 'block';
            pounceStatus.textContent = text;
            if (isSuccess) {
                pounceStatus.style.background = '#065F46';
                pounceStatus.style.color = '#34D399';
            } else if (isError) {
                pounceStatus.style.background = '#831843';
                pounceStatus.style.color = '#F472B6';
            } else {
                pounceStatus.style.background = '#334155';
                pounceStatus.style.color = '#E2E8F0';
            }
        }

        async function fetchServerState() {
            try {
                const team = teamSelect ? teamSelect.value : '';
                const url = '/api/state' + (team ? '?team=' + encodeURIComponent(team) : '');
                const res = await fetch(url);
                if (!res.ok) return;
                const state = await res.json();
                if (pounceCard) {
                    if (state.pounceEnabled) {
                        pounceCard.style.display = 'block';
                        if (state.hasPounced) {
                            pounceSubmitted = true;
                            if (pounceInput) pounceInput.disabled = true;
                            if (pounceBtn) {
                                pounceBtn.disabled = true;
                                pounceBtn.style.opacity = '0.5';
                            }
                            setPounceStatus('Pounce submitted! Awaiting host adjudication.', true, false);
                        }
                    } else {
                        pounceCard.style.display = 'none';
                        if (state.pounceCount === 0) {
                            pounceSubmitted = false;
                            if (pounceInput) {
                                pounceInput.disabled = false;
                                pounceInput.value = '';
                            }
                            if (pounceBtn) {
                                pounceBtn.disabled = false;
                                pounceBtn.style.opacity = '1';
                            }
                            if (pounceStatus) pounceStatus.style.display = 'none';
                        }
                    }
                }
            } catch (e) {}
        }
        setInterval(fetchServerState, 1500);
        fetchServerState();

        async function sendPounce(e) {
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }
            if (pounceSubmitted) return;
            const team = teamSelect ? teamSelect.value : '';
            if (!team) {
                alert('Please select your team first.');
                return;
            }
            const answer = pounceInput ? (pounceInput.value || '').trim() : '';
            if (!answer) {
                if (pounceInput) pounceInput.focus();
                return;
            }

            if (pounceBtn) pounceBtn.disabled = true;
            setPounceStatus('Submitting pounce...', false, false);

            try {
                const res = await fetch('/api/pounce', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ team: team, answer: answer })
                });
                const data = await res.json().catch(() => ({}));
                if (res.ok || data.status === 'ok') {
                    pounceSubmitted = true;
                    if (pounceInput) pounceInput.disabled = true;
                    if (pounceBtn) pounceBtn.style.opacity = '0.5';
                    setPounceStatus('Pounce submitted! Awaiting host adjudication.', true, false);
                    if (navigator.vibrate) {
                        try { navigator.vibrate([100, 50, 100]); } catch (v) {}
                    }
                } else if (res.status === 423 || data.status === 'locked') {
                    if (pounceBtn) pounceBtn.disabled = false;
                    setPounceStatus('Pounce locked by host.', false, true);
                } else if (res.status === 409 || data.status === 'duplicate') {
                    pounceSubmitted = true;
                    if (pounceInput) pounceInput.disabled = true;
                    if (pounceBtn) pounceBtn.style.opacity = '0.5';
                    setPounceStatus('Pounce already submitted for your team.', false, true);
                } else {
                    if (pounceBtn) pounceBtn.disabled = false;
                    setPounceStatus(data.error || 'Failed to submit pounce.', false, true);
                }
            } catch (err) {
                if (pounceBtn) pounceBtn.disabled = false;
                setPounceStatus('Network error submitting pounce.', false, true);
            }
        }

        if (pounceBtn) {
            pounceBtn.addEventListener('click', sendPounce);
        }
    </script>
</body>
</html>";
        }
    }
}

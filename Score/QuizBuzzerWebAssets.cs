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
            color: #818CF8;
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
            border-color: #6366F1;
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
        <div class=""brand"">✦ QUIZSCORE LIVE</div>
        <div class=""title"">Contestant Buzzer</div>
    </div>

    <div class=""card"">
        <label class=""label"" for=""teamSelect"">Select Your Team</label>
        <select id=""teamSelect"">
            <option value=""Team 1"">Team 1</option>
            <option value=""Team 2"">Team 2</option>
            <option value=""Team 3"">Team 3</option>
            <option value=""Team 4"">Team 4</option>
        </select>
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
        let isBuzzed = false;
        let lastBuzzTime = 0;

        // Restore chosen team from localStorage
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
        }

        teamSelect.addEventListener('change', () => {
            localStorage.setItem('quiz_team', teamSelect.value);
            resetStatus();
        });

        // Load active team roster from host
        fetch('/api/teams')
            .then(r => r.json())
            .then(teams => {
                if (teams && teams.length > 0) {
                    const current = teamSelect.value || savedTeam;
                    teamSelect.innerHTML = '';
                    teams.forEach(t => {
                        const opt = document.createElement('option');
                        opt.value = t;
                        opt.textContent = t;
                        if (t === current) opt.selected = true;
                        teamSelect.appendChild(opt);
                    });
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

            if (navigator.vibrate) {
                try { navigator.vibrate([70]); } catch(err) {}
            }
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
            .then(r => r.json())
            .then(res => {
                statusBadge.textContent = '✓ BUZZ REGISTERED FOR ' + team.toUpperCase();
                setTimeout(resetStatus, 3000);
            })
            .catch(err => {
                statusBadge.textContent = '✓ BUZZ SENT FOR ' + team.toUpperCase();
                setTimeout(resetStatus, 3000);
            });
        }

        if (window.PointerEvent) {
            btn.addEventListener('pointerdown', sendBuzz);
        } else {
            btn.addEventListener('touchstart', sendBuzz);
            btn.addEventListener('click', sendBuzz);
        }
    </script>
</body>
</html>";
        }
    }
}

window.blazorInterop = {
    getItem: function (key) {
        return window.localStorage.getItem(key);
    },
    setItem: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    },
    canShare: function () {
        return typeof navigator.share === "function";
    },
    share: async function (title, text, url) {
        try {
            await navigator.share({ title, text, url });
            return true;
        } catch {
            // User cancelled the share sheet, or the platform rejected it — either
            // way the caller should just fall back to the copy buttons, not show an error.
            return false;
        }
    },
    playSound: function (name) {
        try {
            playSequence(getAudioContext(), SOUND_SEQUENCES[name] || []);
        } catch {
            // Web Audio blocked/unavailable (autoplay policy, older browser, etc.) —
            // sound is a nice-to-have, never worth breaking gameplay over.
        }
    },
    confetti: function () {
        try {
            burstConfetti();
        } catch {
            // Purely decorative — never worth breaking the game-over screen over.
        }
    },
};

// Sonidos sintetizados con Web Audio (osciladores), sin archivos de audio externos:
// autocontenido, sin licencias que verificar, y liviano para un juego pensado para
// que niños aprendan inglés e historia de Chile jugando.
let _audioCtx = null;
function getAudioContext() {
    if (!_audioCtx) {
        _audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (_audioCtx.state === "suspended") {
        _audioCtx.resume();
    }
    return _audioCtx;
}

const SOUND_SEQUENCES = {
    // Pregunta enviada: dos notas cortas ascendentes, alegre y breve.
    ask: [
        { freq: 480, dur: 0.09, type: "sine", gain: 0.2 },
        { freq: 640, dur: 0.11, type: "sine", gain: 0.2 },
    ],
    // Respuesta recibida: un solo "ding" de confirmación.
    answer: [{ freq: 720, dur: 0.14, type: "sine", gain: 0.22 }],
    // Marcar/desmarcar un personaje: click seco y muy corto.
    toggle: [{ freq: 340, dur: 0.045, type: "square", gain: 0.12 }],
    // Perder: tres notas descendentes, tono "trombón triste" pero breve, no tétrico.
    lose: [
        { freq: 311.13, dur: 0.16, type: "sawtooth", gain: 0.18 },
        { freq: 233.08, dur: 0.18, type: "sawtooth", gain: 0.18 },
        { freq: 155.56, dur: 0.32, type: "sawtooth", gain: 0.18 },
    ],
    // Ganar: arpegio ascendente mayor (do-mi-sol-do agudo), sonido victorioso.
    win: [
        { freq: 523.25, dur: 0.11, type: "triangle", gain: 0.24 },
        { freq: 659.25, dur: 0.11, type: "triangle", gain: 0.24 },
        { freq: 783.99, dur: 0.11, type: "triangle", gain: 0.24 },
        { freq: 1046.5, dur: 0.32, type: "triangle", gain: 0.26 },
    ],
};

function playSequence(ctx, notes) {
    let t = ctx.currentTime;
    for (const note of notes) {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = note.type;
        osc.frequency.setValueAtTime(note.freq, t);
        gain.gain.setValueAtTime(0.0001, t);
        gain.gain.exponentialRampToValueAtTime(note.gain, t + 0.01);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + note.dur);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(t);
        osc.stop(t + note.dur + 0.02);
        t += note.dur * 0.85;
    }
}

// Confeti con Web Animations API — nada de librerías externas ni canvas: unos divs
// de colores animados con "animate()" y autolimpieza al terminar.
function burstConfetti() {
    const colors = ["#FF6B6B", "#FFD93D", "#6BCB77", "#4D96FF", "#B983FF", "#FF9F45"];
    const container = document.createElement("div");
    container.style.cssText = "position:fixed;inset:0;pointer-events:none;z-index:9999;overflow:hidden;";
    document.body.appendChild(container);

    const pieceCount = 90;
    for (let i = 0; i < pieceCount; i++) {
        const el = document.createElement("div");
        const size = 6 + Math.random() * 6;
        const duration = 2200 + Math.random() * 1400;
        const delay = Math.random() * 400;
        const rotateStart = Math.random() * 360;
        const drift = (Math.random() - 0.5) * 240;

        el.style.cssText =
            `position:absolute;top:-20px;left:${Math.random() * 100}%;` +
            `width:${size}px;height:${size * 0.4}px;background:${colors[i % colors.length]};` +
            "opacity:0.9;border-radius:2px;";

        el.animate(
            [
                { transform: `translate(0, 0) rotate(${rotateStart}deg)`, opacity: 1 },
                {
                    transform: `translate(${drift}px, 100vh) rotate(${rotateStart + 360 + Math.random() * 360}deg)`,
                    opacity: 0.9,
                },
            ],
            { duration, delay, easing: "ease-in", fill: "forwards" },
        );

        container.appendChild(el);
    }

    setTimeout(() => container.remove(), 4200);
}

// Verifica el contrato real de SignalR: negotiate (HTTP) + upgrade a WebSocket +
// mensaje de handshake del protocolo. Un despliegue puede responder /healthz en 200 y
// aun así rechazar WebSocket (ingress/proxy sin soporte de upgrade, timeout distinto
// para conexiones persistentes, etc.) — eso es exactamente lo que este chequeo detecta
// y un curl a /healthz no. Usa fetch/WebSocket globales de Node (18+/22+), sin
// dependencias npm, para no complicar el pipeline de despliegue con un install extra.
const baseUrl = process.argv[2];
if (!baseUrl) {
  console.error('Uso: node smoke-test-ws-check.mjs <baseUrl>');
  process.exit(2);
}

const httpBase = baseUrl.replace(/\/$/, '');
const wsBase = httpBase.replace(/^http/, 'ws');
const TIMEOUT_MS = 15000;

function withTimeout(promise, ms, label) {
  return Promise.race([
    promise,
    new Promise((_, reject) => setTimeout(() => reject(new Error(`timeout esperando ${label}`)), ms)),
  ]);
}

try {
  const negotiateResp = await withTimeout(
    fetch(`${httpBase}/hub/game/negotiate?negotiateVersion=1`, { method: 'POST' }),
    TIMEOUT_MS,
    'negotiate',
  );
  if (!negotiateResp.ok) {
    throw new Error(`negotiate respondió ${negotiateResp.status}`);
  }
  const negotiate = await negotiateResp.json();
  const connectionToken = negotiate.connectionToken ?? negotiate.connectionId;
  if (!connectionToken) {
    throw new Error(`negotiate no devolvió connectionToken/connectionId: ${JSON.stringify(negotiate)}`);
  }

  const wsUrl = `${wsBase}/hub/game?id=${encodeURIComponent(connectionToken)}`;

  const handshakeOk = await withTimeout(
    new Promise((resolve, reject) => {
      const ws = new WebSocket(wsUrl);
      ws.addEventListener('open', () => {
        ws.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\x1e');
      });
      ws.addEventListener('message', (event) => {
        const text = typeof event.data === 'string' ? event.data : '';
        if (text.includes('{}')) {
          ws.close(1000);
          resolve(true);
        } else {
          ws.close();
          reject(new Error(`respuesta de handshake inesperada: ${text}`));
        }
      });
      ws.addEventListener('error', () => reject(new Error('error de WebSocket durante el handshake')));
      ws.addEventListener('close', (event) => {
        if (!event.wasClean && event.code !== 1000) {
          reject(new Error(`WebSocket cerrado inesperadamente: code=${event.code} reason=${event.reason}`));
        }
      });
    }),
    TIMEOUT_MS,
    'handshake de WebSocket',
  );

  if (handshakeOk) {
    console.log('OK: negotiate + upgrade a WebSocket + handshake de protocolo SignalR confirmados');
    process.exit(0);
  }
} catch (err) {
  console.error(`FALLO: ${err.message ?? err}`);
  process.exit(1);
}

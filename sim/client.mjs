// Simulates a React client (WebSocket) + the Market Data service (Redis tick publisher).
// No external deps: Node 22 has a global WebSocket; Redis PUBLISH is done over a raw TCP socket.
import net from 'node:net';

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const [k, ...v] = a.replace(/^--/, '').split('=');
    return [k, v.join('=')];
  }),
);

const WS_URL = args.ws ?? 'ws://localhost:8080/ws';
const REDIS_HOST = args.redisHost ?? 'localhost';
const REDIS_PORT = Number(args.redisPort ?? 6379);

const symbol = args.symbol ?? 'BTCUSDT';
const buy = { name: args.buyEx ?? 'Binance', contractType: args.buyCt ?? 'spot' };
const sell = { name: args.sellEx ?? 'Aster', contractType: args.sellCt ?? 'perpetual' };

const buyTopic = `tick:${buy.name}:${symbol}:${buy.contractType}`;
const sellTopic = `tick:${sell.name}:${symbol}:${sell.contractType}`;

// Timed scenario steps (ms after socket open). Default exercises forward + ping + unsubscribe.
const scenario = args.scenario
  ? JSON.parse(args.scenario)
  : [
      { at: 800, action: 'publishBuy' },
      { at: 1100, action: 'publishSell' },
      { at: 2000, action: 'ping' },
      { at: 3000, action: 'unsubscribe' },
      { at: 4000, action: 'close' },
    ];
const holdMs = Number(args.holdMs ?? 0); // keep open this long with no pings (expiry test)

const log = (...m) => console.log(`[${new Date().toISOString()}]`, ...m);

// --- raw Redis PUBLISH (RESP) -------------------------------------------------
function redisPublish(channel, message) {
  return new Promise((resolve, reject) => {
    const sock = net.createConnection(REDIS_PORT, REDIS_HOST, () => {
      const parts = ['PUBLISH', channel, message];
      const cmd =
        `*${parts.length}\r\n` + parts.map((p) => `$${Buffer.byteLength(p)}\r\n${p}\r\n`).join('');
      sock.write(cmd);
    });
    sock.on('data', (d) => {
      sock.end();
      resolve(d.toString().trim()); // ":N" = number of receivers
    });
    sock.on('error', reject);
  });
}

function nowFormatted() {
  const d = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return `${p(d.getMonth() + 1)}/${p(d.getDate())}/${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

function tick(exchange, contractType, bid, ask) {
  return JSON.stringify({
    exchange,
    symbol,
    contractType,
    bestBid: bid,
    bestAsk: ask,
    receivedAt: nowFormatted(),
  });
}

// --- WebSocket client ---------------------------------------------------------
const ws = new WebSocket(WS_URL);
let received = 0;

ws.addEventListener('open', () => {
  log('WS open ->', WS_URL);
  const subParams = { symbol, buyExchange: buy, sellExchange: sell };
  ws.send(JSON.stringify({ action: 'subscribe', params: subParams }));
  log('SENT subscribe', JSON.stringify(subParams));

  for (const step of scenario) {
    setTimeout(() => runStep(step, subParams), step.at);
  }

  if (holdMs > 0) {
    log(`HOLDING open ${holdMs}ms with no pings (expiry test)`);
    setTimeout(() => {}, holdMs); // keep loop alive; server should close us first
  }
});

ws.addEventListener('message', (ev) => {
  received += 1;
  log('RECV tick #' + received, ev.data);
});

ws.addEventListener('close', (ev) => {
  log(`WS closed code=${ev.code} reason="${ev.reason}" totalTicksReceived=${received}`);
  process.exit(0);
});

ws.addEventListener('error', (e) => log('WS error', e.message ?? e));

async function runStep(step, subParams) {
  switch (step.action) {
    case 'publishBuy': {
      const r = await redisPublish(buyTopic, tick(buy.name, buy.contractType, 69000.045, 69001.002));
      log(`PUBLISH ${buyTopic} -> receivers ${r}`);
      break;
    }
    case 'publishSell': {
      const r = await redisPublish(sellTopic, tick(sell.name, sell.contractType, 69010.5, 69012.0));
      log(`PUBLISH ${sellTopic} -> receivers ${r}`);
      break;
    }
    case 'ping':
      ws.send(JSON.stringify({ action: 'ping' }));
      log('SENT ping');
      break;
    case 'unsubscribe':
      ws.send(JSON.stringify({ action: 'unsubscribe', params: subParams }));
      log('SENT unsubscribe');
      break;
    case 'close':
      log('CLOSING from client');
      ws.close(1000, 'client done');
      break;
  }
}

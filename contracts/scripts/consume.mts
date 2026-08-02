/**
 * End-to-end demo: prove the Data Console telemetry drives a real, explainable
 * recommendation — no game, no LLM, no cloud.
 *
 *   npm run demo                 # replays contracts/fixtures/log-shortage.series.json
 *   TIMBEROS_LIVE=1 npm run demo # polls the live mod at localhost:8080 (game running)
 *
 * It validates every snapshot against the shared schema, then fits a net rate to the
 * Log stock history and forecasts time-to-reserve — the vertical slice's payoff.
 */
import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import {
  DEFAULT_TELEMETRY_URL,
  parseTelemetryEnvelope,
  type TelemetryEnvelope,
} from '../src/index.ts'

const RESERVE_TARGET = 80
const GOOD_ID = 'Log'

async function loadSeries(): Promise<TelemetryEnvelope[]> {
  if (process.env.TIMBEROS_LIVE === '1') {
    // Poll the live mod a few times so we have a rate to fit.
    const samples: TelemetryEnvelope[] = []
    for (let i = 0; i < 4; i++) {
      const res = await fetch(DEFAULT_TELEMETRY_URL)
      if (!res.ok) throw new Error(`GET ${DEFAULT_TELEMETRY_URL} -> HTTP ${res.status}`)
      samples.push(parseTelemetryEnvelope(await res.json()))
      if (i < 3) await new Promise((r) => setTimeout(r, 2500))
    }
    return samples
  }
  const fixturePath = fileURLToPath(new URL('../fixtures/log-shortage.series.json', import.meta.url))
  const raw = JSON.parse(await readFile(fixturePath, 'utf8')) as unknown[]
  return raw.map(parseTelemetryEnvelope)
}

/** Least-squares slope of amount vs. game-day. Returns units per game day. */
function fitNetRate(points: Array<{ day: number; amount: number }>): number {
  const n = points.length
  const sx = points.reduce((s, p) => s + p.day, 0)
  const sy = points.reduce((s, p) => s + p.amount, 0)
  const sxx = points.reduce((s, p) => s + p.day * p.day, 0)
  const sxy = points.reduce((s, p) => s + p.day * p.amount, 0)
  const denom = n * sxx - sx * sx
  return denom === 0 ? 0 : (n * sxy - sx * sy) / denom
}

async function main(): Promise<void> {
  const series = await loadSeries()
  const points = series
    .filter((s) => s.gameTime && s.payload.resources)
    .map((s) => ({
      day: s.gameTime!.partialCycleDay,
      amount: s.payload.resources!.find((r) => r.goodId === GOOD_ID)?.amount,
    }))
    .filter((p): p is { day: number; amount: number } => typeof p.amount === 'number')

  console.log(`Validated ${series.length} snapshot(s). ${GOOD_ID} history:`)
  for (const p of points) console.log(`  day ${p.day.toFixed(1)}: ${p.amount}`)

  if (points.length < 2) {
    console.log('\nForecast unavailable: need at least two snapshots with Log stock.')
    return
  }

  const netRate = fitNetRate(points)
  const latest = points[points.length - 1]!
  const confidence = Math.min(1, points.length / 5)

  console.log(`\nNet ${GOOD_ID} rate: ${netRate.toFixed(1)} per game day (negative = declining)`)

  if (netRate >= 0) {
    console.log(`${GOOD_ID} stock is stable or rising — no depletion recommendation.`)
    return
  }

  const daysToReserve = (latest.amount - RESERVE_TARGET) / -netRate
  console.log(
    `\n[RECOMMENDATION · resource-depletion · confidence ${(confidence * 100).toFixed(0)}%]\n` +
      `${GOOD_ID} reserves are declining. The colony has ${latest.amount} ${GOOD_ID.toLowerCase()}s and is ` +
      `losing about ${Math.abs(netRate).toFixed(0)} per game day. At the current rate, the ` +
      `${RESERVE_TARGET}-${GOOD_ID.toLowerCase()} reserve will be reached in about ` +
      `${daysToReserve.toFixed(1)} days.\n` +
      `Suggested action: add or unpause a lumberjack/forester, or reduce ${GOOD_ID.toLowerCase()} ` +
      `consumption, before the reserve is breached.`,
  )
}

main().catch((err) => {
  console.error(err)
  process.exit(1)
})

import { readFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  SCHEMA_VERSION,
  parseTelemetryEnvelope,
  safeParseTelemetryEnvelope,
} from '../src/index.ts'

async function loadFixture(): Promise<unknown[]> {
  const path = fileURLToPath(new URL('../fixtures/log-shortage.series.json', import.meta.url))
  return JSON.parse(await readFile(path, 'utf8')) as unknown[]
}

describe('telemetry contract', () => {
  it('validates every snapshot in the reference fixture', async () => {
    const series = await loadFixture()
    expect(series.length).toBeGreaterThan(1)
    for (const snap of series) {
      const env = parseTelemetryEnvelope(snap)
      expect(env.schemaVersion).toBe(SCHEMA_VERSION)
      expect(env.payload.collectors.length).toBeGreaterThan(0)
    }
  })

  it('parses the weather and power slices when present', async () => {
    const series = await loadFixture()
    const withWeather = series.map(parseTelemetryEnvelope).find((s) => s.payload.weather)
    expect(withWeather?.payload.weather?.hazardId).toBe('Drought')
    expect(withWeather?.payload.power?.networks.length).toBe(1)
    expect(withWeather?.payload.power?.totalSurplus).toBe(25)
  })

  it('treats a missing collector value as null, not zero', () => {
    const env = parseTelemetryEnvelope({
      schemaVersion: SCHEMA_VERSION,
      messageType: 'snapshot',
      source: 'timberborn-mod',
      settlementId: null,
      sequence: 0,
      capturedAt: '2026-08-01T00:00:00.000Z',
      gameTime: null,
      payload: {
        game: null,
        population: {
          total: 5, beavers: 5, adults: 4, children: 1, bots: 0,
          employed: null, openJobs: null, beds: null, contaminatedBeavers: null,
        },
        resources: null,
        collectors: [{ name: 'population', status: 'available', error: null }],
      },
    })
    expect(env.payload.population?.employed).toBeNull()
    expect(env.payload.resources).toBeNull()
  })

  it('rejects a snapshot with the wrong source', () => {
    const result = safeParseTelemetryEnvelope({
      schemaVersion: SCHEMA_VERSION,
      messageType: 'snapshot',
      source: 'somewhere-else',
      settlementId: null,
      sequence: 0,
      capturedAt: '2026-08-01T00:00:00.000Z',
      gameTime: null,
      payload: { game: null, population: null, resources: null, collectors: [] },
    })
    expect(result.success).toBe(false)
  })
})

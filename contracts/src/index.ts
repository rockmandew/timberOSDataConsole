import { z } from 'zod'

/**
 * timberOS Data Console — telemetry contract (schema v1.0.0).
 *
 * This is the single source of truth for the JSON the Timberborn mod serves at
 * `GET http://localhost:8080/timberos/v1/snapshot`. The C# DTOs in
 * `mod/src/Telemetry/TelemetryDtos.cs` are pinned to these same field names.
 *
 * Convention: a field that the mod could not read is `null`, never `0`. Consumers
 * must treat `null` as "unknown" and surface it as such, not as an empty value.
 */

export const SCHEMA_VERSION = '1.0.0'

/** In-game time. Cycles are Timberborn's seasons; a cycle contains several days. */
export const GameTimeSchema = z.object({
  cycle: z.number().int(),
  cycleDay: z.number().int(),
  /** cycleDay + fractional day/night progress, e.g. 3.42 */
  partialCycleDay: z.number(),
})
export type GameTime = z.infer<typeof GameTimeSchema>

export const GameStateSchema = z.object({
  gameVersion: z.string().nullable(),
  modVersion: z.string(),
  factionId: z.string().nullable(),
  settlementName: z.string().nullable(),
})
export type GameState = z.infer<typeof GameStateSchema>

export const PopulationSchema = z.object({
  total: z.number().int(),
  beavers: z.number().int(),
  adults: z.number().int(),
  children: z.number().int(),
  bots: z.number().int(),
  employed: z.number().int().nullable(),
  openJobs: z.number().int().nullable(),
  beds: z.number().int().nullable(),
  contaminatedBeavers: z.number().int().nullable(),
})
export type Population = z.infer<typeof PopulationSchema>

/** Global stock + capacity for one good, summed across public district inventories. */
export const ResourceSchema = z.object({
  goodId: z.string(),
  amount: z.number().int(),
  capacity: z.number().int(),
})
export type Resource = z.infer<typeof ResourceSchema>

export const CollectorStatusSchema = z.object({
  name: z.string(),
  status: z.enum(['available', 'unavailable', 'error']),
  error: z.string().nullable(),
})
export type CollectorStatus = z.infer<typeof CollectorStatusSchema>

export const SnapshotPayloadSchema = z.object({
  game: GameStateSchema.nullable(),
  population: PopulationSchema.nullable(),
  resources: z.array(ResourceSchema).nullable(),
  collectors: z.array(CollectorStatusSchema),
})
export type SnapshotPayload = z.infer<typeof SnapshotPayloadSchema>

export const TelemetryEnvelopeSchema = z.object({
  schemaVersion: z.string(),
  messageType: z.literal('snapshot'),
  source: z.literal('timberborn-mod'),
  settlementId: z.string().nullable(),
  sequence: z.number().int().nonnegative(),
  capturedAt: z.string(),
  gameTime: GameTimeSchema.nullable(),
  payload: SnapshotPayloadSchema,
})
export type TelemetryEnvelope = z.infer<typeof TelemetryEnvelopeSchema>

/** Parse + validate an unknown value (e.g. a fetch() body) into a typed envelope. */
export function parseTelemetryEnvelope(input: unknown): TelemetryEnvelope {
  return TelemetryEnvelopeSchema.parse(input)
}

/** Non-throwing variant for tolerant consumers. */
export function safeParseTelemetryEnvelope(input: unknown) {
  return TelemetryEnvelopeSchema.safeParse(input)
}

export const DEFAULT_TELEMETRY_URL = 'http://localhost:8080/timberos/v1/snapshot'
export const DEFAULT_HEALTH_URL = 'http://localhost:8080/timberos/v1/health'

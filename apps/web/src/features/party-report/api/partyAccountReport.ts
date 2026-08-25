import { z } from "zod";

const uuidSchema = z.string().uuid();
const dateSchema = z.string().regex(/^\d{4}-\d{2}-\d{2}$/u);
const utcTimestampSchema = z.string().datetime({ offset: true }).refine((value) => value.endsWith("Z"));
const decimalStringSchema = z.string().regex(/^-?(?:0|[1-9]\d*)(?:\.\d+)?$/u);

const visibleAmountSchema = z
  .object({
    visibility: z.literal("visible"),
    value: decimalStringSchema,
  })
  .strict();

const redactedAmountSchema = z
  .object({
    visibility: z.literal("redacted"),
  })
  .strict();

const reportAmountSchema = z.discriminatedUnion("visibility", [
  visibleAmountSchema,
  redactedAmountSchema,
]);

const reportMetaSchema = z
  .object({
    reportCode: z.literal("party-account-statement-aging"),
    reportDefinitionVersion: z.number().int().positive(),
    companyId: uuidSchema,
    partyAccountId: uuidSchema,
    currency: z.string().regex(/^[A-Z]{3}$/u),
    asOf: dateSchema,
    dataThrough: utcTimestampSchema,
    generatedAt: utcTimestampSchema,
    projectionGeneration: uuidSchema,
    stale: z.boolean(),
    allowedActions: z.array(z.enum(["report.refresh", "report.export"])),
  })
  .strict();

const statementLineSchema = z
  .object({
    id: uuidSchema,
    effectiveDate: dateSchema,
    recordedAt: utcTimestampSchema,
    sequenceKey: z.string().regex(/^[1-9]\d*$/u),
    kind: z.enum(["openItem", "allocation", "unallocation", "writeOff", "writeOffReversal"]),
    description: z.string().min(1),
    sourceType: z.string().min(1),
    sourceId: uuidSchema,
    dueScheduleLineId: uuidSchema,
    paymentId: uuidSchema.nullable(),
    exposureEffect: reportAmountSchema,
    runningExposure: reportAmountSchema,
  })
  .strict();

const agingBucketSchema = z
  .object({
    code: z.string().min(1),
    label: z.string().min(1),
    itemCount: z.number().int().nonnegative(),
    remainingAmount: reportAmountSchema,
  })
  .strict();

const lineageNodeSchema = z
  .object({
    id: uuidSchema,
    type: z.enum(["source", "dueLine", "payment", "allocation", "journal"]),
    label: z.string().min(1),
    occurredAt: utcTimestampSchema,
  })
  .strict();

export const partyAccountReportSchema = z
  .object({
    meta: reportMetaSchema,
    summary: z
      .object({
        statementClosing: reportAmountSchema,
        agingTotal: reportAmountSchema,
        controlDifference: reportAmountSchema,
      })
      .strict(),
    statementLines: z.array(statementLineSchema),
    agingBuckets: z.array(agingBucketSchema),
    lineage: z.array(lineageNodeSchema),
  })
  .strict();

export type PartyAccountReport = z.infer<typeof partyAccountReportSchema>;
export type ReportAmount = z.infer<typeof reportAmountSchema>;

export interface PartyAccountReportRequest {
  companyId: string;
  partyAccountId: string;
  asOf: string;
}

export class PartyAccountReportRequestError extends Error {
  constructor(public readonly status: number) {
    super("Party account report request failed.");
  }
}

export const partyAccountReportQueryKey = (request: PartyAccountReportRequest) =>
  ["reports", "party-account", request.companyId, request.partyAccountId, request.asOf] as const;

export async function fetchPartyAccountReport(
  request: PartyAccountReportRequest,
  signal?: AbortSignal,
): Promise<PartyAccountReport> {
  const query = new URLSearchParams({
    companyId: request.companyId,
    partyAccountId: request.partyAccountId,
    asOf: request.asOf,
  });
  const response = await fetch(`/api/v1/reports/party-account?${query.toString()}`, {
    credentials: "same-origin",
    headers: { Accept: "application/json" },
    signal: signal ?? null,
  });

  if (!response.ok) {
    throw new PartyAccountReportRequestError(response.status);
  }

  return partyAccountReportSchema.parse(await response.json());
}

import { z } from "zod";

const healthResponseSchema = z.object({
  status: z.literal("ok"),
});

export type HealthResponse = z.infer<typeof healthResponseSchema>;

export async function fetchHealth(signal?: AbortSignal): Promise<HealthResponse> {
  const response = await fetch("/health/live", {
    credentials: "same-origin",
    headers: {
      Accept: "application/json",
    },
    signal: signal ?? null,
  });

  if (!response.ok) {
    throw new Error("API sağlık denetimi başarısız oldu.");
  }

  return healthResponseSchema.parse(await response.json());
}

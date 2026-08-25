import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { PartyAccountReport } from "../api/partyAccountReport";
import { PartyAccountReportPage } from "./PartyAccountReportPage";

const companyId = "11111111-1111-4111-8111-111111111111";
const partyAccountId = "22222222-2222-4222-8222-222222222222";
const sourceId = "33333333-3333-4333-8333-333333333333";
const reportUrl = `/reports/party-account?companyId=${companyId}&partyAccountId=${partyAccountId}&asOf=2026-08-24`;

function createReport(): PartyAccountReport {
  return {
    meta: {
      reportCode: "party-account-statement-aging",
      reportDefinitionVersion: 1,
      companyId,
      partyAccountId,
      currency: "GBP",
      asOf: "2026-08-24",
      dataThrough: "2026-08-24T10:00:00Z",
      generatedAt: "2026-08-24T10:01:00Z",
      projectionGeneration: "44444444-4444-4444-8444-444444444444",
      stale: false,
      allowedActions: ["report.refresh"],
    },
    summary: {
      statementClosing: { visibility: "visible", value: "90.0000" },
      agingTotal: { visibility: "visible", value: "90.0000" },
      controlDifference: { visibility: "visible", value: "0.0000" },
    },
    statementLines: [
      {
        id: "55555555-5555-4555-8555-555555555555",
        effectiveDate: "2026-08-20",
        recordedAt: "2026-08-20T09:00:00Z",
        sequenceKey: "1",
        kind: "openItem",
        description: "Satış faturası INV-001",
        sourceType: "sales.invoice",
        sourceId,
        dueScheduleLineId: "66666666-6666-4666-8666-666666666666",
        paymentId: null,
        exposureEffect: { visibility: "visible", value: "100.0000" },
        runningExposure: { visibility: "visible", value: "100.0000" },
      },
    ],
    agingBuckets: [
      {
        code: "overdue",
        label: "Vadesi geçmiş",
        itemCount: 1,
        remainingAmount: { visibility: "visible", value: "90.0000" },
      },
    ],
    lineage: [
      {
        id: sourceId,
        type: "source",
        label: "Satış faturası INV-001",
        occurredAt: "2026-08-20T09:00:00Z",
      },
    ],
  };
}

function renderPage(initialEntry = reportUrl) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <QueryClientProvider client={queryClient}>
        <PartyAccountReportPage />
      </QueryClientProvider>
    </MemoryRouter>,
  );
}

function stubReport(report: PartyAccountReport) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(
      new Response(JSON.stringify(report), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    ),
  );
}

describe("PartyAccountReportPage", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("as-of bağlamını, exact decimal tutarları ve drill-down linkini korur", async () => {
    stubReport(createReport());
    renderPage();

    expect(await screen.findByRole("heading", { name: "Cari ekstre" })).toBeVisible();
    expect(screen.getByRole("region", { name: "Rapor veri kesimi" })).toHaveTextContent("2026-08-24");
    expect(screen.getAllByText("90,0000").length).toBeGreaterThanOrEqual(2);
    const sourceLinks = screen.getAllByRole("link", { name: "Satış faturası INV-001" });
    expect(sourceLinks.length).toBeGreaterThanOrEqual(1);
    const sourceLink = sourceLinks[0];
    expect(sourceLink).toBeDefined();
    if (sourceLink === undefined) {
      throw new Error("Kaynak belge bağlantısı render edilmedi.");
    }
    expect(sourceLink).toHaveAttribute("href", expect.stringContaining(`companyId=${companyId}`));
    expect(sourceLink).toHaveAttribute("href", expect.stringContaining("asOf=2026-08-24"));
    expect(sourceLink).toHaveAttribute("href", expect.stringContaining(`focus=${sourceId}`));
  });

  it("stale projection ve server redaction durumlarını açıkça gösterir", async () => {
    const report = createReport();
    report.meta.stale = true;
    report.summary.statementClosing = { visibility: "redacted" };
    report.statementLines[0] = {
      ...report.statementLines[0]!,
      exposureEffect: { visibility: "redacted" },
      runningExposure: { visibility: "redacted" },
    };
    stubReport(report);
    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent("projection güncel değil");
    expect(screen.getAllByText("Yetkiniz kapsamında gizli").length).toBeGreaterThanOrEqual(3);
  });

  it("forbidden yanıtında kaynak varlığını veya teknik ayrıntıyı sızdırmaz", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 403 })));
    renderPage();

    expect(await screen.findByRole("heading", { name: "Rapor görüntülenemiyor" })).toBeVisible();
    expect(screen.getByText(/bulunamadı veya seçili şirket kapsamında/i)).toBeVisible();
    expect(screen.queryByText(/stack|sql|exception/iu)).not.toBeInTheDocument();
  });

  it("zorunlu URL bağlamı yoksa sunucu sorgusu yapmaz", () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    renderPage("/reports/party-account");

    expect(screen.getByRole("heading", { name: "Rapor bağlamı eksik" })).toBeVisible();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

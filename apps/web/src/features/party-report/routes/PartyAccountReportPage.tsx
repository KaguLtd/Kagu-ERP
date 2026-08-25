import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { z } from "zod";

import { ContextBar } from "../../../components/erp/ContextBar";
import {
  fetchPartyAccountReport,
  partyAccountReportQueryKey,
  PartyAccountReportRequestError,
  type PartyAccountReport,
  type PartyAccountReportRequest,
} from "../api/partyAccountReport";
import { AsOfBanner } from "../components/AsOfBanner";
import { ExactMoney } from "../components/ExactMoney";

const requestSchema = z.object({
  companyId: z.string().uuid(),
  partyAccountId: z.string().uuid(),
  asOf: z.string().regex(/^\d{4}-\d{2}-\d{2}$/u),
});

export function PartyAccountReportPage() {
  const [searchParams] = useSearchParams();
  const parsedRequest = requestSchema.safeParse({
    companyId: searchParams.get("companyId"),
    partyAccountId: searchParams.get("partyAccountId"),
    asOf: searchParams.get("asOf"),
  });
  const request = parsedRequest.success ? parsedRequest.data : null;
  const reportQuery = useQuery({
    queryKey: request === null ? ["reports", "party-account", "missing-context"] : partyAccountReportQueryKey(request),
    queryFn: ({ signal }) => fetchPartyAccountReport(requireRequest(request), signal),
    enabled: request !== null,
  });

  return (
    <div className="app-shell">
      <header className="top-bar">
        <Link className="brand" to="/" aria-label="Kagu ERP ana sayfa">
          Kagu ERP
        </Link>
        <span className="environment-badge">Geliştirme ortamı</span>
      </header>

      <main className="main-content">
        <ContextBar
          company={request?.companyId ?? "Henüz seçilmedi"}
          currency={reportQuery.data?.meta.currency ?? "—"}
        />
        <header className="page-heading">
          <p className="eyebrow">Cari raporları</p>
          <h1>Cari ekstre ve aging</h1>
          <p className="lead">
            Ekstre, yaşlandırma ve kaynak zinciri aynı şirket ve veri kesiminde gösterilir.
          </p>
        </header>

        {request === null ? (
          <ReportState
            title="Rapor bağlamı eksik"
            message="Şirket, cari hesap ve as-of tarihi seçilmeden finansal rapor sorgulanmaz."
          />
        ) : reportQuery.isPending ? (
          <ReportState title="Rapor hazırlanıyor" message="Yetkili veri kesimi sunucudan alınıyor." live />
        ) : reportQuery.isError ? (
          <ReportError error={reportQuery.error} onRetry={() => void reportQuery.refetch()} />
        ) : (
          <PartyReportContent report={reportQuery.data} searchParams={searchParams} />
        )}
      </main>
    </div>
  );
}

function PartyReportContent({
  report,
  searchParams,
}: {
  report: PartyAccountReport;
  searchParams: URLSearchParams;
}) {
  const focusedId = searchParams.get("focus");
  const focusedNode = report.lineage.find((node) => node.id === focusedId);

  return (
    <div className="report-stack">
      <AsOfBanner meta={report.meta} />
      {report.meta.stale ? (
        <div className="report-alert report-alert-warning" role="alert">
          Bu projection güncel değil. Veri kesimi yenilenmeden rapor güncel kabul edilmemelidir.
        </div>
      ) : null}

      <section className="summary-grid" aria-label="Rapor kontrol toplamları">
        <SummaryCard label="Ekstre kapanışı" amount={report.summary.statementClosing} currency={report.meta.currency} />
        <SummaryCard label="Aging toplamı" amount={report.summary.agingTotal} currency={report.meta.currency} />
        <SummaryCard label="Kontrol hesabı farkı" amount={report.summary.controlDifference} currency={report.meta.currency} />
      </section>

      <section className="report-panel" aria-labelledby="aging-title">
        <div className="panel-heading">
          <div>
            <p className="eyebrow">As-of yaşlandırma</p>
            <h2 id="aging-title">Aging dağılımı</h2>
          </div>
          <span className="panel-meta">{report.meta.currency}</span>
        </div>
        {report.agingBuckets.length === 0 ? (
          <p className="empty-copy">Bu kesitte açık vade kalemi yok.</p>
        ) : (
          <div className="aging-grid">
            {report.agingBuckets.map((bucket) => (
              <article className="aging-card" key={bucket.code}>
                <h3>{bucket.label}</h3>
                <ExactMoney amount={bucket.remainingAmount} currency={report.meta.currency} />
                <span>{bucket.itemCount} kalem</span>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="report-panel" aria-labelledby="statement-title">
        <div className="panel-heading">
          <div>
            <p className="eyebrow">Kaynak bağlantılı</p>
            <h2 id="statement-title">Cari ekstre</h2>
          </div>
          <span className="panel-meta">{report.statementLines.length} hareket</span>
        </div>
        {report.statementLines.length === 0 ? (
          <p className="empty-copy">Bu rapor kesitinde ekstre hareketi yok.</p>
        ) : (
          <div className="table-scroll">
            <table className="report-table">
              <caption>Cari ekstre hareketleri ve kesit içindeki yürüyen bakiye</caption>
              <thead>
                <tr>
                  <th scope="col">Etkin tarih</th>
                  <th scope="col">Açıklama</th>
                  <th scope="col">Olay</th>
                  <th scope="col" className="amount-column">Etki</th>
                  <th scope="col" className="amount-column">Yürüyen bakiye</th>
                </tr>
              </thead>
              <tbody>
                {report.statementLines.map((line) => (
                  <tr key={line.id}>
                    <td>{line.effectiveDate}</td>
                    <td>
                      <Link className="drill-link" to={createFocusHref(searchParams, line.sourceId)}>
                        {line.description}
                      </Link>
                    </td>
                    <td>{statementKindLabel(line.kind)}</td>
                    <td className="amount-column">
                      <ExactMoney amount={line.exposureEffect} currency={report.meta.currency} />
                    </td>
                    <td className="amount-column">
                      <ExactMoney amount={line.runningExposure} currency={report.meta.currency} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="report-panel" aria-labelledby="lineage-title">
        <div className="panel-heading">
          <div>
            <p className="eyebrow">Drill-down</p>
            <h2 id="lineage-title">Kaynak zinciri</h2>
          </div>
        </div>
        {report.lineage.length === 0 ? (
          <p className="empty-copy">Bu kesitte görüntülenebilir kaynak bağlantısı yok.</p>
        ) : (
          <ol className="lineage-list">
            {report.lineage.map((node) => (
              <li key={node.id}>
                <Link
                  className={node.id === focusedId ? "lineage-link lineage-link-active" : "lineage-link"}
                  to={createFocusHref(searchParams, node.id)}
                  aria-current={node.id === focusedId ? "location" : undefined}
                >
                  <span>{lineageTypeLabel(node.type)}</span>
                  <strong>{node.label}</strong>
                  <small>{node.occurredAt}</small>
                </Link>
              </li>
            ))}
          </ol>
        )}
        {focusedNode === undefined ? null : (
          <aside className="focus-detail" aria-label="Seçili kaynak ayrıntısı">
            <span className="context-label">Seçili kaynak</span>
            <strong>{focusedNode.label}</strong>
            <span className="technical-id">{focusedNode.id}</span>
            <span>Bu ayrıntı aynı as-of ve projection generation bağlamındadır.</span>
          </aside>
        )}
      </section>
    </div>
  );
}

function SummaryCard({
  label,
  amount,
  currency,
}: {
  label: string;
  amount: PartyAccountReport["summary"]["agingTotal"];
  currency: string;
}) {
  return (
    <article className="summary-card">
      <span>{label}</span>
      <strong>
        <ExactMoney amount={amount} currency={currency} />
      </strong>
    </article>
  );
}

function ReportState({ title, message, live = false }: { title: string; message: string; live?: boolean }) {
  return (
    <section className="report-state" aria-live={live ? "polite" : undefined}>
      <h2>{title}</h2>
      <p>{message}</p>
    </section>
  );
}

function ReportError({ error, onRetry }: { error: Error; onRetry: () => void }) {
  if (error instanceof PartyAccountReportRequestError && (error.status === 403 || error.status === 404)) {
    return (
      <ReportState
        title="Rapor görüntülenemiyor"
        message="Bu rapor bulunamadı veya seçili şirket kapsamında görüntüleme yetkiniz yok."
      />
    );
  }

  return (
    <section className="report-state" role="alert">
      <h2>Rapor alınamadı</h2>
      <p>Güvenli rapor yanıtı doğrulanamadı. Teknik ayrıntılar gösterilmedi.</p>
      <button className="secondary-button" type="button" onClick={onRetry}>
        Yeniden dene
      </button>
    </section>
  );
}

function createFocusHref(searchParams: URLSearchParams, focusId: string): string {
  const next = new URLSearchParams(searchParams);
  next.set("focus", focusId);
  return `/reports/party-account?${next.toString()}`;
}

function requireRequest(request: PartyAccountReportRequest | null): PartyAccountReportRequest {
  if (request === null) {
    throw new Error("Party account report context is missing.");
  }

  return request;
}

function statementKindLabel(kind: PartyAccountReport["statementLines"][number]["kind"]): string {
  const labels = {
    openItem: "Açık kalem",
    allocation: "Tahsis",
    unallocation: "Tahsis kaldırma",
    writeOff: "Terkin",
    writeOffReversal: "Terkin ters kaydı",
  } satisfies Record<typeof kind, string>;
  return labels[kind];
}

function lineageTypeLabel(type: PartyAccountReport["lineage"][number]["type"]): string {
  const labels = {
    source: "Kaynak olay",
    dueLine: "Vade satırı",
    payment: "Ödeme",
    allocation: "Tahsis",
    journal: "Muhasebe fişi",
  } satisfies Record<typeof type, string>;
  return labels[type];
}

import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";

import { fetchHealth } from "../api/health";
import { ContextBar } from "../components/erp/ContextBar";

export function HomePage() {
  const healthQuery = useQuery({
    queryKey: ["platform", "health", "live"],
    queryFn: ({ signal }) => fetchHealth(signal),
  });

  const apiStatus = healthQuery.isSuccess
    ? "API erişilebilir"
    : healthQuery.isError
      ? "API erişilemiyor"
      : "API denetleniyor";

  return (
    <div className="app-shell">
      <header className="top-bar">
        <a className="brand" href="/" aria-label="Kagu ERP ana sayfa">
          Kagu ERP
        </a>
        <span className="environment-badge">Geliştirme ortamı</span>
      </header>

      <main className="main-content">
        <ContextBar />

        <section className="welcome-panel" aria-labelledby="welcome-title">
          <p className="eyebrow">MP-02 · Platform bootstrap</p>
          <h1 id="welcome-title">Güvenli ERP çalışma alanı hazırlanıyor</h1>
          <p className="lead">
            Bu kabuk şirket, şube, dönem ve para birimi bağlamını sürekli görünür
            tutacak web istemcisinin başlangıç noktasıdır.
          </p>
          <div className="status-row" role="status" aria-live="polite">
            <span
              className={healthQuery.isSuccess ? "status-dot status-dot-success" : "status-dot"}
              aria-hidden="true"
            />
            <span>{apiStatus}</span>
          </div>
          <Link className="secondary-link" to="/reports/party-account">
            Cari rapor çalışma alanını aç
          </Link>
        </section>
      </main>
    </div>
  );
}

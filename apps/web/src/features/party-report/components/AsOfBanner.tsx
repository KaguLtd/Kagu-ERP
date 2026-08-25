import type { PartyAccountReport } from "../api/partyAccountReport";

interface AsOfBannerProps {
  meta: PartyAccountReport["meta"];
}

export function AsOfBanner({ meta }: AsOfBannerProps) {
  return (
    <section className="as-of-banner" aria-label="Rapor veri kesimi">
      <div>
        <span className="context-label">As-of</span>
        <strong>{meta.asOf}</strong>
      </div>
      <div>
        <span className="context-label">Veri kesimi</span>
        <strong>{meta.dataThrough}</strong>
      </div>
      <div>
        <span className="context-label">Üretim zamanı</span>
        <strong>{meta.generatedAt}</strong>
      </div>
      <div>
        <span className="context-label">Projection generation</span>
        <strong className="technical-id">{meta.projectionGeneration}</strong>
      </div>
      <div>
        <span className="context-label">Rapor sürümü</span>
        <strong>{meta.reportDefinitionVersion}</strong>
      </div>
    </section>
  );
}

interface ContextBarProps {
  company?: string;
  branch?: string;
  period?: string;
  currency?: string;
}

export function ContextBar({
  company = "Henüz seçilmedi",
  branch = "—",
  period = "—",
  currency = "—",
}: ContextBarProps) {
  return (
    <section className="context-bar" aria-label="Aktif çalışma bağlamı">
      <div>
        <span className="context-label">Şirket</span>
        <strong>{company}</strong>
      </div>
      <div>
        <span className="context-label">Şube</span>
        <strong>{branch}</strong>
      </div>
      <div>
        <span className="context-label">Dönem</span>
        <strong>{period}</strong>
      </div>
      <div>
        <span className="context-label">Para birimi</span>
        <strong>{currency}</strong>
      </div>
    </section>
  );
}

export function ContextBar() {
  return (
    <section className="context-bar" aria-label="Aktif çalışma bağlamı">
      <div>
        <span className="context-label">Şirket</span>
        <strong>Henüz seçilmedi</strong>
      </div>
      <div>
        <span className="context-label">Şube</span>
        <strong>—</strong>
      </div>
      <div>
        <span className="context-label">Dönem</span>
        <strong>—</strong>
      </div>
      <div>
        <span className="context-label">Para birimi</span>
        <strong>—</strong>
      </div>
    </section>
  );
}


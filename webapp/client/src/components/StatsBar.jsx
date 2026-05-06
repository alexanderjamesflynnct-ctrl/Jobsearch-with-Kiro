export default function StatsBar({ stats }) {
  return (
    <div className="stats-bar">
      <span><strong>{stats.total_jobs}</strong> total jobs</span>
      <span><strong>{stats.total_searches}</strong> searches run</span>
      {stats.last_search && (
        <span>Last search: <strong>{stats.last_search.replace('T', ' ').replace('Z', '')} UTC</strong></span>
      )}
      {stats.by_country?.map(c => (
        <span key={c.country} className="country-pill">
          {c.country}: {c.n}
        </span>
      ))}
    </div>
  )
}

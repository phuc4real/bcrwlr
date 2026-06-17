import { useState } from 'react'
import { deleteArticle, thumbUrl, type ArticleSummary } from '../api'

interface Props {
  articles: ArticleSummary[]
  loading: boolean
  hasQuery: boolean
  onOpen: (id: string) => void
  onDeleted: (id: string) => void
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime())
    ? ''
    : d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export default function Library({ articles, loading, hasQuery, onOpen, onDeleted }: Props) {
  const [busyId, setBusyId] = useState<string | null>(null)

  const handleDelete = async (e: React.MouseEvent, a: ArticleSummary) => {
    e.stopPropagation()
    if (busyId) return
    if (!window.confirm(`Delete “${a.title}”? This removes it from disk.`)) return

    setBusyId(a.id)
    try {
      await deleteArticle(a.id)
      onDeleted(a.id)
    } catch (err) {
      window.alert(err instanceof Error ? err.message : 'Failed to delete.')
    } finally {
      setBusyId(null)
    }
  }

  if (loading && articles.length === 0) {
    return <div className="empty">Loading…</div>
  }

  if (articles.length === 0) {
    return (
      <div className="empty">
        {hasQuery ? (
          <p>No saved articles match your search.</p>
        ) : (
          <>
            <p className="empty-title">Your archive is empty.</p>
            <p>Paste a blog or article link above to save your first read.</p>
          </>
        )}
      </div>
    )
  }

  return (
    <div className="grid">
      {articles.map((a) => (
        <div key={a.id} className={`card${busyId === a.id ? ' busy' : ''}`}>
          <button className="card-open" onClick={() => onOpen(a.id)} type="button">
            <div className="thumb">
              {a.hasThumb ? (
                <img src={thumbUrl(a.id)} alt="" loading="lazy" />
              ) : (
                <div className="thumb-fallback">
                  {(a.siteName ?? a.title).charAt(0).toUpperCase()}
                </div>
              )}
            </div>
            <div className="card-body">
              <h3 className="card-title">{a.title}</h3>
              {a.excerpt && <p className="card-excerpt">{a.excerpt}</p>}
              <div className="card-meta">
                <span className="site">{a.siteName ?? new URL(a.sourceUrl).hostname}</span>
                <span className="dot">·</span>
                {a.readingMinutes > 0 && (
                  <>
                    <span>{a.readingMinutes} min</span>
                    <span className="dot">·</span>
                  </>
                )}
                <span>{formatDate(a.savedAt)}</span>
              </div>
            </div>
          </button>

          <button
            className="card-delete"
            type="button"
            title="Delete"
            aria-label={`Delete ${a.title}`}
            onClick={(e) => handleDelete(e, a)}
            disabled={busyId === a.id}
          >
            {busyId === a.id ? '…' : '🗑'}
          </button>
        </div>
      ))}
    </div>
  )
}

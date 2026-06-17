import { useEffect, useState } from 'react'
import {
  deleteArticle,
  fileUrl,
  getArticle,
  type ArticleDetail,
} from '../api'

interface Props {
  id: string
  onClose: () => void
  onDeleted: (id: string) => void
}

export default function Reader({ id, onClose, onDeleted }: Props) {
  const [detail, setDetail] = useState<ArticleDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [deleting, setDeleting] = useState(false)

  useEffect(() => {
    let active = true
    getArticle(id)
      .then((d) => active && setDetail(d))
      .catch((e) => active && setError(e instanceof Error ? e.message : 'Failed to load article.'))
    return () => {
      active = false
    }
  }, [id])

  // Close on Escape.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  const handleDelete = async () => {
    if (deleting) return
    if (!window.confirm('Delete this saved article? This removes it from disk.')) return
    setDeleting(true)
    try {
      await deleteArticle(id)
      onDeleted(id)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to delete.')
      setDeleting(false)
    }
  }

  return (
    <div className="overlay" onClick={onClose}>
      <div className="reader" onClick={(e) => e.stopPropagation()}>
        <div className="reader-toolbar">
          <button className="icon-btn" onClick={onClose} type="button" title="Close (Esc)">
            ← Back
          </button>
          <div className="reader-actions">
            {detail && (
              <>
                <a className="ghost-btn" href={detail.summary.sourceUrl} target="_blank" rel="noopener">
                  Original ↗
                </a>
                <a className="ghost-btn" href={fileUrl(id, 'html')}>
                  Download HTML
                </a>
                <a className="ghost-btn" href={fileUrl(id, 'md')}>
                  Download MD
                </a>
                <button className="ghost-btn danger" onClick={handleDelete} type="button" disabled={deleting}>
                  {deleting ? 'Deleting…' : 'Delete'}
                </button>
              </>
            )}
          </div>
        </div>

        {error && <div className="banner error reader-banner">{error}</div>}

        {!detail && !error && <div className="reader-loading">Loading…</div>}

        {detail && (
          <iframe
            className="reader-frame"
            title={detail.summary.title}
            sandbox="allow-popups allow-popups-to-escape-sandbox"
            srcDoc={detail.html}
          />
        )}
      </div>
    </div>
  )
}

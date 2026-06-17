import { useState } from 'react'
import { saveArticle, type ArticleSummary } from '../api'

interface Props {
  onSaved: (article: ArticleSummary) => void
}

export default function SaveBar({ onSaved }: Props) {
  const [url, setUrl] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    const value = url.trim()
    if (!value || saving) return

    setSaving(true)
    setError(null)
    try {
      const article = await saveArticle(value)
      setUrl('')
      onSaved(article)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not save this article.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="savebar" onSubmit={submit}>
      <div className="savebar-row">
        <input
          className="url-input"
          type="url"
          inputMode="url"
          placeholder="Paste an article link to archive it…"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          disabled={saving}
          autoFocus
        />
        <button className="save-btn" type="submit" disabled={saving || !url.trim()}>
          {saving ? <span className="spinner" aria-hidden /> : null}
          {saving ? 'Saving…' : 'Save'}
        </button>
      </div>
      {error && <div className="banner error">{error}</div>}
    </form>
  )
}

import { useCallback, useEffect, useState } from 'react'
import './App.css'
import { listArticles, type ArticleSummary } from './api'
import SaveBar from './components/SaveBar'
import Library from './components/Library'
import Reader from './components/Reader'

export default function App() {
  const [articles, setArticles] = useState<ArticleSummary[]>([])
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [listError, setListError] = useState<string | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)

  const refresh = useCallback(async (q: string) => {
    setLoading(true)
    setListError(null)
    try {
      setArticles(await listArticles(q))
    } catch (e) {
      setListError(e instanceof Error ? e.message : 'Failed to load articles.')
    } finally {
      setLoading(false)
    }
  }, [])

  // Debounce search as the query changes (also runs the initial load).
  useEffect(() => {
    const t = setTimeout(() => refresh(query), 250)
    return () => clearTimeout(t)
  }, [query, refresh])

  const handleSaved = () => {
    // Reload from the server so the new article appears (clearing any active search).
    if (query.trim()) setQuery('')
    else refresh('')
  }

  const handleDeleted = (id: string) => {
    setArticles((prev) => prev.filter((a) => a.id !== id))
    setOpenId(null)
  }

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="logo">◆</span> bcrwlr
          <span className="tagline">your offline reading archive</span>
        </div>
      </header>

      <main className="content">
        <SaveBar onSaved={handleSaved} />

        <div className="library-head">
          <h2>
            Saved articles {articles.length > 0 && <span className="count">{articles.length}</span>}
          </h2>
          <input
            className="search"
            type="search"
            placeholder="Search title, site, excerpt…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </div>

        {listError && <div className="banner error">{listError}</div>}

        <Library
          articles={articles}
          loading={loading}
          hasQuery={query.trim().length > 0}
          onOpen={setOpenId}
          onDeleted={handleDeleted}
        />
      </main>

      {openId && (
        <Reader id={openId} onClose={() => setOpenId(null)} onDeleted={handleDeleted} />
      )}
    </div>
  )
}

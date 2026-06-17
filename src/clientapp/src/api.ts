export interface ArticleSummary {
  id: string
  title: string
  author: string | null
  sourceUrl: string
  siteName: string | null
  excerpt: string | null
  wordCount: number
  readingMinutes: number
  hasThumb: boolean
  publishedAt: string | null
  savedAt: string
}

export interface ArticleDetail {
  summary: ArticleSummary
  html: string
}

async function asError(res: Response): Promise<never> {
  let message = `Request failed (${res.status})`
  try {
    const body = await res.json()
    if (body?.error) message = body.error
  } catch {
    /* non-JSON error body */
  }
  throw new Error(message)
}

export async function saveArticle(url: string): Promise<ArticleSummary> {
  const res = await fetch('/api/articles', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ url }),
  })
  if (!res.ok) return asError(res)
  return res.json()
}

export async function listArticles(query: string): Promise<ArticleSummary[]> {
  const qs = query.trim() ? `?q=${encodeURIComponent(query.trim())}` : ''
  const res = await fetch(`/api/articles${qs}`)
  if (!res.ok) return asError(res)
  return res.json()
}

export async function getArticle(id: string): Promise<ArticleDetail> {
  const res = await fetch(`/api/articles/${id}`)
  if (!res.ok) return asError(res)
  return res.json()
}

export async function deleteArticle(id: string): Promise<void> {
  const res = await fetch(`/api/articles/${id}`, { method: 'DELETE' })
  if (!res.ok && res.status !== 204) return asError(res)
}

export const thumbUrl = (id: string) => `/api/articles/${id}/thumb`
export const fileUrl = (id: string, format: 'html' | 'md') =>
  `/api/articles/${id}/file?format=${format}`

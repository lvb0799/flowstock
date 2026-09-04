function normalizeApiBase(value: string) {
  return value.replace(/\/+$/, '');
}

const configuredApi = import.meta.env.VITE_API_BASE_URL as string | undefined;
const storedApi = localStorage.getItem('apiBase');

// Priority: browser override -> build-time env -> local development default.
// Examples:
//   VITE_API_BASE_URL=https://api.example.com/api
//   VITE_API_BASE_URL=/FlowStockApi/api
const API = normalizeApiBase(storedApi || configuredApi || 'http://localhost:5000/api');

export async function api(path: string, options?: RequestInit) {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  const r = await fetch(`${API}${normalizedPath}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(options?.headers || {})
    },
    ...options
  });

  if (!r.ok) {
    const e = await r.json().catch(() => ({ message: r.statusText }));
    throw new Error(e.message || `API error (${r.status})`);
  }

  if (r.status === 204) return null;
  const text = await r.text();
  return text ? JSON.parse(text) : null;
}

export { API };

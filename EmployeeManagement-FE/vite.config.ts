import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'

/**
 * Injects a Content-Security-Policy <meta> tag into index.html.
 * The production build gets the strict policy. The dev server needs inline
 * scripts (React Fast Refresh preamble), so script-src is relaxed with
 * 'unsafe-inline' only when serving in dev.
 */
function contentSecurityPolicy(): Plugin {
    let policy = ''
    return {
        name: 'content-security-policy',
        configResolved(config) {
            const isDev = config.command === 'serve'
            const connectSrc = new Set([
                "'self'",
                'http://localhost:5000',
                'https://localhost:5001',
                'ws://localhost:5173',
            ])
            const apiUrl: unknown = config.env.VITE_API_URL
            if (typeof apiUrl === 'string' && apiUrl) {
                try {
                    connectSrc.add(new URL(apiUrl).origin)
                } catch {
                    // Ignore a malformed VITE_API_URL; the defaults still apply.
                }
            }
            policy = [
                "default-src 'self'",
                "img-src 'self' data:",
                `connect-src ${[...connectSrc].join(' ')}`,
                "style-src 'self' 'unsafe-inline'",
                isDev ? "script-src 'self' 'unsafe-inline'" : "script-src 'self'",
            ].join('; ')
        },
        transformIndexHtml() {
            return [{
                tag: 'meta',
                attrs: { 'http-equiv': 'Content-Security-Policy', content: policy },
                injectTo: 'head-prepend',
            }]
        },
    }
}

export default defineConfig({
    plugins: [react(), contentSecurityPolicy()],
    server: {
        port: 5173,
        host: 'localhost'
    }
})

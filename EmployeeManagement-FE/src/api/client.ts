import axios, { AxiosError } from 'axios'

/**
 * Shared Axios instance used by all API service modules.
 * Base URL is configured via VITE_API_URL environment variable.
 */
const apiClient = axios.create({
    baseURL: `${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/api`,
    timeout: 30000,
    headers: {
        'Content-Type': 'application/json',
    },
})

// ── Response interceptor — normalize error messages ──────────────────────────
apiClient.interceptors.response.use(
    (response) => response,
    (error: AxiosError<{ message?: string; error?: string; errors?: Record<string, string> }>) => {
        if (error.code === 'ERR_NETWORK' || !error.response) {
            return Promise.reject(
                new Error('Cannot connect to the server. Please ensure the backend is running.')
            )
        }

        if (error.code === 'ECONNABORTED') {
            return Promise.reject(new Error('The request timed out. Please try again.'))
        }

        const serverMessage =
            error.response?.data?.message ||
            error.response?.data?.error ||
            error.message ||
            'An unexpected error occurred.'

        return Promise.reject(new Error(serverMessage))
    }
)

export default apiClient

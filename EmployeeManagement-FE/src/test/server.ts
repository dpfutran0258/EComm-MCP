import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import apiClient from '../api/client'

/** Base URL the shared axios client targets (honours VITE_API_URL). */
export const API = String(apiClient.defaults.baseURL)

/** Default handlers: endpoints that are optional on the backend answer 404. */
export const defaultHandlers = [
    http.get(`${API}/employees/departments`, () => new HttpResponse(null, { status: 404 })),
]

export const server = setupServer(...defaultHandlers)

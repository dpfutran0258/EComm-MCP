import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { AxiosError } from 'axios'
import apiClient, { ApiError, mapFieldErrors, toApiError } from './client'
import { API, server } from '../test/server'

async function captureError(request: Promise<unknown>): Promise<ApiError> {
    try {
        await request
    } catch (err) {
        expect(err).toBeInstanceOf(ApiError)
        return err as ApiError
    }
    throw new Error('Expected the request to fail')
}

describe('apiClient error mapping', () => {
    it('maps a 400 validation response to field errors', async () => {
        server.use(http.post(`${API}/employees`, () => HttpResponse.json({
            message: 'One or more validation errors occurred.',
            statusCode: 400,
            correlationId: 'corr-400',
            errors: { FullName: ['Full Name is required'], Email: ['Invalid email format'] },
            timestamp: '2026-01-01T00:00:00Z',
        }, { status: 400 })))

        const error = await captureError(apiClient.post('/employees', {}))

        expect(error.status).toBe(400)
        expect(error.kind).toBe('http')
        expect(error.message).toBe('One or more validation errors occurred.')
        expect(error.correlationId).toBe('corr-400')
        expect(error.fieldErrors).toEqual({
            FullName: ['Full Name is required'],
            Email: ['Invalid email format'],
        })
        expect(mapFieldErrors(error.fieldErrors, ['fullName', 'email', 'salary'] as const)).toEqual({
            fullName: 'Full Name is required',
            email: 'Invalid email format',
        })
    })

    it('maps a 409 conflict with its server message', async () => {
        server.use(http.post(`${API}/employees`, () => HttpResponse.json({
            message: 'An employee with this email already exists.',
            statusCode: 409,
            errors: { email: ['An employee with this email already exists.'] },
        }, { status: 409, headers: { 'X-Correlation-ID': 'corr-409' } })))

        const error = await captureError(apiClient.post('/employees', {}))

        expect(error.status).toBe(409)
        expect(error.message).toBe('An employee with this email already exists.')
        expect(error.fieldErrors.email).toEqual(['An employee with this email already exists.'])
        expect(error.correlationId).toBe('corr-409')
    })

    it('maps a 429 with Retry-After to a "try again in N s" message', async () => {
        server.use(http.post(`${API}/chat/message`, () => HttpResponse.json(
            { message: 'Rate limit exceeded' },
            { status: 429, headers: { 'Retry-After': '12' } },
        )))

        const error = await captureError(apiClient.post('/chat/message', {}))

        expect(error.status).toBe(429)
        expect(error.retryAfterSeconds).toBe(12)
        expect(error.message).toBe('Too many requests, try again in 12 s.')
    })

    it('maps a 429 without Retry-After to a generic message', async () => {
        server.use(http.get(`${API}/employees`, () => new HttpResponse(null, { status: 429 })))

        const error = await captureError(apiClient.get('/employees'))

        expect(error.status).toBe(429)
        expect(error.message).toMatch(/too many requests/i)
    })

    it('maps a network failure', async () => {
        server.use(http.get(`${API}/employees`, () => HttpResponse.error()))

        const error = await captureError(apiClient.get('/employees'))

        expect(error.kind).toBe('network')
        expect(error.status).toBe(0)
        expect(error.message).toMatch(/cannot connect/i)
    })

    it('maps an aborted request to a canceled error', async () => {
        server.use(http.get(`${API}/employees`, () => new Promise<never>(() => {})))
        const controller = new AbortController()
        const request = apiClient.get('/employees', { signal: controller.signal })
        controller.abort()

        const error = await captureError(request)

        expect(error.kind).toBe('canceled')
        expect(error.isCanceled).toBe(true)
    })
})

describe('toApiError timeout detection', () => {
    it('treats ECONNABORTED with a timeout message as a timeout, not a network error', () => {
        const error = toApiError(new AxiosError('timeout of 30000ms exceeded', AxiosError.ECONNABORTED))
        expect(error.kind).toBe('timeout')
        expect(error.message).toMatch(/timed out/i)
    })

    it('treats ETIMEDOUT as a timeout', () => {
        expect(toApiError(new AxiosError('timeout exceeded', AxiosError.ETIMEDOUT)).kind).toBe('timeout')
    })

    it('treats ERR_CANCELED as canceled', () => {
        expect(toApiError(new AxiosError('canceled', AxiosError.ERR_CANCELED)).kind).toBe('canceled')
    })

    it('treats a non-timeout ECONNABORTED as a network error', () => {
        const error = toApiError(new AxiosError('Request aborted', AxiosError.ECONNABORTED))
        expect(error.kind).toBe('network')
        expect(error.message).toMatch(/aborted/i)
    })
})

describe('mapFieldErrors', () => {
    it('matches keys case-insensitively and strips prefixes', () => {
        expect(mapFieldErrors(
            { 'dto.EMAIL': ['dup'], '$.salary': ['bad'], Unknown: ['x'] },
            ['email', 'salary'] as const,
        )).toEqual({ email: 'dup', salary: 'bad' })
    })
})

import { renderHook, act, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server, API } from '../test/server'
import { EMPLOYEES_CHANGED_EVENT } from '../constants/events'
import type { ChatApiRequest, ChatApiResponse } from '../types/chat.types'
import { useChat } from './useChat'

/** Registers a /chat handler that records each request body and answers with the queued responses. */
function mockChat(...responses: Array<Partial<ChatApiResponse>>) {
    const requests: ChatApiRequest[] = []
    server.use(
        http.post(`${API}/chat`, async ({ request }) => {
            requests.push((await request.json()) as ChatApiRequest)
            const next = responses.shift() ?? { responseText: 'ok', type: 'INFO' as const, newMessages: [] }
            return HttpResponse.json({ type: 'INFO', newMessages: [], ...next })
        }),
    )
    return requests
}

describe('useChat', () => {
    it('sends the whole transcript with every request so the AI has context', async () => {
        const requests = mockChat(
            {
                responseText: 'Found Jane (ID 7).',
                type: 'SUCCESS',
                newMessages: [
                    { role: 'assistant', content: null, toolCalls: [{ id: 'c1', name: 'find_employees', arguments: '{"query":"jane"}' }] },
                    { role: 'tool', toolCallId: 'c1', content: '{"employees":[{"id":7}]}' },
                    { role: 'assistant', content: 'Found Jane (ID 7).' },
                ],
            },
            { responseText: 'Updated.', type: 'SUCCESS', newMessages: [{ role: 'assistant', content: 'Updated.' }] },
        )
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('find jane'))
        await act(() => result.current.sendMessage('give her 90000'))

        expect(requests[0].messages).toEqual([{ role: 'user', content: 'find jane' }])
        expect(requests[1].messages?.map((m) => m.role)).toEqual(['user', 'assistant', 'tool', 'assistant', 'user'])
        expect(requests[1].messages?.[2]).toMatchObject({ toolCallId: 'c1' })
        expect(result.current.messages.map((m) => m.sender)).toEqual(['user', 'bot', 'user', 'bot'])
    })

    it('tells the employee list to refresh when the assistant changed data', async () => {
        mockChat({ responseText: 'Added.', type: 'SUCCESS', dataChanged: true })
        const onChanged = vi.fn()
        window.addEventListener(EMPLOYEES_CHANGED_EVENT, onChanged)
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('add jane'))

        expect(onChanged).toHaveBeenCalledTimes(1)
        window.removeEventListener(EMPLOYEES_CHANGED_EVENT, onChanged)
    })

    it('does not refresh the list for read-only answers', async () => {
        mockChat({ responseText: 'Here they are.', type: 'SUCCESS', dataChanged: false })
        const onChanged = vi.fn()
        window.addEventListener(EMPLOYEES_CHANGED_EVENT, onChanged)
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('list'))

        expect(onChanged).not.toHaveBeenCalled()
        window.removeEventListener(EMPLOYEES_CHANGED_EVENT, onChanged)
    })

    it('confirming sends only the token and marks the prompt as confirmed', async () => {
        const requests = mockChat(
            {
                responseText: 'Delete Jane?',
                type: 'CONFIRMATION',
                pendingAction: { token: 'tok-1', tool: 'delete_employee', summary: 'Delete Jane?', expiresAtUtc: '2999-01-01T00:00:00Z' },
                newMessages: [{ role: 'assistant', content: 'Delete Jane?' }],
            },
            { responseText: 'Jane was deleted.', type: 'SUCCESS', dataChanged: true, newMessages: [{ role: 'assistant', content: 'Jane was deleted.' }] },
        )
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('delete jane'))
        const prompt = result.current.messages.find((m) => m.pendingAction)!
        expect(prompt.pendingState).toBe('open')

        await act(() => result.current.confirmAction(prompt.id))

        expect(requests[1]).toEqual({ confirmAction: 'tok-1' })
        expect(result.current.messages.find((m) => m.id === prompt.id)?.pendingState).toBe('confirmed')
        expect(result.current.messages[result.current.messages.length - 1]?.text).toBe('Jane was deleted.')
    })

    it('cancelling sends nothing, closes the prompt and informs the model on the next message', async () => {
        const requests = mockChat(
            {
                responseText: 'Delete Jane?',
                type: 'CONFIRMATION',
                pendingAction: { token: 'tok-1', tool: 'delete_employee', summary: 'Delete Jane?', expiresAtUtc: '2999-01-01T00:00:00Z' },
                newMessages: [{ role: 'assistant', content: 'Delete Jane?' }],
            },
            { responseText: 'Sure.', type: 'INFO', newMessages: [{ role: 'assistant', content: 'Sure.' }] },
        )
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('delete jane'))
        const prompt = result.current.messages.find((m) => m.pendingAction)!
        act(() => result.current.cancelAction(prompt.id))

        expect(requests).toHaveLength(1)
        expect(result.current.messages.find((m) => m.id === prompt.id)?.pendingState).toBe('cancelled')

        // A second cancel or a confirm after cancel does nothing.
        await act(() => result.current.confirmAction(prompt.id))
        expect(requests).toHaveLength(1)

        await act(() => result.current.sendMessage('what now?'))
        expect(requests[1].messages?.map((m) => m.content)).toEqual([
            'delete jane', 'Delete Jane?', 'No, cancel that. Do not do it.', 'Okay, cancelled. Nothing was changed.', 'what now?',
        ])
    })

    it('shows an error and leaves the transcript unchanged when the request fails', async () => {
        const requests = mockChat()
        server.use(http.post(`${API}/chat`, () => HttpResponse.json({ message: 'boom' }, { status: 500 })))
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('hello'))

        expect(result.current.messages[result.current.messages.length - 1]?.type).toBe('ERROR')
        expect(requests).toHaveLength(0)

        // The failed message is not part of the context of the next attempt.
        const retry = mockChat({ responseText: 'hi', type: 'INFO' })
        await act(() => result.current.sendMessage('hello again'))
        await waitFor(() => expect(retry).toHaveLength(1))
        expect(retry[0].messages).toEqual([{ role: 'user', content: 'hello again' }])
    })

    it('clearing starts a fresh conversation', async () => {
        const requests = mockChat({ responseText: 'a', type: 'INFO', newMessages: [{ role: 'assistant', content: 'a' }] })
        const { result } = renderHook(() => useChat())

        await act(() => result.current.sendMessage('one'))
        act(() => result.current.clearMessages())
        mockChat()
        expect(result.current.messages).toHaveLength(0)
        expect(requests).toHaveLength(1)
    })
})

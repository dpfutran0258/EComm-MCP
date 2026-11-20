import { describe, expect, it } from 'vitest'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { http, HttpResponse } from 'msw'
import EmployeeList from './EmployeeList'
import { notifyEmployeesChanged } from '../constants/events'
import { API, server } from '../test/server'
import { makeEmployee, makePage } from '../test/fixtures'

interface ListRequest {
    page: number
    search: string | null
    department: string | null
}

/** Serves an in-memory employee table and records every list request. */
function mockEmployees(initialCount: number, options: { delayMs?: number } = {}) {
    let employees = Array.from({ length: initialCount }, (_, i) => makeEmployee(i + 1))
    const requests: ListRequest[] = []

    server.use(
        http.get(`${API}/employees`, async ({ request }) => {
            const url = new URL(request.url)
            const page = Number(url.searchParams.get('pageNumber') ?? '1')
            const pageSize = Number(url.searchParams.get('pageSize') ?? '10')
            const search = url.searchParams.get('search')
            requests.push({ page, search, department: url.searchParams.get('department') })
            if (options.delayMs) await new Promise((r) => setTimeout(r, options.delayMs))
            const matching = search
                ? employees.filter((e) => e.fullName.toLowerCase().includes(search.toLowerCase()))
                : employees
            const data = matching.slice((page - 1) * pageSize, page * pageSize)
            return HttpResponse.json(makePage(data, page, matching.length, pageSize))
        }),
        http.delete(`${API}/employees/:id`, ({ params }) => {
            employees = employees.filter((e) => e.id !== Number(params.id))
            return new HttpResponse(null, { status: 204 })
        }),
    )

    return {
        requests,
        rename(id: number, fullName: string) {
            employees = employees.map((e) => (e.id === id ? { ...e, fullName } : e))
        },
    }
}

function renderList() {
    return render(<MemoryRouter><EmployeeList /></MemoryRouter>)
}

describe('EmployeeList', () => {
    it('debounces typing into a single search request', async () => {
        const api = mockEmployees(3)
        const user = userEvent.setup()
        renderList()
        await screen.findByText('Employee 1')
        expect(api.requests).toHaveLength(1)

        await user.type(screen.getByRole('searchbox', { name: 'Search employees' }), 'employee 2')

        await waitFor(() => expect(screen.queryByText('Employee 1')).not.toBeInTheDocument())
        expect(screen.getByText('Employee 2')).toBeInTheDocument()
        const searches = api.requests.filter((r) => r.search !== null)
        expect(searches).toEqual([{ page: 1, search: 'employee 2', department: null }])
    })

    it('keeps the search input enabled and focused while results load', async () => {
        mockEmployees(3, { delayMs: 150 })
        const user = userEvent.setup()
        renderList()
        await screen.findByText('Employee 1')

        const search = screen.getByRole('searchbox', { name: 'Search employees' })
        await user.type(search, 'emp')
        // Wait past the debounce so a request is in flight.
        await new Promise((r) => setTimeout(r, 350))

        expect(search).toBeEnabled()
        expect(search).toHaveFocus()
        await user.type(search, 'loyee')
        expect(search).toHaveValue('employee')
        expect(search).toHaveFocus()
    })

    it('refetches the current page when employees:changed fires', async () => {
        const api = mockEmployees(2)
        renderList()
        await screen.findByText('Employee 1')
        const before = api.requests.length

        api.rename(1, 'Renamed By Chat')
        act(() => notifyEmployeesChanged())

        expect(await screen.findByText('Renamed By Chat')).toBeInTheDocument()
        expect(api.requests.length).toBe(before + 1)
    })

    it('goes to the previous page after deleting the last row on a page', async () => {
        const api = mockEmployees(11)
        const user = userEvent.setup()
        renderList()
        await screen.findByText('Employee 1')
        expect(screen.getByText('Page 1 of 2')).toBeInTheDocument()

        await user.click(screen.getByRole('button', { name: 'Next page' }))
        await screen.findByText('Employee 11')
        expect(screen.getByText('Page 2 of 2')).toBeInTheDocument()

        await user.click(screen.getByRole('button', { name: 'Delete Employee 11' }))
        const dialog = screen.getByRole('dialog', { name: 'Delete Employee?' })
        await user.click(within(dialog).getByRole('button', { name: 'Delete' }))

        expect(await screen.findByText('Employee 1')).toBeInTheDocument()
        expect(screen.queryByText('Employee 11')).not.toBeInTheDocument()
        expect(api.requests[api.requests.length - 1]?.page).toBe(1)
        // 10 employees remain: a single page, so no pagination.
        expect(screen.queryByText(/Page \d of/)).not.toBeInTheDocument()
    })

    it('clamps to the last existing page when the current page comes back empty', async () => {
        const api = mockEmployees(21)
        const user = userEvent.setup()
        renderList()
        await screen.findByText('Employee 1')
        await user.click(screen.getByRole('button', { name: 'Next page' }))
        await user.click(await screen.findByRole('button', { name: 'Next page' }))
        await screen.findByText('Employee 21')

        // Another client deletes page 3's only row, then the list is told to refresh.
        server.use(http.get(`${API}/employees`, ({ request }) => {
            const page = Number(new URL(request.url).searchParams.get('pageNumber'))
            api.requests.push({ page, search: null, department: null })
            const data = page <= 2 ? [makeEmployee(page * 10)] : []
            return HttpResponse.json(makePage(data, page, 20))
        }))
        act(() => notifyEmployeesChanged())

        expect(await screen.findByText('Page 2 of 2')).toBeInTheDocument()
        expect(screen.getByText('Employee 20')).toBeInTheDocument()
    })

    it('shows an error state with retry', async () => {
        mockEmployees(1)
        server.use(http.get(`${API}/employees`, () => HttpResponse.json(
            { message: 'An unexpected error occurred.', statusCode: 500 },
            { status: 500 },
        ), { once: true }))
        const user = userEvent.setup()
        renderList()

        expect(await screen.findByRole('alert')).toHaveTextContent('An unexpected error occurred.')
        await user.click(screen.getByRole('button', { name: 'Retry' }))
        expect(await screen.findByText('Employee 1')).toBeInTheDocument()
    })

    it('shows the empty state', async () => {
        mockEmployees(0)
        renderList()
        expect(await screen.findByText('No employees found')).toBeInTheDocument()
    })

    it('offers department suggestions when the endpoint exists', async () => {
        mockEmployees(1)
        server.use(http.get(`${API}/employees/departments`, () => HttpResponse.json(['Engineering', 'Sales'])))
        renderList()
        await screen.findByText('Employee 1')

        const department = screen.getByRole('combobox', { name: 'Filter by department' })
        await waitFor(() => expect(department).toHaveAttribute('list'))
        const listId = department.getAttribute('list') ?? ''
        const options = document.getElementById(listId)?.querySelectorAll('option') ?? []
        expect([...options].map((o) => o.value)).toEqual(['Engineering', 'Sales'])
    })
})

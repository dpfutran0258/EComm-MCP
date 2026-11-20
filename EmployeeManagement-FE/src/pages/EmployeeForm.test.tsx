import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import App from '../App'
import { API, server } from '../test/server'
import { makeEmployee, makePage } from '../test/fixtures'

function renderAt(path: string) {
    window.history.pushState({}, '', path)
    return render(<App />)
}

async function fillForm(user: ReturnType<typeof userEvent.setup>, values: Record<string, string>) {
    for (const [label, value] of Object.entries(values)) {
        const input = screen.getByLabelText(label, { exact: false })
        await user.clear(input)
        await user.type(input, value)
    }
}

const VALID_VALUES = {
    'Full Name': 'Jane Doe',
    Email: 'jane@example.com',
    Department: 'Engineering',
    Salary: '1000',
}

describe('EmployeeForm', () => {
    it('shows a duplicate email 409 as an inline Email error', async () => {
        server.use(http.post(`${API}/employees`, () => HttpResponse.json(
            { message: 'Email already exists', statusCode: 409, errors: null },
            { status: 409 },
        )))
        const user = userEvent.setup()
        renderAt('/employees/new')

        await screen.findByRole('heading', { name: 'Add New Employee' })
        await fillForm(user, VALID_VALUES)
        await user.click(screen.getByRole('button', { name: 'Create Employee' }))

        expect(await screen.findByText('Email already exists')).toBeInTheDocument()
        const email = screen.getByLabelText('Email', { exact: false })
        expect(email).toHaveAttribute('aria-invalid', 'true')
        expect(email).toHaveAccessibleDescription('Email already exists')
    })

    it('maps 400 field errors onto fields case-insensitively', async () => {
        server.use(http.post(`${API}/employees`, () => HttpResponse.json({
            message: 'One or more validation errors occurred.',
            statusCode: 400,
            errors: { EMAIL: ['Email already exists'], Department: ['Department is not allowed'] },
        }, { status: 400 })))
        const user = userEvent.setup()
        renderAt('/employees/new')

        await screen.findByRole('heading', { name: 'Add New Employee' })
        await fillForm(user, VALID_VALUES)
        await user.click(screen.getByRole('button', { name: 'Create Employee' }))

        expect(await screen.findByText('Email already exists')).toBeInTheDocument()
        expect(screen.getByLabelText('Department', { exact: false }))
            .toHaveAccessibleDescription('Department is not allowed')
    })

    it('validates trimmed values and sends trimmed values', async () => {
        let body: unknown
        server.use(http.post(`${API}/employees`, async ({ request }) => {
            body = await request.json()
            return HttpResponse.json(makeEmployee(1), { status: 201 })
        }))
        const user = userEvent.setup()
        renderAt('/employees/new')
        await screen.findByRole('heading', { name: 'Add New Employee' })

        await fillForm(user, { 'Full Name': '   J   ', Email: '  bad  ', Department: 'Eng<b>', Salary: '1000' })
        await user.click(screen.getByRole('button', { name: 'Create Employee' }))

        expect(screen.getByText('Full name must be at least 2 characters.')).toBeInTheDocument()
        expect(screen.getByText('Enter a valid email.')).toBeInTheDocument()
        expect(screen.getByText(/Department can only contain/)).toBeInTheDocument()
        expect(body).toBeUndefined()

        await fillForm(user, {
            'Full Name': "  José O'Brien-Smith  ",
            Email: '  jose@example.com ',
            Department: ' R&D - 2 ',
            Salary: '1000',
        })
        await user.click(screen.getByRole('button', { name: 'Create Employee' }))

        expect(await screen.findByText('Employee created successfully!')).toBeInTheDocument()
        expect(body).toEqual({
            fullName: "José O'Brien-Smith",
            email: 'jose@example.com',
            department: 'R&D - 2',
            salary: 1000,
        })
    })

    it('resets the form when switching from Edit to Add', async () => {
        server.use(
            http.get(`${API}/employees/7`, () => HttpResponse.json(makeEmployee(7, { fullName: 'Edited Person' }))),
            http.get(`${API}/employees`, () => HttpResponse.json(makePage([], 1, 0))),
        )
        const user = userEvent.setup()
        renderAt('/employees/7/edit')

        expect(await screen.findByDisplayValue('Edited Person')).toBeInTheDocument()

        await user.click(screen.getByRole('link', { name: 'Add Employee' }))

        expect(await screen.findByRole('heading', { name: 'Add New Employee' })).toBeInTheDocument()
        expect(screen.getByLabelText('Full Name', { exact: false })).toHaveValue('')
        expect(screen.getByLabelText('Email', { exact: false })).toHaveValue('')
    })

    it('shows a not-found state for a non-numeric id', async () => {
        renderAt('/employees/abc/edit')

        expect(await screen.findByRole('heading', { name: 'Employee not found' })).toBeInTheDocument()
        expect(screen.getByRole('link', { name: /back to employees/i })).toHaveAttribute('href', '/')
        expect(screen.queryByRole('button', { name: 'Save Changes' })).not.toBeInTheDocument()
    })

    it('shows a not-found state when the employee does not exist', async () => {
        server.use(http.get(`${API}/employees/999`, () => HttpResponse.json(
            { message: 'Employee with ID 999 not found', statusCode: 404 },
            { status: 404 },
        )))
        renderAt('/employees/999/edit')

        expect(await screen.findByRole('heading', { name: 'Employee not found' })).toBeInTheDocument()
        expect(screen.queryByLabelText('Full Name', { exact: false })).not.toBeInTheDocument()
    })

    it('sends the version token back on update when the server provides one', async () => {
        let body: Record<string, unknown> | undefined
        server.use(
            http.get(`${API}/employees/3`, () => HttpResponse.json(makeEmployee(3, { fullName: 'Version Person', version: 42 }))),
            http.put(`${API}/employees/3`, async ({ request }) => {
                body = await request.json() as Record<string, unknown>
                return HttpResponse.json(makeEmployee(3))
            }),
        )
        const user = userEvent.setup()
        renderAt('/employees/3/edit')

        await screen.findByDisplayValue('Version Person')
        await user.click(screen.getByRole('button', { name: 'Save Changes' }))

        expect(await screen.findByText('Employee updated successfully!')).toBeInTheDocument()
        expect(body?.version).toBe(42)
    })
})

import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import Input from './Input'

describe('Input', () => {
    it('links the label to the input with a generated id', () => {
        render(<><Input label="Full Name" name="a" /><Input label="Email" name="b" /></>)

        const name = screen.getByLabelText('Full Name')
        const email = screen.getByLabelText('Email')
        expect(name.id).toBeTruthy()
        expect(email.id).toBeTruthy()
        expect(name.id).not.toBe(email.id)
    })

    it('passes required and aria-* props through to the input', () => {
        render(<Input label="Email" name="email" required aria-label="Work email" aria-describedby="hint" />)

        const input = screen.getByRole('textbox', { name: 'Work email' })
        expect(input).toBeRequired()
        expect(input).toHaveAttribute('aria-describedby', 'hint')
        expect(input).not.toHaveAttribute('aria-invalid', 'true')
    })

    it('marks the input invalid and describes it with the error message', () => {
        render(<Input label="Email" name="email" error="Email already exists" aria-describedby="hint" />)

        const input = screen.getByLabelText('Email')
        expect(input).toHaveAttribute('aria-invalid', 'true')
        expect(input).toHaveAccessibleDescription('Email already exists')
        const error = screen.getByText('Email already exists')
        expect(input.getAttribute('aria-describedby')?.split(' ')).toEqual(['hint', error.id])
    })

    it('describes the input with helper text when there is no error', () => {
        render(<Input label="Salary" name="salary" helperText="Annual, in USD" />)

        expect(screen.getByLabelText('Salary')).toHaveAccessibleDescription('Annual, in USD')
    })

    it('keeps an explicit id', () => {
        render(<Input label="Search" id="search-box" />)
        expect(screen.getByLabelText('Search')).toHaveAttribute('id', 'search-box')
    })
})

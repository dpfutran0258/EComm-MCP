import { describe, expect, it, vi } from 'vitest'
import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ConfirmDialog from './ConfirmDialog'

function Harness({ onConfirm = vi.fn(), isLoading = false }: { onConfirm?: () => void; isLoading?: boolean }) {
    const [open, setOpen] = useState(false)
    return (
        <>
            <button onClick={() => setOpen(true)}>Delete John</button>
            <ConfirmDialog
                title="Delete Employee?"
                message="This action cannot be undone."
                confirmText="Delete"
                isDestructive
                isOpen={open}
                isLoading={isLoading}
                onConfirm={onConfirm}
                onCancel={() => setOpen(false)}
            />
        </>
    )
}

describe('ConfirmDialog', () => {
    it('is labelled and described by its title and message', async () => {
        const user = userEvent.setup()
        render(<Harness />)
        await user.click(screen.getByRole('button', { name: 'Delete John' }))

        const dialog = screen.getByRole('dialog', { name: 'Delete Employee?' })
        expect(dialog).toHaveAccessibleDescription('This action cannot be undone.')
        expect(dialog).toHaveAttribute('open')
    })

    it('moves focus into the dialog, closes on Escape and returns focus to the trigger', async () => {
        const user = userEvent.setup()
        render(<Harness />)
        const trigger = screen.getByRole('button', { name: 'Delete John' })
        await user.click(trigger)

        expect(screen.getByRole('button', { name: 'Cancel' })).toHaveFocus()

        await user.keyboard('{Escape}')

        expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
        expect(trigger).toHaveFocus()
    })

    it('returns focus to the trigger after Cancel', async () => {
        const user = userEvent.setup()
        render(<Harness />)
        const trigger = screen.getByRole('button', { name: 'Delete John' })
        await user.click(trigger)
        await user.click(screen.getByRole('button', { name: 'Cancel' }))

        expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
        expect(trigger).toHaveFocus()
    })

    it('calls onConfirm', async () => {
        const user = userEvent.setup()
        const onConfirm = vi.fn()
        render(<Harness onConfirm={onConfirm} />)
        await user.click(screen.getByRole('button', { name: 'Delete John' }))
        await user.click(screen.getByRole('button', { name: 'Delete' }))

        expect(onConfirm).toHaveBeenCalledOnce()
    })

    it('ignores Escape while the action is in progress', async () => {
        const user = userEvent.setup()
        render(<Harness isLoading />)
        await user.click(screen.getByRole('button', { name: 'Delete John' }))
        screen.getByRole('dialog').focus()
        await user.keyboard('{Escape}')

        expect(screen.getByRole('dialog')).toBeInTheDocument()
    })
})

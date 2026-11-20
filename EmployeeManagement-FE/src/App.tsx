import { useState, useCallback, lazy, Suspense } from 'react'
import { BrowserRouter, Routes, Route, Link, useParams } from 'react-router-dom'
import Header from './components/Header'
import ChatBotToggle from './components/ChatBotToggle'
import ChatBot from './components/ChatBot'
import './App.css'

// Code-split pages so each loads independently
const EmployeeList = lazy(() => import('./pages/EmployeeList'))
const EmployeeForm = lazy(() => import('./pages/EmployeeForm'))

function PageLoader() {
    return (
        <div className="page-loader" aria-label="Loading">
            <div className="spinner" aria-hidden="true" />
        </div>
    )
}

/** Keys the edit form by id so navigating between employees (or to Add) mounts a fresh form. */
function EditEmployeeRoute() {
    const { id } = useParams<{ id: string }>()
    return <EmployeeForm key={`edit-${id}`} />
}

export default function App() {
    const [chatOpen, setChatOpen] = useState(false)
    const closeChat = useCallback(() => setChatOpen(false), [])

    return (
        <BrowserRouter>
            <div className="app">
                <Header />

                <main className="app-main">
                    <Suspense fallback={<PageLoader />}>
                        <Routes>
                            <Route path="/" element={<EmployeeList />} />
                            <Route path="/employees/new" element={<EmployeeForm key="new" />} />
                            <Route path="/employees/:id/edit" element={<EditEmployeeRoute />} />
                            <Route path="*" element={
                                <div className="not-found">
                                    <h2>404 — Page Not Found</h2>
                                    <Link to="/">← Back to employees</Link>
                                </div>
                            } />
                        </Routes>
                    </Suspense>
                </main>

                <ChatBotToggle isOpen={chatOpen} onToggle={setChatOpen} />
                <ChatBot isOpen={chatOpen} onClose={closeChat} />
            </div>
        </BrowserRouter>
    )
}

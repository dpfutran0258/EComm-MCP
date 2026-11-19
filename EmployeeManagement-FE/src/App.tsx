import { useState, lazy, Suspense } from 'react'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
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

export default function App() {
    const [chatOpen, setChatOpen] = useState(false)

    return (
        <BrowserRouter>
            <div className="app">
                <Header />

                <main className="app-main">
                    <Suspense fallback={<PageLoader />}>
                        <Routes>
                            <Route path="/" element={<EmployeeList />} />
                            <Route path="/employees/new" element={<EmployeeForm />} />
                            <Route path="/employees/:id/edit" element={<EmployeeForm />} />
                            <Route path="*" element={
                                <div className="not-found">
                                    <h2>404 — Page Not Found</h2>
                                    <a href="/">← Back to employees</a>
                                </div>
                            } />
                        </Routes>
                    </Suspense>
                </main>

                <ChatBotToggle isOpen={chatOpen} onToggle={setChatOpen} />
                <ChatBot isOpen={chatOpen} onClose={() => setChatOpen(false)} />
            </div>
        </BrowserRouter>
    )
}

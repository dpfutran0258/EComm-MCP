import { NavLink } from 'react-router-dom'
import './Header.css'

export default function Header() {
    return (
        <header className="header">
            <div className="header-content">
                <div className="header-brand">
                    <span className="header-logo" aria-hidden="true">👥</span>
                    <h1 className="header-title">Employee Management</h1>
                </div>

                <nav className="header-nav" aria-label="Main navigation">
                    <NavLink
                        to="/"
                        end
                        className={({ isActive }) => `nav-link${isActive ? ' nav-link--active' : ''}`}
                    >
                        Employees
                    </NavLink>
                    <NavLink
                        to="/employees/new"
                        className={({ isActive }) => `nav-link${isActive ? ' nav-link--active' : ''}`}
                    >
                        Add Employee
                    </NavLink>
                </nav>
            </div>
        </header>
    )
}

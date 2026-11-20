import apiClient, { isApiError } from './client'
import type {
    Employee,
    CreateEmployeePayload,
    UpdateEmployeePayload,
    EmployeeListResponse,
    EmployeeListParams,
} from '../types/employee.types'

export const employeesApi = {
    getAll(params: EmployeeListParams = {}, signal?: AbortSignal): Promise<EmployeeListResponse> {
        return apiClient
            .get<EmployeeListResponse>('/employees', { params, signal })
            .then((r) => r.data)
    },

    getById(id: number, signal?: AbortSignal): Promise<Employee> {
        return apiClient.get<Employee>(`/employees/${id}`, { signal }).then((r) => r.data)
    },

    /**
     * Distinct department names for the filter suggestions.
     * Resolves to an empty list when the endpoint is unavailable (e.g. 404 on older backends).
     */
    getDepartments(signal?: AbortSignal): Promise<string[]> {
        return apiClient
            .get<unknown>('/employees/departments', { signal })
            .then((r) => (Array.isArray(r.data) ? r.data.filter((d): d is string => typeof d === 'string') : []))
            .catch((err: unknown) => {
                if (isApiError(err) && err.isCanceled) throw err
                return []
            })
    },

    create(payload: CreateEmployeePayload): Promise<Employee> {
        return apiClient.post<Employee>('/employees', payload).then((r) => r.data)
    },

    update(id: number, payload: UpdateEmployeePayload): Promise<Employee> {
        return apiClient.put<Employee>(`/employees/${id}`, payload).then((r) => r.data)
    },

    remove(id: number): Promise<void> {
        return apiClient.delete(`/employees/${id}`).then(() => undefined)
    },
}

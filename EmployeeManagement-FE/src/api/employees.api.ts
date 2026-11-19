import apiClient from './client'
import type {
    Employee,
    CreateEmployeePayload,
    UpdateEmployeePayload,
    EmployeeListResponse,
    EmployeeListParams,
} from '../types/employee.types'

export const employeesApi = {
    getAll(params: EmployeeListParams = {}): Promise<EmployeeListResponse> {
        return apiClient
            .get<EmployeeListResponse>('/employees', { params })
            .then((r) => r.data)
    },

    getById(id: number): Promise<Employee> {
        return apiClient.get<Employee>(`/employees/${id}`).then((r) => r.data)
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

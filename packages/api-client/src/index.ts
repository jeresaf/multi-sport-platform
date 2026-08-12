import axios, { AxiosInstance, AxiosError, InternalAxiosRequestConfig, AxiosResponse } from 'axios';
import type { 
  ApiResponse, 
  ApiError, 
  PaginatedResponse,
  Tenant,
  Competition,
  Contest,
  User,
  AuthTokens,
  LiveEventSubmission
} from '@sports-platform/shared-types';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

class ApiClient {
  private client: AxiosInstance;
  private accessToken: string | null = null;
  private refreshToken: string | null = null;

  constructor() {
    this.client = axios.create({
      baseURL: API_BASE_URL,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
    });

    this.setupInterceptors();
  }

  private setupInterceptors() {
    // Request interceptor - add auth token
    this.client.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        if (this.accessToken && config.headers) {
          config.headers.Authorization = `Bearer ${this.accessToken}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor - handle errors and token refresh
    this.client.interceptors.response.use(
      (response: AxiosResponse) => response,
      async (error: AxiosError<ApiError>) => {
        const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
        
        if (error.response?.status === 401 && !originalRequest._retry && this.refreshToken) {
          originalRequest._retry = true;
          
          try {
            const tokens = await this.refreshAccessToken();
            this.setTokens(tokens);
            if (originalRequest.headers) {
              originalRequest.headers.Authorization = `Bearer ${tokens.accessToken}`;
            }
            return this.client(originalRequest);
          } catch (refreshError) {
            this.clearTokens();
            window.location.href = '/login';
            return Promise.reject(refreshError);
          }
        }
        
        return Promise.reject(error);
      }
    );
  }

  // Token management
  setTokens(tokens: AuthTokens) {
    this.accessToken = tokens.accessToken;
    this.refreshToken = tokens.refreshToken;
    if (typeof window !== 'undefined') {
      localStorage.setItem('accessToken', tokens.accessToken);
      localStorage.setItem('refreshToken', tokens.refreshToken);
    }
  }

  clearTokens() {
    this.accessToken = null;
    this.refreshToken = null;
    if (typeof window !== 'undefined') {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
    }
  }

  loadStoredTokens() {
    if (typeof window !== 'undefined') {
      this.accessToken = localStorage.getItem('accessToken');
      this.refreshToken = localStorage.getItem('refreshToken');
    }
  }

  private async refreshAccessToken(): Promise<AuthTokens> {
    const response = await axios.post<AuthTokens>(
      `${API_BASE_URL}/api/auth/refresh`,
      { refreshToken: this.refreshToken }
    );
    return response.data;
  }

  // Generic request methods
  async get<T>(url: string, params?: Record<string, unknown>): Promise<ApiResponse<T>> {
    const response = await this.client.get<ApiResponse<T>>(url, { params });
    return response.data;
  }

  async post<T>(url: string, data?: unknown): Promise<ApiResponse<T>> {
    const response = await this.client.post<ApiResponse<T>>(url, data);
    return response.data;
  }

  async put<T>(url: string, data?: unknown): Promise<ApiResponse<T>> {
    const response = await this.client.put<ApiResponse<T>>(url, data);
    return response.data;
  }

  async patch<T>(url: string, data?: unknown): Promise<ApiResponse<T>> {
    const response = await this.client.patch<ApiResponse<T>>(url, data);
    return response.data;
  }

  async delete<T>(url: string): Promise<ApiResponse<T>> {
    const response = await this.client.delete<ApiResponse<T>>(url);
    return response.data;
  }

  async getPaginated<T>(url: string, params?: Record<string, unknown>): Promise<PaginatedResponse<T>> {
    const response = await this.client.get<PaginatedResponse<T>>(url, { params });
    return response.data;
  }

  // Auth endpoints
  async login(email: string, password: string): Promise<AuthTokens> {
    const response = await this.client.post<AuthTokens>('/api/auth/login', { email, password });
    this.setTokens(response.data);
    return response.data;
  }

  async logout(): Promise<void> {
    await this.post('/api/auth/logout');
    this.clearTokens();
  }

  // Tenant endpoints
  async getTenants(params?: { page?: number; pageSize?: number; status?: string }) {
    return this.getPaginated<Tenant>('/api/tenants', params);
  }

  async getTenant(id: string): Promise<ApiResponse<Tenant>> {
    return this.get<Tenant>(`/api/tenants/${id}`);
  }

  async createTenant(data: Partial<Tenant>): Promise<ApiResponse<Tenant>> {
    return this.post<Tenant>('/api/tenants', data);
  }

  async updateTenant(id: string, data: Partial<Tenant>): Promise<ApiResponse<Tenant>> {
    return this.patch<Tenant>(`/api/tenants/${id}`, data);
  }

  // Competition endpoints
  async getCompetitions(params?: { tenantId?: string; sportId?: string; status?: string; page?: number; pageSize?: number }) {
    return this.getPaginated<Competition>('/api/competitions', params);
  }

  async getCompetition(id: string): Promise<ApiResponse<Competition>> {
    return this.get<Competition>(`/api/competitions/${id}`);
  }

  async createCompetition(data: Partial<Competition>): Promise<ApiResponse<Competition>> {
    return this.post<Competition>('/api/competitions', data);
  }

  async updateCompetition(id: string, data: Partial<Competition>): Promise<ApiResponse<Competition>> {
    return this.patch<Competition>(`/api/competitions/${id}`, data);
  }

  // Contest endpoints
  async getContests(params?: { competitionId?: string; status?: string; page?: number; pageSize?: number }) {
    return this.getPaginated<Contest>('/api/contests', params);
  }

  async getContest(id: string): Promise<ApiResponse<Contest>> {
    return this.get<Contest>(`/api/contests/${id}`);
  }

  async getLiveContests(): Promise<ApiResponse<Contest[]>> {
    return this.get<Contest[]>('/api/contests/live');
  }

  // Live scoring endpoints
  async submitLiveEvent(submission: LiveEventSubmission): Promise<ApiResponse<{ eventId: string }>> {
    return this.post<{ eventId: string }>('/api/live-scoring/events', submission);
  }

  async requestCorrection(contestId: string, correction: {
    originalEventId: string;
    correctionType: 'REVERSE' | 'MODIFY' | 'ADD';
    newValue?: number | string;
    reason: string;
  }): Promise<ApiResponse<{ correctionId: string }>> {
    return this.post<{ correctionId: string }>(`/api/live-scoring/contests/${contestId}/corrections`, correction);
  }

  // User endpoints
  async getCurrentUser(): Promise<ApiResponse<User>> {
    return this.get<User>('/api/users/me');
  }

  async getUsers(params?: { tenantId?: string; role?: string }): Promise<PaginatedResponse<User>> {
    return this.getPaginated<User>('/api/users', params);
  }
}

// Export singleton instance
export const apiClient = new ApiClient();
export default apiClient;

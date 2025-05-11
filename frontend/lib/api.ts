import axios from 'axios'
import { getSession } from 'next-auth/react'

const baseURL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'

export const api = axios.create({
  baseURL,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Add auth token to requests
api.interceptors.request.use(async (config) => {
  const session = await getSession()
  if (session?.accessToken) {
    config.headers.Authorization = `Bearer ${session.accessToken}`
  }
  return config
})

// Handle auth errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Redirect to login or refresh token
      window.location.href = '/auth/signin'
    }
    return Promise.reject(error)
  }
)

// API endpoints
export const projectApi = {
  getProjects: () => api.get('/api/projects'),
  getProject: (id: string) => api.get(`/api/projects/${id}`),
  createProject: (data: any) => api.post('/api/projects', data),
  syncGitHub: (projectId: string, repoUrl: string) => 
    api.post(`/api/projects/${projectId}/sync-github`, { repoUrl }),
}

export const chatApi = {
  sendMessage: (projectId: string, content: string, model?: string) =>
    api.post('/api/chat', { projectId, content, model }),
  getHistory: (sessionId: string) =>
    api.get(`/api/chat/history/${sessionId}`),
}

export const fileApi = {
  uploadFile: (projectId: string, file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('projectId', projectId)
    return api.post('/api/files/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    })
  },
  getFiles: (projectId: string) =>
    api.get(`/api/files/${projectId}`),
  downloadFile: (projectId: string, filePath: string) =>
    api.get(`/api/files/${projectId}/${filePath}`, { responseType: 'blob' }),
  deleteFile: (projectId: string, filePath: string) =>
    api.delete(`/api/files/${projectId}/${filePath}`),
}

// Chat streaming utilities
export const setupChatStream = (projectId: string, content: string, model?: string) => {
  const eventSource = new EventSource(
    `${baseURL}/api/chat/stream?projectId=${projectId}&content=${encodeURIComponent(
      content
    )}${model ? `&model=${model}` : ''}`
  )
  
  return eventSource
}

// SignalR connection utilities
export const setupSignalRConnection = async (hubUrl: string) => {
  const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr')
  
  const connection = new HubConnectionBuilder()
    .withUrl(`${baseURL}${hubUrl}`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build()
    
  return connection
}
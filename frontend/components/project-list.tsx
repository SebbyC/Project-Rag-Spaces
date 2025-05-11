'use client'

import { useState, useEffect } from 'react'
import Link from 'next/link'
import { FolderGit2, Calendar, ExternalLink } from 'lucide-react'
import { formatDate } from '@/lib/utils'
import { useSession } from 'next-auth/react'

type Project = {
  id: string
  name: string
  description: string
  type: string
  gitHubUrl?: string
  createdAt: string
  updatedAt: string
}

export function ProjectList() {
  const { data: session } = useSession()
  const [projects, setProjects] = useState<Project[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    // In a real app, this would fetch from the API
    // Example: api.get('/api/projects').then(res => setProjects(res.data))
    
    // Simulating API call with mock data
    setTimeout(() => {
      setProjects([
        {
          id: '1',
          name: 'RAG Workspace Backend',
          description: 'Backend implementation for the RAG Workspace system',
          type: 'Repository',
          gitHubUrl: 'https://github.com/user/rag-workspace-backend',
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
        {
          id: '2',
          name: 'Frontend App',
          description: 'Next.js frontend for the RAG Workspace',
          type: 'Repository',
          gitHubUrl: 'https://github.com/user/rag-workspace-frontend',
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        },
      ])
      setLoading(false)
    }, 1000)
  }, [])

  if (loading) {
    return (
      <div className="space-y-4">
        {[1, 2, 3].map((n) => (
          <div key={n} className="border rounded-lg p-6 animate-pulse">
            <div className="h-4 bg-gray-200 rounded-md w-1/3 mb-4"></div>
            <div className="h-3 bg-gray-200 rounded-md w-1/2 mb-2"></div>
            <div className="h-3 bg-gray-200 rounded-md w-1/4"></div>
          </div>
        ))}
      </div>
    )
  }

  if (projects.length === 0) {
    return (
      <div className="text-center py-12">
        <FolderGit2 className="w-12 h-12 mx-auto text-gray-400" />
        <h3 className="mt-4 text-lg font-medium">No projects yet</h3>
        <p className="mt-2 text-gray-500">Create your first project to get started.</p>
      </div>
    )
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
      {projects.map((project) => (
        <Link key={project.id} href={`/dashboard/chat/${project.id}`}>
          <div className="border rounded-lg overflow-hidden hover:shadow-lg transition-shadow">
            <div className="p-6">
              <h3 className="text-lg font-medium">{project.name}</h3>
              <p className="mt-2 text-gray-500">{project.description}</p>
              <div className="mt-4 flex items-center justify-between">
                <div className="flex items-center">
                  <Calendar className="w-4 h-4 text-gray-400 mr-1" />
                  <span className="text-xs text-gray-500">
                    {formatDate(new Date(project.createdAt))}
                  </span>
                </div>
                {project.gitHubUrl && (
                  <a 
                    href={project.gitHubUrl} 
                    target="_blank" 
                    rel="noopener noreferrer"
                    className="text-gray-500 hover:text-blue-600 flex items-center"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <ExternalLink className="w-4 h-4" />
                  </a>
                )}
              </div>
            </div>
          </div>
        </Link>
      ))}
    </div>
  )
}
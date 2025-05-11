'use client'

import { ProjectList } from '@/components/project-list'
import { CreateProjectButton } from '@/components/create-project-button'

export default function Dashboard() {
  return (
    <div>
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-2xl font-bold">Projects</h1>
        <CreateProjectButton />
      </div>
      <ProjectList />
    </div>
  )
}
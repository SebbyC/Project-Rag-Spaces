'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { Home, FolderGit2, MessageSquare, Github, Settings } from 'lucide-react'
import { cn } from '@/lib/utils'

const navigation = [
  { name: 'Dashboard', href: '/dashboard', icon: Home },
  { name: 'Projects', href: '/dashboard/projects', icon: FolderGit2 },
  { name: 'Chat', href: '/dashboard/chat', icon: MessageSquare },
  { name: 'GitHub', href: '/dashboard/github', icon: Github },
  { name: 'Settings', href: '/dashboard/settings', icon: Settings },
]

export function Sidebar() {
  const pathname = usePathname()

  return (
    <div className="w-64 bg-gray-900 text-white">
      <div className="flex h-16 items-center px-4">
        <Link href="/dashboard" className="text-xl font-bold">RAG Workspace</Link>
      </div>
      <nav className="space-y-1 px-2">
        {navigation.map((item) => {
          const isActive = pathname === item.href || pathname?.startsWith(`${item.href}/`)
          return (
            <Link
              key={item.name}
              href={item.href}
              className={cn(
                isActive
                  ? 'bg-gray-800 text-white'
                  : 'text-gray-300 hover:bg-gray-700 hover:text-white',
                'group flex items-center px-2 py-2 text-sm font-medium rounded-md'
              )}
            >
              <item.icon
                className={cn(
                  isActive ? 'text-white' : 'text-gray-400 group-hover:text-white',
                  'mr-3 h-5 w-5'
                )}
                aria-hidden="true"
              />
              {item.name}
            </Link>
          )
        })}
      </nav>
    </div>
  )
}
'use client'

import { useSession, signOut } from 'next-auth/react'
import { User, LogOut } from 'lucide-react'

export function Header() {
  const { data: session } = useSession()

  return (
    <header className="h-16 border-b flex items-center justify-between px-6">
      <div>
        {/* Page-specific header content can go here */}
      </div>
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-full bg-gray-200 flex items-center justify-center">
            <User className="h-4 w-4 text-gray-500" />
          </div>
          <span className="text-sm font-medium">{session?.user?.name || 'User'}</span>
        </div>
        <button
          onClick={() => signOut()}
          className="p-2 rounded-full hover:bg-gray-100"
          title="Sign out"
        >
          <LogOut className="h-4 w-4" />
        </button>
      </div>
    </header>
  )
}
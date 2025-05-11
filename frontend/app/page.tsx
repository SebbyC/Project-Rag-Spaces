import Link from 'next/link'

export default function Home() {
  return (
    <main className="flex min-h-screen flex-col items-center justify-center p-24">
      <div className="z-10 max-w-5xl w-full items-center justify-between font-mono text-sm">
        <h1 className="text-4xl font-bold mb-8 text-center">RAG Workspace</h1>
        <p className="text-xl text-center mb-8">
          AI-powered code assistant with deep project understanding
        </p>
        <div className="flex gap-4 justify-center">
          <Link href="/auth/signin" className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors">
            Sign In
          </Link>
          <Link href="/dashboard" className="px-4 py-2 border border-blue-600 text-blue-600 rounded hover:bg-blue-50 transition-colors">
            Dashboard
          </Link>
        </div>
      </div>
    </main>
  )
}
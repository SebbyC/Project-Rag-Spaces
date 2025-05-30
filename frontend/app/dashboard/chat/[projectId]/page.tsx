'use client'

import { useState, useRef, useEffect } from 'react'
import { useParams } from 'next/navigation'
import { SendHorizontal, PaperclipIcon, Loader2 } from 'lucide-react'

export default function ChatPage() {
  const params = useParams()
  const projectId = params.projectId as string
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)
  const [messages, setMessages] = useState<any[]>([])
  const bottomRef = useRef<HTMLDivElement>(null)

  // Scroll to bottom whenever messages change
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!message.trim() || loading) return

    const userMessage = { id: Date.now().toString(), role: 'user', content: message }
    setMessages(prev => [...prev, userMessage])
    setMessage('')
    setLoading(true)

    try {
      // In a real app, this would be an API call
      // Example: const response = await api.post('/api/chat', { projectId, content: message })
      
      // Simulating API call with delay
      await new Promise(resolve => setTimeout(resolve, 2000))
      
      const assistantMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `This is a simulated response for project ${projectId}. In the actual implementation, this would be generated by the RAG system with context from your project files.\n\nYou can discuss code, documentation, or ask questions about this project.`
      }
      
      setMessages(prev => [...prev, assistantMessage])
    } catch (error) {
      console.error('Error sending message:', error)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="h-full flex flex-col">
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.length === 0 ? (
          <div className="h-full flex items-center justify-center">
            <div className="text-center">
              <h2 className="text-xl font-semibold mb-2">Welcome to the Chat</h2>
              <p className="text-gray-500">
                Ask questions about your project or send a message to get started.
              </p>
            </div>
          </div>
        ) : (
          messages.map(msg => (
            <div 
              key={msg.id} 
              className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
            >
              <div 
                className={`max-w-3/4 rounded-lg p-4 ${msg.role === 'user' 
                  ? 'bg-blue-600 text-white' 
                  : 'bg-gray-100'}`}
              >
                <div className="whitespace-pre-wrap">{msg.content}</div>
              </div>
            </div>
          ))
        )}
        {loading && (
          <div className="flex justify-start">
            <div className="bg-gray-100 rounded-lg p-4">
              <Loader2 className="w-5 h-5 animate-spin text-gray-500" />
            </div>
          </div>
        )}
        <div ref={bottomRef} />
      </div>

      <form onSubmit={handleSubmit} className="border-t p-4">
        <div className="flex items-center gap-2">
          <button 
            type="button" 
            className="p-2 rounded-md hover:bg-gray-100 transition-colors"
            title="Upload files"
          >
            <PaperclipIcon className="w-5 h-5 text-gray-500" />
          </button>
          <input
            type="text"
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            placeholder="Type a message..."
            className="flex-1 border-0 bg-transparent focus:ring-0 focus:outline-none"
          />
          <button 
            type="submit" 
            disabled={!message.trim() || loading}
            className="p-2 rounded-md bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <SendHorizontal className="w-5 h-5" />
          </button>
        </div>
      </form>
    </div>
  )
}
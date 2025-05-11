# RAG Workspace API Documentation

## Authentication

All API endpoints require JWT authentication. Include the token in the Authorization header:

```
Authorization: Bearer <token>
```

## Endpoints

### Authentication

- `POST /api/auth/register` - Register a new user
  - Body: `{ "email": "user@example.com", "password": "password", "name": "User Name" }`
  - Response: `{ "id": "user_id", "email": "user@example.com", "name": "User Name" }`

- `POST /api/auth/login` - Login and get JWT token
  - Body: `{ "email": "user@example.com", "password": "password" }`
  - Response: `{ "token": "jwt_token", "user": { "id": "user_id", "email": "user@example.com", "name": "User Name" } }`

### Projects

- `GET /api/projects` - Get all projects for the authenticated user
  - Response: Array of project objects

- `GET /api/projects/{id}` - Get project details
  - Response: Project object with details

- `POST /api/projects` - Create a new project
  - Body: `{ "name": "Project Name", "description": "Project description", "type": "Repository", "gitHubUrl": "https://github.com/user/repo" }`
  - Response: Created project object

- `PUT /api/projects/{id}` - Update project
  - Body: `{ "name": "Updated Name", "description": "Updated description" }`
  - Response: Updated project object

- `DELETE /api/projects/{id}` - Delete project
  - Response: `204 No Content`

- `POST /api/projects/{id}/sync-github` - Sync with GitHub repository
  - Body: `{ "repoUrl": "https://github.com/user/repo" }`
  - Response: `{ "status": "syncing" }`

### Chat

- `POST /api/chat` - Send a chat message
  - Body: `{ "projectId": "project_id", "content": "Message content", "model": "gpt-4o", "useRag": true }`
  - Response: `{ "id": "message_id", "role": "assistant", "content": "Response content", "createdAt": "2023-01-01T00:00:00Z", "model": "gpt-4o", "provider": "azure-openai" }`

- `POST /api/chat/stream` - Stream chat responses (Server-Sent Events)
  - Body: Same as `/api/chat`
  - Response: Stream of text chunks

- `GET /api/chat/history/{sessionId}` - Get chat history for a session
  - Response: Array of message objects

### Files

- `POST /api/files/upload` - Upload a file
  - Form data: `file` (file), `projectId` (string)
  - Response: `{ "path": "user_id/uploads/filename.ext", "status": "processing" }`

- `GET /api/files/{projectId}` - Get files for a project
  - Response: Array of file objects

- `GET /api/files/{projectId}/{*filePath}` - Download a file
  - Response: File content with appropriate content type

- `DELETE /api/files/{projectId}/{*filePath}` - Delete a file
  - Response: `204 No Content`

## WebSocket/SignalR

Connect to `/hubs/chat` for real-time chat updates.

### Events

- `ReceiveMessage` - Receive a new message
  - Payload: Message object

- `ProcessingStatus` - File processing status updates
  - Payload: `{ "projectId": "project_id", "filePath": "path/to/file", "status": "completed", "message": "Processing complete" }`

### Methods

- `JoinProjectGroup(projectId)` - Join a project group to receive project-specific events
- `LeaveProjectGroup(projectId)` - Leave a project group

## Models

### User

```json
{
  "id": "string",
  "email": "string",
  "name": "string",
  "createdAt": "string (ISO date)",
  "updatedAt": "string (ISO date)"
}
```

### Project

```json
{
  "id": "string",
  "name": "string",
  "description": "string",
  "ownerId": "string",
  "type": "string (Repository|Documentation|Mixed)",
  "gitHubUrl": "string (optional)",
  "createdAt": "string (ISO date)",
  "updatedAt": "string (ISO date)"
}
```

### ChatMessage

```json
{
  "id": "string",
  "role": "string (user|assistant|system)",
  "content": "string",
  "createdAt": "string (ISO date)",
  "model": "string (optional)",
  "provider": "string (optional)"
}
```

### FileInfo

```json
{
  "name": "string",
  "path": "string",
  "size": "number",
  "modifiedAt": "string (ISO date)",
  "isDirectory": "boolean"
}
```
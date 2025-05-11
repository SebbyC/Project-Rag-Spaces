# RAG Workspace

A production-ready RAG (Retrieval-Augmented Generation) platform that enables developers to interact with their codebases using AI. The system supports multiple LLMs (Azure OpenAI, OpenAI, Google Gemini), integrates with Azure File Share for persistent storage, and provides a seamless experience across local development and cloud deployments.

## Features

- **Multi-LLM Support**: Switch between Azure OpenAI (GPT-4o), OpenAI API, and Google Gemini 2.5 Pro
- **GitHub Integration**: Connect and analyze entire repositories
- **Persistent Storage**: Azure File Share integration for project files
- **Vector Search**: Qdrant for efficient semantic search
- **Modern Stack**: Next.js frontend, .NET 8 backend
- **Real-time**: Streaming responses from LLMs
- **Containerized**: Docker for consistent development and deployment

## Getting Started

### Prerequisites

- [Docker](https://www.docker.com/products/docker-desktop) and Docker Compose
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (for local backend development)
- [Node.js](https://nodejs.org/) (for local frontend development)
- Azure Account (for cloud features)
- API keys for at least one LLM provider (Azure OpenAI, OpenAI, or Google Vertex AI)

### Local Development Setup

1. Clone the repository
   ```bash
   git clone https://github.com/yourusername/rag-workspace.git
   cd rag-workspace
   ```

2. Set up environment variables
   ```bash
   cp .env.example .env
   # Edit .env with your API keys and configuration
   ```

3. Start the development environment
   ```bash
   ./scripts/dev.sh
   ```

4. Access the application
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:8080
   - Qdrant UI: http://localhost:6333/dashboard

## Project Structure

```
rag-workspace/
├── backend/               # .NET 8 API
├── frontend/              # Next.js application
├── infrastructure/        # Terraform/Bicep for cloud deployment
├── vector-db/             # Qdrant configuration
├── docs/                  # Documentation
├── .github/               # GitHub Actions workflows
├── docker-compose.yml     # Local development setup
└── docker-compose.azure.yml # Azure-specific configuration
```

## Documentation

- [API Documentation](docs/api/README.md)
- [Architecture Overview](docs/architecture/README.md)
- [Development Guide](docs/DEVELOPMENT.md)

## Deployment

The application can be deployed to Azure using the included GitHub Actions workflow and Terraform/Bicep templates. See the [Deployment Guide](docs/deployment.md) for details.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

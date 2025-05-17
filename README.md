# Project-RAG-CoWorkspace

**Version:** 1.0 (Target)
**Status:** Active Development
**Live Demo Target:** [https://project-rag.com](https://project-rag.com) (Note: This is the envisioned production URL; site may not be live yet.)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

An AI-powered, flexible co-workspace designed for developers. `Project-RAG-CoWorkspace` enables deep interaction with project codebases through a Retrieval-Augmented Generation (RAG) architecture, multi-LLM support, dynamic project file management, advanced Qdrant indexing, GitHub integration, and a seamless Next.js web interface across local and cloud deployments.

## North Star Vision 🌟

The ultimate goal is to create an AI pair-programmer that deeply understands an entire software project—its code, documentation, and evolving artifacts. It aims to significantly boost developer productivity by handling tasks like project bootstrapping, context-aware code generation, multi-file refactoring, and providing insightful explanations, making it feel like you have an incredibly knowledgeable AI teammate available 24/7.

## Core Features

*   **Advanced RAG Pipeline:**
    *   Sophisticated, type-aware chunking strategies (AST-based for code, heading-based for Markdown, structured for configs).
    *   Utilizes **Qdrant** with efficient payload indexing (including tenant and on-disk optimizations) for fast, filtered semantic search.
*   **Multi-LLM Support:** Dynamically switch between leading models:
    *   Google Gemini 2.5 Pro (via Vertex AI)
    *   Azure OpenAI Service (e.g., GPT-4, GPT-4o)
    *   OpenAI API
*   **Dynamic Project Workspace:**
    *   Each project has its own structured directory on **Azure File Share** for:
        *   `source/`: User-uploaded files, cloned GitHub repositories.
        *   `ai/summaries/`: LLM-generated conversation summaries.
        *   `ai/implementation_plans/`: LLM-generated plans.
        *   `ai/change_logs/`: Logs of AI-assisted changes.
        *   `meta/project.json`: Project metadata and settings.
    *   All generated artifacts are automatically indexed for ongoing RAG.
*   **GitHub Integration:** Connect, clone, and incrementally sync entire GitHub repositories into the project workspace for analysis and RAG.
*   **Persistent Storage:** Robust Azure File Share integration for all project files and AI-generated artifacts.
*   **Modern Tech Stack:** Next.js (Frontend with TypeScript), .NET 8 (Backend API with C#).
*   **Real-time Interaction:**
    *   Streaming LLM responses to the UI.
    *   (Planned) SignalR for real-time status updates (e.g., file processing).
*   **Containerized & Cloud-Ready:** Fully containerized with Docker for consistent local development and scalable Azure cloud deployments (Azure Container Apps).
*   **User Management & (Planned) Billing:** Secure user registration, authentication, and a tiered system (free/pro) with usage limits (potentially integrated with Stripe).
*   **Adaptive Conversation Management:** Dynamic conversation summarization based on active LLM's context window size.

## Technology Stack

*   **Frontend:** Next.js 14, React 18, TypeScript, Tailwind CSS, Axios, NextAuth.js, Zustand (or React Query), SignalR Client.
*   **Backend:** .NET 8 (ASP.NET Core Web API), C#.
    *   **AI/LLM SDKs:** `Azure.AI.OpenAI`, `Google.Cloud.AIPlatform.V1`, `OpenAI-API-dotnet`.
    *   **Storage SDK:** `Azure.Storage.Files.Shares`.
    *   **Database:** Entity Framework Core with Npgsql for PostgreSQL.
    *   **Vector DB Client:** `Qdrant.Client`.
    *   **Other Key Packages:** Serilog, AutoMapper, FluentValidation, MediatR, Octokit.NET, SharpToken.
*   **Vector Database:** Qdrant (with HNSW vector indexing and advanced payload indexing).
*   **Persistent File Storage:** Azure File Share.
*   **Relational Database:** PostgreSQL (for user, project, chat metadata).
*   **Caching (Optional):** Redis.
*   **Containerization:** Docker, Docker Compose.
*   **CI/CD:** GitHub Actions.
*   **Cloud Platform:** Microsoft Azure (Azure Container Apps, Azure Storage Account, Azure Database for PostgreSQL, Azure Container Registry, Azure Key Vault).

## Getting Started

### Prerequisites

*   [Docker Desktop](https://www.docker.com/products/docker-desktop) (with Docker Compose V2 enabled).
*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (primarily for CLI tools like EF Core migrations, local backend dev without Docker if preferred).
*   [Node.js](https://nodejs.org/) (LTS version, e.g., v18 or v20+) & npm (for local frontend dev without Docker if preferred).
*   [Azure Account](https://azure.microsoft.com/) (for cloud features and Azure File Share setup, even for local CIFS mount).
*   API keys for at least one LLM provider (Azure OpenAI, OpenAI API, or Google Vertex AI).
*   (For Local Azure File Share CIFS Mount) Storage Account Name & Key from your Azure Storage Account.
*   (Optional) [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli).
*   (Optional) [Tree-sitter CLI](https://tree-sitter.github.io/tree-sitter/creating-parsers#installation) and language grammars if you plan to extend or test code chunking locally outside the .NET integration.

## Getting Started & Local Development

Follow these steps to get the project running locally:

1.  **Clone the Repository:**
    ```bash
    git clone https://github.com/YOUR_USERNAME/Project-RAG-CoWorkspace.git
    # Or your actual fork/repo URL:
    # git clone https://github.com/SebbyC/Project-Rag-Spaces.git
    cd Project-RAG-CoWorkspace
    ```

2.  **Initial Setup Script:**
    This script creates local data directories and your `.env` file from the example.
    ```bash
    chmod +x ./scripts/setup.sh
    ./scripts/setup.sh
    ```

3.  **Configure Environment Variables:**
    *   **Critically important:** Open the newly created `.env` file in the project root.
    *   Fill in all required API keys, connection strings (especially `AZURE_STORAGE_CONNECTION_STRING`, `STORAGE_ACCOUNT`, `STORAGE_KEY`, `AZURE_FILE_SHARE_NAME` for the local CIFS mount), and secrets as per the comments in `.env.example`.
    *   If using Google Vertex AI, ensure your `GOOGLE_APPLICATION_CREDENTIALS` JSON file is placed at the path specified in `.env` (e.g., `./credentials/gcp-credentials.json`) and that this path is correctly volume-mounted in `docker-compose.yml`.

4.  **Start the Development Environment:**
    This comprehensive script builds images, starts all services, waits for health checks, and applies database migrations.
    ```bash
    chmod +x ./scripts/start.sh
    ./scripts/start.sh
    ```
    *(Note: If `start.sh` isn't present yet, use `chmod +x ./scripts/dev.sh && ./scripts/dev.sh` for a more basic startup, then manually run EF migrations if needed: `docker exec rag-backend dotnet ef database update --project src/RagWorkspace.Api`)*

5.  **Access the Application:**
    *   **Frontend:** [http://localhost:3000](http://localhost:3000)
    *   **Backend API (Swagger):** [http://localhost:8080/swagger](http://localhost:8080/swagger)
    *   **Qdrant UI:** [http://localhost:6333/dashboard](http://localhost:6333/dashboard)
    *   **PostgreSQL (e.g., via pgAdmin/DBeaver):** `localhost:5432` (credentials in `.env`)
rag-workspace/
├── backend/ # .NET 8 API (ASP.NET Core)
│ ├── src/RagWorkspace.Api/ # Main API project
│ ├── tests/ # Unit & Integration tests
│ └── Dockerfile
├── frontend/ # Next.js application (App Router)
│ ├── app/
│ ├── components/
│ ├── lib/
│ ├── hooks/
│ └── Dockerfile
├── infrastructure/ # Terraform/Bicep scripts for Azure provisioning
│ ├── terraform/
│ └── bicep/
├── vector-db/ # Qdrant specific configurations (if any beyond compose)
├── docs/ # Project documentation
│ ├── api/ # API specifications
│ ├── architecture/ # Architecture diagrams and explanations
│ └── claude/ # AI collaboration guidelines & logs
├── scripts/ # Helper scripts (setup.sh, dev.sh, start.sh)
├── .github/ # GitHub Actions workflows for CI/CD
├── .dockerignore # Specifies files to ignore during Docker builds
├── .gitignore
├── docker-compose.yml # Docker Compose for local development
├── docker-compose.azure.yml # (Optional) Overrides for Azure-like local setup
├── .env.example # Template for environment variables
└── README.md # This file

## Key Architectural Concepts

*   **RAG Pipeline:** `FileProcessingService` chunks and embeds content into Qdrant. `RAGService` retrieves relevant chunks based on query embeddings and constructs augmented prompts. `ChatService` orchestrates this for user interactions.
*   **Qdrant Indexing:** Payload indexes are created on `userId`, `projectId`, `fileType`, `language` etc., with `is_tenant: true` for `userId` and `projectId` to optimize multi-tenant searches.
*   **Dynamic Project Storage:** Each project on Azure File Share has a dedicated, structured directory (`source/`, `ai/summaries/`, etc.) managed by the backend.
*   **Single Embedding Strategy:** Uses a primary embedding model (e.g., Azure OpenAI `text-embedding-3-large`) for consistency across all indexed content and queries, managed via `IEmbeddingProvider`.

## Documentation

*   **API Documentation:** [`docs/api/README.md`](docs/api/README.md) (auto-generated via Swagger, with manual additions).
*   **Architecture Overview:** [`docs/architecture/README.md`](docs/architecture/README.md) (should contain the Mermaid diagram and explanations).
*   **Development Guide:** [`docs/DEVELOPMENT_GUIDE.md`](docs/DEVELOPMENT_GUIDE.md).
*   **AI Collaboration Guide (for Claude):** [`docs/claude/CLAUDE.md`](docs/claude/CLAUDE.md).
*   **(Planned) Deployment Guide:** `docs/DEPLOYMENT_GUIDE.md`.

## Deployment

The application is designed for deployment to **Microsoft Azure** using Azure Container Apps, Azure File Share, Azure Database for PostgreSQL, and Azure Container Registry. Deployment is automated via GitHub Actions workflows defined in `.github/workflows/` and can be provisioned using Infrastructure as Code templates in `infrastructure/`.

## Contributing

(TODO: Add contribution guidelines - e.g., branching strategy, PR process, coding standards).

## License

This project is licensed under the MIT License - see the `LICENSE` file for details.

---

This README aims to be comprehensive. You'll want to fill in details specific to your `YOUR_USERNAME`, and as the project evolves, keep this document updated, especially the environment variables and deployment notes.
```

**Next Steps for You:**

1.  **Create the `README.md` file** in the root of your `Project-RAG-CoWorkspace` repository and paste the content above.
2.  **Replace `YOUR_USERNAME`** with your actual GitHub username in the clone URL.
3.  **Create `.env.example`:** Based on the "Environment Variables" section, create an `.env.example` file that lists all the keys without their values, serving as a template.
4.  **Implement `scripts/dev.sh` and `scripts/setup.sh`** as described. The `dev.sh` would primarily be `docker-compose up --build -d` (or without `-d` for attached logs). `setup.sh` could create local data directories if your `docker-compose.yml` maps local volumes to simulate Azure File Share paths during local development.
5.  **Update `docker-compose.yml`:** Ensure it defines services for `frontend`, `backend` (your .NET API), and `qdrant`.
    *   The backend service in `docker-compose.yml` should have volume mounts for local development to simulate the Azure File Share structure if you are not using the Azure SDK directly with connection strings for *all* local file operations. For example:
        ```yaml
        services:
          backend:
            # ... other backend config ...
            volumes:
              - ./data/local_azure_repos:/mnt/projectrag/repos # Simulates Azure File Share for repos
              - ./data/local_azure_uploads:/mnt/projectrag/uploads # Simulates Azure File Share for uploads
            environment: # Ensure these match the paths used in .NET config
              - BACKEND_REPOS_MOUNT_PATH=/mnt/projectrag/repos
              - BACKEND_UPLOADS_MOUNT_PATH=/mnt/projectrag/uploads
              # ... other env vars from .env file
            env_file:
              - .env
        ```
    *   Make sure to create `./data/local_azure_repos` and `./data/local_azure_uploads` locally if you use such a mapping.
6.  **Verify the `init-repo.sh`** script generated by the previous assistant aligns with this README, particularly directory structures and file names.

This README should provide a solid foundation for your project!
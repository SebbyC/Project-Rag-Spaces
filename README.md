
```markdown
# Project-RAG-CoWorkspace

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Status: Active Development](https://img.shields.io/badge/status-active_development-green.svg)]()

An AI-powered, flexible co-workspace designed for developers. It enables deep interaction with project codebases through a Retrieval-Augmented Generation (RAG) architecture, multi-LLM support, and GitHub integration, all accessible via a Next.js web interface.

**Live Demo Target:** [https://project-rag.com](https://project-rag.com) (Note: This is the envisioned production URL. Site may not be live yet.)

## North Star Vision 🌟

The ultimate goal of **Project-RAG-CoWorkspace** is to create an AI pair-programmer that deeply understands an entire codebase and collaborates with developers in real-time. The system aims to significantly boost developer productivity by handling tasks like project bootstrapping, context-aware code generation, multi-file refactoring, and providing insightful explanations about any part of the project. Imagine an AI assistant that can reason over your entire repository, suggest intelligent changes, and seamlessly integrate with your development workflow, making it feel like you have a knowledgeable teammate available 24/7.

## Key Features

*   **Retrieval-Augmented Generation (RAG):** Leverages project files (code, documentation) and external text sources to provide contextually rich and accurate LLM responses.
*   **Multi-LLM Support:** Switchable language models including:
    *   Google Gemini 2.5 Pro (via Vertex AI API) - for large context and advanced reasoning.
    *   Azure OpenAI models (e.g., GPT-4, GPT-4o).
    *   OpenAI API models.
    *   (Future) Local models via Ollama.
*   **Context Overload:** Designed to handle large context windows, ingesting significant portions of a codebase or extensive documentation to inform LLM prompts.
*   **GitHub Integration:**
    *   Connect and index entire GitHub repositories.
    *   Maintain awareness of project structure, files, and (eventually) commit history.
    *   Enable code generation and modification suggestions in the context of the full repository (similar to Claude projects).
    *   (Future) Propose file edits, manage pull requests.
*   **Persistent Storage with Azure File Share:**
    *   Cloned GitHub repositories and user-uploaded files (e.g., zipped repos, markdown specs) are stored persistently on Azure File Share.
    *   Ensures data survives container restarts and can be accessed consistently.
*   **Vector Database:** Uses Qdrant to store embeddings of code, documentation, and other text data for efficient similarity search.
*   **Flexible Cowork Space:**
    *   User registration and (planned) authentication.
    *   (Future) Persistent user/agent memory for conversational context.
    *   (Future) Real-time collaboration features.
*   **Modern Tech Stack:**
    *   **Frontend:** Next.js with TypeScript.
    *   **Backend:** .NET 8 (ASP.NET Core Web API) for robust API development and excellent Azure integration.
    *   **Containerization:** Docker and Docker Compose for consistent development and deployment.
*   **Real-Time Streaming:** Token responses streamed from LLMs to the frontend for an interactive experience.
*   **Developer Focused:** Designed as a product (`project-rag.com`) for developers, with a focus on productivity and seamless workflow integration.

## Architecture Overview

Project-RAG-CoWorkspace employs a decoupled architecture:

1.  **Frontend (Next.js):** Provides the user interface for chat, code viewing, repository connection, and interaction with the AI. Communicates with the backend via REST APIs and WebSockets/SSE for streaming.
2.  **Backend (.NET API):** The core orchestration layer.
    *   Handles API requests, user authentication (planned).
    *   Manages LLM routing (selecting between Gemini, Azure OpenAI, etc.).
    *   Interfaces with Qdrant for RAG retrieval.
    *   Manages file operations, including cloning GitHub repos to and reading from **Azure File Share**.
    *   Processes user uploads to **Azure File Share**.
    *   Orchestrates the RAG pipeline: fetching context, constructing prompts, calling LLMs.
3.  **Vector Database (Qdrant):** Stores embeddings of project files and other textual data to enable semantic search and retrieval for the RAG pipeline.
4.  **Azure File Share:** Provides persistent, cloud-based storage for:
    *   Cloned GitHub repositories.
    *   User-uploaded files (e.g., `.zip` archives, `.md` documents).
    This ensures that indexed data and large files are not lost and are managed efficiently outside the container's lifecycle.
5.  **LLM Services (External):**
    *   Google Vertex AI (for Gemini Pro).
    *   Azure OpenAI Service.
    *   OpenAI API.

All backend services are containerized using Docker for portability and scalability.

```mermaid
graph TD
    User[Developer User] -->|Interacts via Browser| Frontend[Next.js UI on project-rag.com]
    Frontend -->|HTTP API Calls / WebSocket| BackendDotNet[Backend .NET 8 API]

    subgraph Backend Services
        BackendDotNet -->|Manages/Routes| LLMRouter[LLM Router]
        LLMRouter --> AzureOpenAI[Azure OpenAI Service]
        LLMRouter --> VertexAI[Google Vertex AI (Gemini)]
        LLMRouter --> OpenAI_API[OpenAI API]

        BackendDotNet -->|Stores/Retrieves Embeddings| Qdrant[Qdrant Vector DB]
        BackendDotNet -->|Reads/Writes Files| AzureFileShare[Azure File Share (Repos, Uploads)]
        BackendDotNet -->|Clones/Fetches| GitHubAPI[GitHub API]
    end

    Qdrant -->|Stores Embeddings of| CodeDocs[Code & Documents]
    AzureFileShare -->|Source for Indexing| CodeDocs
    GitHubAPI -->|Provides Code for| AzureFileShare
```

## Technology Stack

*   **Frontend:** Next.js, React, TypeScript, Tailwind CSS (or your preferred CSS solution)
*   **Backend:** .NET 8 (ASP.NET Core Web API), C#
*   **Vector Database:** Qdrant
*   **File Storage:** Azure File Share
*   **Containerization:** Docker, Docker Compose
*   **LLM APIs:** Google Vertex AI, Azure OpenAI, OpenAI
*   **CI/CD:** GitHub Actions (initial setup provided)
*   **(Planned) Authentication:** JWT

## Prerequisites

Before you begin, ensure you have the following installed:

*   [Node.js](https://nodejs.org/) (LTS version, e.g., v18 or v20+)
*   [npm](https://www.npmjs.com/) or [yarn](https://yarnpkg.com/)
*   [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
*   [Docker Desktop](https://www.docker.com/products/docker-desktop/)
*   [Git](https://git-scm.com/)
*   [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) (Optional, but useful for Azure File Share setup/management)

## Getting Started & Local Development

Follow these steps to get the project running locally:

1.  **Clone the Repository:**
    ```bash
    git clone https://github.com/YOUR_USERNAME/Project-RAG-CoWorkspace.git
    cd Project-RAG-CoWorkspace
    ```

2.  **Environment Configuration:**
    *   Copy the example environment file to create your local configuration:
        ```bash
        cp .env.example .env
        ```
    *   **Edit `.env`** with your actual API keys and service endpoints. See the [Environment Variables](#environment-variables) section below for details on what's needed.
        *   **Crucially, for Azure File Share to work locally (simulated via Docker volumes or for direct SDK access if not mounting directly in compose for local dev), you might need to set `AZURE_STORAGE_ACCOUNT_NAME` and `AZURE_STORAGE_ACCOUNT_KEY` (or a connection string `AZURE_STORAGE_CONNECTION_STRING`).** How these are used locally vs. cloud depends on your `docker-compose.yml` and Azure deployment strategy. The backend code will be written to expect files at specific mount paths.

3.  **Initial Setup Script (Optional but Recommended):**
    If a `scripts/setup.sh` script is provided, it might automate some of the initial configuration steps (e.g., creating necessary local directories for Docker volume mounts if simulating Azure File Share locally).
    ```bash
    chmod +x ./scripts/setup.sh
    ./scripts/setup.sh
    ```
    *If no `setup.sh` exists, ensure any local directories specified in `docker-compose.yml` for volumes are created, e.g., `./data/azure_file_share_local_repos`, `./data/azure_file_share_local_uploads`.*

4.  **Build and Run with Docker Compose:**
    The `./scripts/dev.sh` script typically handles this.
    ```bash
    chmod +x ./scripts/dev.sh
    ./scripts/dev.sh
    ```
    This command should:
    *   Build the Docker images for the frontend and backend.
    *   Start all services defined in `docker-compose.yml` (Next.js frontend, .NET backend, Qdrant).

5.  **Accessing the Application:**
    *   **Frontend:** [http://localhost:3000](http://localhost:3000)
    *   **Backend API:** [http://localhost:8080](http://localhost:8080) (or the port specified in `docker-compose.yml` and your .NET configuration)

## Environment Variables (`.env` file)

You **must** create a `.env` file in the project root by copying `.env.example`. Fill in the following (examples):

```ini
# LLM Provider API Keys
OPENAI_API_KEY="sk-yourOpenAIkey"
AZURE_OPENAI_ENDPOINT="https://your-aoai-resource.openai.azure.com/"
AZURE_OPENAI_API_KEY="yourAzureOpenAIkey"
AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4" # Your specific deployment name for chat
AZURE_OPENAI_EMBEDDINGS_DEPLOYMENT_NAME="text-embedding-ada-002" # Your specific deployment for embeddings

# Google Vertex AI (for Gemini) - Ensure your environment is authenticated (e.g., gcloud auth application-default login)
# Or set GOOGLE_APPLICATION_CREDENTIALS to path of your service account JSON key
GOOGLE_PROJECT_ID="your-gcp-project-id"
GOOGLE_REGION="us-central1" # e.g., us-central1

# Qdrant Configuration
QDRANT_HOST="qdrant" # Service name in docker-compose
QDRANT_PORT="6333"
QDRANT_COLLECTION_NAME="project_rag_collection"

# Azure File Share (primarily for cloud, but keys might be needed for local SDK dev if not using direct mounts for all local scenarios)
# For local development, docker-compose volumes will likely simulate these paths.
# For cloud, the .NET backend will write to paths configured via environment variables that map to mounted Azure File Shares.
AZURE_STORAGE_ACCOUNT_NAME="yourstorageaccountname"
AZURE_STORAGE_ACCOUNT_KEY="yourstorageaccountkey" # Use Managed Identity in production!
AZURE_STORAGE_CONNECTION_STRING="" # Alternative to Account Name/Key
AZURE_REPOS_FILE_SHARE_NAME="projectrag-repos" # The name of your file share for repos
AZURE_UPLOADS_FILE_SHARE_NAME="projectrag-uploads" # The name of your file share for uploads
# Mount paths expected by the .NET application (these should align with volume mounts in Docker/Azure)
BACKEND_REPOS_MOUNT_PATH="/mnt/projectrag/repos"
BACKEND_UPLOADS_MOUNT_PATH="/mnt/projectrag/uploads"

# JWT Authentication (Planned)
JWT_SECRET="your_super_secret_jwt_key_at_least_32_characters_long"
JWT_ISSUER="project-rag.com"
JWT_AUDIENCE="project-rag.com"

# Frontend API URL (if different from default)
NEXT_PUBLIC_API_BASE_URL="http://localhost:8080/api" # During local dev, points to backend container
```
**Note on Azure File Share in `.env`:**
*   For **local development**, `docker-compose.yml` might map local host directories to `BACKEND_REPOS_MOUNT_PATH` and `BACKEND_UPLOADS_MOUNT_PATH` inside the backend container, simulating the Azure File Share structure.
*   For **cloud deployment (e.g., Azure Container Apps)**, you will configure volume mounts to map actual Azure File Shares to these same paths inside the container. The .NET application code consistently uses these paths. The storage account credentials (`AZURE_STORAGE_ACCOUNT_NAME`, `AZURE_STORAGE_ACCOUNT_KEY`, or `AZURE_STORAGE_CONNECTION_STRING`) are primarily for the .NET SDK to interact with Azure Storage if needed directly (e.g., for presigned URLs, or if not using direct OS-level mounts for *all* interactions). **In Azure, always prefer Managed Identities over storing account keys in environment variables.**

## Development Scripts

Located in the `./scripts/` directory:

*   `dev.sh`: Builds and starts the development environment using Docker Compose.
*   `setup.sh` (if provided): Performs initial one-time setup tasks.
*   `logs.sh` (example, you might add this): `docker-compose logs -f backend frontend qdrant`
*   `down.sh` (example, you might add this): `docker-compose down`

## Cloud Deployment (Azure)

This project is designed to be deployed to Azure, leveraging services like:

*   **Azure Container Apps** or **Azure Kubernetes Service (AKS)** for hosting the Docker containers.
*   **Azure File Share** for persistent storage of repositories and user uploads. Configuration involves creating the file shares in an Azure Storage Account and then mounting them as volumes into the backend container.
*   **Azure Cache for Redis** (Optional, for session state, message queue if adding Python AI service).
*   **Azure Key Vault** for securely managing secrets like API keys and connection strings (instead of plain `.env` variables in production).
*   **Azure Application Insights** for monitoring and logging.
*   **Azure Active Directory (Azure AD)** for user authentication (recommended for production).

The backend is configured to use paths like `/mnt/projectrag/repos` and `/mnt/projectrag/uploads`. In your Azure deployment, you will map your actual Azure File Shares to these paths within the container environment.

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request.
(TODO: Add more detailed contributing guidelines, code style, testing procedures).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

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
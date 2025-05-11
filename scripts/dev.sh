#!/usr/bin/env bash
set -euo pipefail # Exit on error, undefined variable, or pipe failure

echo "🚀 Starting Development Environment for Project RAG CoWorkspace..."

# Ensure .env file exists
if [ ! -f .env ]; then
    echo "❌ Error: .env file not found. Please run ./scripts/setup.sh first or create it manually from .env.example."
    exit 1
fi

# Export variables from .env to be available for docker-compose
export $(grep -v '^#' .env | xargs)

# Validate essential variables for Docker Compose CIFS mount (if used)
if [[ -z "${STORAGE_ACCOUNT:-}" || -z "${STORAGE_KEY:-}" || -z "${AZURE_FILE_SHARE_NAME:-}" ]]; then
  echo "⚠️ Warning: STORAGE_ACCOUNT, STORAGE_KEY, or AZURE_FILE_SHARE_NAME not set in .env. CIFS mount for Azure Files in Docker Compose might fail."
  echo "Continuing, but ensure these are set if you intend to use local Azure File Share mounting via CIFS."
fi


echo "🐳 Building and starting Docker containers..."
# Build images if they don't exist or if specified, and start services in detached mode
docker-compose up --build -d postgres qdrant redis backend

# Wait for backend to be healthy (optional, basic check)
echo "⏳ Waiting for backend service to be ready..."
retries=30
while ! docker-compose ps backend | grep -q "Up"; do
  sleep 2
  retries=$((retries-1))
  if [ $retries -eq 0 ]; then
    echo "❌ Backend service failed to start."
    docker-compose logs backend
    exit 1
  fi
done
echo "✅ Backend service is up."

echo "📦 Installing frontend dependencies and starting Next.js dev server..."
# Run frontend in the foreground to see logs directly
# Ensure frontend Docker service is NOT started by docker-compose up -d if running locally like this
# If frontend is also containerized for dev, adjust accordingly (e.g. `docker-compose up -d frontend` and then `docker-compose logs -f frontend`)
(cd frontend && npm install && npm run dev)

echo "🛑 To stop services, run: docker-compose down"
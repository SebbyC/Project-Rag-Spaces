#!/usr/bin/env bash
echo "🚀 Starting Project RAG CoWorkspace Setup..."

# Create local data directories for Docker volumes (if not using direct cloud mounts for dev)
mkdir -p ./data/postgres_data
mkdir -p ./data/qdrant_storage
mkdir -p ./data/redis_data
mkdir -p ./data/azure_files_local_simulation # For CIFS mount target if directly mounting local folder

# Create .env if it doesn't exist, from .env.example
if [ ! -f .env ]; then
    echo "📋 .env file not found. Copying from .env.example..."
    cp .env.example .env
    echo "✅ .env file created. Please edit it with your actual API keys and configurations."
else
    echo "👍 .env file already exists."
fi

# (Optional) Initialize Git submodules if any
# git submodule update --init --recursive

echo "🎉 Setup complete. Remember to configure your .env file!"
echo "👉 Next step: Run ./scripts/dev.sh to start the development environment."
# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies
COPY frontend/package*.json ./
RUN npm ci

# Copy app source
COPY frontend/ ./

# Build the app
RUN npm run build

# Production stage
FROM node:20-alpine AS production
WORKDIR /app

ENV NODE_ENV=production

# Copy build artifacts and dependencies
COPY --from=build /app/package*.json ./
COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/node_modules ./node_modules

# Expose port
EXPOSE 3000

# Start the app
CMD ["npm", "start"]
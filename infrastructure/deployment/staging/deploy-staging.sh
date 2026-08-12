#!/bin/bash
# Staging Deployment Script for SportsPlatform
# Usage: ./deploy-staging.sh [action]
# Actions: start, stop, restart, logs, status, build, validate, clean, backup, restore

set -Eeuo pipefail
export COMPOSE_PARALLEL_LIMIT=1

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

ACTION="${1:-start}"
ENV_FILE=".env.staging"

# Prefer the current Docker CLI plugin, with support for older installations.
if docker compose version >/dev/null 2>&1; then
    COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE=(docker-compose)
else
    echo "Error: Docker Compose is not installed or is not on PATH."
    exit 1
fi

COMPOSE_ARGS=(-f docker-compose.staging.yml)
if [ -f "$ENV_FILE" ]; then
    COMPOSE_ARGS+=(--env-file "$ENV_FILE")
elif [[ "$ACTION" == "start" || "$ACTION" == "build" || "$ACTION" == "validate" ]]; then
    echo "Error: $SCRIPT_DIR/$ENV_FILE is required for the '$ACTION' action."
    exit 1
fi

compose() {
    "${COMPOSE[@]}" "${COMPOSE_ARGS[@]}" "$@"
}

env_value() {
    local name="$1"
    sed -n -E "s/^[[:space:]]*${name}=(.*)$/\\1/p" "$ENV_FILE" | tail -n 1 | sed -E "s/^[[:space:]]*['\"]?//; s/['\"]?[[:space:]]*$//"
}

validate_staging_configuration() {
    local db_password jwt_secret
    db_password="$(env_value DB_PASSWORD)"
    jwt_secret="$(env_value JWT_SECRET)"

    if [ -z "$db_password" ]; then
        echo "Error: DB_PASSWORD is required in $SCRIPT_DIR/$ENV_FILE. Copy .env.example and provision a secret value."
        exit 1
    fi
    if [ "${#db_password}" -lt 12 ]; then
        echo "Error: DB_PASSWORD in $SCRIPT_DIR/$ENV_FILE must contain at least 12 characters."
        exit 1
    fi
    if [ "${#jwt_secret}" -lt 32 ]; then
        echo "Error: JWT_SECRET in $SCRIPT_DIR/$ENV_FILE must contain at least 32 characters."
        exit 1
    fi
    if printf '%s' "$jwt_secret" | grep -Eiq 'change-me|developmentonly|example|replace-|yoursupersecret'; then
        echo "Warning: JWT_SECRET in $SCRIPT_DIR/$ENV_FILE appears to contain an old example value. Rotate it before exposing staging outside the local machine."
    fi

    for recommended in MINIO_ROOT_PASSWORD GRAFANA_ADMIN_PASSWORD; do
        if [ -z "$(env_value "$recommended")" ]; then
            echo "Warning: $recommended is not set; staging will temporarily fall back to DB_PASSWORD. Provision a distinct value at the next credential rotation."
        fi
    done
}

if [[ "$ACTION" == "start" || "$ACTION" == "build" || "$ACTION" == "validate" ]]; then
    validate_staging_configuration
fi

echo "=========================================="
echo "SportsPlatform Staging Deployment"
echo "=========================================="
echo "Action: ${ACTION}"
echo "=========================================="

case "$ACTION" in
    start)
        echo "Starting staging environment..."
        # Docker Desktop on Windows can race while replacing services built in
        # parallel, so build and recreate serially.
        compose build
        compose up -d
        # Nginx resolves Compose service names when it starts. Refresh it after
        # application containers are replaced so it cannot retain stale IPs.
        compose restart nginx
        echo "Waiting for services to be healthy..."
        HEALTHY=false
        for _ in {1..30}; do
            if curl --silent --fail http://localhost:8080/health >/dev/null 2>&1 \
                && curl --silent --fail --header 'Host: staging.sportsplatform.local' http://localhost/health >/dev/null 2>&1 \
                && curl --silent --fail http://localhost:3000/ >/dev/null 2>&1 \
                && curl --silent --fail http://localhost:3001/ >/dev/null 2>&1 \
                && curl --silent --fail http://localhost:9000/minio/health/live >/dev/null 2>&1 \
                && curl --silent --fail http://localhost:8025/readyz >/dev/null 2>&1; then
                HEALTHY=true
                break
            fi
            sleep 2
        done
        compose ps
        if [ "$HEALTHY" != "true" ]; then
            echo "Error: One or more application services did not become healthy within 60 seconds."
            compose logs --tail 100 api admin-web public-web
            exit 1
        fi
        echo ""
        echo "Staging environment started!"
        echo "API: http://localhost:8080"
        echo "Admin web: http://localhost:3000"
        echo "Public web: http://localhost:3001"
        echo "Nginx: http://localhost"
        ;;
    
    stop)
        echo "Stopping staging environment..."
        compose down
        echo "Staging environment stopped."
        ;;
    
    restart)
        echo "Restarting staging environment..."
        compose restart
        echo "Staging environment restarted."
        ;;
    
    logs)
        echo "Showing logs..."
        compose logs -f
        ;;
    
    status)
        echo "Checking service status..."
        compose ps
        ;;
    
    build)
        echo "Building staging images..."
        compose build --no-cache
        echo "Build completed."
        ;;

    validate)
        compose config --quiet
        echo "Staging configuration is valid."
        ;;
    
    clean)
        echo "Warning: this removes the staging database and Redis volumes."
        echo "Cleaning up staging environment..."
        compose down -v
        echo "Staging environment cleaned."
        ;;
    
    backup)
        echo "Creating backup..."
        cd ../../database
        ./backup.sh staging
        cd ../deployment/staging
        ;;
    
    restore)
        BACKUP_FILE="${2:-}"
        if [ -z "$BACKUP_FILE" ]; then
            echo "Error: Backup file not specified"
            echo "Usage: ./deploy-staging.sh restore [backup_file]"
            exit 1
        fi
        cd ../../database
        ./restore.sh "$BACKUP_FILE" staging
        cd ../deployment/staging
        ;;
    
    *)
        echo "Unknown action: $ACTION"
        echo "Usage: ./deploy-staging.sh [start|stop|restart|logs|status|build|clean|validate|backup|restore]"
        exit 1
        ;;
esac

echo "=========================================="

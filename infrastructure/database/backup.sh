#!/bin/bash
# Database Backup Script for SportsPlatform
# Usage: ./backup.sh [environment]
# Example: ./backup.sh production

set -e

ENVIRONMENT=${1:-staging}
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="./backups/${ENVIRONMENT}"
CONTAINER_NAME="sportsplatform_db"

# Create backup directory
mkdir -p "${BACKUP_DIR}"

echo "=========================================="
echo "SportsPlatform Database Backup"
echo "=========================================="
echo "Environment: ${ENVIRONMENT}"
echo "Timestamp: ${TIMESTAMP}"
echo "=========================================="

# PostgreSQL backup
if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Backing up PostgreSQL database..."
    
    docker exec -t ${CONTAINER_NAME} pg_dump -U postgres -d sports_platform \
        -F c \
        -f /tmp/backup_${TIMESTAMP}.sql \
        -v
    
    docker cp ${CONTAINER_NAME}:/tmp/backup_${TIMESTAMP}.sql "${BACKUP_DIR}/sports_platform_${TIMESTAMP}.dump"
    
    # Remove temporary file from container
    docker exec ${CONTAINER_NAME} rm /tmp/backup_${TIMESTAMP}.sql
    
    echo "PostgreSQL backup completed: ${BACKUP_DIR}/sports_platform_${TIMESTAMP}.dump"
else
    echo "Warning: PostgreSQL container not running. Skipping PostgreSQL backup."
fi

# SQLite backup (for development)
if [ -f "sportsplatform.db" ]; then
    echo "Backing up SQLite database..."
    cp sportsplatform.db "${BACKUP_DIR}/sports_platform_${TIMESTAMP}.db"
    echo "SQLite backup completed: ${BACKUP_DIR}/sports_platform_${TIMESTAMP}.db"
fi

# Create backup manifest
cat > "${BACKUP_DIR}/manifest_${TIMESTAMP}.json" << EOF
{
    "timestamp": "${TIMESTAMP}",
    "environment": "${ENVIRONMENT}",
    "backup_date": "$(date -Iseconds)",
    "files": [
        $(ls -1 ${BACKUP_DIR}/*.dump 2>/dev/null | xargs -I {} echo "\"$(basename {})\"" | tr '\n' ',' | sed 's/,$//')
    ]
}
EOF

# Cleanup old backups (keep last 7 days)
echo "Cleaning up old backups..."
find ${BACKUP_DIR} -name "*.dump" -mtime +7 -delete 2>/dev/null || true
find ${BACKUP_DIR} -name "*.db" -mtime +7 -delete 2>/dev/null || true
find ${BACKUP_DIR} -name "manifest_*.json" -mtime +7 -delete 2>/dev/null || true

echo "=========================================="
echo "Backup completed successfully!"
echo "=========================================="
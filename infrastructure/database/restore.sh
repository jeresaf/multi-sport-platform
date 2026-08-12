#!/bin/bash
# Database Restore Script for SportsPlatform
# Usage: ./restore.sh [backup_file] [environment]
# Example: ./restore.sh backups/staging/sports_platform_20240115_120000.dump staging

set -e

BACKUP_FILE=$1
ENVIRONMENT=${2:-staging}
CONTAINER_NAME="sportsplatform_db"

if [ -z "$BACKUP_FILE" ]; then
    echo "Error: Backup file not specified"
    echo "Usage: ./restore.sh [backup_file] [environment]"
    echo "Example: ./restore.sh backups/staging/sports_platform_20240115_120000.dump staging"
    exit 1
fi

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Error: Backup file not found: $BACKUP_FILE"
    exit 1
fi

echo "=========================================="
echo "SportsPlatform Database Restore"
echo "=========================================="
echo "Backup file: ${BACKUP_FILE}"
echo "Environment: ${ENVIRONMENT}"
echo "=========================================="

# Confirmation prompt
read -p "This will overwrite the current database. Are you sure? (yes/no): " CONFIRM
if [ "$CONFIRM" != "yes" ]; then
    echo "Restore cancelled."
    exit 0
fi

# PostgreSQL restore
if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Restoring PostgreSQL database..."
    
    # Copy backup to container
    docker cp "$BACKUP_FILE" ${CONTAINER_NAME}:/tmp/restore_backup.dump
    
    # Drop and recreate database
    docker exec -t ${CONTAINER_NAME} psql -U postgres -c "DROP DATABASE IF EXISTS sports_platform;"
    docker exec -t ${CONTAINER_NAME} psql -U postgres -c "CREATE DATABASE sports_platform;"
    
    # Restore backup
    docker exec -t ${CONTAINER_NAME} pg_restore -U postgres -d sports_platform \
        --clean \
        --if-exists \
        -v \
        /tmp/restore_backup.dump
    
    # Remove temporary file from container
    docker exec ${CONTAINER_NAME} rm /tmp/restore_backup.dump
    
    echo "PostgreSQL restore completed successfully!"
else
    echo "Error: PostgreSQL container not running."
    exit 1
fi

echo "=========================================="
echo "Restore completed successfully!"
echo "=========================================="
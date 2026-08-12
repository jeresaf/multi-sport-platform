#!/bin/bash
# System Health Check Script
# Checks the health of all services in the staging environment

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ENV_FILE="${PROJECT_ROOT}/deployment/staging/.env.staging"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Load environment variables
if [ -f "$ENV_FILE" ]; then
    source "$ENV_FILE"
else
    echo -e "${RED}Error: Environment file not found at $ENV_FILE${NC}"
    exit 1
fi

# Default values
API_URL="${API_URL:-http://localhost:8080}"
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
POSTGRES_DB="${POSTGRES_DB:-sportsplatform}"
REDIS_HOST="${REDIS_HOST:-localhost}"
REDIS_PORT="${REDIS_PORT:-6379}"

# Counters
PASSED=0
FAILED=0
WARNINGS=0

# Function to print status
print_status() {
    local status=$1
    local message=$2
    if [ "$status" == "PASS" ]; then
        echo -e "${GREEN}[PASS]${NC} $message"
        ((PASSED++))
    elif [ "$status" == "FAIL" ]; then
        echo -e "${RED}[FAIL]${NC} $message"
        ((FAILED++))
    elif [ "$status" == "WARN" ]; then
        echo -e "${YELLOW}[WARN]${NC} $message"
        ((WARNINGS++))
    fi
}

echo "========================================"
echo "  System Health Check"
echo "  $(date)"
echo "========================================"
echo ""

# Check API Health
echo "Checking API Health..."
API_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" "${API_URL}/api/health" 2>/dev/null || echo "000")
if [ "$API_RESPONSE" == "200" ]; then
    print_status "PASS" "API is responding (HTTP $API_RESPONSE)"
else
    print_status "FAIL" "API is not responding (HTTP $API_RESPONSE)"
fi

# Check API detailed health
if [ "$API_RESPONSE" == "200" ]; then
    HEALTH_JSON=$(curl -s "${API_URL}/api/health" 2>/dev/null)
    if echo "$HEALTH_JSON" | grep -q '"status":"healthy"'; then
        print_status "PASS" "API health check passed"
    else
        print_status "WARN" "API health check returned unexpected response"
    fi
fi

echo ""

# Check Database Connection
echo "Checking Database Connection..."
if command -v psql &> /dev/null; then
    PG_RESULT=$(PGPASSWORD="$POSTGRES_PASSWORD" psql -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT 1;" -t 2>/dev/null || echo "FAILED")
    if [ "$PG_RESULT" == "1" ]; then
        print_status "PASS" "PostgreSQL is accessible"
    else
        print_status "FAIL" "PostgreSQL connection failed"
    fi
else
    print_status "WARN" "psql not installed, skipping database check"
fi

# Check database size
if command -v psql &> /dev/null; then
    DB_SIZE=$(PGPASSWORD="$POSTGRES_PASSWORD" psql -h "$POSTGRES_HOST" -p "$POSTGRES_PORT" -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT pg_size_pretty(pg_database_size('$POSTGRES_DB'));" -t 2>/dev/null | xargs || echo "unknown")
    echo "  Database size: $DB_SIZE"
fi

echo ""

# Check Redis Connection
echo "Checking Redis Connection..."
if command -v redis-cli &> /dev/null; then
    REDIS_RESULT=$(redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" -a "$REDIS_PASSWORD" ping 2>/dev/null || echo "FAILED")
    if [ "$REDIS_RESULT" == "PONG" ]; then
        print_status "PASS" "Redis is responding"
    else
        print_status "FAIL" "Redis connection failed"
    fi
    
    # Check Redis memory usage
    if [ "$REDIS_RESULT" == "PONG" ]; then
        REDIS_INFO=$(redis-cli -h "$REDIS_HOST" -p "$REDIS_PORT" -a "$REDIS_PASSWORD" info memory 2>/dev/null | grep used_memory_human | cut -d: -f2 | tr -d '\r')
        echo "  Redis memory: $REDIS_INFO"
    fi
else
    print_status "WARN" "redis-cli not installed, skipping Redis check"
fi

echo ""

# Check Docker Containers
echo "Checking Docker Containers..."
if command -v docker &> /dev/null; then
    CONTAINERS=$(docker ps --format "{{.Names}}" 2>/dev/null || echo "")
    if [ -n "$CONTAINERS" ]; then
        echo "  Running containers:"
        while IFS= read -r container; do
            echo "    - $container"
        done <<< "$CONTAINERS"
        print_status "PASS" "Docker containers are running"
    else
        print_status "WARN" "No Docker containers running"
    fi
    
    # Check container health
    UNHEALTHY=$(docker ps --filter "health=unhealthy" --format "{{.Names}}" 2>/dev/null || echo "")
    if [ -n "$UNHEALTHY" ]; then
        print_status "FAIL" "Unhealthy containers: $UNHEALTHY"
    fi
else
    print_status "WARN" "Docker not installed, skipping container check"
fi

echo ""

# Check Disk Space
echo "Checking Disk Space..."
DISK_USAGE=$(df -h / | tail -1 | awk '{print $5}' | sed 's/%//')
if [ "$DISK_USAGE" -lt 80 ]; then
    print_status "PASS" "Disk usage is ${DISK_USAGE}%"
elif [ "$DISK_USAGE" -lt 90 ]; then
    print_status "WARN" "Disk usage is ${DISK_USAGE}%"
else
    print_status "FAIL" "Disk usage is critical at ${DISK_USAGE}%"
fi

# Check memory
echo ""
echo "Checking Memory..."
MEMORY_TOTAL=$(free -h | grep Mem | awk '{print $2}')
MEMORY_USED=$(free -h | grep Mem | awk '{print $3}')
MEMORY_PERCENT=$(free | grep Mem | awk '{print int($3/$2 * 100)}')
echo "  Memory: $MEMORY_USED / $MEMORY_TOTAL (${MEMORY_PERCENT}%)"

if [ "$MEMORY_PERCENT" -lt 80 ]; then
    print_status "PASS" "Memory usage is normal"
elif [ "$MEMORY_PERCENT" -lt 90 ]; then
    print_status "WARN" "Memory usage is high"
else
    print_status "FAIL" "Memory usage is critical"
fi

echo ""

# Check recent logs for errors
echo "Checking Recent Logs..."
if command -v docker &> /dev/null; then
    API_CONTAINER=$(docker ps --filter "name=api" --format "{{.Names}}" 2>/dev/null | head -1)
    if [ -n "$API_CONTAINER" ]; then
        ERROR_COUNT=$(docker logs "$API_CONTAINER" --since 1h 2>&1 | grep -i "error" | wc -l || echo "0")
        if [ "$ERROR_COUNT" -eq 0 ]; then
            print_status "PASS" "No errors in API logs (last hour)"
        elif [ "$ERROR_COUNT" -lt 10 ]; then
            print_status "WARN" "$ERROR_COUNT errors in API logs (last hour)"
        else
            print_status "FAIL" "High number of errors in API logs: $ERROR_COUNT"
        fi
    fi
fi

echo ""
echo "========================================"
echo "  Summary"
echo "========================================"
echo -e "  ${GREEN}Passed:${NC}   $PASSED"
echo -e "  ${YELLOW}Warnings:${NC} $WARNINGS"
echo -e "  ${RED}Failed:${NC}   $FAILED"
echo ""

if [ "$FAILED" -gt 0 ]; then
    echo -e "${RED}Health check FAILED${NC}"
    exit 1
elif [ "$WARNINGS" -gt 0 ]; then
    echo -e "${YELLOW}Health check completed with warnings${NC}"
    exit 0
else
    echo -e "${GREEN}Health check PASSED${NC}"
    exit 0
fi

#!/bin/bash
# Log Analysis Script
# Analyzes application logs for errors, warnings, and patterns

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
ENV_FILE="${PROJECT_ROOT}/deployment/staging/.env.staging"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
CONTAINER_NAME="${API_CONTAINER:-sportsplatform-api}"
TIME_RANGE="${TIME_RANGE:-1h}"
LOG_FILE=""
OUTPUT_FORMAT="text"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--container)
            CONTAINER_NAME="$2"
            shift 2
            ;;
        -t|--time)
            TIME_RANGE="$2"
            shift 2
            ;;
        -f|--file)
            LOG_FILE="$2"
            shift 2
            ;;
        -o|--output)
            OUTPUT_FORMAT="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -c, --container NAME   Container name to analyze (default: sportsplatform-api)"
            echo "  -t, --time RANGE       Time range (e.g., 1h, 30m, 24h) (default: 1h)"
            echo "  -f, --file FILE        Use local log file instead of container"
            echo "  -o, --output FORMAT    Output format: text, json (default: text)"
            echo "  -h, --help             Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Function to get logs
get_logs() {
    if [ -n "$LOG_FILE" ]; then
        cat "$LOG_FILE"
    else
        if command -v docker &> /dev/null; then
            docker logs "$CONTAINER_NAME" --since "$TIME_RANGE" 2>&1
        else
            echo "Error: Docker not available and no log file specified"
            exit 1
        fi
    fi
}

# Function to print section header
print_header() {
    echo ""
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}"
}

# Function to count and display pattern
analyze_pattern() {
    local pattern="$1"
    local description="$2"
    local count
    
    if [ -n "$LOG_FILE" ]; then
        count=$(grep -c "$pattern" "$LOG_FILE" 2>/dev/null || echo "0")
    else
        count=$(get_logs | grep -c "$pattern" 2>/dev/null || echo "0")
    fi
    
    echo -e "$description: ${YELLOW}$count${NC}"
    
    if [ "$count" -gt 0 ] && [ "$count" -lt 20 ]; then
        echo "  Recent occurrences:"
        if [ -n "$LOG_FILE" ]; then
            grep "$pattern" "$LOG_FILE" | tail -5
        else
            get_logs | grep "$pattern" | tail -5
        fi
    fi
}

echo -e "${BLUE}Log Analysis Report${NC}"
echo "Generated: $(date)"
echo "Container: $CONTAINER_NAME"
echo "Time Range: $TIME_RANGE"

# Get total log lines
TOTAL_LINES=$(get_logs | wc -l)
echo "Total Log Lines: $TOTAL_LINES"

print_header "Error Analysis"

# Count errors
ERROR_COUNT=$(get_logs | grep -ci "error" || echo "0")
echo -e "Total Errors: ${RED}$ERROR_COUNT${NC}"

# Count exceptions
EXCEPTION_COUNT=$(get_logs | grep -ci "exception" || echo "0")
echo -e "Total Exceptions: ${RED}$EXCEPTION_COUNT${NC}"

# Count critical
CRITICAL_COUNT=$(get_logs | grep -ci "critical" || echo "0")
echo -e "Critical Messages: ${RED}$CRITICAL_COUNT${NC}"

print_header "Warning Analysis"

# Count warnings
WARNING_COUNT=$(get_logs | grep -ci "warning" || echo "0")
echo -e "Total Warnings: ${YELLOW}$WARNING_COUNT${NC}"

# Count deprecations
DEPRECATION_COUNT=$(get_logs | grep -ci "deprecated" || echo "0")
echo -e "Deprecation Notices: ${YELLOW}$DEPRECATION_COUNT${NC}"

print_header "Performance Metrics"

# Count slow queries
analyze_pattern "slow query" "Slow Queries"

# Count timeouts
analyze_pattern "timeout" "Timeouts"

# Count connection issues
analyze_pattern "connection" "Connection Messages"

print_header "Security Events"

# Count authentication failures
analyze_pattern "authentication failed" "Auth Failures"

# Count unauthorized access
analyze_pattern "unauthorized" "Unauthorized Access"

# Count forbidden
analyze_pattern "forbidden" "Forbidden Access"

print_header "Application Events"

# Count API requests
REQUEST_COUNT=$(get_logs | grep -ci "request" || echo "0")
echo -e "API Requests: ${GREEN}$REQUEST_COUNT${NC}"

# Count responses
RESPONSE_COUNT=$(get_logs | grep -ci "response" || echo "0")
echo -e "API Responses: ${GREEN}$RESPONSE_COUNT${NC}"

# Count SignalR events
SIGNALR_COUNT=$(get_logs | grep -ci "signalr" || echo "0")
echo -e "SignalR Events: ${GREEN}$SIGNALR_COUNT${NC}"

print_header "Database Events"

# Count database operations
DB_COUNT=$(get_logs | grep -ci "database" || echo "0")
echo -e "Database Operations: ${GREEN}$DB_COUNT${NC}"

# Count migrations
MIGRATION_COUNT=$(get_logs | grep -ci "migration" || echo "0")
echo -e "Migration Events: ${GREEN}$MIGRATION_COUNT${NC}"

print_header "Top Error Messages"

if [ "$ERROR_COUNT" -gt 0 ]; then
    echo "Most frequent error messages:"
    if [ -n "$LOG_FILE" ]; then
        get_logs | grep -i "error" | sed 's/.*error.*/error/i' | sort | uniq -c | sort -rn | head -10
    else
        get_logs | grep -i "error" | sed 's/.*error.*/error/i' | sort | uniq -c | sort -rn | head -10
    fi
else
    echo "No errors found in the specified time range."
fi

print_header "Summary"

echo "Log analysis complete. Key findings:"
echo ""
echo -e "  ${RED}Errors:${NC}   $ERROR_COUNT"
echo -e "  ${YELLOW}Warnings:${NC} $WARNING_COUNT"
echo -e "  ${GREEN}Requests:${NC} $REQUEST_COUNT"
echo ""

if [ "$ERROR_COUNT" -gt 100 ]; then
    echo -e "${RED}⚠ High number of errors detected. Immediate attention required.${NC}"
elif [ "$ERROR_COUNT" -gt 50 ]; then
    echo -e "${YELLOW}⚠ Moderate number of errors. Monitor closely.${NC}"
elif [ "$ERROR_COUNT" -gt 0 ]; then
    echo -e "${YELLOW}⚠ Some errors detected. Review recommended.${NC}"
else
    echo -e "${GREEN}✓ No errors detected in the specified time range.${NC}"
fi
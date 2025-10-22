#!/bin/bash

# Script to run integration tests with DynamoDB Local
# Starts DynamoDB Local if not running, runs tests, and stops DynamoDB Local on completion

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
DYNAMODB_LOCAL_DIR="./dynamodb-local"
DYNAMODB_LOCAL_PORT=8000
DYNAMODB_LOCAL_PID_FILE=".dynamodb-local.pid"
TEST_PROJECT="Oproto.FluentDynamoDb.IntegrationTests"

# Flag to track if we started DynamoDB Local
STARTED_DYNAMODB=false

echo "========================================="
echo "Integration Tests Runner"
echo "========================================="
echo ""

# Function to check if DynamoDB Local is running
check_dynamodb_running() {
    if curl -s "http://localhost:$DYNAMODB_LOCAL_PORT" > /dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# Function to start DynamoDB Local
start_dynamodb() {
    echo "Starting DynamoDB Local..."
    
    # Check if DynamoDB Local is installed
    if [ ! -f "$DYNAMODB_LOCAL_DIR/DynamoDBLocal.jar" ]; then
        echo -e "${RED}ERROR: DynamoDB Local is not installed${NC}"
        echo ""
        echo "Run the setup script first:"
        echo "  ./scripts/setup-dynamodb-local.sh"
        echo ""
        exit 1
    fi
    
    # Start DynamoDB Local in background
    cd "$DYNAMODB_LOCAL_DIR"
    java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -inMemory -port $DYNAMODB_LOCAL_PORT > ../dynamodb-local.log 2>&1 &
    DYNAMODB_PID=$!
    cd ..
    
    # Save PID to file
    echo $DYNAMODB_PID > "$DYNAMODB_LOCAL_PID_FILE"
    
    # Wait for DynamoDB Local to be ready
    echo "Waiting for DynamoDB Local to be ready..."
    MAX_RETRIES=30
    RETRY_COUNT=0
    
    while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
        if check_dynamodb_running; then
            echo -e "${GREEN}✓ DynamoDB Local is ready (PID: $DYNAMODB_PID)${NC}"
            echo ""
            STARTED_DYNAMODB=true
            return 0
        fi
        
        sleep 1
        RETRY_COUNT=$((RETRY_COUNT + 1))
        echo -n "."
    done
    
    echo ""
    echo -e "${RED}ERROR: DynamoDB Local failed to start${NC}"
    echo "Check dynamodb-local.log for details"
    exit 1
}

# Function to stop DynamoDB Local
stop_dynamodb() {
    if [ "$STARTED_DYNAMODB" = true ] && [ -f "$DYNAMODB_LOCAL_PID_FILE" ]; then
        DYNAMODB_PID=$(cat "$DYNAMODB_LOCAL_PID_FILE")
        echo ""
        echo "Stopping DynamoDB Local (PID: $DYNAMODB_PID)..."
        
        if kill -0 $DYNAMODB_PID 2>/dev/null; then
            kill $DYNAMODB_PID
            
            # Wait for process to stop
            WAIT_COUNT=0
            while kill -0 $DYNAMODB_PID 2>/dev/null && [ $WAIT_COUNT -lt 10 ]; do
                sleep 1
                WAIT_COUNT=$((WAIT_COUNT + 1))
            done
            
            # Force kill if still running
            if kill -0 $DYNAMODB_PID 2>/dev/null; then
                echo "Force stopping DynamoDB Local..."
                kill -9 $DYNAMODB_PID 2>/dev/null || true
            fi
            
            echo -e "${GREEN}✓ DynamoDB Local stopped${NC}"
        fi
        
        rm -f "$DYNAMODB_LOCAL_PID_FILE"
    fi
}

# Trap to ensure cleanup on exit
trap stop_dynamodb EXIT INT TERM

# Check if DynamoDB Local is already running
if check_dynamodb_running; then
    echo -e "${YELLOW}DynamoDB Local is already running on port $DYNAMODB_LOCAL_PORT${NC}"
    echo "Using existing instance..."
    echo ""
else
    start_dynamodb
fi

# Run integration tests
echo "Running integration tests..."
echo "========================================="
echo ""

if dotnet test "$TEST_PROJECT" --verbosity normal --logger "console;verbosity=detailed"; then
    echo ""
    echo -e "${GREEN}=========================================${NC}"
    echo -e "${GREEN}✓ All integration tests passed!${NC}"
    echo -e "${GREEN}=========================================${NC}"
    TEST_EXIT_CODE=0
else
    echo ""
    echo -e "${RED}=========================================${NC}"
    echo -e "${RED}✗ Some integration tests failed${NC}"
    echo -e "${RED}=========================================${NC}"
    TEST_EXIT_CODE=1
fi

# Cleanup happens via trap
exit $TEST_EXIT_CODE

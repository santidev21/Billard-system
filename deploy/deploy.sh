#!/bin/bash
set -euo pipefail

DEPLOY_DIR="/opt/billard"
LOG_FILE="/tmp/billard-deploy.log"

log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

error_exit() {
    log "ERROR: $1"
    exit 1
}

validate_env() {
    log "Validating .env file..."
    if [ ! -f "$DEPLOY_DIR/.env" ]; then
        error_exit ".env file not found at $DEPLOY_DIR/.env. Copy .env.example to .env and configure production secrets."
    fi
    if grep -q "CHANGE_ME" "$DEPLOY_DIR/.env" 2>/dev/null; then
        error_exit ".env contains CHANGE_ME placeholder values. Configure real production secrets."
    fi
    log ".env validated."
}

validate_config() {
    log "Validating docker compose configuration..."
    cd "$DEPLOY_DIR"
    docker compose config --quiet || error_exit "docker compose config validation failed"
    log "Configuration valid."
}

validate_docker() {
    log "Checking Docker..."
    docker info >/dev/null 2>&1 || error_exit "Docker is not running or not accessible"
    log "Docker available."
}

pull() {
    log "Pulling latest code..."
    cd "$DEPLOY_DIR"
    git fetch origin master
    git reset --hard origin/master
    log "Code updated."
}

build() {
    log "Building containers..."
    cd "$DEPLOY_DIR"
    docker compose build
    log "Build complete."
}

up() {
    log "Starting containers..."
    cd "$DEPLOY_DIR"
    docker compose up -d --force-recreate --remove-orphans
    log "Containers started."
}

wait_healthy() {
    log "Waiting for services to become healthy..."
    local TIMEOUT=120
    local INTERVAL=5
    local ELAPSED=0
    while [ $ELAPSED -lt $TIMEOUT ]; do
        # db has healthcheck
        local DB_HEALTH
        DB_HEALTH=$(docker inspect --format='{{.State.Health.Status}}' billard-system-db-1 2>/dev/null || docker inspect --format='{{.State.Health.Status}}' billard-db-1 2>/dev/null || echo "not_found")
        if [ "$DB_HEALTH" = "healthy" ]; then
            log "Database healthy."
            break
        fi
        log "Waiting for db... ($ELAPSED/$TIMEOUT) status: $DB_HEALTH"
        sleep $INTERVAL
        ELAPSED=$((ELAPSED + INTERVAL))
    done
    if [ $ELAPSED -ge $TIMEOUT ]; then
        log "WARNING: Timeout waiting for db health"
        docker compose ps
        docker compose logs --tail=50
        error_exit "Health check timeout"
    fi
    # Verify API responds
    log "Checking API health endpoint..."
    local RETRIES=12
    for i in $(seq 1 $RETRIES); do
        if curl -sf http://localhost:5000/api/health >/dev/null 2>&1; then
            log "API healthy."
            return 0
        fi
        sleep 5
    done
    log "WARNING: API health check failed"
    docker compose logs --tail=50 billard
    error_exit "API not healthy"
}

verify() {
    log "Verifying deployment..."
    local BILLARD_STATUS
    BILLARD_STATUS=$(docker inspect --format='{{.State.Status}}' billard 2>/dev/null || echo "not_found")
    if [ "$BILLARD_STATUS" != "running" ]; then
        error_exit "billard container not running (status: $BILLARD_STATUS)"
    fi
    log "Deployment verified."
}

status() {
    cd "$DEPLOY_DIR"
    docker compose ps
    echo ""
    docker compose logs --tail=20
}

logs() {
    cd "$DEPLOY_DIR"
    docker compose logs --tail=100
}

deploy() {
    log "=== Starting full deployment ==="
    validate_docker
    validate_env
    validate_config
    pull
    build
    up
    wait_healthy
    verify
    log "=== Deployment successful ==="
    status
}

case "${1:-deploy}" in
    pull) pull ;;
    build) build ;;
    up) up ;;
    deploy) deploy ;;
    status) status ;;
    logs) logs ;;
    verify) verify ;;
    *)
        echo "Usage: $0 [pull|build|up|deploy|status|logs|verify]"
        exit 1
        ;;
esac

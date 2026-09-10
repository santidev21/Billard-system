#!/bin/bash
set -Eeuo pipefail

# Billard Production Deployment Script
# Usage: ./deploy.sh [pull|build|up|deploy|status|rollback|logs|verify]

DEPLOY_DIR="/opt/billard"
BACKUP_DIR="/tmp/billard-backup-$(date +%Y%m%d-%H%M%S)"
LOG_FILE="/tmp/billard-deploy.log"
MAX_BACKUPS=5

trap 'log "ERROR: command failed at line $LINENO"' ERR

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
    if grep -qE "change-me|CHANGE-ME" "$DEPLOY_DIR/.env" 2>/dev/null; then
        error_exit ".env contains placeholder values. Configure real production secrets."
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

backup() {
    if [ -d "$DEPLOY_DIR" ]; then
        log "Creating backup at $BACKUP_DIR..."
        cp -r "$DEPLOY_DIR" "$BACKUP_DIR"
        chmod 700 "$BACKUP_DIR"
        # Rotate old backups via glob array (never parse ls; safe with odd filenames).
        # Lexical order == chronological order for YYYYMMDD-HHMMSS names.
        shopt -s nullglob
        local backups=(/tmp/billard-backup-*)
        shopt -u nullglob
        if [[ "${#backups[@]}" -gt "$MAX_BACKUPS" ]]; then
            log "Rotating backups (keeping last $MAX_BACKUPS)..."
            local to_delete=$(( ${#backups[@]} - MAX_BACKUPS ))
            for (( i = 0; i < to_delete; i++ )); do
                rm -rf -- "${backups[$i]}" || true
            done
        fi
        log "Backup created with permissions 700."
    fi
}

backup_database() {
    log "Attempting database backup before deployment..."
    local DB_CONTAINER=""
    for c in billard-db-1 billard-system-db-1; do
        if docker inspect "$c" >/dev/null 2>&1; then
            DB_CONTAINER="$c"
            break
        fi
    done
    if [ -z "$DB_CONTAINER" ]; then
        log "WARNING: DB container not found. Skipping database backup."
        return 0
    fi

    # Tolerates optional "export" prefix, surrounding whitespace and quotes.
    local env_line
    env_line=$(grep -E "^[[:space:]]*(export[[:space:]]+)?POSTGRES_PASSWORD=" "$DEPLOY_DIR/.env" 2>/dev/null | tail -n 1 || true)
    local PG_PASSWORD="${env_line#*=}"
    PG_PASSWORD="${PG_PASSWORD#"${PG_PASSWORD%%[![:space:]]*}"}"
    PG_PASSWORD="${PG_PASSWORD%"${PG_PASSWORD##*[![:space:]]}"}"
    PG_PASSWORD="${PG_PASSWORD%\"}"; PG_PASSWORD="${PG_PASSWORD#\"}"
    PG_PASSWORD="${PG_PASSWORD%\'}"; PG_PASSWORD="${PG_PASSWORD#\'}"
    if [ -z "$PG_PASSWORD" ]; then
        log "WARNING: POSTGRES_PASSWORD not found in .env. Skipping database backup."
        return 0
    fi

    local BACKUP_FILE="/tmp/billard-db-$(date +%Y%m%d-%H%M%S).sql"
    if PGPASSWORD="$PG_PASSWORD" docker exec "$DB_CONTAINER" pg_dump -U postgres -d billiard > "$BACKUP_FILE" 2>/dev/null; then
        log "Database backup created: $BACKUP_FILE"
    else
        log "WARNING: Database backup failed. Continuing deployment."
        rm -f "$BACKUP_FILE" 2>/dev/null || true
    fi
}

pull() {
    log "Pulling latest code..."
    cd "$DEPLOY_DIR"
    git fetch origin main
    git reset --hard origin/main
    log "Code updated."
}

build() {
    log "Building containers..."
    cd "$DEPLOY_DIR"
    docker compose build --no-cache
    log "Build complete."
}

up() {
    log "Starting containers..."
    cd "$DEPLOY_DIR"
    docker compose up -d --remove-orphans
    log "Containers started."
}

wait_healthy() {
    log "Waiting for services to become healthy..."
    local TIMEOUT=120
    local INTERVAL=5
    local ELAPSED=0

    while [[ $ELAPSED -lt $TIMEOUT ]]; do
        local DB_OK=0
        local APP_OK=0

        local DB_CONTAINER=""
        for c in billard-db-1 billard-system-db-1; do
            if docker inspect "$c" >/dev/null 2>&1; then
                DB_CONTAINER="$c"
                break
            fi
        done

        if [ -n "$DB_CONTAINER" ] && [ "$(docker inspect --format='{{.State.Health.Status}}' "$DB_CONTAINER" 2>/dev/null | tr -d '[:space:]')" = "healthy" ]; then
            DB_OK=1
        fi
        if [ "$(docker inspect --format='{{.State.Health.Status}}' billard 2>/dev/null | tr -d '[:space:]')" = "healthy" ]; then
            APP_OK=1
        fi

        if [ "$DB_OK" = "1" ] && [ "$APP_OK" = "1" ]; then
            log "All services healthy."
            return 0
        fi

        log "Waiting... ($ELAPSED/$TIMEOUT seconds) db=$DB_OK app=$APP_OK"
        sleep "$INTERVAL"
        ELAPSED=$((ELAPSED + INTERVAL))
    done

    log "WARNING: Timeout waiting for health checks"
    docker compose ps
    docker compose logs --tail=50
    error_exit "Health check timeout"
}

verify() {
    log "Verifying deployment..."
    local APP_STATUS
    APP_STATUS=$(docker inspect --format='{{.State.Health.Status}}' billard 2>/dev/null || echo "not_found")
    if [ "$APP_STATUS" != "healthy" ]; then
        error_exit "billard is not healthy (status: $APP_STATUS)"
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

rollback() {
    log "=== Starting rollback ==="
    # Newest backup = last in lexical order for timestamped names.
    shopt -s nullglob
    local backups=(/tmp/billard-backup-*)
    shopt -u nullglob
    if [[ "${#backups[@]}" -eq 0 ]]; then
        error_exit "No backup found for rollback"
    fi
    LATEST_BACKUP="${backups[${#backups[@]}-1]}"
    log "Rolling back to $LATEST_BACKUP..."

    cd "$DEPLOY_DIR"
    docker compose down || true

    if [ -d "${DEPLOY_DIR}.broken" ]; then
        rm -rf "${DEPLOY_DIR}.broken"
    fi
    mv "$DEPLOY_DIR" "${DEPLOY_DIR}.broken"
    cp -r "$LATEST_BACKUP" "$DEPLOY_DIR"
    chmod 700 "$DEPLOY_DIR"

    cd "$DEPLOY_DIR"
    if ! docker compose up -d --remove-orphans; then
        log "CRITICAL: Rollback containers failed. Attempting to restore from .broken..."
        rm -rf "$DEPLOY_DIR"
        mv "${DEPLOY_DIR}.broken" "$DEPLOY_DIR"
        cd "$DEPLOY_DIR"
        docker compose up -d --remove-orphans || error_exit "ROLLBACK FAILED: both new and old deployments are down"
        error_exit "Rollback failed, but previous deployment restored"
    fi

    if wait_healthy; then
        log "=== Rollback successful ==="
        rm -rf "${DEPLOY_DIR}.broken"
    else
        error_exit "Rollback verification failed"
    fi
}

deploy() {
    log "=== Starting full deployment ==="
    validate_docker
    validate_env
    validate_config
    backup
    backup_database
    pull
    build
    up

    if ! wait_healthy; then
        log "Deployment health check failed. Attempting automatic rollback..."
        if rollback; then
            error_exit "Deployment failed and automatic rollback succeeded. Previous deployment restored."
        else
            error_exit "CRITICAL: Deployment failed AND automatic rollback failed. Manual intervention required."
        fi
    fi

    verify
    log "=== Deployment successful ==="
    status
}

usage() {
    cat <<EOF
Usage: $0 [COMMAND]

Commands:
    pull       Fetch latest code (git fetch + reset --hard origin/main)
    build      Build containers (docker compose build --no-cache)
    up         Start containers (docker compose up -d --remove-orphans)
    deploy     Full deployment: validate, backup, pull, build, up, verify (default)
    status     Show container status and recent logs
    rollback   Restore the newest /tmp/billard-backup-* snapshot
    logs       Tail container logs
    verify     Check that the billard container is healthy
    help       Show this help message
EOF
    exit "${1:-0}"
}

case "${1:-deploy}" in
    pull) pull ;;
    build) build ;;
    up) up ;;
    deploy) deploy ;;
    status) status ;;
    rollback) rollback ;;
    logs) logs ;;
    verify) verify ;;
    help|-h|--help) usage 0 ;;
    *)
        echo "ERROR: Unknown command: $1" >&2
        usage 1
        ;;
esac
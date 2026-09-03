# Deploy — billard.santidev21.tech

## Arquitectura

El tráfico entra por **`vps-gateway`** (nginx en 80/443, repo privado) → red `billard-net` → contenedor `billard`. La DB Postgres está aislada en `billard-internal-net` (`internal: true`). **No se publican puertos al host.**

```
Internet → vps-gateway → billard-net → billard (app) ── billard-internal-net ── db
```

## One-time VPS setup (ya hecho)

1. Red compartida `billard-net` (external, dueña = vps-gateway). Creada por `docker compose up` del gateway (o `docker network create billard-net`).
2. Certificado: emitido por el contenedor `certbot` del gateway (`certbot certonly --webroot`).
3. Site config: `billard.santidev21.tech.conf` versionada en el repo `vps-gateway` → `sites-enabled/`.
4. Repo clonado en `/opt/billard` (rama `main`).

## Deploys

Push a `main` → CI (backend + frontend) → SSH → `./deploy/deploy.sh deploy`.

Manual:
```bash
cd /opt/billard
./deploy/deploy.sh deploy
```

## First login

1. Go to `billard.santidev21.tech/#/login`
2. Enter default password: `admin`
3. Change it (min 8 characters) — all other sessions are revoked

## Repo secrets para CI/CD

| Secret | Valor |
|--------|-------|
| `VPS_HOST` | IP del VPS |
| `VPS_USER` | `santidev21` |
| `VPS_SSH_PRIVATE_KEY` | Llave SSH privada (misma en todos los repos) |

## Rollback

```bash
cd /opt/billard
./deploy/deploy.sh rollback
```
# Deploy — billard.santidev21.tech

## One-time VPS setup

```bash
# 1. Clone the repo
cd /opt
sudo git clone git@github.com:santidev21/Billard-system.git billard
cd billard

# 2. Build and start
sudo docker compose up -d billard

# 3. Connect gateway to the billard network
sudo docker network connect billard-net gateway

# 4. Get SSL certificate (Let's Encrypt via gateway webroot)
sudo certbot certonly \
  --config-dir /opt/gateway/certbot \
  --work-dir /opt/gateway/work \
  --logs-dir /opt/gateway/logs \
  --webroot -w /opt/gateway/www \
  -d billard.santidev21.tech \
  --email orsantiago21@gmail.com \
  --agree-tos --no-eff-email --keep-until-expiring

# 5. Copy nginx site config and enable
sudo cp /opt/billard/deploy/nginx-billard.conf \
  /opt/gateway/sites-available/billard.santidev21.tech.conf
sudo cp /opt/gateway/sites-available/billard.santidev21.tech.conf \
  /opt/gateway/sites-enabled/

# 6. Validate and reload
docker exec gateway nginx -t
docker exec gateway nginx -s reload

# 7. Verify (and that existing sites still work)
curl -I https://billard.santidev21.tech/
curl -I https://splitit.santidev21.tech/
curl -I https://santidev21.tech/
```

## Subsequent deploys

Push to `master` → CI builds image → pushes to GHCR → CD SSHs to VPS → pulls and restarts.

## Manual deploy

```bash
cd /opt/billard
sudo docker compose pull billard
sudo docker compose up -d billard
```

## First login

1. Go to `billard.santidev21.tech/#/login`
2. Enter default password: `admin`
3. Change it (min 8 characters) — all other sessions are revoked

## Repo secrets for CI/CD

| Secret | Value |
|--------|-------|
| `VPS_HOST` | Your VPS IP |
| `VPS_USER` | `santidev21` |
| `VPS_SSH_KEY` | Private SSH key |

## Rollback

```bash
# Disable the site
sudo rm /opt/gateway/sites-enabled/billard.santidev21.tech.conf
docker exec gateway nginx -s reload
```

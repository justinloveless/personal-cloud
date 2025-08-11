FROM caddy:2-builder AS builder

# Build Caddy with the Hetzner DNS plugin
RUN xcaddy build \
    --with github.com/caddy-dns/hetzner

FROM caddy:2

# Copy the custom-built Caddy binary that includes the Hetzner DNS plugin
COPY --from=builder /usr/bin/caddy /usr/bin/caddy

# Default Caddyfile location (will be overridden by volume mount in compose)
COPY Caddyfile /etc/caddy/Caddyfile



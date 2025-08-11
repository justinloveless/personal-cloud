create extension if not exists pgcrypto;
create table if not exists tenants (
  id uuid primary key default gen_random_uuid(),
  subdomain text unique not null,
  db_name text not null,
  plan text not null default 'business',
  region text not null default 'eu-central',
  created_at timestamptz not null default now()
);

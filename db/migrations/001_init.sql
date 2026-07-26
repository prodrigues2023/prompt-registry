-- Prompt Registry — initial schema.
--
-- Design contract: prompt_versions is append-only. A row is never UPDATEd or DELETEd.
-- Immutability is the whole point — it is what makes a rollback a pointer move instead of
-- a redeploy, and what lets a test result stay attached to the exact bytes that were tested.

create table if not exists prompt_versions (
    id            bigserial   primary key,
    name          text        not null,
    version       int         not null,
    template      text        not null,
    variables     jsonb       not null default '[]'::jsonb,
    metadata      jsonb       not null default '{}'::jsonb,
    content_hash  text        not null,
    gate_status   text        not null default 'untested',  -- untested | passed | failed
    test_results  jsonb,
    created_at    timestamptz not null default now(),
    unique (name, version)
);

create index if not exists idx_prompt_versions_name on prompt_versions (name);

-- aliases is the ONLY mutable table: an environment pointer at a version.
-- Promotion and rollback are both just an UPDATE of the version this row points to.
create table if not exists aliases (
    name         text        not null,
    environment  text        not null,
    version      int         not null,
    updated_at   timestamptz not null default now(),
    primary key (name, environment),
    foreign key (name, version) references prompt_versions (name, version)
);

-- Every alias move is recorded, so a rollback knows where "back" is and a drill can
-- measure propagation.
create table if not exists alias_history (
    id           bigserial   primary key,
    name         text        not null,
    environment  text        not null,
    from_version int,
    to_version   int         not null,
    action       text        not null,  -- promote | rollback
    changed_at   timestamptz not null default now()
);

create index if not exists idx_alias_history_lookup
    on alias_history (name, environment, changed_at desc);

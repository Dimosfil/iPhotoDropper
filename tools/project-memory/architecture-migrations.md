# Architecture Migrations

Use this file for major architecture rewrites, platform moves, framework
replacements, storage changes, service splits, routing changes, or other
architecture-level migrations that future agents must preserve or understand.

For each migration, record:

- date and migration name;
- product or technical reason;
- behavior that must remain true;
- old architecture and new architecture;
- implementation map with evidence paths;
- verification checks and rollback notes.

Do not store secrets, credentials, private production data, logs, or generated
artifacts here.

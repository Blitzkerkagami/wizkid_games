# SQLite for WizKid Android

This folder vendors the official SQLite 3.54.0 amalgamation for the Android
IL2CPP build. Unity compiles `sqlite3.c` into the Android player, and
`LocalDatabase` calls the exported SQLite C API through `__Internal`.

Source: https://www.sqlite.org/2026/sqlite-amalgamation-3540000.zip

Verified source metadata from `sqlite3.h`:

- `SQLITE_VERSION`: `3.54.0`
- `SQLITE_SOURCE_ID`: `2026-10-09 15:46:58 be8d059e9a49089ab2dce5ed26dd87aaf598fdc5fbe0b107c0dd758464bcd5e3`
- Official published SHA3-256 for the archive:
  `7b670a62fdfbd672b75fef004cb703c8a3e87d3a5cc7d675b4a08337004a2d93`

SQLite is in the public domain. See https://www.sqlite.org/copyright.html.

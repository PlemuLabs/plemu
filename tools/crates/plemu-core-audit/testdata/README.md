# plemu-core-audit test fixtures

`expected-licenses.lock` and `cores.build.json` here are **verbatim synced copies**
of the files shipped in the `retrohub-cores` repo (`cores/expected-licenses.lock`
and `cores/cores.build.json`). They exist as a drift-detection guard:
`tests/real_files_test.rs` runs the audit against them and asserts it parses
successfully and fails only on the intended TBD/unpinned business logic — proving
the audit tool's schema and the cores repo's shipped data still agree. If the cores
repo changes those files' shape, re-sync these copies and confirm the audit still
parses them.

# Ambiquality Wiki (mdBook)

Markdown source for the operator/developer wiki, built with
[mdBook](https://rust-lang.github.io/mdBook/) and published to
**<https://wiki.ambiquality.org>** (GitHub Pages, custom domain) by
[`.github/workflows/wiki.yml`](../../.github/workflows/wiki.yml) on every push to `main`
that touches `docs/wiki/**`.

## Edit

Pages live in [`src/`](src/); the table of contents is [`src/SUMMARY.md`](src/SUMMARY.md).
Add a page by creating the markdown file and linking it from `SUMMARY.md`.

## Build locally

```bash
# one-off: install mdbook (prebuilt binary or `cargo install mdbook`)
mdbook serve docs/wiki   # live-reload preview at http://localhost:3000
mdbook build docs/wiki   # static site into docs/wiki/book/ (git-ignored)
```

The API contracts themselves are **not** duplicated here — they are the live Scalar
references generated from the running services (see
[`src/api-reference.md`](src/api-reference.md)). The wiki narrates the process and links
to them.

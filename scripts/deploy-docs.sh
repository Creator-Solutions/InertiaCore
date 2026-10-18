#!/usr/bin/env bash
#
# Build the VitePress documentation site and publish it to a remote host.
#
# Usage:
#   DOCS_DEPLOY_TARGET=user@host:/var/www/inertiacore ./scripts/deploy-docs.sh
#
# The target is an rsync destination. Set it via the environment so no host or
# credentials are committed. Extra flags can be passed to rsync through
# DOCS_DEPLOY_RSYNC_ARGS.
#
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
docs_dir="$repo_root/docs"
dist_dir="$docs_dir/.vitepress/dist"

if [[ -z "${DOCS_DEPLOY_TARGET:-}" ]]; then
  echo "error: DOCS_DEPLOY_TARGET is not set." >&2
  echo "       example: DOCS_DEPLOY_TARGET=user@host:/var/www/inertiacore" >&2
  exit 1
fi

if ! command -v rsync >/dev/null 2>&1; then
  echo "error: rsync is required but was not found on PATH." >&2
  exit 1
fi

echo "==> Installing docs dependencies"
(cd "$docs_dir" && npm ci)

echo "==> Building the documentation site"
(cd "$docs_dir" && npm run build)

if [[ ! -d "$dist_dir" ]]; then
  echo "error: build output not found at $dist_dir" >&2
  exit 1
fi

echo "==> Publishing $dist_dir to $DOCS_DEPLOY_TARGET"
# shellcheck disable=SC2086
rsync -az --delete \
  ${DOCS_DEPLOY_RSYNC_ARGS:-} \
  "$dist_dir/" \
  "$DOCS_DEPLOY_TARGET"

echo "==> Done"

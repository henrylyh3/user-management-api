#!/usr/bin/env bash
set -euo pipefail

# Usage: cleanup-releases.sh [--dry-run] [absolute-release-directory]
dry_run=false
if [[ ${1:-} == --dry-run ]]; then
    dry_run=true
    shift
fi
(( $# <= 1 )) || { echo "Usage: $0 [--dry-run] [absolute-release-directory]" >&2; exit 1; }
release_dir=${1:-/henry/release}
[[ "$release_dir" == /* && "$release_dir" != / && -d "$release_dir" && ! -L "$release_dir" ]] || {
    echo "Release directory must be an existing absolute directory, not a symlink or /." >&2
    exit 1
}

# Cooperate with API deployment and avoid overlapping cleanup runs.
root=$(dirname -- "$release_dir")
exec 9>"$root/.user-management-api.deploy.lock"
if ! flock -n 9; then
    echo "$(date '+%Y-%m-%dT%H:%M:%S%z'): API deployment is active; skipping cleanup."
    exit 0
fi
exec 8>"$release_dir/.cleanup.lock"
flock -n 8 || exit 0

python3 - "$release_dir" "$dry_run" <<'PYTHON'
import datetime
import os
import re
import stat
import sys
import time

root, dry_run = sys.argv[1], sys.argv[2] == 'true'
cutoff = time.time() - 7 * 24 * 60 * 60
pattern = re.compile(r'user-management-(?:api|web)-.+\.tar\.gz(?:\.sha256)?')
count = 0
with os.scandir(root) as entries:
    for entry in entries:
        if not pattern.fullmatch(entry.name):
            continue
        info = entry.stat(follow_symlinks=False)
        if not stat.S_ISREG(info.st_mode) or info.st_mtime >= cutoff:
            continue
        if not dry_run:
            os.unlink(entry.path)
        print(('Would delete: ' if dry_run else 'Deleted: ') + entry.name)
        count += 1
now = datetime.datetime.now().astimezone().isoformat(timespec='seconds')
print(f'{now}: {count} release file(s) {"eligible for deletion" if dry_run else "deleted"}; retention 7 days.')
PYTHON

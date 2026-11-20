"""Resolve one successful branch build to an immutable run and artifact ID."""
import hashlib
import json
import os
import re
import subprocess
from urllib.parse import urlencode


def package_name(branch, run):
    slug = re.sub(r'[^A-Za-z0-9_-]', '-', branch)[:40]
    digest = hashlib.sha256(branch.encode()).hexdigest()[:12]
    return f"user-management-api-{slug}-{digest}-{run['head_sha']}-{run['id']}-{run['run_attempt']}"


def select_run(branch, repository, runs):
    candidates = [run for run in runs if
                  run.get('head_branch') == branch
                  and (run.get('head_repository') or {}).get('full_name') == repository
                  and run.get('event') in ('push', 'workflow_dispatch')
                  and run.get('status') == 'completed'
                  and run.get('conclusion') == 'success']
    if not candidates:
        raise ValueError('No successful CI publish run exists for this branch.')
    # Run number orders commits/builds, even when an older run is rerun later.
    return max(candidates, key=lambda run: run['run_number'])


def select_artifact(branch, run, artifacts):
    expected = package_name(branch, run)
    matches = [artifact for artifact in artifacts
               if artifact.get('name') == expected and not artifact.get('expired', True)]
    if len(matches) != 1:
        raise ValueError('The latest successful build has no available release artifact. Run CI again; no older build was substituted.')
    return matches[0]


def api_pages(path, field):
    result = subprocess.check_output(['gh', 'api', '--paginate', '--slurp', path], text=True)
    return [item for page in json.loads(result) for item in page[field]]


def main():
    branch = os.environ['DEPLOY_BRANCH']
    subprocess.run(['git', 'check-ref-format', f'refs/heads/{branch}'], check=True, capture_output=True)
    repository = os.environ['GITHUB_REPOSITORY']
    base = f'repos/{repository}/actions'
    query = urlencode({'branch': branch, 'status': 'success', 'per_page': 100})
    runs = api_pages(f'{base}/workflows/publish.yml/runs?{query}', 'workflow_runs')
    run = select_run(branch, repository, runs)
    artifacts = api_pages(f"{base}/runs/{run['id']}/artifacts?per_page=100", 'artifacts')
    artifact = select_artifact(branch, run, artifacts)
    outputs = {'run-id': run['id'], 'artifact-id': artifact['id'], 'package-name': artifact['name'], 'commit': run['head_sha']}
    with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
        for key, value in outputs.items():
            output.write(f'{key}={value}\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as summary:
        summary.write(f"### Selected API release\nBranch: `{branch}`\n\nCommit: `{run['head_sha']}`\n\nCI run: {run['html_url']}\n\nArtifact: `{artifact['name']}`\n")
    print(f"Selected run {run['id']}, commit {run['head_sha']}, artifact {artifact['id']}")


if __name__ == '__main__':
    main()

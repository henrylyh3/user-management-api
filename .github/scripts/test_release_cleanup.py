import fcntl
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import unittest

SCRIPT = Path(__file__).resolve().parents[2] / 'deployment/cleanup-releases.sh'


class ReleaseCleanupTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='release-cleanup-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.release = self.root / 'release'
        self.release.mkdir()
        self.env = os.environ.copy()
        if not shutil.which('flock'):
            # macOS test helper: same advisory-lock operation as Linux flock.
            tools = self.root / 'bin'
            tools.mkdir()
            helper = tools / 'flock'
            helper.write_text('#!/usr/bin/env python3\nimport fcntl,sys\ntry: fcntl.flock(int(sys.argv[-1]), fcntl.LOCK_EX | fcntl.LOCK_NB)\nexcept BlockingIOError: sys.exit(1)\n')
            helper.chmod(0o755)
            self.env['PATH'] = str(tools) + ':' + self.env['PATH']

    def file(self, name, days):
        path = self.release / name
        path.write_text('test release')
        stamp = time.time() - days * 86400
        os.utime(path, (stamp, stamp))
        return path

    def run_script(self, *options):
        return subprocess.run(['bash', str(SCRIPT), *options, str(self.release)], env=self.env, capture_output=True, text=True, check=True)

    def test_deletes_only_old_release_files(self):
        old = [self.file('user-management-api-old.tar.gz', 7.1), self.file('user-management-api-old.tar.gz.sha256', 7.1), self.file('user-management-web-old.tar.gz', 8)]
        retained = [self.file('user-management-api-new.tar.gz', 6.99), self.file('unrelated.tar.gz', 30), self.file('notes.txt', 30)]
        staging = self.release / '.upload-test'
        staging.mkdir()
        nested = staging / 'user-management-api-old.tar.gz'
        nested.write_text('staging')
        link = self.release / 'user-management-api-link.tar.gz'
        link.symlink_to(retained[0])
        self.run_script()
        self.assertTrue(all(not path.exists() for path in old))
        self.assertTrue(all(path.exists() for path in retained))
        self.assertTrue(nested.exists())
        self.assertTrue(link.is_symlink())

    def test_dry_run_deletes_nothing(self):
        old = self.file('user-management-api-old.tar.gz', 8)
        result = self.run_script('--dry-run')
        self.assertIn('Would delete:', result.stdout)
        self.assertTrue(old.exists())

    def test_active_api_deployment_skips_cleanup(self):
        old = self.file('user-management-api-old.tar.gz', 8)
        with (self.root / '.user-management-api.deploy.lock').open('w') as lock:
            fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            result = self.run_script()
        self.assertIn('skipping cleanup', result.stdout)
        self.assertTrue(old.exists())


if __name__ == '__main__':
    unittest.main()

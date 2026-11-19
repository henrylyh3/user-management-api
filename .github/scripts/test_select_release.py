import unittest
from select_release import package_name, select_artifact, select_run


class ReleaseSelectionTests(unittest.TestCase):
    def run_fixture(self, number=2, **changes):
        return dict(id=number, run_number=number, run_attempt=1, head_sha='a' * 40,
                    head_branch='feature/login', head_repository={'full_name': 'owner/api'},
                    event='push', status='completed', conclusion='success', **changes)

    def test_latest_successful_branch_run_only(self):
        old = self.run_fixture(1)
        latest = self.run_fixture(2)
        failed = self.run_fixture(3) | {'conclusion': 'failure'}
        running = self.run_fixture(4) | {'status': 'in_progress'}
        other = self.run_fixture(5) | {'head_branch': 'main'}
        fork = self.run_fixture(6) | {'head_repository': {'full_name': 'fork/api'}}
        pr = self.run_fixture(7) | {'event': 'pull_request'}
        self.assertEqual(select_run('feature/login', 'owner/api', [fork, pr, other, running, failed, old, latest]), latest)

    def test_rerunning_old_build_does_not_make_it_latest(self):
        old = self.run_fixture(1) | {'run_attempt': 9}
        new = self.run_fixture(2)
        self.assertEqual(select_run('feature/login', 'owner/api', [old, new]), new)

    def test_no_success_fails(self):
        with self.assertRaises(ValueError):
            select_run('missing', 'owner/api', [self.run_fixture()])

    def test_branch_slug_collisions_are_distinct(self):
        run = self.run_fixture()
        names = [package_name(branch, run) for branch in ['feature/login', 'feature-login', 'Feature/login']]
        self.assertEqual(len(set(names)), 3)
        self.assertTrue(all('/' not in name for name in names))

    def test_exact_artifact_selected(self):
        run = self.run_fixture()
        expected = {'id': 20, 'name': package_name('feature/login', run), 'expired': False}
        results = [{'id': 19, 'name': 'test-results', 'expired': False}, expected]
        self.assertEqual(select_artifact('feature/login', run, results), expected)

    def test_missing_expired_or_wrong_attempt_artifact_fails(self):
        run = self.run_fixture()
        for artifacts in [[], [{'name': package_name('feature/login', run), 'expired': True}],
                          [{'name': package_name('feature/login', run | {'run_attempt': 2}), 'expired': False}]]:
            with self.subTest(artifacts=artifacts), self.assertRaises(ValueError):
                select_artifact('feature/login', run, artifacts)


if __name__ == '__main__':
    unittest.main()

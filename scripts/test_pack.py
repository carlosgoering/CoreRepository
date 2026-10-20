"""Checks the pack approval rules without starting databases."""
import contextlib
import io
import tempfile
from pathlib import Path
import unittest
from unittest.mock import patch
import pack


class PackTests(unittest.TestCase):
    def test_only_completed_nonempty_fully_passing_results_are_approved(self):
        with tempfile.TemporaryDirectory() as folder:
            directory = Path(folder)
            self.assertFalse(pack.passed_results(directory))
            for outcome, total, passed, expected in [
                ("Completed", 8, 8, True), ("Completed", 0, 0, False),
                ("Failed", 8, 7, False), ("Completed", 8, 7, False),
                ("Aborted", 8, 8, False),
            ]:
                (directory / "results.trx").write_text(
                    f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                    f'<ResultSummary outcome="{outcome}"><Counters total="{total}" passed="{passed}" />'
                    '</ResultSummary></TestRun>')
                self.assertEqual(pack.passed_results(directory), expected)
            (directory / "results.trx").write_text("invalid xml")
            self.assertFalse(pack.passed_results(directory))

    def exercise_pipeline(self, core=True, fail_pack=False, mysql=False):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder)
            calls = []
            def fake_run(command, log):
                calls.append(command)
                if fail_pack:
                    return 1
                staging = Path(command[command.index("-o") + 1])
                (staging / (Path(command[2]).stem + ".1.0.0.nupkg")).write_text("package")
                return 0
            with patch.object(pack, "test_suite", side_effect=lambda name, *_: core and (name != "MySQL" or mysql)), \
                 patch.object(pack, "run", side_effect=fake_run):
                with contextlib.redirect_stdout(io.StringIO()):
                    status = pack.build_packages("Release", output)
            import json
            return status, json.loads((output / "report.json").read_text()), calls

    def test_all_approved_packages_return_success(self):
        status, report, calls = self.exercise_pipeline(mysql=True)
        self.assertEqual(status, 0)
        self.assertEqual(len(calls), 8)
        self.assertTrue(all(x["status"] == "generated" for x in report))

    def test_failed_suite_stays_pending_while_other_packages_are_generated(self):
        status, report, calls = self.exercise_pipeline()
        self.assertEqual(status, 1)
        self.assertEqual(len(calls), 7)
        self.assertEqual([x["package"] for x in report if x["status"] == "pending"], ["DataAccess.MySQL"])

    def test_failed_core_blocks_all_dependent_packages(self):
        status, report, calls = self.exercise_pipeline(core=False)
        self.assertEqual(status, 1)
        self.assertFalse(calls)
        self.assertTrue(all(x["status"] == "pending" for x in report))

    def test_pack_failures_are_reported_as_pending(self):
        status, report, calls = self.exercise_pipeline(fail_pack=True)
        self.assertEqual(status, 1)
        self.assertEqual(len(calls), 7)
        self.assertTrue(all(x["status"] == "pending" for x in report))


if __name__ == "__main__":
    unittest.main()

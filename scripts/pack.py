#!/usr/bin/env python3
"""Test and pack independently validated DataAccess packages (Python 3.9+)."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
CACHES = ("Cache.Abstractions", "Cache.MemoryCache", "Cache.Redis", "Cache.Garnet")
PROVIDERS = ("Firestore", "MongoDB", "MySQL", "PostgreSQL", "SQLite", "SQLServer")


def run(command, log):
    with log.open("w", encoding="utf-8") as output:
        return subprocess.run(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT).returncode


def passed_results(directory):
    """Require actual test results, with every discovered test passing."""
    reports = list(directory.glob("*.trx"))
    if not reports:
        return False
    try:
        for report in reports:
            root = ET.parse(report).getroot()
            summary = root.find("{*}ResultSummary")
            counters = root.find(".//{*}Counters")
            if summary is None or summary.get("outcome") != "Completed" or counters is None:
                return False
            total = int(counters.get("total", "0"))
            if total <= 0 or int(counters.get("passed", "0")) != total:
                return False
        return True
    except (ET.ParseError, ValueError):
        return False


def test_suite(name, configuration, output):
    print(f"Testing DataAccess.{name}...", flush=True)
    results = output / "tests" / name
    results.mkdir(parents=True)
    project = ROOT / "Tests" / f"DataAccess.{name}.Tests" / f"DataAccess.{name}.Tests.csproj"
    code = run(["dotnet", "test", str(project), "-c", configuration,
                "--logger", "trx", "--results-directory", str(results),
                "--verbosity", "minimal"], results / "test.log")
    success = code == 0 and passed_results(results)
    print(f"  {'PASSED' if success else 'PENDING: failure, skipped tests, or no tests executed'}", flush=True)
    return success


def build_packages(configuration, output):
    suites = {name: test_suite(name, configuration, output) for name in ("Core", "Cache", *PROVIDERS)}
    packages = output / "packages"
    packages.mkdir()
    logs = output / "logs"
    logs.mkdir()
    report = []
    for name in ("Abstractions", "Core", *PROVIDERS, *CACHES):
        package = f"DataAccess.{name}"
        gates = ["Core", "Cache"] if name in CACHES else (["Core"] if name in ("Abstractions", "Core") else ["Core", name])
        failed = [gate for gate in gates if not suites[gate]]
        reason = "Tests not approved: " + ", ".join(failed) if failed else ""
        if not failed:
            project = ((ROOT / "Cache" if name == "Cache.Abstractions" else ROOT / "Cache" / "Providers") if name in CACHES else (ROOT if name in ("Abstractions", "Core") else ROOT / "Providers")) / package / f"{package}.csproj"
            print(f"Packing {package}...", flush=True)
            # Stage each package so a failed pack cannot leave a publishable artifact.
            with tempfile.TemporaryDirectory(prefix="pack-", dir=output) as staging:
                code = run(["dotnet", "pack", str(project), "-c", configuration,
                            "-o", staging, "--verbosity", "minimal"], logs / f"{package}.log")
                artifacts = list(Path(staging).glob("*.nupkg"))
                if code or not artifacts:
                    reason = "Pack failed or produced no package; see pack log"
                else:
                    for artifact in artifacts:
                        shutil.move(str(artifact), packages / artifact.name)
                    for symbols in Path(staging).glob("*.snupkg"):
                        shutil.move(str(symbols), packages / symbols.name)
        report.append({"package": package, "status": "pending" if reason else "generated", "reason": reason})
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("\nResults:", flush=True)
    for item in report:
        print(f"  {item['package']}: {item['status']}" + (f" ({item['reason']})" if item['reason'] else ""))
    print(f"\nPackages, logs, test results and report: {output}")
    return 1 if any(item["status"] == "pending" for item in report) else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--configuration", default="Release", help="Build configuration (default: Release)")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts" / "pack",
                        help="Parent directory for a new, isolated run directory")
    args = parser.parse_args()
    if not shutil.which("dotnet"):
        parser.error("dotnet was not found in PATH")
    args.output.mkdir(parents=True, exist_ok=True)
    output = Path(tempfile.mkdtemp(prefix="run-", dir=args.output.resolve()))
    print(f"Output: {output}", flush=True)
    return build_packages(args.configuration, output)


if __name__ == "__main__":
    sys.exit(main())

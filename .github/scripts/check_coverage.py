"""Enforce Xdiff coverage from a single Coverlet Cobertura report."""

import argparse
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def check_coverage(directory):
    reports = list(directory.glob("*.cobertura.*.xml"))
    if len(reports) != 1:
        raise ValueError(f"Expected exactly one Cobertura report; found {len(reports)}.")

    root = ET.parse(reports[0]).getroot()
    if root.tag != "coverage":
        raise ValueError("Expected a Cobertura coverage root.")
    packages = root.findall("./packages/package")
    if len(packages) != 1 or packages[0].get("name") != "Xdiff":
        raise ValueError("Coverage must contain exactly one package named Xdiff.")
    if not packages[0].findall("./classes/class/lines/line"):
        raise ValueError("Xdiff coverage contains no source lines.")

    rows = []
    passed = True
    for metric, threshold in (("lines", 95), ("branches", 90)):
        counts = []
        for suffix in ("covered", "valid"):
            value = root.get(f"{metric}-{suffix}", "")
            if not re.fullmatch(r"[0-9]+", value):
                raise ValueError(f"Missing or invalid {metric}-{suffix} count.")
            counts.append(int(value))
        covered, valid = counts
        if valid == 0 or covered > valid:
            raise ValueError(f"Invalid or empty {metric} coverage: {covered}/{valid}.")

        # Compare exact integer counts; rounding is only for display.
        meets_threshold = covered * 100 >= valid * threshold
        passed &= meets_threshold
        result = "PASS" if meets_threshold else "FAIL"
        rows.append(
            f"| {metric.capitalize()} | {covered}/{valid} | "
            f"{covered / valid:.2%} | {threshold}% | {result} |"
        )

    summary = "\n".join([
        "## Xdiff coverage",
        "",
        "| Metric | Covered/valid | Coverage | Minimum | Result |",
        "| --- | --- | --- | --- | --- |",
        *rows,
        "",
    ])
    return passed, summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="Directory containing the raw report")
    args = parser.parse_args()
    try:
        passed, summary = check_coverage(args.directory)
    except (OSError, ET.ParseError, ValueError) as error:
        passed = False
        summary = f"## Xdiff coverage\n\nFAIL: {error}\n"

    print(summary)
    if summary_path := os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(summary_path, "a", encoding="utf-8") as output:
            output.write(summary + "\n")
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())

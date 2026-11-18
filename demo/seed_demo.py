import argparse
import http.cookiejar
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CASES = Path(__file__).resolve().parent / "cases.json"
SEED_CONFIG = ROOT / "backend" / "src" / "CreditScoring.Api" / "appsettings.Development.json"
LEFT_OPEN = 10
SCORE_TOLERANCE = 0.01


class Api:
    def __init__(self, base: str, user: str, password: str):
        self.base = base.rstrip("/")
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.call("POST", "/api/auth/login", {"userName": user, "password": password})

    def call(self, method: str, path: str, body=None):
        data = None if body is None else json.dumps(body).encode()
        request = urllib.request.Request(self.base + path, data=data, method=method,
                                         headers={} if data is None else {"Content-Type": "application/json"})
        try:
            with self.opener.open(request) as response:
                text = response.read().decode()
        except urllib.error.HTTPError as error:
            raise SystemExit(f"{method} {path}: {error.code} {error.read().decode()}") from None
        return json.loads(text) if text else None


def passwords() -> dict:
    text = SEED_CONFIG.read_text(encoding="utf-8-sig")
    return {u["UserName"]: u["Password"] for u in json.loads(text)["Seed"]["Users"]}


def months_before(day: date, months: int) -> date:
    total = day.year * 12 + day.month - 1 - months
    return date(total // 12, total % 12 + 1, 1)


def main():
    parser = argparse.ArgumentParser(description="Submit the demo cases through the API of a running stack.")
    parser.add_argument("--base", default="http://localhost:8080")
    args = parser.parse_args()

    document = json.loads(CASES.read_text(encoding="utf-8"))
    secrets = passwords()
    officer = Api(args.base, "officer", secrets["officer"])
    manager = Api(args.base, "manager", secrets["manager"])
    risk = Api(args.base, "risk", secrets["risk"])

    first = document["cases"][0]["name"]
    found = officer.call("GET", "/api/clients?search=" + urllib.parse.quote(first))
    if (found.get("items") if isinstance(found, dict) else found):
        raise SystemExit(f"client {first} already exists: this database is already seeded")

    this_month = date.today().replace(day=1)
    mismatches, counts = [], {"decided": 0, "signed off": 0, "outcomes": 0, "open": 0}
    for number, case in enumerate(document["cases"]):
        answers = dict(case["answers"])
        first_line = months_before(this_month, answers.pop("creditHistoryMonths"))
        born = first_line.replace(year=first_line.year - case["yearsOldAtFirstCredit"])
        client = officer.call("POST", "/api/clients", {"fullName": case["name"], "dateOfBirth": born.isoformat()})
        application = officer.call("POST", "/api/applications",
                                   {"clientId": client["id"], "earliestCreditLine": first_line.isoformat(), **answers})

        decision = application["decision"] or {}
        expected = case["expected"]
        if decision.get("recommendation") != expected["band"] or abs((decision.get("pBad") or -1) - expected["pBad"]) > SCORE_TOLERANCE:
            mismatches.append(f"{case['name']}: expected {expected}, got {decision.get('recommendation')} {decision.get('pBad')}")

        band = expected["band"]
        if band == "Reject" or number >= len(document["cases"]) - LEFT_OPEN:
            counts["open"] += 1
            continue
        reviewed = sum(c["expected"]["band"] == "Review" for c in document["cases"][:number])
        approve = band == "Approve" or reviewed % 2 == 0
        reason = None if band == "Approve" else (
            "Checked the pay slips and the credit report; the file supports the loan." if approve
            else "Recent inquiries and high utilisation; asked the client to reapply with a smaller amount.")
        application = officer.call("POST", f"/api/applications/{application['id']}/decision",
                                   {"outcome": "Approve" if approve else "Reject", "reason": reason})
        counts["decided"] += 1
        if application["stage"] == "AwaitingSignOff":
            application = manager.call("POST", f"/api/applications/{application['id']}/sign-off",
                                       {"approve": True, "note": "Amount checked against income."})
            counts["signed off"] += 1
        if approve:
            risk.call("POST", f"/api/applications/{application['id']}/outcome",
                      {"outcome": case["observedOutcome"], "note": "Recorded outcome of the 2015 LendingClub loan."})
            counts["outcomes"] += 1

    print(f"seeded {len(document['cases'])} cases at {args.base}: {counts}")
    if mismatches:
        print("scores that differ from demo/cases.json:", *mismatches, sep="\n  ")
        sys.exit(1)


if __name__ == "__main__":
    main()

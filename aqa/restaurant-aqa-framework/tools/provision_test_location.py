"""Provision the dedicated test location the group pre-order scenarios book at.

Booking at a real location fills its real waiter's dashboard with test bookings. This creates, once per environment:
  * a location named "AQA Test Location" (deliberately NOT prefixed "AQA-Sample-", so the run-level sweep keeps it);
  * four tables at it (seats 4, 4, 6, 8);
  * a test waiter assigned to it: registered as a customer, added with POST /waiters, then taken out of the
    Customer group in Cognito so the token carries only the Waiter role.
It writes WAITER_USERNAME / WAITER_PASSWORD and TESTTABLE_* into the given .env (the password is generated and never
printed). Re-running it reuses what exists.

Needs the AWS CLI (for the Cognito group change), signed in to the account that hosts the backend.

    python3 tools/provision_test_location.py --env .env --region us-west-2 --user-pool-id <pool>
"""
import argparse
import json
import re
import secrets
import string
import subprocess
import urllib.error
import urllib.request

ADDRESS = "AQA Test Location"
WAITER_EMAIL = "aqa.test.waiter@example.com"
TABLES = [(1, 4), (2, 4), (3, 6), (4, 8)]


def call(api, method, path, body=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(api + path, method=method, headers=headers,
                                     data=json.dumps(body).encode() if body is not None else None)
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            raw = response.read().decode()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def password():
    rng = secrets.SystemRandom()
    chars = [rng.choice(string.ascii_uppercase), rng.choice(string.ascii_lowercase), rng.choice(string.digits), rng.choice("!@%^*-_")]
    chars += [rng.choice(string.ascii_letters + string.digits + "!@%^*-_") for _ in range(10)]
    rng.shuffle(chars)
    return "".join(chars)


def set_env(path, values):
    text = open(path).read()
    for key, value in values.items():
        text, count = re.subn(rf"^{key}=.*$", lambda _: f"{key}={value}", text, flags=re.M)
        if count == 0:
            text += f"\n{key}={value}\n"
    open(path, "w").write(text)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--env", required=True, help="the .env to read API_URL from and write the results to")
    parser.add_argument("--region", required=True)
    parser.add_argument("--user-pool-id", required=True)
    args = parser.parse_args()

    env = dict(re.findall(r"^([A-Z_]+)=(.*)$", open(args.env).read(), re.M))
    api = env["API_URL"].rstrip("/")

    status, locations = call(api, "GET", "/locations")
    location = next((l for l in locations if l.get("address") == ADDRESS), None)
    if location is None:
        status, location = call(api, "POST", "/locations", {
            "address": ADDRESS, "description": "Used by the automated tests only.",
            "totalCapacity": 22, "averageOccupancy": "0"})
        assert status < 300, f"creating the location failed: {status} {location}"
    location_id = location["id"]
    print("location:", location_id)

    status, tables = call(api, "GET", f"/locations/{location_id}/tables/all")
    existing = {t["tableNumber"]: t for t in (tables if status == 200 else [])}
    for number, seats in TABLES:
        if number not in existing:
            status, table = call(api, "POST", "/tables", {"locationId": location_id, "capacity": seats, "tableNumber": number})
            assert status < 300, f"creating table {number} failed: {status} {table}"
            existing[number] = table
    biggest = existing[TABLES[-1][0]]
    print("tables:", sorted(existing))

    secret = password()
    status, body = call(api, "POST", "/register", {"firstName": "Aqa", "lastName": "Waiter", "email": WAITER_EMAIL,
                                                   "password": secret, "confirmPassword": secret})
    if status >= 300:
        # Already registered by an earlier run: give it a fresh password instead.
        subprocess.run(["aws", "cognito-idp", "admin-set-user-password", "--region", args.region, "--user-pool-id",
                        args.user_pool_id, "--username", WAITER_EMAIL, "--password", secret, "--permanent"],
                       check=True, capture_output=True)
    status, body = call(api, "POST", "/waiters", {"email": WAITER_EMAIL, "locationId": location_id})
    print("assign waiter:", status, body if status >= 300 else "")
    subprocess.run(["aws", "cognito-idp", "admin-remove-user-from-group", "--region", args.region, "--user-pool-id",
                    args.user_pool_id, "--username", WAITER_EMAIL, "--group-name", "Customer"], capture_output=True)

    set_env(args.env, {"WAITER_USERNAME": WAITER_EMAIL, "WAITER_PASSWORD": secret, "TESTTABLE_ID": biggest["id"],
                       "TESTTABLE_WAITER_USERNAME": WAITER_EMAIL, "TESTTABLE_WAITER_PASSWORD": secret})
    print(".env updated: WAITER_*, TESTTABLE_* now point at the test location")


if __name__ == "__main__":
    main()

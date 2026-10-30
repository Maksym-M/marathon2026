"""Purge cancelled test bookings from the environment under test.

The group pre-order scenarios cancel every reservation they make, but the backend has no way to delete one, and the
waiter's dashboard loads every reservation of its location (plus three calls per reservation), so cancelled test
bookings slow it down run after run. This removes them directly from DynamoDB.

A reservation is removed only if ALL of these hold:
  * its status is Cancelled;
  * the customer who booked it is a test customer (Cognito given_name starts with "AQA-Sample-");
  * it belongs to --waiter-id, when one is given.
Its order, group pre-order and guest sessions go with it. Nothing is deleted without --delete (dry run).

Needs the AWS CLI, signed in to the account that hosts the backend.

    python3 tools/purge_test_bookings.py --region us-west-2 --user-pool-id <pool> [--waiter-id <sub>] [--delete]
"""
import argparse
import json
import subprocess

CANCELLED = "1"
TEST_PREFIX = "AQA-Sample-"


def aws(*args):
    out = subprocess.run(["aws", *args, "--output", "json"], capture_output=True, text=True, check=True).stdout
    return json.loads(out) if out.strip() else {}


def scan(region, table):
    items, start = [], None
    while True:
        page = aws("dynamodb", "scan", "--region", region, "--table-name", table,
                   *(["--exclusive-start-key", json.dumps(start)] if start else []))
        items += page.get("Items", [])
        start = page.get("LastEvaluatedKey")
        if not start:
            return items


def test_customers(region, pool):
    subs, token = set(), None
    while True:
        page = aws("cognito-idp", "list-users", "--region", region, "--user-pool-id", pool,
                   "--filter", f'given_name ^= "{TEST_PREFIX}"', *(["--pagination-token", token] if token else []))
        for user in page.get("Users", []):
            subs.update(a["Value"] for a in user["Attributes"] if a["Name"] == "sub")
        token = page.get("PaginationToken")
        if not token:
            return subs


def value(item, key):
    attribute = item.get(key) or {}
    return next(iter(attribute.values()), None)


def delete(region, table, keys):
    for i in range(0, len(keys), 25):
        request = {table: [{"DeleteRequest": {"Key": key}} for key in keys[i:i + 25]]}
        while request:
            request = aws("dynamodb", "batch-write-item", "--region", region,
                          "--request-items", json.dumps(request)).get("UnprocessedItems") or None


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--region", required=True)
    parser.add_argument("--user-pool-id", required=True)
    parser.add_argument("--waiter-id", help="only this waiter's reservations (Cognito sub)")
    parser.add_argument("--delete", action="store_true", help="really delete (default: dry run)")
    args = parser.parse_args()

    customers = test_customers(args.region, args.user_pool_id)
    reservations = {value(r, "id") for r in scan(args.region, "Reservations")
                    if value(r, "status") == CANCELLED
                    and value(r, "customerId") in customers
                    and (args.waiter_id is None or value(r, "waiterId") == args.waiter_id)}
    orders = [o for o in scan(args.region, "Orders") if value(o, "reservationId") in reservations]
    groups = [g for g in scan(args.region, "GroupReservations") if value(g, "reservationId") in reservations]
    group_ids = {value(g, "groupReservationId") for g in groups}
    sessions = [s for s in scan(args.region, "GuestSessions") if value(s, "groupReservationId") in group_ids]

    plan = {
        "Reservations": [{"id": {"S": r}} for r in reservations],
        "Orders": [{"orderId": o["orderId"], "customerId": o["customerId"]} for o in orders],
        "GroupReservations": [{"groupReservationId": g["groupReservationId"]} for g in groups],
        "GuestSessions": [{"guestSessionId": s["guestSessionId"]} for s in sessions],
    }
    for table, keys in plan.items():
        if args.delete:
            delete(args.region, table, keys)
        print(f"{table}: {len(keys)} {'deleted' if args.delete else 'would be deleted (dry run)'}")


if __name__ == "__main__":
    main()

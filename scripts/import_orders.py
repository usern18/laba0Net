import argparse
import sqlite3
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description="Copy legacy SQLite orders into the OrderingService database.")
parser.add_argument("--source", type=Path, default=root / "laba0Net" / "monolith.db")
parser.add_argument("--target", type=Path, default=root / "OrderingService" / "ordering.db")
arguments = parser.parse_args()
source_path = arguments.source
target_path = arguments.target

if not source_path.exists():
	raise SystemExit(f"Legacy database not found: {source_path}")
if not target_path.exists():
	raise SystemExit(f"OrderingService database not found: {target_path}. Start OrderingService once to apply its migrations.")

with sqlite3.connect(source_path) as source, sqlite3.connect(target_path) as target:
	source.row_factory = sqlite3.Row
	target.execute("PRAGMA foreign_keys = ON")
	existing = target.execute("SELECT COUNT(*) FROM Orders").fetchone()[0]
	if existing:
		raise SystemExit("OrderingService already contains orders; refusing to overwrite or merge data.")

	legacy_orders = source.execute(
		'SELECT "Id", "UserId", "ProductId", "ProductName", "Quantity", "TotalPrice" FROM "Orders" ORDER BY "Id"'
	).fetchall()
	now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")
	target.executemany(
		'INSERT INTO "Orders" '
		'("Id", "UserId", "ProductId", "ProductName", "UnitPrice", "Quantity", "TotalPrice", "Status", "CreatedAt", "IdempotencyKey") '
		'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, NULL)',
		[
			(row["Id"], row["UserId"], row["ProductId"], row["ProductName"],
			 str(Decimal(str(row["TotalPrice"])) / row["Quantity"]), row["Quantity"],
			 str(Decimal(str(row["TotalPrice"]))), "Pending", now)
			for row in legacy_orders
		],
	)
	maximum_id = max((row["Id"] for row in legacy_orders), default=0)
	sequence = target.execute('SELECT seq FROM sqlite_sequence WHERE name = "Orders"').fetchone()
	if sequence is None:
		target.execute('INSERT INTO sqlite_sequence (name, seq) VALUES ("Orders", ?)', (maximum_id,))
	else:
		target.execute('UPDATE sqlite_sequence SET seq = ? WHERE name = "Orders"', (maximum_id,))

print(f"Copied {len(legacy_orders)} orders; next generated ID will exceed {maximum_id}.")

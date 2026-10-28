"""Copy legacy monolith Products into Catalog's independent SQLite database."""

from __future__ import annotations

import argparse
import sqlite3
import sys
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path


def main() -> int:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("--source", type=Path, default=Path("laba0Net/monolith.db"))
	parser.add_argument("--target", type=Path, default=Path("CatalogService/miniproject_catalog.db"))
	args = parser.parse_args()

	source_path = args.source.resolve()
	target_path = args.target.resolve()
	if source_path == target_path:
		parser.error("source and target must be separate service databases")
	if not source_path.is_file() or not target_path.is_file():
		parser.error("both database files must exist; run CatalogService once to apply its migrations")

	source = sqlite3.connect(f"file:{source_path.as_posix()}?mode=ro", uri=True)
	target = sqlite3.connect(target_path)
	try:
		products = source.execute(
			"SELECT Id, Name, Category, Price, StockQuantity FROM Products ORDER BY Id"
		).fetchall()
		if not products:
			print("No monolith products to import.")
			return 0

		target.execute("BEGIN IMMEDIATE")
		now = datetime.now(timezone.utc).isoformat(timespec="seconds")
		imported = 0
		unchanged = 0
		for product_id, title, category_name, raw_price, stock_quantity in products:
			if not title or not category_name:
				raise ValueError(f"Product {product_id} has an empty title or category")
			price = Decimal(str(raw_price))
			category = target.execute(
				"SELECT Id FROM Categories WHERE Name = ?", (category_name,)
			).fetchone()
			if category is None:
				cursor = target.execute(
					"INSERT INTO Categories (Name) VALUES (?)", (category_name,)
				)
				category_id = cursor.lastrowid
			else:
				category_id = category[0]

			existing = target.execute(
				"SELECT Items.Title, Items.Price, Categories.Name, Items.StockQuantity "
				"FROM Items JOIN Categories ON Categories.Id = Items.CategoryId "
				"WHERE Items.Id = ?",
				(product_id,),
			).fetchone()
			if existing is not None:
				if (existing[0] == title and Decimal(str(existing[1])) == price and
						existing[2] == category_name and existing[3] == stock_quantity):
					unchanged += 1
					continue
				raise ValueError(
					f"Catalog item ID {product_id} is already owned by different data; "
					"no conflicting rows were overwritten"
				)

			target.execute(
				"INSERT INTO Items (Id, Title, Description, Price, StockQuantity, CategoryId, IsActive, CreatedAt) "
				"VALUES (?, ?, NULL, ?, ?, ?, 1, ?)",
				(product_id, title, str(price), stock_quantity, category_id, now),
			)
			imported += 1

		target.commit()
		print(f"Imported {imported} products; {unchanged} were already present.")
		return 0
	except (sqlite3.Error, ValueError) as error:
		target.rollback()
		print(f"Catalog import failed: {error}", file=sys.stderr)
		return 1
	finally:
		source.close()
		target.close()


if __name__ == "__main__":
	raise SystemExit(main())

using System;
using System.Collections.Generic;
using System.Linq;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;

namespace ShoppingListServer.Logic
{
    /// <summary>
    /// Translates between the persisted shape of a list
    /// (<see cref="ShoppingList"/> plus its <see cref="ShoppingListProduct"/> rows) and the wire shape
    /// the clients exchange (<see cref="ShoppingList.ProductList"/> of <see cref="GenericProduct"/>).
    ///
    /// This is the only place where the two shapes meet. The wire contract must stay byte-for-byte
    /// compatible with the previous JSON-file implementation: item identity is the name, order is the
    /// array order, and every string the clients validate must be present (null becomes an empty
    /// string, a missing <c>imagePath</c> stays <c>null</c>).
    /// </summary>
    public static class ShoppingListContentMapper
    {
        /// <summary>
        /// Populates <see cref="ShoppingList.ProductList"/> from the persisted item rows (in
        /// <see cref="ShoppingListProduct.Position"/> order) and normalizes nullable content for the
        /// wire. Mutates and returns the given instance so a freshly added list can be returned to the
        /// caller unchanged.
        /// </summary>
        public static ShoppingList ToWire(ShoppingList entity)
        {
            if (entity == null)
                return null;

            entity.Name ??= string.Empty;
            entity.Category ??= string.Empty;
            entity.DateString ??= string.Empty;
            entity.Notes ??= string.Empty;

            entity.ProductList = (entity.Products ?? Enumerable.Empty<ShoppingListProduct>())
                .OrderBy(row => row.Position)
                .ThenBy(row => row.Id)
                .Select(ToProduct)
                .ToList();

            return entity;
        }

        /// <summary>
        /// Builds a plain (non-proxy, non-tracked) copy of the given list for serialization. The EF
        /// lazy-loading proxies override their navigation properties, which hides the
        /// <c>[JsonIgnore]</c> attributes from System.Text.Json and lets a serializer trigger lazy
        /// loading on a detached entity. Copying the content onto a fresh instance removes that
        /// hazard entirely.
        /// </summary>
        public static ShoppingList ToWireCopy(ShoppingList entity)
        {
            if (entity == null)
                return null;

            return new ShoppingList
            {
                SyncId = entity.SyncId,
                LastChangeServerTime = entity.LastChangeServerTime,
                Version = entity.Version,
                Name = entity.Name ?? string.Empty,
                Category = entity.Category ?? string.Empty,
                DateString = entity.DateString ?? string.Empty,
                Notes = entity.Notes ?? string.Empty,
                ProductList = (entity.Products ?? Enumerable.Empty<ShoppingListProduct>())
                    .OrderBy(row => row.Position)
                    .ThenBy(row => row.Id)
                    .Select(ToProduct)
                    .ToList()
            };
        }

        /// <summary>
        /// Replaces the content of <paramref name="entity"/> (a tracked list) with the content of
        /// <paramref name="source"/>, syncing the item rows by name so surviving items keep their
        /// primary key instead of being deleted and re-inserted.
        ///
        /// Whole-list <c>PATCH /shopping/list</c> semantics are preserved: items omitted from the
        /// payload are deleted, the payload order becomes the new <c>Position</c> order, and duplicate
        /// names in the payload keep the first occurrence.
        /// </summary>
        public static void ApplyContent(ShoppingList source, ShoppingList entity)
        {
            entity.Name = source.Name ?? string.Empty;
            entity.Category = source.Category ?? string.Empty;
            entity.DateString = source.DateString ?? string.Empty;
            entity.Notes = source.Notes ?? string.Empty;

            entity.Products ??= new List<ShoppingListProduct>();

            (Dictionary<string, GenericProduct> incoming, List<string> order) = IndexByName(source.ProductList);

            // Drop rows the payload no longer contains.
            for (int index = entity.Products.Count - 1; index >= 0; index--)
            {
                if (!incoming.ContainsKey(entity.Products[index].ItemName ?? string.Empty))
                    entity.Products.RemoveAt(index);
            }

            Dictionary<string, ShoppingListProduct> existing = new Dictionary<string, ShoppingListProduct>(StringComparer.Ordinal);
            foreach (ShoppingListProduct row in entity.Products)
            {
                existing[row.ItemName ?? string.Empty] = row;
            }

            for (int position = 0; position < order.Count; position++)
            {
                string name = order[position];
                if (!existing.TryGetValue(name, out ShoppingListProduct row))
                {
                    row = new ShoppingListProduct
                    {
                        ShoppingListId = entity.SyncId,
                        ItemName = name
                    };
                    entity.Products.Add(row);
                }

                ApplyProduct(row, incoming[name]);
                row.Position = position;
            }
        }

        /// <summary>
        /// Builds fresh item rows for a brand new list. Duplicate names keep the first occurrence;
        /// positions are assigned in payload order.
        /// </summary>
        public static List<ShoppingListProduct> BuildProducts(IEnumerable<GenericProduct> products, string shoppingListId)
        {
            (Dictionary<string, GenericProduct> incoming, List<string> order) = IndexByName(products);
            List<ShoppingListProduct> rows = new List<ShoppingListProduct>(order.Count);
            int position = 0;

            foreach (string name in order)
            {
                GenericProduct product = incoming[name];
                ShoppingListProduct row = new ShoppingListProduct
                {
                    ShoppingListId = shoppingListId,
                    ItemName = name,
                    Position = position++
                };
                ApplyProduct(row, product);
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Projects one persisted row to the wire product shape. The category is always emitted as an
        /// object because both clients validate it as required.
        /// </summary>
        public static GenericProduct ToProduct(ShoppingListProduct row)
        {
            return new GenericProduct
            {
                Item = new GenericItem
                {
                    Name = row.ItemName ?? string.Empty,
                    ImagePath = row.ItemImagePath,
                    Category = new Category
                    {
                        Name = row.CategoryName ?? string.Empty,
                        ColorHex = row.CategoryColorHex ?? string.Empty,
                        ImagePath = row.CategoryImagePath
                    }
                },
                Count = row.Count,
                Checked = row.Checked
            };
        }

        private static void ApplyProduct(ShoppingListProduct row, GenericProduct product)
        {
            GenericItem item = product.Item;
            row.ItemName = item?.Name ?? string.Empty;
            row.ItemImagePath = item?.ImagePath;
            row.CategoryName = item?.Category?.Name ?? string.Empty;
            row.CategoryColorHex = item?.Category?.ColorHex ?? string.Empty;
            row.CategoryImagePath = item?.Category?.ImagePath;
            row.Count = product.Count;
            row.Checked = product.Checked;
        }

        private static (Dictionary<string, GenericProduct> ByName, List<string> Order) IndexByName(IEnumerable<GenericProduct> products)
        {
            Dictionary<string, GenericProduct> byName = new Dictionary<string, GenericProduct>(StringComparer.Ordinal);
            List<string> order = new List<string>();

            if (products == null)
                return (byName, order);

            foreach (GenericProduct product in products)
            {
                if (product == null)
                    continue;

                string name = product.Item?.Name ?? string.Empty;
                if (byName.ContainsKey(name))
                    continue;

                byName.Add(name, product);
                order.Add(name);
            }

            return (byName, order);
        }
    }
}

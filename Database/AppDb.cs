using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShoppingListServer.Entities;
using ShoppingListServer.Models;
using ShoppingListServer.Models.ShoppingData;

namespace ShoppingListServer.Database
{
    // Database in Asp.Net Core:
    //
    // Create all tables (see https://docs.microsoft.com/en-us/ef/core/get-started/overview/first-app?tabs=netcore-cli):
    // dotnet ef migrations add InitialCreate
    // dotnet ef database update
    // 
    // Tutorials:
    // - https://docs.microsoft.com/en-us/ef/core/get-started/overview/first-app?tabs=netcore-cli
    // - https://docs.microsoft.com/en-us/ef/core/modeling/
    // 
    public class AppDb : DbContext
    {
        public DbSet<User> Users { get; set; }
        //public DbSet<string> SyncIDs { get; set; }
        public DbSet<ShoppingList> ShoppingLists { get; set; }

        /// <summary>
        /// The line items of all shopping lists. One row per item via the unique
        /// <c>(ShoppingListId, ItemName)</c> index, so a count/checked change is a single-row update.
        /// </summary>
        public DbSet<ShoppingListProduct> ShoppingListProducts { get; set; }

        public AppDb(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // see "Indirect many-to-many relationships" in
            // https://docs.microsoft.com/en-us/ef/core/modeling/relationships?tabs=fluent-api%2Cfluent-api-simple-key%2Csimple-key
            // ShoppingListPermission is a n:n relationship between ShoppingList and User.
            // The indirect method is used here so it's possible to add the "PermissionType" property to the ShoppingListPermission relationship.
            modelBuilder.Entity<ShoppingListPermission>()
                .HasKey(accessRight => new { accessRight.ShoppingListId, accessRight.UserId});

            // ShoppingList -> ShoppingListPermission
            modelBuilder.Entity<ShoppingListPermission>()
                .HasOne(accessRight => accessRight.ShoppingList)
                .WithMany(shoppingList => shoppingList.ShoppingListPermissions)
                .HasForeignKey(accessRight => accessRight.ShoppingListId);

            // User -> ShoppingListPermission
            modelBuilder.Entity<ShoppingListPermission>()
                .HasOne(accessRight => accessRight.User)
                .WithMany(user => user.ShoppingListPermissions)
                .HasForeignKey(accessRight => accessRight.UserId);

            // n-n UserContacts
            modelBuilder.Entity<UserContact>()
                .HasKey(accessRight => new { accessRight.UserSourceId, accessRight.UserTargetId });

            // UserSource -> UserContact
            modelBuilder.Entity<UserContact>()
                .HasOne(accessRight => accessRight.UserSource)
                .WithMany(user => user.UserContactsThis)
                .HasForeignKey(accessRight => accessRight.UserSourceId);

            // UserTarget -> UserContact
            modelBuilder.Entity<UserContact>()
                .HasOne(accessRight => accessRight.UserTarget)
                .WithMany(user => user.UserContactsOthers)
                .HasForeignKey(accessRight => accessRight.UserTargetId);

            // ShoppingList -> ShoppingListProduct (cascade: deleting a list deletes its items)
            modelBuilder.Entity<ShoppingListProduct>(product =>
            {
                product.ToTable("ShoppingListProducts");
                product.HasKey(item => item.Id);
                product.Property(item => item.ShoppingListId).IsRequired();
                product.Property(item => item.ItemName).IsRequired();
                product.HasOne(item => item.ShoppingList)
                    .WithMany(list => list.Products)
                    .HasForeignKey(item => item.ShoppingListId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Item identity within a list is the item name (no stable ids on the wire).
                product.HasIndex(item => new { item.ShoppingListId, item.ItemName }).IsUnique();
                // Ordering is the Position column.
                product.HasIndex(item => new { item.ShoppingListId, item.Position });
            });

            // Optimistic concurrency: every mutation bumps ShoppingList.Version, and an update whose
            // WHERE clause matches no row is reported as DbUpdateConcurrencyException.
            modelBuilder.Entity<ShoppingList>()
                .Property(list => list.Version)
                .IsConcurrencyToken();

            // Store DateTime in Database in UTC, see
            // https://stackoverflow.com/a/61243301
            var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
                v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
                v => v.HasValue ? v.Value.ToUniversalTime() : v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.IsKeyless)
                {
                    continue;
                }

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }
    }
}

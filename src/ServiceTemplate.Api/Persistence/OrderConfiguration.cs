using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceTemplate.Api.Domain;

namespace ServiceTemplate.Api.Persistence;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t =>
        {
            // The database repeats the most important invariants so a bad deploy or manual SQL cannot break them.
            t.HasCheckConstraint("ck_orders_status", "status IN ('Draft', 'Submitted', 'Cancelled')");
        });

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.CustomerReference).HasColumnName("customer_reference")
            .HasMaxLength(Order.MaxCustomerReferenceLength).IsRequired();
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(o => o.CancelledAt).HasColumnName("cancelled_at");

        // Npgsql maps a uint row version to the xmin system column: optimistic concurrency without an extra column.
        builder.Property(o => o.Version).IsRowVersion();

        builder.Ignore(o => o.Total);
        builder.HasIndex(o => o.CustomerReference).HasDatabaseName("ix_orders_customer_reference");

        builder.OwnsMany(o => o.Lines, line =>
        {
            line.ToTable("order_lines", t =>
            {
                t.HasCheckConstraint("ck_order_lines_quantity", $"quantity BETWEEN 1 AND {Order.MaxQuantityPerLine}");
                t.HasCheckConstraint("ck_order_lines_unit_price", "unit_price > 0");
            });
            line.WithOwner().HasForeignKey("OrderId");
            line.Property<Guid>("OrderId").HasColumnName("order_id");
            line.HasKey("OrderId", nameof(OrderLine.Sku));
            line.Property(l => l.Sku).HasColumnName("sku").HasMaxLength(32);
            line.Property(l => l.Quantity).HasColumnName("quantity");
            line.Property(l => l.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            line.Ignore(l => l.LineTotal);
        });

        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

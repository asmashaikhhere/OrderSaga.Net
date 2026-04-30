using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OrderSaga.StateMachine;

public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options) { }

    public DbSet<OrderSagaState> OrderSagaStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OrderSagaStateMap());
    }
}

public class OrderSagaStateMap : IEntityTypeConfiguration<OrderSagaState>
{
    public void Configure(EntityTypeBuilder<OrderSagaState> entity)
    {
        entity.ToTable("OrderSagaState");

        entity.HasKey(x => x.CorrelationId);

        entity.Property(x => x.CurrentState)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.CustomerId)
            .HasMaxLength(256)
            .IsRequired();

        entity.Property(x => x.TotalAmount)
            .HasColumnType("decimal(18,2)");

        entity.Property(x => x.FailureReason)
            .HasMaxLength(1024);

        entity.Property(x => x.Version)
            .IsConcurrencyToken();
    }
}
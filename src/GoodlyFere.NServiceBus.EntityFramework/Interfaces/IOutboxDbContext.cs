using System.Data.Entity;
using GoodlyFere.NServiceBus.EntityFramework.Outbox;
using GoodlyFere.NServiceBus.EntityFramework.SubscriptionStorage;

namespace GoodlyFere.NServiceBus.EntityFramework.Interfaces
{
    public interface IOutboxDbContext : INServiceBusDbContext
    {
        DbSet<OutboxEntity> OutboxRecords{ get; set; }
    }
}
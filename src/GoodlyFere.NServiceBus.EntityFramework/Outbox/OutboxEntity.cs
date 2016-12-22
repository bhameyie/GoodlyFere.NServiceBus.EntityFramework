using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Outbox;

namespace GoodlyFere.NServiceBus.EntityFramework.Outbox
{
    public class OutboxEntity
    {
        [Key]
        public long Id { get; set; }
        public string MessageId { get; set; }
        public bool Dispatched { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public string TransportOperations { get; set; }
    }
}

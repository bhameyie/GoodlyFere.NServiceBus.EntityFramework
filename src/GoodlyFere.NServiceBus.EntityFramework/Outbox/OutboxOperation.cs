using System;
using System.Collections.Generic;

namespace GoodlyFere.NServiceBus.EntityFramework.Outbox
{
    [Serializable]
    public class OutboxOperation
    {
        public string MessageId { get; set; }
        public byte[] Message { get; set; }
        public Dictionary<string, string> Options { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
}
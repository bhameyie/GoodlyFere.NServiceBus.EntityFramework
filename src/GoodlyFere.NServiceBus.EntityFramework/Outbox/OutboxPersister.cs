using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Transactions;
using GoodlyFere.NServiceBus.EntityFramework.Interfaces;
using NServiceBus.Outbox;
using IsolationLevel = System.Data.IsolationLevel;

namespace GoodlyFere.NServiceBus.EntityFramework.Outbox
{
    internal interface IOutboxPersister : IOutboxStorage
    {
        void RemoveEntriesOlderThan(DateTime dateTime);
    }

    internal class OutboxPersister : IOutboxPersister
    {
        public string EndpointName { get; set; }
        private readonly IOutboxDbContext _dbContext;

        public OutboxPersister(INServiceBusDbContextFactory dbContextFactory)
        {
            _dbContext = dbContextFactory.CreateOutboxDbContext();
        }

        public void RemoveEntriesOlderThan(DateTime dateTime)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                using (var tx = _dbContext.Database.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    var query = _dbContext.OutboxRecords.Where(e => e.Dispatched && e.DispatchedAt < dateTime);
                    _dbContext.OutboxRecords.RemoveRange(query);
                    _dbContext.SaveChanges();

                    tx.Commit();
                }
            }
        }

        public bool TryGet(string messageId, out OutboxMessage message)
        {
            object[] possibleIds =
            {
                EndpointQualifiedMessageId(messageId),
                messageId,
            };
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                try
                {
                    OutboxEntity result;
                    using (var tx = _dbContext.Database.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                    {
                        //todo: make this faster
                        result = _dbContext.OutboxRecords.ToArray().SingleOrDefault(e => possibleIds.Contains(e.MessageId));
                        tx.Commit();
                    }
                    if (result == null)
                    {
                        message = null;
                        return false;
                    }
                    if (result.Dispatched)
                    {
                        message = new OutboxMessage(result.MessageId);
                        return true;
                    }
                    var transportOperations = ConvertStringToObject(result.TransportOperations)
                        .Select(t => new TransportOperation(t.MessageId, t.Options, t.Message, t.Headers))
                        .ToList();

                    message = new OutboxMessage(result.MessageId);
                    message.TransportOperations.AddRange(transportOperations);
                    return true;
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }

        static IEnumerable<OutboxOperation> ConvertStringToObject(string data)
        {
            return string.IsNullOrEmpty(data)
                ? Enumerable.Empty<OutboxOperation>()
                : DefaultSerializer.DeSerialize<IEnumerable<OutboxOperation>>(data);
        }

        static string ConvertObjectToString(IEnumerable<OutboxOperation> operations)
        {
            if (operations == null || !operations.Any())
            {
                return null;
            }

            return DefaultSerializer.Serialize(operations);
        }

        public void Store(string messageId, IEnumerable<TransportOperation> transportOperations)
        {
            using (var tx = new TransactionScope(TransactionScopeOption.Required))
            {
                var operations = transportOperations.Select(t => new OutboxOperation
                {
                    Message = t.Body,
                    Headers = t.Headers,
                    MessageId = t.MessageId,
                    Options = t.Options,
                });
                var serializedOperations = ConvertObjectToString(operations);
                var entity = new OutboxEntity
                {
                    Dispatched = false,
                    MessageId = EndpointQualifiedMessageId(messageId),
                    TransportOperations = serializedOperations
                };

                _dbContext.OutboxRecords.Add(entity);
                _dbContext.SaveChanges();
                tx.Complete();
            }
        }

        public void SetAsDispatched(string messageId)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            {
                using (var tx = _dbContext.Database.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    var mid = EndpointQualifiedMessageId(messageId);
                    var rec =
                        _dbContext.OutboxRecords.SingleOrDefault(e => e.MessageId == mid);

                    if (rec == null)
                        return;

                    rec.Dispatched = true;
                    rec.DispatchedAt = DateTime.UtcNow;

                    _dbContext.SaveChanges();

                    tx.Commit();
                }
            }
        }

        string EndpointQualifiedMessageId(string messageId)
        {
            return EndpointName + "/" + messageId;
        }
    }
}
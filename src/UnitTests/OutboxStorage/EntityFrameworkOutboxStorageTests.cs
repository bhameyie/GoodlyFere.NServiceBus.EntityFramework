using System;
using FluentAssertions;
using GoodlyFere.NServiceBus.EntityFramework.Outbox;
using Xunit;

namespace UnitTests.OutboxStorage
{
    public class EntityFrameworkOutboxStorageTests
    {
        [Fact]
        public void Constructor_DoesNotThrow()
        {
            Action action = () => new EntityFrameworkOutboxStorageFeature();

            action.Invoking(a => a.Invoke()).ShouldNotThrow();
        }
    }
}
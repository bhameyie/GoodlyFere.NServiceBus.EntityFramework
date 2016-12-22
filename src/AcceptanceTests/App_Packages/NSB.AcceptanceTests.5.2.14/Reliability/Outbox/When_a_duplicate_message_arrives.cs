using System.Configuration;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;

namespace NServiceBus.AcceptanceTests.Reliability.Outbox
{
    using System;
    using AcceptanceTesting;
    using Configuration.AdvanceExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;
    using ScenarioDescriptors;

    public class When_a_duplicate_message_arrives : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_not_dispatch_messages_already_dispatched()
        {
            Scenario.Define<Context>()
                .WithEndpoint<OutboxEndpoint>(b => b.When(session =>
                {
                    var duplicateMessageId = Guid.NewGuid().ToString();
                    var p1 = new PlaceOrder();
                    var p2 = new PlaceOrder();
                    var dest = Address.Parse("ADuplicateMessageArrives.OutboxEndpoint");

                    session.SetMessageHeader(p1, Headers.MessageId, duplicateMessageId);
                    session.SetMessageHeader(p2, Headers.MessageId, duplicateMessageId);
                    session.Send(dest, p1);
                    session.Send(dest, p2);
                    session.SendLocal(new PlaceOrder
                    {
                        Terminator = true
                    });
                }))
                .WithEndpoint<DownstreamEndpoint>()
                .Done(c => c.Done)
                .Repeat(r => r.For<AllOutboxCapableStorages>())
                .Should(context => Assert.AreEqual(2, context.MessagesReceivedByDownstreamEndpoint))
                .Run(TimeSpan.FromMinutes(1));
        }

        public class Context : ScenarioContext
        {
            public int MessagesReceivedByDownstreamEndpoint { get; set; }
            public bool Done { get; set; }
        }

        class NoConcurrencyConfigSource : IConfigurationSource
        {
            public T GetConfiguration<T>() where T : class, new()
            {
                if (typeof(T) == typeof(TransportConfig))
                {
                    return new TransportConfig
                    {
                        MaximumConcurrencyLevel = 1
                    } as T;
                }
                if (typeof(T) == typeof(MessageForwardingInCaseOfFaultConfig))
                {
                    return new MessageForwardingInCaseOfFaultConfig
                    {
                        ErrorQueue = "testing.error"
                    } as T;
                }
                return ConfigurationManager.GetSection(typeof(T).Name) as T;
            }
        }

        //todo: why does this endpoint not get picked up
        public class DownstreamEndpoint : EndpointConfigurationBuilder
        {
            public DownstreamEndpoint()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    b.CustomConfigurationSource(new NoConcurrencyConfigSource());
                });
            }

            public class SendOrderAcknowledgementHandler : IHandleMessages<SendOrderAcknowledgement>
            {
                public Context Context { get; set; }
                public void Handle(SendOrderAcknowledgement message)
                {
                    Context.MessagesReceivedByDownstreamEndpoint++;
                    if (message.Terminator)
                    {
                        Context.Done = true;
                    }
                }
            }
        }


        public class OutboxEndpoint : EndpointConfigurationBuilder
        {
            public OutboxEndpoint()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    b.CustomConfigurationSource(new NoConcurrencyConfigSource());
                    b.GetSettings().Set("DisableOutboxTransportCheck", true);
                    b.EnableOutbox();
                }).AddMapping<SendOrderAcknowledgement>(typeof(DownstreamEndpoint));
            }

            public class PlaceOrderHandler : IHandleMessages<PlaceOrder>
            {
                public IBus Bus { get; set; }

                public void Handle(PlaceOrder message)
                {
                    var dest = Address.Parse("ADuplicateMessageArrives.DownstreamEndpoint");
                    Bus.Send(dest,new SendOrderAcknowledgement
                    {
                        Terminator = message.Terminator
                    });
                }
            }
        }

        public class PlaceOrder : ICommand
        {
            public bool Terminator { get; set; }
        }

        public class SendOrderAcknowledgement : IMessage
        {
            public bool Terminator { get; set; }
        }
    }
}
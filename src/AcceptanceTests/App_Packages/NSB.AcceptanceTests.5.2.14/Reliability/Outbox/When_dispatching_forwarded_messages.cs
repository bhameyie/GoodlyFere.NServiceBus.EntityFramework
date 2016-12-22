using AcceptanceTests.App_Packages.NSB.AcceptanceTests._5._2._14;
using NServiceBus.Pipeline.Contexts;

namespace NServiceBus.AcceptanceTests.Reliability.Outbox
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Configuration.AdvanceExtensibility;
    using EndpointTemplates;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using ScenarioDescriptors;

    [Ignore("Unrealistic scenario in NSB 5")]
    public class When_dispatching_forwarded_messages : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_be_dispatched_immediately()
        {
            Scenario.Define<Context>()
               .WithEndpoint<EndpointWithAuditOn>(b => b
                   .When(session => session.SendLocal(new MessageToBeForwarded())))
               .WithEndpoint<ForwardingSpyEndpoint>()
               .Done(c => c.Done)
               .Repeat(r => r.For<AllOutboxCapableStorages>())
               .Should(c => Assert.IsTrue(c.Done))
               .Run(TimeSpan.FromMinutes(2));
        }

        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
        }

        class EndpointWithAuditOn : EndpointConfigurationBuilder
        {
            public EndpointWithAuditOn()
            {
                EndpointSetup<DefaultServer>(
                    b =>
                    {
                        b.GetSettings().Set("DisableOutboxTransportCheck", true);
                        b.EnableOutbox();
                        b.Pipeline.Register("BlowUpAfterDispatchBehavior", typeof(BlowUpAfterDispatchBehavior), "For testing");
                        b.ForwardReceivedMessagesTo("forward_receiver_outbox");
                    });
            }

            class BlowUpAfterDispatchBehavior : IBehavior<OutgoingContext>
            {
                public void Invoke(OutgoingContext context, Action next)
                {
                    string typeName;
                    context.OutgoingMessage.Headers.TryGetValue(Headers.EnclosedMessageTypes, out typeName);

                    if (!(typeName != null && typeName.Contains(typeof(MessageToBeForwarded).Name)))
                    {
                        return;
                    }

                    throw new SimulatedException();
                }

            }
            public class MessageToBeForwardedHandler : IHandleMessages<MessageToBeForwarded>
            {
                public void Handle(MessageToBeForwarded message)
                {
                }
            }
            /*
            public class MessageToBeAuditedHandler : IHandleMessages<MessageToBeForwarded>
            {
                public Context Context { get; set; }

                public void Handle(MessageToBeForwarded message)
                {
                    Context.Done = true;
                }
            }   */
        }

        //todo: why does this endpoint not get picked up
        public class ForwardingSpyEndpoint : EndpointConfigurationBuilder
        {
            public ForwardingSpyEndpoint()
            {
                EndpointSetup<DefaultServer>()
                    .CustomEndpointName("forward_receiver_outbox");
            }

            public class MessageToBeAuditedHandler : IHandleMessages<MessageToBeForwarded>
            {
                public Context Context { get; set; }

                public void Handle(MessageToBeForwarded message)
                {
                    Context.Done = true;
                }
            }
        }


        public class MessageToBeForwarded : IMessage
        {
            public string RunId { get; set; }
        }
    }
    public static class ConfigureForwarding
    {
        public static void ForwardReceivedMessagesTo(this BusConfiguration config, string address)
        {
            config.GetSettings().Set(SettingsKey, address);
        }

        internal const string SettingsKey = "forwardReceivedMessagesTo";
    }
}
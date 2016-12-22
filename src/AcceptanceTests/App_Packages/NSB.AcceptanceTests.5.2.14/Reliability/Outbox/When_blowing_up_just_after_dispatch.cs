using AcceptanceTests.App_Packages.NSB.AcceptanceTests._5._2._14;
using NServiceBus.Pipeline.Contexts;

namespace NServiceBus.AcceptanceTests.Reliability.Outbox
{
    using System;
    using System.Linq;
    using AcceptanceTesting;
    using Configuration.AdvanceExtensibility;
    using EndpointTemplates;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using ScenarioDescriptors;

    public class When_blowing_up_just_after_dispatch : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_still_release_the_outgoing_messages_to_the_transport()
        {
            Scenario.Define<Context>()
               .WithEndpoint<NonDtcReceivingEndpoint>(b => b.When(session => session.SendLocal(new PlaceOrder())))
               .Done(c => c.OrderAckReceived == 1)
               .Repeat(r => r.For<AllOutboxCapableStorages>())
               .Should(context => Assert.AreEqual(1, context.OrderAckReceived, "Order ack should have been received since outbox dispatch isn't part of the receive tx"))
               .Run(TimeSpan.FromSeconds(20));
        }

        public class Context : ScenarioContext
        {
            public int OrderAckReceived { get; set; }
        }

        public class NonDtcReceivingEndpoint : EndpointConfigurationBuilder
        {
            public NonDtcReceivingEndpoint()
            {
                EndpointSetup<DefaultServer>(
                    b =>
                    {
                        b.GetSettings().Set("DisableOutboxTransportCheck", true);
                        b.EnableOutbox();
                        b.Pipeline.Register("BlowUpAfterDispatchBehavior", typeof(BlowUpAfterDispatchBehavior), "For testing");
                    });
            }

            class BlowUpAfterDispatchBehavior : IBehavior<OutgoingContext>
            {
                private static bool _called;

                public void Invoke(OutgoingContext context, Action next)
                {
                    string typeName;
                    context.OutgoingMessage.Headers.TryGetValue(Headers.EnclosedMessageTypes, out typeName);

                    if (typeName != null && typeName.Contains(typeof(PlaceOrder).Name))
                    {
                        return;
                    }

                    if (_called)
                    {
                        Console.WriteLine("Called once, skipping next");
                        return;
                    }

                    _called = true;

                    throw new SimulatedException();
                }
            }

            public class PlaceOrderHandler : IHandleMessages<PlaceOrder>
            {
                public IBus Bus { get; set; }
                public void Handle(PlaceOrder message)
                {
                    Bus.SendLocal(new SendOrderAcknowledgment());
                }
            }

            public class SendOrderAcknowledgmentHandler : IHandleMessages<SendOrderAcknowledgment>
            {
                public Context Context { get; set; }

                public void Handle(SendOrderAcknowledgment message)
                {
                    Context.OrderAckReceived++;
                }
            }
        }

        public class PlaceOrder : ICommand
        {
        }

        public class SendOrderAcknowledgment : IMessage
        {
        }
    }
}
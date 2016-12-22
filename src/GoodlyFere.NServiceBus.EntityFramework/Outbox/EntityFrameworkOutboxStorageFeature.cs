using GoodlyFere.NServiceBus.EntityFramework.Interfaces;
using GoodlyFere.NServiceBus.EntityFramework.SagaStorage;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Logging;

namespace GoodlyFere.NServiceBus.EntityFramework.Outbox
{
    public class EntityFrameworkOutboxStorageFeature : Feature
    {
        private static readonly ILog Logger = LogManager.GetLogger<EntityFrameworkOutboxStorageFeature>();

        public EntityFrameworkOutboxStorageFeature()
        {
            DependsOn<global::NServiceBus.Features.Outbox>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            Logger.Debug("Configure Outbox");
            context.Container.ConfigureComponent<OutboxPersister>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(e =>e.EndpointName,context.Settings.EndpointName());

            RegisterStartupTask<OutboxCleaner>();
        }
    }
}
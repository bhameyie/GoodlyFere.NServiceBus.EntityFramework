using System;
using System.Configuration;
using System.Threading;
using NServiceBus.Features;

namespace GoodlyFere.NServiceBus.EntityFramework.Outbox
{
    internal class OutboxCleaner : FeatureStartupTask
    {
        private Timer _cleanupTimer;
        private TimeSpan _timeToKeepDeduplicationData;
        private TimeSpan _frequencyToRunDeduplicationDataCleanup;
        private readonly OutboxPersister _outboxPersister;

        public OutboxCleaner(OutboxPersister outboxPersister)
        {
            _outboxPersister = outboxPersister;
        }

        protected override void OnStart()
        {
            var configValue = ConfigurationManager.AppSettings.Get("NServiceBus/Outbox/EntityFramework/TimeToKeepDeduplicationData");

            if (configValue == null)
            {
                _timeToKeepDeduplicationData = TimeSpan.FromDays(7);
            }
            else if (!TimeSpan.TryParse(configValue, out _timeToKeepDeduplicationData))
            {
                throw new Exception("Invalid value in \"NServiceBus/Outbox/EntityFramework/TimeToKeepDeduplicationData\" AppSetting. Please ensure it is a TimeSpan.");
            }

            configValue = ConfigurationManager.AppSettings.Get("NServiceBus/Outbox/EntityFramework/FrequencyToRunDeduplicationDataCleanup");

            if (configValue == null)
            {
                _frequencyToRunDeduplicationDataCleanup = TimeSpan.FromMinutes(1);
            }
            else if (!TimeSpan.TryParse(configValue, out _frequencyToRunDeduplicationDataCleanup))
            {
                throw new Exception("Invalid value in \"NServiceBus/Outbox/EntityFramework/FrequencyToRunDeduplicationDataCleanup\" AppSetting. Please ensure it is a TimeSpan.");
            }

            _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(1), _frequencyToRunDeduplicationDataCleanup);
        }

        protected override void OnStop()
        {
            using (var waitHandle = new ManualResetEvent(false))
            {
                _cleanupTimer.Dispose(waitHandle);

                waitHandle.WaitOne();
            }
        }

        void PerformCleanup(object state)
        {
            _outboxPersister.RemoveEntriesOlderThan(DateTime.UtcNow - _timeToKeepDeduplicationData);
        }

    }
}
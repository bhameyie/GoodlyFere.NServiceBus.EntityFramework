﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects.DataClasses;
using System.Data.Entity.Infrastructure;
using System.Linq;
using GoodlyFere.NServiceBus.EntityFramework.Interfaces;
using GoodlyFere.NServiceBus.EntityFramework.Outbox;
using GoodlyFere.NServiceBus.EntityFramework.SharedDbContext;
using GoodlyFere.NServiceBus.EntityFramework.SubscriptionStorage;
using GoodlyFere.NServiceBus.EntityFramework.TimeoutStorage;
using NServiceBus.Saga;

namespace AcceptanceTests
{
    public class TestDbContext : ISubscriptionDbContext, ITimeoutDbContext, ISagaDbContext, IOutboxDbContext
    {
        private readonly TestBaseDbContext _baseDbContext;
        private readonly Dictionary<Type, DbContext> _dbContexts;
        private bool _alreadyCalled;

        public TestDbContext()
        {
            _dbContexts = new Dictionary<Type, DbContext>();
            _baseDbContext = new TestBaseDbContext("TestDbContext");
        }

        public void Dispose()
        {
            foreach (var dbContext in _dbContexts.Values)
            {
                dbContext.Dispose();
            }

            _baseDbContext.Dispose();
        }

        public Database Database
        {
            get
            {
                return _baseDbContext.Database;
            }
        }

        public DbEntityEntry Entry(object entity)
        {
            Type entityType = GetRealEntityType(entity);

            if (IsSagaDataType(entityType) && _dbContexts.ContainsKey(entityType))
            {
                return _dbContexts[entityType].Entry(entity);
            }
            return _baseDbContext.Entry(entity);
        }

        public DbEntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
        {
            Type entityType = GetRealEntityType(entity);

            if (IsSagaDataType(entityType) && _dbContexts.ContainsKey(entityType))
            {
                return _dbContexts[entityType].Entry(entity);
            }
            return _baseDbContext.Entry(entity);
        }

        public int SaveChanges()
        {
            int returnVal = 0;
            if (_alreadyCalled)
            {
                return returnVal;
            }

            _alreadyCalled = true;

            foreach (var dbContext in _dbContexts.Values.Where(dbc => dbc.ChangeTracker.HasChanges()))
            {
                returnVal += dbContext.SaveChanges();
            }

            if (_baseDbContext.ChangeTracker.HasChanges())
            {
                returnVal += _baseDbContext.SaveChanges();
            }

            return returnVal;
        }

        public DbSet<TEntity> Set<TEntity>() where TEntity : class
        {
            Type entityType = typeof(TEntity);

            if (IsSagaDataType(entityType) && _dbContexts.ContainsKey(entityType))
            {
                return _dbContexts[entityType].Set<TEntity>();
            }
            return _baseDbContext.Set<TEntity>();
        }

        public DbSet Set(Type entityType)
        {
            if (IsSagaDataType(entityType))
            {
                DbContext dbContext;

                if (_dbContexts.ContainsKey(entityType))
                {
                    dbContext = _dbContexts[entityType];
                }
                else
                {
                    Type dbContextType = typeof(TestGenericDbContext<>).MakeGenericType(entityType);
                    dbContext = (DbContext)Activator.CreateInstance(dbContextType, "TestDbContext");
                    _dbContexts.Add(entityType, dbContext);
                }

                return dbContext.Set(entityType);
            }
            return _baseDbContext.Set(entityType);
        }

        public bool HasSet(Type entityType)
        {
            return _baseDbContext.HasSet(entityType) || _dbContexts.ContainsKey(entityType);
        }

        public DbSet<SubscriptionEntity> Subscriptions
        {
            get
            {
                return _baseDbContext.Subscriptions;
            }
            set
            {
                _baseDbContext.Subscriptions = value;
            }
        }

        public DbSet<TimeoutDataEntity> Timeouts
        {
            get
            {
                return _baseDbContext.Timeouts;
            }
            set
            {
                _baseDbContext.Timeouts = value;
            }
        }

        public DbSet<OutboxEntity> OutboxRecords
        {
            get { return _baseDbContext.OutboxRecords; }
            set { _baseDbContext.OutboxRecords = value; }
        }

        private static Type GetRealEntityType(object saga)
        {
            Type sagaType = saga.GetType();

            // if this class is the dynamic proxy type inserted by EF
            // then we want the base type (actual saga data type) not the dynamic proxy
            if (typeof(IEntityWithChangeTracker).IsAssignableFrom(sagaType))
            {
                sagaType = sagaType.BaseType;
            }

            return sagaType;
        }

        private static bool IsSagaDataType(Type entityType)
        {
            return typeof(IContainSagaData).IsAssignableFrom(entityType);
        }

    }

    public class TestBaseDbContext : NServiceBusDbContext
    {
        public TestBaseDbContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<TestBaseDbContext>());
        }
    }

    public class TestGenericDbContext<TSagaDataType> : DbContext
        where TSagaDataType : class, IContainSagaData
    {
        public TestGenericDbContext(string connectionString)
            : base(connectionString)
        {
        }

        public DbSet<TSagaDataType> SagaData { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var type = typeof(TSagaDataType);
            string tableName = type.FullName.Replace(".", "_").Replace("+", "_");

            modelBuilder.Entity<TSagaDataType>()
                .ToTable(tableName);

            base.OnModelCreating(modelBuilder);
        }
    }
}
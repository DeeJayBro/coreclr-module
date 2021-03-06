﻿using System;
using System.Collections.Generic;
using System.Numerics;
using AltV.Net.EntitySync.Events;
using AltV.Net.EntitySync.SpatialPartitions;

namespace AltV.Net.EntitySync
{
    /// <summary>
    /// The sync server creates the threads, the client repository and entity repositories.
    /// It transmits the streamer callbacks to the network layer.
    /// It will also transfer callbacks from network layer to streamer, for e.g. updating entity position when net owner is implemented.
    /// </summary>
    public class EntitySyncServer
    {
        private readonly EntityThread[] entityThreads;

        private readonly EntityThreadRepository[] entityThreadRepositories;
        
        private readonly ClientThreadRepository[] clientThreadRepositories;

        private readonly IEntityRepository entityRepository;

        private readonly IClientRepository clientRepository;

        private readonly NetworkLayer networkLayer;

        private readonly IIdProvider<ulong> idProvider;
        
        internal readonly LinkedList<EntityCreateDelegate> EntityCreateCallbacks = new LinkedList<EntityCreateDelegate>();
        
        internal readonly LinkedList<EntityRemoveDelegate> EntityRemoveCallbacks = new LinkedList<EntityRemoveDelegate>();

        public EntitySyncServer(long threadCount, int syncRate,
            Func<IClientRepository, NetworkLayer> createNetworkLayer,
            Func<SpatialPartition> createSpatialPartition, IIdProvider<ulong> idProvider)
        {
            if (threadCount < 1)
            {
                throw new ArgumentException("threadCount must be >= 1");
            }

            entityThreads = new EntityThread[threadCount];
            entityThreadRepositories = new EntityThreadRepository[threadCount];
            clientThreadRepositories = new ClientThreadRepository[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                var clientThreadRepository = new ClientThreadRepository();
                var entityThreadRepository = new EntityThreadRepository();
                entityThreadRepositories[i] = entityThreadRepository;
                clientThreadRepositories[i] = clientThreadRepository;
                entityThreads[i] = new EntityThread(entityThreadRepository, clientThreadRepository, createSpatialPartition(),syncRate,
                    OnEntityCreate,
                    OnEntityRemove, OnEntityDataChange, OnEntityPositionChange, OnEntityClearCache);
            }

            entityRepository = new EntityRepository(entityThreadRepositories);
            clientRepository = new ClientRepository(clientThreadRepositories);
            networkLayer = createNetworkLayer(clientRepository);
            networkLayer.OnConnectionConnect += OnConnectionConnect;
            networkLayer.OnConnectionDisconnect += OnConnectionDisconnect;
            this.idProvider = idProvider;
        }

        private void OnConnectionConnect(IClient client)
        {
            //clientRepository.Add(client);
        }

        private void OnConnectionDisconnect(IClient client)
        {
            //clientRepository.Remove(client);
        }

        private void OnEntityCreate(IClient client, IEntity entity, LinkedList<string> changedKeys)
        {
            Dictionary<string, object> data;
            if (changedKeys != null) {
                data = new Dictionary<string, object>();
                var changedKey = changedKeys.First;
                while (changedKey != null)
                {
                    var key = changedKey.Value;
                    if (entity.TryGetData(key, out var value))
                    {
                        data[key] = value;
                    }
                    else
                    {
                        data[key] = null;
                    }
                    changedKey = changedKey.Next;
                }
            }
            else
            {
                data = null;
            }
            networkLayer.SendEvent(client, new EntityCreateEvent(entity, data));

            var callback = EntityCreateCallbacks.First;
            while (callback != null)
            {
                callback.Value(client, entity);
                callback = callback.Next;
            }
        }

        private void OnEntityRemove(IClient client, IEntity entity)
        {
            networkLayer.SendEvent(client, new EntityRemoveEvent(entity));
            var callback = EntityRemoveCallbacks.First;
            while (callback != null)
            {
                callback.Value(client, entity);
                callback = callback.Next;
            }
        }

        private void OnEntityDataChange(IClient client, IEntity entity, LinkedList<string> changedKeys)
        {
            Dictionary<string, object> data;
            if (changedKeys != null) {
                data = new Dictionary<string, object>();
                var changedKey = changedKeys.First;
                while (changedKey != null)
                {
                    var key = changedKey.Value;
                    if (entity.TryGetData(key, out var value))
                    {
                        data[key] = value;
                    }
                    else
                    {
                        data[key] = null;
                    }
                    changedKey = changedKey.Next;
                }
            }
            else
            {
                data = null;
            }
            networkLayer.SendEvent(client, new EntityDataChangeEvent(entity, data));
        }

        private void OnEntityPositionChange(IClient client, IEntity entity, Vector3 newPosition)
        {
            networkLayer.SendEvent(client, new EntityPositionUpdateEvent(entity, newPosition));
        }

        private void OnEntityClearCache(IClient client, IEntity entity)
        {
            networkLayer.SendEvent(client, new EntityClearCacheEvent(entity));
        }

        public IEntity CreateEntity(ulong type, Vector3 position, int dimension, uint range, IDictionary<string, object> data)
        {
            var entity = new Entity(idProvider.GetNext(), type, position, dimension, range, data);
            AddEntity(entity);
            return entity;
        }

        public void AddEntity(IEntity entity)
        {
            entityRepository.Add(entity);
        }

        public void RemoveEntity(IEntity entity)
        {
            entityRepository.Remove(entity);
            idProvider.Free(entity.Id);
        }
        
        public void UpdateEntity(IEntity entity)
        {
            entityRepository.Update(entity);
        }

        public bool TryGetEntity(ulong id, out IEntity entity)
        {
            return entityRepository.TryGet(id, out entity);
        }

        public IEnumerable<IEntity> GetAllEntities()
        {
            return entityRepository.GetAll();
        }

        public void Stop()
        {
            foreach (var entityThread in entityThreads)
            {
                entityThread.Stop();
            }
        }
    }
}
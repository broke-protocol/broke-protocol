using BrokeProtocol.API;
using BrokeProtocol.API.Types;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Jobs;
using BrokeProtocol.LiteDB;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Manager
    {
        public List<ShPlayer> skinPrefabs;

        [Target(GameSourceEvent.ManagerStart, ExecutionMode.Override)]
        public void OnStart(SvManager svManager) {
            
            var skins = new HashSet<string>();
            svManager.ParseFile(ref skins, Paths.AbsolutePath("skins.txt"));
            skinPrefabs = skins.ToEntityList<ShPlayer>();


            var waypointTypes = Enum.GetValues(typeof(WaypointType)).Length;

            for (byte jobIndex = 0; jobIndex < BPAPI.Instance.Jobs.Count; jobIndex++)
            {
                var job = BPAPI.Instance.Jobs[jobIndex];

                job.randomEntities = new HashSet<ShEntity>[waypointTypes];

                for (int i = 0; i < waypointTypes; i++)
                {
                    job.randomEntities[i] = new HashSet<ShEntity>();
                }

                int count = 0;
                int limit = 0;
                while (count < job.poolSize && limit < 100)
                {
                    var randomSkin = skinPrefabs.GetRandom();

                    if (job.characterType == CharacterType.All || randomSkin.characterType == job.characterType)
                    {
                        svManager.AddRandomSpawn(randomSkin, jobIndex, (int)WaypointType.Player);
                        count++;
                    }
                    else
                    {
                        limit++;
                    }
                }

                int waypointIndex = 1; // PlayerWaypoints has no transports
                foreach (var transportArray in job.transports)
                {
                    if (transportArray.transports.Length > 0)
                    {
                        for (int i = 0; i < job.poolSize; i++)
                        {
                            if (SceneManager.Instance.TryGetEntity<ShTransport>(transportArray.transports.GetRandom(), out var t))
                                svManager.AddRandomSpawn(t, jobIndex, waypointIndex);
                        }
                    }
                    waypointIndex++;
                }
            }
        }

        //[Target(GameSourceEvent.ManagerUpdate, ExecutionMode.Override)]
        //public void OnUpdate(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerFixedUpdate, ExecutionMode.Override)]
        //public void OnFixedUpdate(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerConsoleInput, ExecutionMode.Override)]
        //public void OnConsoleInput(SvManager svManager, string cmd) { }

        [Target(GameSourceEvent.ManagerTryLogin, ExecutionMode.Override)]
        public void OnTryLogin(SvManager svManager, ConnectionData connectData)
        {
            if (ValidateUser(svManager, connectData))
            {
                if (!svManager.TryGetUserData(connectData.username, out var playerData))
                {
                    svManager.RegisterFail(connectData.connection, "Account not found - Please Register");
                    return;
                }

                if (playerData.PasswordHash != connectData.passwordHash)
                {
                    svManager.RegisterFail(connectData.connection, "Invalid credentials");
                    return;
                }

                svManager.LoadSavedPlayer(playerData, connectData);
            }
        }


        [Target(GameSourceEvent.ManagerTryRegister, ExecutionMode.Override)]
        public void OnTryRegister(SvManager svManager, ConnectionData connectData)
        {
            if (ValidateUser(svManager, connectData))
            {
                if (svManager.TryGetUserData(connectData.username, out var playerData))
                {
                    if (playerData.PasswordHash != connectData.passwordHash)
                    {
                        svManager.RegisterFail(connectData.connection, "Invalid credentials");
                        return;
                    }

                    if (!Utility.tryRegister.Limit(connectData.username))
                    {
                        svManager.RegisterFail(connectData.connection, $"Character {connectData.username} Exists - Sure you want to Register?");
                        return;
                    }
                }

                if (!connectData.username.ValidCredential())
                {
                    svManager.RegisterFail(connectData.connection, $"Name cannot be registered (min: {Util.minCredential}, max: {Util.maxCredential})");
                    return;
                }

                if(connectData.skinIndex >= 0 && connectData.skinIndex < skinPrefabs.Count && connectData.wearableIndices?.Length == svManager.manager.nullWearable.Length)
                    svManager.AddNewPlayer(skinPrefabs[connectData.skinIndex], connectData, playerData?.Persistent);
                else
                    svManager.RegisterFail(connectData.connection, "Invalid data");
            }
        }

        [Target(GameSourceEvent.ManagerSave, ExecutionMode.Override)]
        public void OnSave(SvManager svManager)
        {
            var bountyData = new Data
            {
                ID = Hitman.bountiesKey
            };
            foreach (var bounty in Hitman.bounties)
            {
                // Only save bounties targeting Humans
                if (!Hitman.aiTarget || Hitman.aiTarget.username != bounty.Key)
                {
                    bountyData.CustomData[bounty.Key] = bounty.Value;
                }
            }
            svManager.database.Data.Upsert(bountyData);

            ChatHandler.SendToAll("Saving server status..");
            foreach (var player in EntityCollections.Humans)
            {
                player.svPlayer.Save();
            }
            svManager.database.WriteOut();
        }

        [Target(GameSourceEvent.ManagerLoad, ExecutionMode.Override)]
        public void OnLoad(SvManager svManager)
        {
            var bountyData = svManager.database.Data.FindById(Hitman.bountiesKey);

            if (bountyData != null)
            {
                foreach (var bounty in bountyData.CustomData.Data)
                {
                    Hitman.bounties.Add(bounty.Key, CustomData.ConvertData<DateTimeOffset>(bounty.Value));
                }
            }
        }

        [Target(GameSourceEvent.ManagerReadGroups, ExecutionMode.Override)]
        public void OnReadGroups()
        {
            try
            {
                var groups = JsonConvert.DeserializeObject<List<Group>>(File.ReadAllText(Paths.groupsFile));
                GroupManager.Groups = groups.ToDictionary(x => x.Name, y => y);
            }
            catch(Exception e)
            {
                Debug.Log("[SVR] Error reading groups file: " + e);
            }
        }

        [Target(GameSourceEvent.ManagerPlayerLoaded, ExecutionMode.Override)]
        public void OnPlayerLoaded(SvManager svManager, ConnectionData connectData)
        {
            connectData.connectionStatus = ConnectionStatus.LoadedMap;
            Buffers.writer.SeekZero();
            Buffers.writer.WriteClPacket(ClPacket.RegisterMenu);
            Buffers.writer.Write(skinPrefabs.Count);
            foreach (var p in skinPrefabs)
            {
                Buffers.writer.Write(p.name);
            }
            SvManager.SendToConnection(connectData.connection, Channel.Reliable);
        }

        private bool ValidateUser(SvManager svManager, ConnectionData connectData)
        {
            if (!svManager.HandleWhitelist(connectData.username))
            {
                svManager.RegisterFail(connectData.connection, "Account not whitelisted");
                return false;
            }

            // Don't allow multi-boxing, WebAPI doesn't prevent this
            if (EntityCollections.Accounts.ContainsKey(connectData.username))
            {
                svManager.RegisterFail(connectData.connection, "Account still logged in");
                return false;
            }

            return true;
        }
    }
}

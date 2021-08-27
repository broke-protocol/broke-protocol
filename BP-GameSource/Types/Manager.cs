using BrokeProtocol.API;
using BrokeProtocol.API.Types;
using BrokeProtocol.Collections;
using BrokeProtocol.GameSource.Jobs;
using BrokeProtocol.LiteDB;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
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
        //[Target(GameSourceEvent.ManagerStart, ExecutionMode.Override)]
        //public void OnStart(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerUpdate, ExecutionMode.Override)]
        //public void OnUpdate(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerFixedUpdate, ExecutionMode.Override)]
        //public void OnFixedUpdate(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerConsoleInput, ExecutionMode.Override)]
        //public void OnConsoleInput(SvManager svManager, string cmd) { }

        [Target(GameSourceEvent.ManagerTryLogin, ExecutionMode.Override)]
        public void OnTryLogin(SvManager svManager, ConnectionData connectionData)
        {
            if (ValidateUser(svManager, connectionData))
            {
                if (!svManager.TryGetUserData(connectionData.username, out User playerData))
                {
                    svManager.RegisterFail(connectionData.connection, "Account not found - Please Register");
                    return;
                }

                if (playerData.PasswordHash != connectionData.passwordHash)
                {
                    svManager.RegisterFail(connectionData.connection, "Invalid credentials");
                    return;
                }

                svManager.LoadSavedPlayer(playerData, connectionData);
            }
        }

        [Target(GameSourceEvent.ManagerTryRegister, ExecutionMode.Override)]
        public void OnTryRegister(SvManager svManager, ConnectionData connectionData)
        {
            if (ValidateUser(svManager, connectionData))
            {
                if (svManager.TryGetUserData(connectionData.username, out User playerData))
                {
                    if (playerData.PasswordHash != connectionData.passwordHash)
                    {
                        svManager.RegisterFail(connectionData.connection, "Invalid credentials");
                        return;
                    }

                    if (!Utility.tryRegister.Limit(connectionData.username))
                    {
                        svManager.RegisterFail(connectionData.connection, $"Character {connectionData.username} Exists - Sure you want to Register?");
                        return;
                    }
                }

                if (!connectionData.username.ValidCredential())
                {
                    svManager.RegisterFail(connectionData.connection, $"Name cannot be registered (min: {Util.minCredential}, max: {Util.maxCredential})");
                    return;
                }

                svManager.AddNewPlayer(connectionData, playerData?.Persistent);
            }
        }

        [Target(GameSourceEvent.ManagerSave, ExecutionMode.Override)]
        public void OnSave(SvManager svManager)
        {
            var bountyData = new Data();
            bountyData.ID = Hitman.bountiesKey;
            foreach(var bounty in Hitman.bounties)
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
            var groups = JsonConvert.DeserializeObject<List<Group>>(File.ReadAllText(Paths.groupsFile));
            if (groups == null)
            {
                Debug.Log("[SVR] Groups file has an error");
                return;
            }

            GroupManager.Groups = groups.ToDictionary(x => x.Name, y => y);
        }

        private bool ValidateUser(SvManager svManager, ConnectionData connectionData)
        {
            if (!svManager.HandleWhitelist(connectionData.username))
            {
                svManager.RegisterFail(connectionData.connection, "Account not whitelisted");
                return false;
            }

            // Don't allow multi-boxing, WebAPI doesn't prevent this
            if (EntityCollections.Accounts.ContainsKey(connectionData.username))
            {
                svManager.RegisterFail(connectionData.connection, "Account still logged in");
                return false;
            }

            return true;
        }
    }
}

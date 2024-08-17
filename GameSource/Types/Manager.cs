using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{

    public class Manager : ManagerEvents
    {
        public static Dictionary<ShEntity, GameSourceEntity> pluginEntities = new();
        public static Dictionary<ShEntity, GameSourcePlayer> pluginPlayers = new();

        public static List<ShPlayer> skinPrefabs;

        [NonSerialized]
        public static List<SpawnLocation> spawnLocations = new();
        [NonSerialized]
        public static List<ShTerritory> territories = new();

        public static bool TryGetTerritory(ShEntity entity, out ShTerritory territory)
        {
            const float extent = 0.5f;
            var pos = entity.mainT.position;

            foreach (var t in territories)
            {
                var localPos = t.mainT.InverseTransformPoint(pos);

                if (localPos.x < extent &&
                    localPos.x > -extent &&
                    localPos.y < extent &&
                    localPos.y > -extent &&
                    localPos.z < extent &&
                    localPos.z > -extent)
                {
                    territory = t;
                    return true;
                }
            }

            territory = null;
            return false;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Start()
        {
            var skins = new HashSet<string>();
            SvManager.Instance.ParseFile(ref skins, Paths.AbsolutePath("skins.txt"));
            skinPrefabs = skins.ToEntityList<ShPlayer>();

            foreach (Transform place in SceneManager.Instance.mTransform)
            {
                foreach (Transform child in place)
                {
                    if (child.TryGetComponent(out SpawnLocation s)) spawnLocations.Add(s);
                    else if (child.TryGetComponent(out ShTerritory t)) territories.Add(t);
                }
            }

            if (spawnLocations.Count == 0) Util.Log("No spawn locations found", LogLevel.Warn);
            if (territories.Count == 0) Util.Log("No territories found", LogLevel.Warn);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TryLogin(ConnectData connectData)
        {
            if (ValidateUser(connectData))
            {
                if (!SvManager.Instance.TryGetUserData(connectData.username, out var playerData))
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} not found - Please Register");
                }
                else if (playerData.PasswordHash != connectData.passwordHash)
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Invalid credentials for Account {connectData.username}");
                }
                else
                {
                    SvManager.Instance.LoadSavedPlayer(playerData, connectData);
                    return true;
                }
            }

            return false;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TryRegister(ConnectData connectData)
        {
            if (ValidateUser(connectData))
            {
                if (SvManager.Instance.TryGetUserData(connectData.username, out var playerData))
                {
                    if (playerData.PasswordHash != connectData.passwordHash)
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, $"Invalid credentials for Account {connectData.username}");
                        return false;
                    }

                    if (!Utility.accountWipe.Limit(connectData.username))
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} exists - Sure you want to Register?");
                        return false;
                    }
                }

                if (!connectData.username.ValidCredential())
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} cannot be registered (check length and characters)");
                }
                else if (connectData.skinIndex >= 0 && connectData.skinIndex < skinPrefabs.Count && connectData.wearableIndices?.Length == ShManager.Instance.nullWearable.Length)
                {
                    var spawn = spawnLocations.GetRandom();

                    if (spawn)
                    {
                        var location = spawn.mainT;
                        SvManager.Instance.AddNewPlayer(skinPrefabs[connectData.skinIndex], connectData, playerData?.Persistent, location.position, location.rotation, location.parent);
                        return true;
                    }
                    else
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, "No spawn locations");
                    }
                }
                else
                {
                    SvManager.Instance.RegisterFail(connectData.connection, "Invalid data");
                }
            }

            return false;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TryDelete(ConnectData connectData)
        {
            if (ValidateUser(connectData))
            {
                if (!SvManager.Instance.TryGetUserData(connectData.username, out var playerData))
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} not found - Please Register");
                }
                else if (playerData.PasswordHash != connectData.passwordHash)
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Invalid credentials for Account {connectData.username}");
                }
                else if (!Utility.accountWipe.Limit(connectData.username))
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Sure you want to delete {connectData.username} account?");
                }
                else if (SvManager.Instance.database.Users.Delete(connectData.username))
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} deleted");
                    return true;
                }
                else
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} deletion error");
                }
            }

            return false;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool PlayerLoaded(ConnectData connectData)
        {
            SvManager.Instance.SendRegisterMenu(connectData.connection, true, skinPrefabs);
            return true;
        }

        private bool ValidateUser(ConnectData connectData)
        {
            if (!SvManager.Instance.HandleWhitelist(connectData.username))
            {
                SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} not whitelisted");
                return false;
            }

            // Don't allow multi-boxing, WebAPI doesn't prevent this
            if (EntityCollections.Accounts.ContainsKey(connectData.username))
            {
                SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} still logged in");
                return false;
            }

            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool PrepareMap()
        {
            // Prepare the map and mapHash before navmesh generation and static analysis

            // MapHash is used for caching on both server and clients
            SceneManager.Instance.mapHash = SceneManager.Instance.mapData.GetChecksum();
            // Modify this (or keep static) to determine whether a new navmesh is generated or not

            return true;
        }
    }
}

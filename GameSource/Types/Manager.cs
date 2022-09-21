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
        public static Dictionary<ShEntity, GameSourcePlayer> pluginPlayers = new Dictionary<ShEntity, GameSourcePlayer>();

        public static List<ShPlayer> skinPrefabs;

        [NonSerialized]
        public static List<SpawnLocation> spawnLocations = new List<SpawnLocation>();
        [NonSerialized]
        public static List<ShTerritory> territories = new List<ShTerritory>();

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

            if (spawnLocations.Count == 0) Debug.LogWarning("[SVR] No spawn locations found");
            if (territories.Count == 0) Debug.LogWarning("[SVR] No territories found");

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TryLogin(ConnectData connectData)
        {
            if (ValidateUser(connectData))
            {
                if (!SvManager.Instance.TryGetUserData(connectData.username, out var playerData))
                {
                    SvManager.Instance.RegisterFail(connectData.connection, "Account not found - Please Register");
                }
                else if (playerData.PasswordHash != connectData.passwordHash)
                {
                    SvManager.Instance.RegisterFail(connectData.connection, "Invalid credentials");
                }
                else
                {
                    SvManager.Instance.LoadSavedPlayer(playerData, connectData);
                }
            }

            return true;
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
                        SvManager.Instance.RegisterFail(connectData.connection, "Invalid credentials");
                        return true;
                    }

                    if (!Utility.tryRegister.Limit(connectData.username))
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, $"Character {connectData.username} Exists - Sure you want to Register?");
                        return true;
                    }
                }

                if (!connectData.username.ValidCredential())
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Name cannot be registered (min: {Util.minCredential}, max: {Util.maxCredential})");
                }
                else if (connectData.skinIndex >= 0 && connectData.skinIndex < skinPrefabs.Count && connectData.wearableIndices?.Length == ShManager.Instance.nullWearable.Length)
                {
                    var spawn = spawnLocations.GetRandom();

                    if (spawn)
                    {
                        var location = spawn.mainT;
                        SvManager.Instance.AddNewPlayer(skinPrefabs[connectData.skinIndex], connectData, playerData?.Persistent, location.position, location.rotation, location.parent);
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

            return true;
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
                SvManager.Instance.RegisterFail(connectData.connection, "Account not whitelisted");
                return false;
            }

            // Don't allow multi-boxing, WebAPI doesn't prevent this
            if (EntityCollections.Accounts.ContainsKey(connectData.username))
            {
                SvManager.Instance.RegisterFail(connectData.connection, "Account still logged in");
                return false;
            }

            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool PrepareMap()
        {
            // Prepare the map and mapHash before navmesh generation and static analysis

            // Clone all apartments up to the max playerCount (needed for proper AI navigation)
            var playerCount = SvManager.Instance.settings.players;
            foreach (var apartment in SvManager.Instance.apartments)
            {
                SceneManager.Instance.CloneInterior(apartment.Key, playerCount);
            }

            // MapHash is used for caching on both server and clients
            SceneManager.Instance.mapHash = SceneManager.Instance.mapData.GetChecksum() + playerCount;
            // Since playerCounts will affect the navmesh (see loop above) it's included as part of the mapHash

            return true;
        }
    }
}

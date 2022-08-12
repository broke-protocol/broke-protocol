using BrokeProtocol.API;
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Spawn
    {
        public Vector3 position;
        public Quaternion rotation;
        public Waypoint nextWaypoint;

        public Spawn(Vector3 position, Quaternion rotation, Waypoint nextWaypoint)
        {
            this.position = position;
            this.rotation = rotation;
            this.nextWaypoint = nextWaypoint;
        }
    }

    public class WaypointGroup
    {
        private WaypointType waypointType;
        private float spawnRate;

        public readonly Dictionary<ValueTuple<int, Vector2Int>, List<Spawn>> spawns = new Dictionary<ValueTuple<int, Vector2Int>, List<Spawn>>();


        public WaypointGroup(WaypointType waypointType, float spawnRate)
        {
            this.waypointType = waypointType;
            this.spawnRate = spawnRate;
        }

        private List<Waypoint> GetWaypointArray => SceneManager.Instance.ExteriorPlace.waypoints[waypointType];

        private float AdjustedSpawnRate(Vector2Int position, float limit, WaypointType type)
        {
            if (SvManager.Instance.sectors.TryGetValue((SceneManager.Instance.ExteriorT.GetHashCode(), position), out var sector))
            {
                var count = sector.AreaTypeCount(type);

                return (1f - (count / limit)) * spawnRate;
            }

            return spawnRate;
        }

        public void Initialize()
        {
            const float spawnGap = 12f;

            foreach (var node in GetWaypointArray)
            {
                foreach (var neighbor in node.neighbors)
                {
                    var delta = node.Delta(neighbor);
                    var ray = new Ray(node.mainT.position, delta);
                    float distance = delta.magnitude;

                    for (var i = spawnGap; i < distance - spawnGap; i += spawnGap)
                    {
                        var spawnPosition = ray.GetPoint(i);

                        var floor = SvManager.Instance.GetSectorFloor(spawnPosition);

                        var tuple = (SceneManager.Instance.ExteriorT.GetHashCode(), floor);
                        if (!spawns.ContainsKey(tuple))
                        {
                            spawns[tuple] = new List<Spawn>();
                        }

                        spawns[tuple].Add(new Spawn(spawnPosition, Quaternion.LookRotation(ray.direction), neighbor));
                    }
                }
            }
        }

        public void SpawnRandom(ShPlayer spawner, Sector sector)
        {
            if (spawns.TryGetValue((SceneManager.Instance.ExteriorT.GetHashCode(), sector.position), out var sectorSpawns))
            {
                if (waypointType == WaypointType.Player)
                {
                    foreach (var s in sectorSpawns)
                    {
                        if (UnityEngine.Random.value < AdjustedSpawnRate(sector.position, 16, waypointType))
                        {
                            var spawnEntity = Manager.GetAvailable(spawner, s.position, out _, waypointType);

                            if (spawnEntity)
                            {
                                var spawnBot = spawnEntity.Player;

                                if (spawnBot)
                                {
                                    spawnBot.svPlayer.SpawnBot(
                                        s.position,
                                        s.rotation,
                                        SceneManager.Instance.ExteriorPlace,
                                        s.nextWaypoint,
                                        spawner,
                                        null,
                                        null);
                                }
                            }
                        }
                    }
                }
                else
                {
                    //VehicleWaypointGroup
                    foreach (var s in sectorSpawns)
                    {
                        if (UnityEngine.Random.value < AdjustedSpawnRate(sector.position, 8, waypointType))
                        {
                            var spawnEntity = Manager.GetAvailable(spawner, s.position, out var jobIndex, WaypointType.Player);

                            if (spawnEntity)
                            {
                                var spawnBot = spawnEntity.Player;
                                if (spawnBot && spawnBot.characterType == CharacterType.Human && ((MyJobInfo)BPAPI.Jobs[jobIndex]).transports[((int)waypointType) - 1].transports.Length > 0)
                                {
                                    var spawnTransport = Manager.GetAvailable(jobIndex, waypointType) as ShTransport;

                                    if (spawnTransport && spawnTransport.CanSpawn(s.position, s.rotation))
                                    {
                                        spawnTransport.Spawn(s.position, s.rotation, SceneManager.Instance.ExteriorT);
                                        spawnTransport.SetVelocity(0.5f * spawnTransport.maxSpeed * spawnTransport.GetPositionT.forward);
                                        spawnBot.svPlayer.SpawnBot(
                                            s.position,
                                            s.rotation,
                                            SceneManager.Instance.ExteriorPlace,
                                            s.nextWaypoint,
                                            spawner,
                                            spawnTransport,
                                            null);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }


    public class Manager : ManagerEvents
    {
        public static readonly WaypointGroup[] worldWaypoints = new WaypointGroup[]
        {
            new WaypointGroup(WaypointType.Player, 0.08f),
            new WaypointGroup(WaypointType.Vehicle, 0.06f),
            new WaypointGroup(WaypointType.Aircraft, 0.003f),
            new WaypointGroup(WaypointType.Boat, 0.01f),
        };

        public static Dictionary<ShPlayer, PluginPlayer> pluginPlayers = new Dictionary<ShPlayer, PluginPlayer>();

        public List<ShPlayer> skinPrefabs;

        [NonSerialized]
        public static List<SpawnLocation> spawnLocations = new List<SpawnLocation>();
        [NonSerialized]
        public static List<ServerTrigger> jails = new List<ServerTrigger>();
        [NonSerialized]
        public static List<ShTerritory> territories = new List<ShTerritory>();

        private static float endTime;
        private static int attackerLimit;
        public static int defenderLimit;
        public static int attackersKilled;
        public static int defendersKilled;
        public static ShTerritory warTerritory;

        public void AddRandomSpawn<T>(T prefab, int randomJobIndex) where T : ShEntity
        {
            T newEntity = GameObject.Instantiate(prefab, SceneManager.Instance.ExteriorT);
            newEntity.name = prefab.name;
            newEntity.svEntity.randomSpawn = true;

            if (newEntity is ShPlayer player)
            {
                player.svPlayer.spawnJobIndex = randomJobIndex;
                player.svPlayer.spawnJobRank = UnityEngine.Random.Range(0, BPAPI.Jobs[randomJobIndex].upgrades.Length);
            }

            SvManager.Instance.AddNewEntityExisting(newEntity);

            ((MyJobInfo)BPAPI.Jobs[randomJobIndex]).randomEntities[(int)newEntity.svEntity.WaypointProperty].Add(newEntity);
        }

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

        public static void StartGangWar(ShTerritory startT, int attackerJob)
        {
            if (!warTerritory)
            {
                warTerritory = startT;

                var total = 0;
                var count = 0;
                foreach (var t in territories)
                {
                    if (t.ownerIndex == attackerJob)
                    {
                        count++;
                    }
                    total++;
                }

                endTime = Time.time + 300f;
                attackersKilled = 0;
                defendersKilled = 0;
                defenderLimit = 2 + count;
                attackerLimit = Mathf.CeilToInt((total - count) * 0.25f);

                var jobs = BPAPI.Jobs;
                ChatHandler.SendToAll($"{jobs[attackerJob].shared.jobName} Attacking {jobs[startT.ownerIndex].shared.jobName}");

                startT.svTerritory.SvSetTerritory(startT.ownerIndex, attackerJob);
                startT.StartCoroutine(Defend());

                SendTerritoryStats();
            }
        }

        public static void SendTerritoryStats()
        {
            warTerritory.svEntity.Send(SvSendType.All, Channel.Reliable, ClPacket.GameMessage, 
                $"Defender lives: {defenderLimit - defendersKilled} Attacker lives: {attackerLimit - attackersKilled} Timeleft: {(int)(endTime - Time.time)}");
        }

        public static void EndGangWar(int owner)
        {
            var jobs = BPAPI.Jobs;
            if (warTerritory.ownerIndex == owner)
            {
                ChatHandler.SendToAll(
                    $"{jobs[owner].shared.jobName} Defended Against {jobs[warTerritory.attackerIndex].shared.jobName}");
            }
            else
            {
                ChatHandler.SendToAll(
                    $"{jobs[owner].shared.jobName} Won Against {jobs[warTerritory.ownerIndex].shared.jobName}");
            }

            warTerritory.svTerritory.SvSetTerritory(owner);
            warTerritory = null;
        }

        private static IEnumerator Defend()
        {
            var delay = new WaitForSeconds(0.5f);
            while (Time.time < endTime)
            {
                if (attackersKilled >= attackerLimit)
                {
                    EndGangWar(warTerritory.ownerIndex);
                    yield break;
                }
                else if (defendersKilled >= defenderLimit)
                {
                    EndGangWar(warTerritory.attackerIndex);
                    yield break;
                }
                yield return delay;
            }
            EndGangWar(warTerritory.ownerIndex);
        }






        public static ShEntity GetAvailable(int jobIndex, WaypointType type = WaypointType.Player)
        {
            var jobInfo = BPAPI.Jobs[jobIndex];

            var randomEntities = ((MyJobInfo)jobInfo).randomEntities[(int)type];

            if (randomEntities.Count == 0) return null;

            for (int i = 0; i < 5; i++) // Try just a few times to find an available Random Entity
            {
                var index = UnityEngine.Random.Range(0, randomEntities.Count);

                var randomEntity = randomEntities.ElementAt(index);
                if (!randomEntity.isActiveAndEnabled)
                {
                    return randomEntity;
                }
            }

            return null;
        }

        public static ShEntity GetAvailable(ShPlayer spawner, Vector3 position, out int jobIndex, WaypointType type = WaypointType.Player)
        {
            var options = new List<Tuple<float, int, ShEntity>>();

            var total = 0f;
            foreach (var j in BPAPI.Jobs)
            {
                var entity = GetAvailable(j.shared.jobIndex, type);

                if (entity)
                {
                    entity.svEntity.spawner = spawner;
                    entity.SetPosition(position);
                    var spawnRate = entity.svEntity.SpawnRate;
                    if (spawnRate > 0f)
                    {
                        total += spawnRate;
                        options.Add(new Tuple<float, int, ShEntity>(total, j.shared.jobIndex, entity));
                    }
                }
            }

            var spawnValue = UnityEngine.Random.value * total;

            foreach (var p in options)
            {
                if (spawnValue < p.Item1)
                {
                    jobIndex = p.Item2;
                    return p.Item3;
                }
            }

            jobIndex = 0;
            return null;
        }








        [Execution(ExecutionMode.Override)]
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
                    else if (child.name.Equals("Jailyard") && child.TryGetComponent(out ServerTrigger j)) jails.Add(j);
                    else if (child.TryGetComponent(out ShTerritory t)) territories.Add(t);
                }
            }

            if (spawnLocations.Count == 0) Debug.LogWarning("[SVR] No spawn locations found");
            if (jails.Count == 0) Debug.LogWarning("[SVR] No jails found");
            if (territories.Count == 0) Debug.LogWarning("[SVR] No territories found");

            foreach (var g in worldWaypoints) g.Initialize();

            var waypointTypes = Enum.GetValues(typeof(WaypointType)).Length;

            for (byte jobIndex = 0; jobIndex < BPAPI.Jobs.Count; jobIndex++)
            {
                var info = BPAPI.Jobs[jobIndex];

                var myInfo = (MyJobInfo)info;

                myInfo.randomEntities = new HashSet<ShEntity>[waypointTypes];

                for (int i = 0; i < waypointTypes; i++)
                {
                    myInfo.randomEntities[i] = new HashSet<ShEntity>();
                }

                int count = 0;
                int limit = 0;
                while (count < myInfo.poolSize && limit < 100)
                {
                    var randomSkin = skinPrefabs.GetRandom();

                    if (info.characterType == CharacterType.All || randomSkin.characterType == info.characterType)
                    {
                        AddRandomSpawn(randomSkin, jobIndex);
                        count++;
                    }
                    else
                    {
                        limit++;
                    }
                }

                foreach (var transportArray in myInfo.transports)
                {
                    if (transportArray.transports.Length > 0)
                    {
                        for (int i = 0; i < myInfo.poolSize; i++)
                        {
                            if (SceneManager.Instance.TryGetEntity<ShTransport>(transportArray.transports.GetRandom(), out var t))
                                AddRandomSpawn(t, jobIndex);
                        }
                    }
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool TryLogin(ConnectionData connectData)
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

        [Execution(ExecutionMode.Override)]
        public override bool TryRegister(ConnectionData connectData)
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

        [Execution(ExecutionMode.Override)]
        public override bool Save()
        {
            var bountyData = new Data{ ID = Hitman.bountiesKey };

            foreach (var bounty in Hitman.bounties)
            {
                // Only save bounties targeting Humans
                if (!Hitman.aiTarget || Hitman.aiTarget.username != bounty.Key)
                {
                    bountyData.CustomData[bounty.Key] = bounty.Value;
                }
            }
            SvManager.Instance.database.Data.Upsert(bountyData);

            ChatHandler.SendToAll("Saving server status..");
            foreach (var player in EntityCollections.Humans)
            {
                player.svPlayer.Save();
            }
            SvManager.Instance.database.WriteOut();

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool Load()
        {
            var bountyData = SvManager.Instance.database.Data.FindById(Hitman.bountiesKey);

            if (bountyData != null)
            {
                foreach (var bounty in bountyData.CustomData.Data)
                {
                    Hitman.bounties.Add(bounty.Key, CustomData.ConvertData<DateTimeOffset>(bounty.Value));
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool ReadGroups()
        {
            try
            {
                var groups = JsonConvert.DeserializeObject<List<Group>>(File.ReadAllText(Paths.groupsFile));
                GroupHandler.Groups = groups.ToDictionary(x => x.Name, y => y);
            }
            catch(Exception e)
            {
                Debug.Log("[SVR] Error reading groups file: " + e);
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool PlayerLoaded(ConnectionData connectData)
        {
            connectData.connectionStatus = ConnectionStatus.LoadedMap;
            SvManager.Instance.SendRegisterMenu(connectData.connection, true, skinPrefabs);

            return true;
        }

        private bool ValidateUser(ConnectionData connectData)
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

        // Read packet data from Buffers.reader
        [Execution(ExecutionMode.Override)]
        public override bool CustomPacket(ConnectionData connectData, SvPacket packet)
        {
            var packetID = Buffers.reader.ReadByte();

            return true;
        }
    }
}

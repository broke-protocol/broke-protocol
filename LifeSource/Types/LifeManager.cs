using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.LiteDB;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BrokeProtocol.GameSource.Types
{
    public class Spawn
    {
        public Vector3 position;
        public Quaternion rotation;
        public Waypoint prevWaypoint;
        public Waypoint nextWaypoint;
        public Place place;

        public Spawn(Vector3 position, Quaternion rotation, Waypoint prevWaypoint, Waypoint nextWaypoint)
        {
            this.position = position;
            this.rotation = rotation;
            this.prevWaypoint = prevWaypoint;
            this.nextWaypoint = nextWaypoint;
            place = nextWaypoint.GetPlace;
        }
    }

    public class WaypointGroup
    {
        private readonly WaypointType waypointType;
        private readonly float spawnRate;
        private readonly List<Waypoint> waypoints = new();

        public readonly Dictionary<ValueTuple<Place, Vector2Int>, List<Spawn>> spawns = new();

        public WaypointGroup(WaypointType waypointType, float spawnRate)
        {
            this.waypointType = waypointType;
            this.spawnRate = spawnRate;

            foreach (var p in SceneManager.Instance.places)
            {
                waypoints.AddRange(p.waypoints[waypointType]);
            }

            const float spawnGap = 12f;

            foreach (var node in waypoints)
            {
                if (node.randomSpawns)
                {
                    foreach (var neighbor in node.neighbors)
                    {
                        var delta = node.Delta(neighbor);
                        var ray = new Ray(node.mainT.position, delta);
                        var distance = delta.magnitude;

                        for (var i = spawnGap; i < distance - spawnGap; i += spawnGap)
                        {
                            var spawnPosition = ray.GetPoint(i);

                            var floor = Util.GetSectorFloor(spawnPosition);
                            var tuple = (node.GetPlace, floor);
                            if (!spawns.ContainsKey(tuple))
                            {
                                spawns[tuple] = new List<Spawn>();
                            }

                            spawns[tuple].Add(new Spawn(spawnPosition, Quaternion.LookRotation(ray.direction), node, neighbor));
                        }
                    }
                }
            }
        }

        private float AdjustedSpawnRate(Sector sector, float limit, WaypointType type) =>
            (1f - (sector.AreaTypeCount(type) / limit)) * spawnRate;

        private void SetupTrain(ShTransport transport, Spawn s)
        {
            if (transport is ShTrain train)
            {
                train.waypoints.Clear();
                train.waypoints.Add(s.nextWaypoint);
                train.waypoints.Add(s.prevWaypoint);
            }
        }

        public void SpawnRandom(ShPlayer spawner, Sector sector)
        {
            if (spawns.TryGetValue((sector.place, sector.position), out var sectorSpawns))
            {
                if (waypointType == WaypointType.Player)
                {
                    foreach (var s in sectorSpawns)
                    {
                        if (UnityEngine.Random.value < AdjustedSpawnRate(sector, 16, waypointType))
                        {
                            var spawnBot = LifeManager.GetAvailable<ShPlayer>(spawner, s.position, out _, waypointType);

                            if (spawnBot)
                            {
                                spawnBot.svPlayer.SpawnBot(
                                    s.position,
                                    s.rotation,
                                    s.place,
                                    s.nextWaypoint,
                                    spawner,
                                    null,
                                    null);
                            }
                        }
                    }
                }
                else
                {
                    //VehicleWaypointGroup
                    foreach (var s in sectorSpawns)
                    {
                        if (UnityEngine.Random.value < AdjustedSpawnRate(sector, 8, waypointType))
                        {
                            var spawnBot = LifeManager.GetAvailable<ShPlayer>(spawner, s.position, out var jobIndex, WaypointType.Player);

                            if (spawnBot && spawnBot.characterType == CharacterType.Humanoid && ((MyJobInfo)BPAPI.Jobs[jobIndex]).transports[((int)waypointType) - 1].transports.Length > 0)
                            {
                                var transport = LifeManager.GetAvailable<ShTransport>(jobIndex, waypointType);

                                if (transport && transport.CanSpawn(s.position, s.rotation, new ShEntity[] { }))
                                {
                                    transport.Spawn(s.position, s.rotation, sector.place.mTransform);
                                    SetupTrain(transport, s);
                                    transport.SetVelocity(0.5f * transport.maxSpeed * transport.mainT.forward);
                                    spawnBot.svPlayer.SpawnBot(
                                        s.position,
                                        s.rotation,
                                        s.place,
                                        s.nextWaypoint,
                                        spawner,
                                        transport,
                                        null);

                                    while (transport.svTransport.TryGetTowOption(out var towable))
                                    {
                                        var spawnTowable = LifeManager.GetAvailable<ShTransport>(jobIndex, WaypointType.Towable, e => e.index == towable.index);

                                        if (spawnTowable && transport.svTransport.TryTowing(spawnTowable))
                                        {
                                            SetupTrain(spawnTowable, s);
                                            transport = spawnTowable;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }


    public class LifeManager : ManagerEvents
    {
        public static Dictionary<ShEntity, LifeSourcePlayer> pluginPlayers = new();

        public static List<WaypointGroup> waypointGroups = new ();
        
        [NonSerialized]
        public static List<ServerTrigger> jails = new();

        private static float endTime;
        private static int attackerLimit;
        public static int defenderLimit;
        public static int attackersKilled;
        public static int defendersKilled;
        public static ShTerritory warTerritory;

        public void AddRandomSpawn<T>(T prefab, int jobIndex, int waypointIndex) where T : ShEntity
        {
            T newEntity = GameObject.Instantiate(prefab, SceneManager.Instance.ExteriorT);
            newEntity.name = prefab.name;
            newEntity.svEntity.randomSpawn = true;

            if (newEntity is ShPlayer player)
            {
                player.svPlayer.spawnJobIndex = jobIndex;
                player.svPlayer.spawnJobRank = Random.Range(0, BPAPI.Jobs[jobIndex].shared.upgrades.Length);
            }

            SvManager.Instance.AddNewEntityExisting(newEntity);
            ((MyJobInfo)BPAPI.Jobs[jobIndex]).randomEntities[waypointIndex].Add(newEntity);
        }

        public static void StartGangWar(ShTerritory startT, int attackerJob)
        {
            if (!warTerritory)
            {
                warTerritory = startT;

                var total = 0;
                var count = 0;
                foreach (var t in Manager.territories)
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
                InterfaceHandler.SendGameMessageToAll($"{jobs[attackerJob].shared.jobName} Attacking {jobs[startT.ownerIndex].shared.jobName}");

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
                InterfaceHandler.SendGameMessageToAll(
                    $"{jobs[owner].shared.jobName} Defended Against {jobs[warTerritory.attackerIndex].shared.jobName}");
            }
            else
            {
                InterfaceHandler.SendGameMessageToAll(
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

        public static T GetAvailable<T>(int jobIndex, WaypointType type) where T : ShEntity => GetAvailable<T>(jobIndex, type, e => true);

        public static T GetAvailable<T>(int jobIndex, WaypointType type, Predicate<T> predicate) where T : ShEntity
        {
            var randomEntities = ((MyJobInfo)BPAPI.Jobs[jobIndex]).randomEntities[(int)type];

            var count = randomEntities.Count;

            if (count == 0)
                return null;

            var start = Random.Range(0, count);
            var i = start;

            do
            { 
                if (randomEntities.ElementAt(i) is T randomEntity && !randomEntity.isActiveAndEnabled && predicate(randomEntity))
                {
                    return randomEntity;
                }
                i = Util.Mod(i + 1, count);
            } while (i != start);

            return null;
        }

        public static T GetAvailable<T>(ShPlayer spawner, Vector3 position, out int jobIndex, WaypointType type = WaypointType.Player) where T : ShEntity
        {
            var options = new List<Tuple<float, int, ShEntity>>();

            var total = 0f;
            foreach (var j in BPAPI.Jobs)
            {
                var entity = GetAvailable<T>(j.shared.jobIndex, type);

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

            var spawnValue = Random.value * total;

            foreach (var p in options)
            {
                if (spawnValue < p.Item1)
                {
                    jobIndex = p.Item2;
                    return p.Item3 as T;
                }
            }

            jobIndex = 0;
            return null;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Start()
        {
            foreach (Transform place in SceneManager.Instance.mTransform)
            {
                foreach (Transform child in place)
                {
                    if (child.name.Equals("Jailyard") && child.TryGetComponent(out ServerTrigger j)) jails.Add(j);
                }
            }

            if (jails.Count == 0) Debug.LogWarning("[SVR] No jails found");

            waypointGroups.Add(new WaypointGroup(WaypointType.Player, 0.08f));
            waypointGroups.Add(new WaypointGroup(WaypointType.Vehicle, 0.06f));
            waypointGroups.Add(new WaypointGroup(WaypointType.Aircraft, 0.003f));
            waypointGroups.Add(new WaypointGroup(WaypointType.Boat, 0.01f));
            waypointGroups.Add(new WaypointGroup(WaypointType.Train, 0.005f));

            var waypointTypes = Enum.GetValues(typeof(WaypointType)).Length;

            for (byte jobIndex = 0; jobIndex < BPAPI.Jobs.Count; jobIndex++)
            {
                var info = BPAPI.Jobs[jobIndex];

                var myInfo = (MyJobInfo)info;

                myInfo.randomEntities = new HashSet<ShEntity>[waypointTypes];

                for (var i = 0; i < waypointTypes; i++)
                {
                    myInfo.randomEntities[i] = new HashSet<ShEntity>();
                }

                var count = 0;
                var limit = 0;
                while (count < myInfo.poolSize && limit < 100)
                {
                    var randomSkin = Manager.skinPrefabs.GetRandom();

                    if (info.characterType == CharacterType.All || randomSkin.characterType == info.characterType)
                    {
                        AddRandomSpawn(randomSkin, jobIndex, 0);
                        count++;
                    }
                    else
                    {
                        limit++;
                    }
                }

                for (var waypointType = 0; waypointType < myInfo.transports.Length; ++waypointType)
                {
                    var transportArray = myInfo.transports[waypointType];
                    if (transportArray.transports.Length > 0)
                    {
                        for (var index = 0; index < myInfo.poolSize; index++)
                        {
                            if (SceneManager.Instance.TryGetEntity<ShTransport>(transportArray.transports.GetRandom(), out var t))
                                AddRandomSpawn(t, jobIndex, waypointType + 1);
                        }
                    }
                }
            }

            return true;
        }


        [Execution(ExecutionMode.Additive)]
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

            return true;
        }

        [Execution(ExecutionMode.Additive)]
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
    }
}

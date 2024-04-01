using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using ENet;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class TerritoryState
    {
        // Used for territory transition state (gray) between 2 teams
        public readonly int initialOwner;
        public const int transitionTeamIndex = int.MaxValue;
        public const string territoryProgressBarID = "territory";
        public ShTerritory territory;
        public float lastSpeed;
        public float captureState;
        public IDCollection<ShPlayer> players = new();
        public Dictionary<int, int> attackerCounts = new();

        public TerritoryState(ShTerritory territory)
        {
            this.territory = territory;
            initialOwner = territory.ownerIndex;
        }

        public StringBuilder PrettyString()
        {
            const char barCharacter = '|';
            const int segments = 20;
            var sb = new StringBuilder();
            
            sb.Append("[");

            var capSegments = Mathf.RoundToInt(captureState * segments);
            if(capSegments > 0)
                sb.AppendColorText(new string(barCharacter, capSegments), Util.GetJobColor(BPAPI.JobInfoShared, territory.attackerIndex));
            
            var endSegments = segments - capSegments;
            if(endSegments > 0)
                sb.AppendColorText(new string(barCharacter, endSegments), Util.GetJobColor(BPAPI.JobInfoShared, territory.ownerIndex));
            
            sb.Append("]");
            return sb;
        }

        public void ResetCaptureState()
        {
            captureState = lastSpeed = 0f;
            foreach (var p in players) p.svPlayer.SvProgressStop(territoryProgressBarID);
        }

        public void Update()
        {
            foreach(var p in players.ToArray())
            {
                if (!p || !p.isActiveAndEnabled || p.IsDead || !Manager.TryGetTerritory(p, out var t) || t != territory)
                {
                    p.svPlayer.SvProgressStop(territoryProgressBarID);
                    players.Remove(p);
                }
            }

            // Initialize to 1 so capture progress decays with no players
            var newSpeed = 1f; 
            var attackerIndex = territory.ownerIndex;

            if (players.Count > 0)
            {
                attackerCounts.Clear();
                foreach (var p in players)
                {
                    var j = p.svPlayer.spawnJobIndex;
                    if (attackerCounts.TryGetValue(j, out var count))
                    {
                        attackerCounts[j] = count + 1;
                    }
                    else
                    {
                        attackerCounts[j] = 1;
                    }
                }

                var highestCount = attackerIndex = -1;

                foreach (var pair in attackerCounts)
                {
                    if (pair.Value > highestCount)
                    {
                        highestCount = pair.Value;
                        attackerIndex = pair.Key;
                    }
                }

                // Clamp capture speed to sane levels
                // highestCount - (players.Count - highestCount)
                newSpeed = Mathf.Clamp(2 * highestCount - players.Count, 1, 5);
            }

            newSpeed *= 0.05f;

            if (attackerIndex == territory.ownerIndex ||
                territory.attackerIndex >= 0 && territory.attackerIndex < BPAPI.Jobs.Count && attackerIndex != territory.attackerIndex)
            {
                newSpeed = -newSpeed;
            }

            if (newSpeed <= 0f && captureState == 0f)
            {
                return;
            }

            if (newSpeed != 0f)
            {
                captureState += newSpeed * Time.deltaTime;

                if (territory.attackerIndex < 0)
                {
                    // Set to Gray (unowned) area before transitioning to attackerIndex
                    if (territory.ownerIndex >= 0 && territory.ownerIndex < BPAPI.Jobs.Count)
                        territory.svTerritory.SvSetTerritory(territory.ownerIndex, transitionTeamIndex);
                    else
                        territory.svTerritory.SvSetTerritory(territory.ownerIndex, attackerIndex);
                }

                if (captureState >= 1f)
                {
                    if(territory.attackerIndex == transitionTeamIndex)
                    {
                        territory.svTerritory.SvSetTerritory(-1);
                    }
                    else
                    {
                        var captureJob = BPAPI.Jobs[territory.attackerIndex].shared;
                        var captureSB = new StringBuilder();
                        captureSB.AppendColorText(captureJob.jobName, captureJob.GetColor()).Append($" captured {territory.text}!");
                        InterfaceHandler.SendTextToAll(captureSB.ToString(), 3f);
                        Events.StartSlowMotion(null, 2f);

                        territory.svTerritory.SvSetTerritory(territory.attackerIndex);
                    }
                    
                    ResetCaptureState();
                    return;
                }
                
                if (captureState <= 0f)
                {
                    territory.svTerritory.SvSetTerritory(territory.ownerIndex);
                    ResetCaptureState();
                    return;
                }
            }

            if (newSpeed != lastSpeed)
            {
                lastSpeed = newSpeed;

                foreach (var p in players)
                {
                    p.svPlayer.SvProgressBar(captureState, newSpeed, territoryProgressBarID);
                }
            }
        }
    }

    public class WarManager : ManagerEvents
    {
        public static Dictionary<ShPlayer, WarSourcePlayer> pluginPlayers = new();

        public static Dictionary<ShTerritory, TerritoryState> territoryStates = new();

        public static List<ShPlayer>[] skinPrefabs = new List<ShPlayer>[2];

        public static List<List<ClassInfo>> classes;

        private ClassInfo[][] GetClasses => new ClassInfo[][] {
            new ClassInfo[]
            {
                new ("Rifleman", new InventoryStruct[] {
                    new ("M4", 1),
                    new ("AmmoRifle", 180),
                }),
                new ("Officer", new InventoryStruct[] {
                    new ("MP5SD", 1),
                    new ("AmmoSMG", 180),
                }),
                new ("Sniper", new InventoryStruct[] {
                    new ("Winchester", 1),
                    new ("AmmoRifle", 60),
                }),
                new ("Support", new InventoryStruct[] {
                    new ("MachineGun", 1),
                    new ("AmmoMG", 300),
                }),
                new ("Medic", new InventoryStruct[] {
                    new ("Mac", 1),
                    new ("AmmoSMG", 180),
                    new ("Defibrillator", 1),
                    new ("MedicBox1", 5),
                }),
                new ("Anti-Tank", new InventoryStruct[] {
                    new ("Springfield", 1),
                    new ("AmmoRifle", 80),
                    new ("Bazooka", 1),
                    new ("Rocket", 5),
                }),
                new ("Demoman", new InventoryStruct[] {
                    new ("SPAS12", 1),
                    new ("AmmoShotgun", 80),
                    new ("MineAT", 2),
                    new ("MineAP", 3),
                    new ("SatchelCharge", 2),
                    new ("Detonator", 1),
                }),
            },
            new ClassInfo[]
            {
                new ("Rifleman", new InventoryStruct[] {
                    new ("AK47", 1),
                    new ("AmmoRifle", 180),
                }),
                new ("Officer", new InventoryStruct[] {
                    new ("P90", 1),
                    new ("AmmoSMG", 200),
                }),
                new ("Sniper", new InventoryStruct[] {
                    new ("Winchester", 1),
                    new ("AmmoRifle", 60),
                }),
                new ("Support", new InventoryStruct[] {
                    new ("PKM", 1),
                    new ("AmmoMG", 300),
                }),
                new ("Medic", new InventoryStruct[] {
                    new ("Mac", 1),
                    new ("AmmoSMG", 180),
                    new ("Defibrillator", 1),
                    new ("MedicBox2", 5),
                }),
                new ("Anti-Tank", new InventoryStruct[] {
                    new ("Springfield", 1),
                    new ("AmmoRifle", 80),
                    new ("Bazooka", 1),
                    new ("Rocket", 5),
                }),
                new ("Demoman", new InventoryStruct[] {
                    new ("Shotgun", 1),
                    new ("AmmoShotgun", 60),
                    new ("MineAT", 2),
                    new ("MineAP", 3),
                    new ("SatchelCharge", 2),
                    new ("Detonator", 1),
                }),
            }
        };

        public void AddBot(ShPlayer prefab, int jobIndex)
        {
            var player = GameObject.Instantiate(prefab, SceneManager.Instance.ExteriorT);
            player.name = prefab.name;
            player.svPlayer.spawnJobIndex = jobIndex;
            player.svPlayer.spawnJobRank = Random.Range(0, BPAPI.Jobs[jobIndex].shared.upgrades.Length);
            SvManager.Instance.AddNewEntityExisting(player);
        }

        [Execution(ExecutionMode.Override)]
        public override bool Start()
        {
            foreach (Transform place in SceneManager.Instance.mTransform)
            {
                foreach (Transform child in place)
                {
                    if (child.TryGetComponent(out ShTerritory t)) Manager.territories.Add(t);
                }
            }

            if (Manager.territories.Count == 0) Util.Log("No territories found", LogLevel.Warn);

            foreach (var t in Manager.territories)
            {
                if(t.capturable)
                    territoryStates.Add(t, new TerritoryState(t));
            }

            var skins = new HashSet<string>();

            for (var i = 0; i <= 1; i++)
            {
                SvManager.Instance.ParseFile(ref skins, Paths.AbsolutePath($"skins{i}.txt"));
                skinPrefabs[i] = skins.ToEntityList<ShPlayer>();
            }

            var classesFilename = "Classes.json";

            if (!File.Exists(classesFilename))
            {
                File.WriteAllText(classesFilename, JsonConvert.SerializeObject(GetClasses, Formatting.Indented));
            }

            classes = JsonConvert.DeserializeObject<List<List<ClassInfo>>>(File.ReadAllText(classesFilename));

            for (var i = 0; i < 64; i++)
            {
                var teamIndex = i % skinPrefabs.Length;
                AddBot(skinPrefabs[teamIndex].GetRandom(), teamIndex);
            }

            ResetGame();

            return true;
        }

        private IEnumerator GameLoop()
        {
            var delay = new WaitForSeconds(1f);

            var sentWarning = false;
            const float warningLevel = 100f;

            while(true)
            {
                foreach(var team in tickets)
                {
                    if(team.Value <= warningLevel)
                    {
                        if (!sentWarning)
                        {
                            sentWarning = true;

                            var warningJob = BPAPI.Jobs[team.Key].shared;
                            var warningSB = new StringBuilder();
                            warningSB.AppendColorText(warningJob.jobName, warningJob.GetColor())
                                .Append($" is down to {(int)warningLevel} tickets!");
                            InterfaceHandler.SendTextToAll(warningSB.ToString(), 3f);
                        }

                        if (team.Value <= 0f)
                        {
                            var winner = -1;
                            var highest = -1f;

                            foreach (var otherTeam in tickets)
                            {
                                if (otherTeam.Value > highest)
                                {
                                    highest = otherTeam.Value;
                                    winner = otherTeam.Key;
                                }
                            }
                            var winnerJobInfo = BPAPI.Jobs[winner].shared;
                            var victorySB = new StringBuilder();
                            victorySB.AppendColorText(winnerJobInfo.jobName, winnerJobInfo.GetColor())
                                .Append(" win the match");
                            InterfaceHandler.SendTextToAll(victorySB.ToString(), 3f);
                            ResetGame();
                            Events.StartSlowMotion(null, 1f);
                            yield break;
                        }
                    }
                }

                var scoreSB = new StringBuilder();
                foreach (var team in tickets)
                {
                    var jobInfo = BPAPI.Jobs[team.Key].shared;
                    scoreSB.Append("   ").AppendColorText(((int)team.Value).ToString(), jobInfo.GetColor());
                }

                foreach(var player in EntityCollections.Humans)
                {
                    player.svPlayer.SetTextElementText("WarScore", scoreSB.ToString().Trim());
                }

                var territoriesSB = new StringBuilder();
                var index = 0;
                foreach(var t in territoryStates.Values)
                {
                    territoriesSB.AppendLine(WarUtility.GetTerritoryName(index));
                    territoriesSB.Append(t.PrettyString()).AppendLine();
                    index++;
                }

                InterfaceHandler.SendTextPanelToAll(territoriesSB.ToString(), "WarPlugin");

                yield return delay;
            }
        }

        public static readonly Dictionary<int, int> controlledTerritories = new();
        public static readonly Dictionary<int, float> tickets = new();
        public static readonly Dictionary<int, float> tempTickets = new();

        public void ResetGame()
        {
            foreach (var pair in territoryStates)
            {
                pair.Key.svTerritory.SvSetTerritory(pair.Value.initialOwner);
            }

            tickets.Clear();
            foreach(var t in BPAPI.Jobs)
            {
                tickets.Add(t.shared.jobIndex, 2000f);
            }

            foreach(var pair in pluginPlayers)
            {
                pair.Key.svPlayer.HealFull();
                pair.Key.svPlayer.SvClearInjuries();
                pair.Key.svPlayer.Respawn();
            }

            SvManager.Instance.StartCoroutine(GameLoop());
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Update()
        {
            foreach(var p in EntityCollections.Players)
            {
                if(p.isActiveAndEnabled && !p.IsDead && Manager.TryGetTerritory(p, out var territory) &&
                    territoryStates.TryGetValue(territory, out var state) && state.players.TryAdd(p) && state.captureState != 0f)
                {
                    p.svPlayer.SvProgressBar(state.captureState, state.lastSpeed, TerritoryState.territoryProgressBarID);
                }
            }

            controlledTerritories.Clear();
            foreach (var t in territoryStates.Values)
            {
                t.Update();

                var ownerIndex = t.territory.ownerIndex;

                if (ownerIndex >= 0)
                {
                    if (controlledTerritories.TryGetValue(ownerIndex, out var count))
                    {
                        controlledTerritories[ownerIndex] = count + 1;
                    }
                    else
                    {
                        controlledTerritories[ownerIndex] = 1;
                    }
                }
            }

            const float burnScalar = 3f;

            tempTickets.Clear();
            foreach (var team in tickets)
            {
                var burn = burnScalar;
                if (controlledTerritories.TryGetValue(team.Key, out var count))
                {
                    burn = burnScalar * (1f - (float)count / territoryStates.Count);

                    if (count == Manager.territories.Count && !EntityCollections.Players.Any(x =>
                        x.svPlayer.job.info.shared.jobIndex != team.Key &&
                        !x.IsDead))
                    {
                        // Super burn if all territories captured no enemies alive to retake
                        burn *= 1000;
                    }
                }

                tempTickets.Add(
                    team.Key,
                    Mathf.Max(0f, team.Value - burn * Time.deltaTime));
            }

            tickets.Clear();
            foreach(var pair in tempTickets)
            {
                tickets.Add(pair.Key, pair.Value);
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool TryLogin(ConnectData connectData)
        {
            // Logins disabled here
            SvManager.Instance.Disconnect(connectData.connection, DisconnectTypes.ClientIssue);
            return true;
        }


        [Execution(ExecutionMode.Override)]
        public override bool TryRegister(ConnectData connectData)
        {
            if (ValidateUser(connectData))
            {
                if (SvManager.Instance.TryGetUserData(connectData.username, out var playerData))
                {
                    if (playerData.PasswordHash != connectData.passwordHash)
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, $"Invalid credentials for Account {connectData.username}");
                        return true;
                    }
                }

                if (!connectData.username.ValidCredential())
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Account {connectData.username} cannot be registered (check length and characters)");
                    return true;
                }

                if (connectData.customData.TryFetchCustomData(teamIndexKey, out int teamIndex) && connectData.skinIndex >= 0 && connectData.skinIndex < skinPrefabs[teamIndex].Count && connectData.wearableIndices?.Length == ShManager.Instance.nullWearable.Length)
                {
                    var territories = WarUtility.GetTerritories(teamIndex);

                    if (territories.Count() > 0 &&
                        WarUtility.GetValidTerritoryPosition(territories.GetRandom(), out var position, out var rotation, out var place))
                    {
                        SvManager.Instance.AddNewPlayer(skinPrefabs[teamIndex][connectData.skinIndex], connectData, playerData?.Persistent, position, rotation, place.mTransform, teamIndex);
                    }
                    else
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, "No spawn territories for this team");
                    }
                }
                else
                {
                    SvManager.Instance.RegisterFail(connectData.connection, "Invalid data");
                }
            }
            return true;
        }

        public const string selectTeam = "Select Team";
        public const string selectClass = "Select Class";

        [Execution(ExecutionMode.Override)]
        public override bool PlayerLoaded(ConnectData connectData)
        {
            SendTeamSelectMenu(connectData.connection);
            return true;
        }

        public static void SendTeamSelectMenu(Peer connection)
        {
            var options = new List<LabelID>();
            foreach (var j in BPAPI.Jobs)
            {
                var sb = new StringBuilder();
                sb.AppendColorText(j.shared.jobName, j.shared.GetColor())
                .Append($" ({j.members.Count} players)");
                options.Add(new LabelID(sb.ToString(), j.shared.jobName));
            }
            var actions = new LabelID[] { new (selectTeam, selectTeam) };
            SvManager.Instance.SendOptionMenu(connection, selectTeam, 0, selectTeam, options.ToArray(), actions, 0.3f, 0.2f, 0.7f, 0.6f);
        }

        public static void SendClassSelectMenu(Peer connection, int teamIndex)
        {
            var teamJob = BPAPI.Jobs[teamIndex];
            var options = new List<LabelID>();
            var classIndex = 0;
            foreach (var c in classes[teamIndex])
            {
                var classCount = 0;
                foreach(var m in teamJob.members)
                {
                    if(m.WarPlayer().classIndex == classIndex)
                    {
                        classCount++;
                    }
                }
                options.Add(new LabelID($"{c.className} ({classCount} players)", c.className));

                classIndex++;
            }
            var actions = new LabelID[] { new (selectTeam, selectTeam) };
            SvManager.Instance.SendOptionMenu(connection, selectClass, 0, selectClass, options.ToArray(), actions);
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

        public const string teamIndexKey = "teamIndex";
        public const string classIndexKey = "classIndex";

        // Read packet data from Buffers.reader
        [Execution(ExecutionMode.Additive)]
        public override bool CustomPacket(ConnectData connectData, SvPacket packet)
        {
            switch(packet)
            {
                case SvPacket.OptionAction:
                    Util.ParseOptionAction(out var targetID, out var menuID, out var optionID, out var actionID);

                    switch (menuID)
                    {
                        case selectTeam:
                            {
                                int teamIndex = 0;
                                foreach (var c in BPAPI.Jobs)
                                {
                                    if (c.shared.jobName == optionID)
                                    {
                                        connectData.customData.AddOrUpdate(teamIndexKey, teamIndex);
                                        SvManager.Instance.DestroyMenu(connectData.connection, selectTeam);
                                        SendClassSelectMenu(connectData.connection, teamIndex);
                                        break;
                                    }
                                    teamIndex++;
                                }
                            }
                            break;

                        case selectClass:
                            {
                                if (connectData.customData.TryFetchCustomData(teamIndexKey, out int teamIndex))
                                {
                                    int classIndex = 0;
                                    foreach(var c in classes[teamIndex])
                                    {
                                        if(c.className == optionID)
                                        {
                                            connectData.customData.AddOrUpdate(classIndexKey, classIndex);
                                            SvManager.Instance.DestroyMenu(connectData.connection, selectClass);
                                            SvManager.Instance.SendRegisterMenu(connectData.connection, false, skinPrefabs[teamIndex]);
                                            break;
                                        }
                                        classIndex++;
                                    }
                                }
                            }
                            break;
                    }
                    break;

                case SvPacket.MenuClosed:
                    var menu = Buffers.reader.ReadString(); // skip menuID
                    if(menu == selectClass || menu == selectTeam) 
                        SvManager.Instance.Disconnect(connectData.connection, DisconnectTypes.Normal);
                    break;
            }
            return true;
        }
    }
}

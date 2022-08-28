using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BrokeProtocol.WarSource.Types
{
    public class TerritoryState
    {
        public const string territoryProgressBarID = "territory";
        public ShTerritory territory;
        public float lastSpeed;
        public float captureState;
        public IDCollection<ShPlayer> players = new IDCollection<ShPlayer>();
        public Dictionary<int, int> attackerCounts = new Dictionary<int, int>();

        public TerritoryState(ShTerritory territory)
        {
            this.territory = territory;
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
                if (!p.isActiveAndEnabled || p.IsDead || !Manager.TryGetTerritory(p, out var t) || t != territory)
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

            //Debug.Log(territory.ID + " " + territory.ownerIndex + " " + territory.attackerIndex + " " + newSpeed + " " + captureState + " " + players.Count);

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
                        territory.svTerritory.SvSetTerritory(territory.ownerIndex, int.MaxValue);
                    else
                        territory.svTerritory.SvSetTerritory(territory.ownerIndex, attackerIndex);
                }

                if (captureState >= 1f)
                {
                    territory.svTerritory.SvSetTerritory(territory.attackerIndex == int.MaxValue ? -1 : territory.attackerIndex);
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
        public static Dictionary<ShEntity, WarSourcePlayer> pluginPlayers = new Dictionary<ShEntity, WarSourcePlayer>();

        public Dictionary<ShTerritory, TerritoryState> territoryStates = new Dictionary<ShTerritory, TerritoryState>();

        public List<ShPlayer>[] skinPrefabs = new List<ShPlayer>[2];

        public void AddBot(ShPlayer prefab, int jobIndex)
        {
            var player = GameObject.Instantiate(prefab, SceneManager.Instance.ExteriorT);
            player.name = prefab.name;
            player.svPlayer.spawnJobIndex = jobIndex;
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

            if (Manager.territories.Count == 0) Debug.LogWarning("[SVR] No territories found");

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

            for (var i = 0; i < 5; i++)
            {
                var teamIndex = i % 2;
                //AddBot(skinPrefabs[teamIndex].GetRandom(), teamIndex);
            }

            ResetGame();

            SvManager.Instance.StartCoroutine(GameLoop());

            return true;
        }

        private IEnumerator GameLoop()
        {
            var delay = new WaitForSeconds(1f);

            while(true)
            {
                foreach(var team in tickets)
                {
                    if(team.Value <= 0f)
                    {
                        var winner = -1;
                        var highest = -1f;

                        foreach(var otherTeam in tickets)
                        {
                            if(otherTeam.Value > highest)
                            {
                                highest = otherTeam.Value;
                                winner = otherTeam.Key;
                            }
                        }

                        ChatHandler.SendAlertToAll($"{BPAPI.Jobs[winner].shared.jobName} have won the battle", 1f);

                        ResetGame();
                        break;
                    }
                }

                var sb = new StringBuilder();
                foreach(var team in tickets)
                {
                    var jobInfo = BPAPI.Jobs[team.Key].shared;
                    sb.Append("<color=#").
                        Append(ColorUtility.ToHtmlStringRGB(jobInfo.GetColor())).
                        Append(">").
                        Append(jobInfo.jobName).
                        Append("</color>: ").
                        AppendLine(((int)team.Value).ToString());
                }
                ChatHandler.SendTextPanelToAll(sb.ToString(), "WarPlugin");

                yield return delay;
            }
        }


        private Dictionary<int, int> controlledTerritories = new Dictionary<int, int>();
        private Dictionary<int, float> tickets = new Dictionary<int, float>();
        private Dictionary<int, float> tempTickets = new Dictionary<int, float>();

        public void ResetGame()
        {
            tickets.Clear();
            foreach(var t in BPAPI.Jobs)
            {
                tickets.Add(t.shared.jobIndex, 1000f);
            }

            foreach(var player in EntityCollections.Players)
            {
                player.svPlayer.Respawn();
            }
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
                    burn = burnScalar * (1f - count / territoryStates.Count);
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
        public override bool TryLogin(ConnectionData connectData)
        {
            // Logins disabled here
            SvManager.Instance.Disconnect(connectData.connection, DisconnectTypes.ClientIssue);
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
                }

                if (!connectData.username.ValidCredential())
                {
                    SvManager.Instance.RegisterFail(connectData.connection, $"Name cannot be registered (min: {Util.minCredential}, max: {Util.maxCredential})");
                    return true;
                }

                if (connectData.customData.TryFetchCustomData(teamIndexKey, out int teamIndex) && connectData.skinIndex >= 0 && connectData.skinIndex < skinPrefabs[teamIndex].Count && connectData.wearableIndices?.Length == ShManager.Instance.nullWearable.Length)
                {
                    var territories = WarPlayer.GetTerritories(teamIndex);

                    if (territories.Count() > 0 &&
                        Utility.GetSpawn(territories.GetRandom(), out var position, out var rotation, out var place))
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

        public readonly List<string> teams = new List<string> { "SpecOps", "OpFor" };

        public readonly List<string> classes = new List<string> { "Rifleman", "Officer", "Sniper", "Support", "Medic", "Anti-Tank" };

        public const string selectTeam = "Select Team";
        public const string selectClass = "Select Class";

        [Execution(ExecutionMode.Override)]
        public override bool PlayerLoaded(ConnectionData connectData)
        {
            connectData.connectionStatus = ConnectionStatus.LoadedMap;

            var options = new List<LabelID>();
            foreach (var c in teams) options.Add(new LabelID(c, c));
            var actions = new LabelID[] { new LabelID(selectTeam, selectTeam)};

            SvManager.Instance.SendOptionMenu(connectData.connection, selectTeam, 0, selectTeam, options.ToArray(), actions);
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

        public const string teamIndexKey = "teamIndex";
        public const string classIndexKey = "classIndex";

        // Read packet data from Buffers.reader
        [Execution(ExecutionMode.Additive)]
        public override bool CustomPacket(ConnectionData connectData, SvPacket packet)
        {
            switch(packet)
            {
                case SvPacket.OptionAction:
                    Util.ParseOptionAction(out var targetID, out var menuID, out var optionID, out var actionID);

                    switch (menuID)
                    {
                        case selectTeam:
                            {
                                var teamIndex = teams.IndexOf(optionID);
                                if (teamIndex >= 0)
                                {
                                    connectData.customData.AddOrUpdate(teamIndexKey, teamIndex);
                                    var options = new List<LabelID>();
                                    foreach (var c in classes) options.Add(new LabelID(c, c));
                                    var actions = new LabelID[] { new LabelID(selectTeam, selectTeam) };
                                    SvManager.Instance.DestroyMenu(connectData.connection, selectTeam);
                                    SvManager.Instance.SendOptionMenu(connectData.connection, selectClass, 0, selectClass, options.ToArray(), actions);
                                }
                            }
                            break;

                        case selectClass:
                            {
                                var classIndex = classes.IndexOf(optionID);
                                if (classIndex >= 0 && connectData.customData.TryFetchCustomData(teamIndexKey, out int teamIndex))
                                {
                                    connectData.customData.AddOrUpdate(classIndexKey, classIndex);

                                    SvManager.Instance.DestroyMenu(connectData.connection, selectClass);
                                    SvManager.Instance.SendRegisterMenu(connectData.connection, false, skinPrefabs[teamIndex]);
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

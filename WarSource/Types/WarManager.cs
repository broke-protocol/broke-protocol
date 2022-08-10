using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BrokeProtocol.WarSource.Types
{
    public class TerritoryState
    {
        public const string territoryProgressBarID = "territory";
        private ShTerritory territory;
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
            foreach (var p in players) p.svPlayer.SvProgressBar(0f, 0f, territoryProgressBarID);
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

            var newSpeed = 1f;
            var attackerIndex = territory.ownerIndex;

            if (players.Count > 0)
            {
                attackerCounts.Clear();
                foreach (var p in players)
                {
                    var j = p.svPlayer.job.info.shared.jobIndex;
                    if (attackerCounts.TryGetValue(j, out var count))
                    {
                        attackerCounts[j] = count++;
                    }
                    else
                    {
                        attackerCounts[j] = 1;
                    }
                }

                attackerIndex = -1;
                var highestCount = -1;

                foreach (var pair in attackerCounts)
                {
                    if (pair.Value > highestCount)
                    {
                        highestCount = pair.Value;
                        attackerIndex = pair.Key;
                    }
                }

                newSpeed = highestCount - (players.Count - highestCount);
            }

            newSpeed *= 0.05f;

            if (attackerIndex == territory.ownerIndex) newSpeed = -newSpeed;

            //Debug.Log(territory.ID + " " + territory.ownerIndex + " " + territory.attackerIndex + " " + newSpeed + " " + captureState + " " + players.Count);

            if (newSpeed < 0f && captureState <= 0f)
            {
                return;
            }

            if (newSpeed != 0f)
            {
                captureState += newSpeed * Time.deltaTime;

                if (captureState >= 1f)
                {
                    territory.svTerritory.SvSetTerritory(territory.attackerIndex);
                    ResetCaptureState();
                    return;
                }
                else if (captureState <= 0f)
                {
                    territory.svTerritory.SvSetTerritory(territory.ownerIndex);
                    ResetCaptureState();
                    return;
                }
                else if (territory.attackerIndex < 0)
                {
                    // Set to Gray (unowned) area before transitioning to attackerIndex
                    if (territory.ownerIndex >= 0 && territory.ownerIndex < BPAPI.Jobs.Count)
                        territory.svTerritory.SvSetTerritory(territory.ownerIndex, BPAPI.Jobs.Count);
                    else
                        territory.svTerritory.SvSetTerritory(territory.ownerIndex, attackerIndex);
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
        public Dictionary<ShTerritory, TerritoryState> territoryStates = new Dictionary<ShTerritory, TerritoryState>();

        public List<ShPlayer>[] skinPrefabs = new List<ShPlayer>[2];

        [Execution(ExecutionMode.Override)]
        public override bool Start()
        {
            var skins = new HashSet<string>();

            for (int i = 0; i <= 1; i++)
            {
                SvManager.Instance.ParseFile(ref skins, Paths.AbsolutePath($"skins{i}.txt"));
                skinPrefabs[i] = skins.ToEntityList<ShPlayer>();
            }

            for (int i = 0; i < 20; i++)
            {
                Utility.GetSpawn(out var position, out var rotation, out var place);
                SvManager.Instance.AddNewEntity(skinPrefabs[i % 2].GetRandom(), place, position, rotation, true);
            }

            foreach(var t in Manager.territories)
            {
                if(t.capturable)
                    territoryStates.Add(t, new TerritoryState(t));
            }

            return true;
        }

        [Execution(ExecutionMode.Override)]
        public override bool Update()
        {
            foreach(var t in territoryStates.Values)
            {
                t.Update();
            }

            foreach(var p in EntityCollections.Players)
            {
                if(p.isActiveAndEnabled && !p.IsDead && Manager.TryGetTerritory(p, out var territory) && 
                    territoryStates.TryGetValue(territory, out var state) && state.players.TryAdd(p))
                {
                    p.svPlayer.SvProgressBar(state.captureState, state.lastSpeed, TerritoryState.territoryProgressBarID);
                }
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
                    if (Utility.GetSpawn(out var position, out var rotation, out var place))
                    {
                        SvManager.Instance.AddNewPlayer(skinPrefabs[teamIndex][connectData.skinIndex], connectData, playerData?.Persistent, position, rotation, place.mTransform);
                    }
                    else
                    {
                        SvManager.Instance.RegisterFail(connectData.connection, "No spawn territories");
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
        [Execution(ExecutionMode.Override)]
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

                                    SvManager.Instance.SendRegisterMenu(connectData.connection, false, skinPrefabs[teamIndex]);
                                }
                            }
                            break;
                    }
                    break;

                case SvPacket.MenuClosed:
                    Buffers.reader.ReadString(); // skip menuID
                    var manualClose = Buffers.reader.ReadBoolean();
                    if(manualClose) SvManager.Instance.Disconnect(connectData.connection, DisconnectTypes.Normal);
                    break;
            }
            return true;
        }
    }
}

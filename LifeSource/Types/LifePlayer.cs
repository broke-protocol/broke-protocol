using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Networking;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BrokeProtocol.GameSource.Types
{
    public class LifeSourcePlayer
    {
        public readonly ShPlayer player;

        public bool trespassing;

        public float jailExitTime;

        public readonly Dictionary<int, Offense> offenses = new();

        public readonly Dictionary<ShPlayer, int> witnessedPlayers = new();

        public int wantedLevel;
        public float wantedNormalized;

        public LifeSourcePlayer(ShPlayer player)
        {
            this.player = player;
        }

        public virtual void StartJailTimer(float jailtime)
        {
            if (player.isHuman)
            {
                player.svPlayer.SendTimer(jailtime, Utility.defaultAnchor);
            }
            player.StartCoroutine(JailTimer(jailtime));
        }

        private IEnumerator JailTimer(float jailtime)
        {
            var delay = new WaitForSeconds(1f);
            jailExitTime = jailtime + Time.time;

            while (Time.time < jailExitTime)
            {
                yield return delay;

                if (player.IsDead)
                {
                    player.svPlayer.SvResetJob();
                    JailDone();
                    yield break;
                }

                if (((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.Prisoner)
                {
                    JailDone();
                    yield break;
                }
            }
            player.svPlayer.SvResetJob();
            player.svPlayer.Respawn();
            JailDone();
        }

        private void JailDone() => jailExitTime = 0f;

        public virtual int GetFineAmount()
        {
            var fine = 0;
            foreach (var o in offenses.Values) fine += o.crime.fine;
            return fine;
        }

        public virtual void AddWitnessedCriminal(ShPlayer criminal) => witnessedPlayers[criminal] = witnessedPlayers.TryGetValue(criminal, out var value) ? value + 1 : 1;

        public virtual ShPlayer GetWitness(ShPlayer victim)
        {
            if (victim && !victim.IsDead && ((MyJobInfo)victim.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement)
            {
                return victim;
            }

            ShPlayer w = null;

            const float witnessDistance = 100f;
            const float proximityDistanceSqr = 20f * 20f;

            player.svPlayer.LocalEntitiesOne(
                e => e != victim && e is ShPlayer && !e.IsDead && (player.DistanceSqr(e) <= proximityDistanceSqr || player.CanSeeEntity(e, false, witnessDistance)),
                e => w = e.Player);

            return w;
        }

        public virtual bool InvalidCrime(CrimeIndex crimeIndex)
        {
            foreach (var o in offenses.Values)
            {
                if (o.crime.index == crimeIndex && o.ElapsedTime < o.crime.repeatDelay)
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void ClearCrimes()
        {
            foreach (var o in offenses.Values)
            {
                if (o.witness && LifeManager.pluginPlayers.TryGetValue(o.witness, out var pluginWitness)) pluginWitness.witnessedPlayers.Remove(player);
            }
            
            offenses.Clear();
            UpdateWantedLevel(false);
            player.svPlayer.SendGameMessage("Crimes Cleared");
        }

        public virtual void ClearWitnessed()
        {
            foreach (var criminal in witnessedPlayers.Keys)
            {
                if(LifeManager.pluginPlayers.TryGetValue(criminal, out var pluginCriminal))
                {
                    pluginCriminal.RemoveWitness(player);
                }
            }

            witnessedPlayers.Clear();
        }

        public virtual void RemoveWitness(ShPlayer witness)
        {
            foreach (var offense in offenses.Values)
            {
                if (offense.witness == witness) offense.witness = null;
            }
            player.svPlayer.SendGameMessage($"Witness Eliminated : {witness.username}");
            UpdateWantedLevel(false);
        }

        public virtual void UpdateWantedLevel(bool updateDisguised)
        {
            var totalExpiration = 0f;

            foreach (var o in offenses.Values)
            {
                if (updateDisguised)
                {
                    o.disguised = false;
                    int j = 0;
                    int count = 0;
                    foreach (var w in player.curWearables)
                    {
                        if (w.index != ShManager.Instance.nullWearable[j].index && w.index != o.wearables[j])
                        {
                            if (count > 1)
                            {
                                o.disguised = true;
                                break;
                            }
                            else
                            {
                                count++;
                            }
                        }
                        j++;
                    }
                }

                totalExpiration += o.AdjustedExpiration;
            }

            wantedLevel = Mathf.CeilToInt(Mathf.Min(totalExpiration / 360f, LifeUtility.maxWantedLevel));
            wantedNormalized = (float)wantedLevel / LifeUtility.maxWantedLevel;

            for (var i = 1; i <= LifeUtility.maxWantedLevel; i++)
            {
                player.svPlayer.VisualElementVisibility(LifeUtility.starName + i.ToString(), i <= wantedLevel);
            }
        }

        public virtual void AddCrime(CrimeIndex crimeIndex, ShPlayer victim)
        {
            if (player.svPlayer.godMode || InvalidCrime(crimeIndex)) return;

            var crime = LifeUtility.crimeTypes[(int)crimeIndex];

            ShPlayer witness;
            if (!crime.witness)
            {
                witness = victim; // May be null which is fine in this case
            }
            else
            {
                witness = GetWitness(victim);
                if (!witness) return;
            }

            if (witness && LifeManager.pluginPlayers.TryGetValue(witness, out var pluginWitness))
            {
                pluginWitness.AddWitnessedCriminal(player);
            }

            var offense = new Offense(crime, player.curWearables, witness);
            offenses.Add(offense.GetHashCode(), offense);
            player.svPlayer.SendGameMessage("Committed Crime: " + crime.crimeName);
            UpdateWantedLevel(false);

            // Don't hand out crime penalties for criminal jobs and default job
            if (((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.Criminal && player.svPlayer.job.info.shared.jobIndex > 0)
            {
                player.svPlayer.Reward(-crime.experiencePenalty, -crime.fine);
            }
        }

        public virtual bool ApartmentUnlawful(ShPlayer apartmentOwner) => apartmentOwner != player && LifeManager.pluginPlayers.TryGetValue(apartmentOwner, out var pluginOwner) && 
            (((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement || pluginOwner.wantedLevel == 0);

        public virtual bool ApartmentTrespassing(ShPlayer apartmentOwner) => 
            LifeManager.pluginPlayers.TryGetValue(player, out var lifeSourcePlayer) && 
            lifeSourcePlayer.trespassing && ApartmentUnlawful(apartmentOwner);

        public virtual int GoToJail()
        {
            if (player.IsDead || !player.IsRestrained || ((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.Prisoner)
            {
                return 0;
            }

            var time = 0f;
            var fine = 0;
            foreach (var o in offenses.Values)
            {
                time += o.crime.jailtime;
                fine += o.crime.fine;
            }

            if (fine <= 0) return 0;

            player.svPlayer.SvSetJob(BPAPI.Jobs[LifeCore.prisonerIndex], true, false);
            var jailSpawn = LifeManager.jails.GetRandom().mainT;
            player.svPlayer.SvRestore(jailSpawn.position, jailSpawn.rotation, jailSpawn.parent.GetSiblingIndex());
            player.svPlayer.SvForceEquipable(player.Hands.index);
            ClearCrimes();

            // Remove jail items
            foreach (var i in player.myItems.Values.ToArray())
            {
                if (i.item.illegal)
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, i.item.index, i.count);
                }
            }

            if (LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.StartJailTimer(time);
            }

            return fine;
        }

        public virtual void CommandHandsUp(ShPlayer source)
        {
            if (((MyJobInfo)source.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement &&
                LifeManager.pluginPlayers.TryGetValue(source, out var pluginSource))
            {
                pluginSource.AddCrime(CrimeIndex.Intimidation, player);
            }

            if (player.isHuman)
            {
                player.svPlayer.SendText("Hands Up!", 2f, new Vector2(0.5f, 0.75f));
            }
            else if (!player.svPlayer.currentState.IsBusy)
            {
                if (Random.value < 0.2f)
                {
                    player.GamePlayer().SetAttackState(source);
                }
                else
                {
                    player.svPlayer.SetState(Core.Freeze.index);
                }
            }
        }
    }

    

    public class MinigameContainer
    {
        public ShPlayer player;
        public ShEntity targetEntity;

        public MinigameContainer(ShPlayer player, int entityID)
        {
            this.player = player;
            targetEntity = EntityCollections.FindByID(entityID);
        }

        public virtual bool IsValid() => player && targetEntity && player.IsMobile && player.InActionRange(targetEntity);

        public bool Active => player.svPlayer.minigame != null;
    }


    public class HackingContainer : MinigameContainer
    {
        public ShApartment targetApartment;
        public ShPlayer targetPlayer;

        public HackingContainer(ShPlayer player, int apartmentID, string username) : base(player, apartmentID)
        {
            targetApartment = targetEntity as ShApartment;
            EntityCollections.TryGetPlayerByNameOrID(username, out targetPlayer);
        }

        public override bool IsValid() => base.IsValid() && targetPlayer && GetPlace != null;

        public Place GetPlace => targetPlayer.ownedApartments.TryGetValue(targetApartment, out var apartmentPlace) ? apartmentPlace : null;
    }

    public class CrackingContainer : MinigameContainer
    {
        public CrackingContainer(ShPlayer player, int entityID) : base(player, entityID)
        {
        }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;

            if (!targetEntity.CanBeCracked(player))
            {
                Util.Log($"Invalid crack attempt on {targetEntity} by {player.username}", LogLevel.Warn);
                return false;
            }

            if (!player.HasItem(player.manager.lockpick))
            {
                player.svPlayer.SendGameMessage($"Missing {player.manager.lockpick.itemName} item");
                return false;
            }

            return true;
        }
    }

    public class LifePlayer : PlayerEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Initialize(ShEntity entity)
        {
            if (entity.Player)
            {
                LifeManager.pluginPlayers.Add(entity.Player, new LifeSourcePlayer(entity.Player));
                entity.Player.svPlayer.SvAddSelfAction("MyCrimes", "Crimes");
                entity.Player.svPlayer.SvAddInventoryAction("GetItemValue", "ShItem", ButtonType.Sellable, "Get Sell Value");
                entity.Player.svPlayer.SvAddTypeAction("HandsUp", "ShPlayer", "Hands Up!");
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Destroy(ShEntity entity)
        {
            if (LifeManager.pluginPlayers.TryGetValue(entity, out var pluginPlayer))
            {
                pluginPlayer.ClearWitnessed();
                pluginPlayer.ClearCrimes();

                LifeManager.pluginPlayers.Remove(entity);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Load(ShPlayer player)
        {
            if (LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (player.svPlayer.PlayerData.Character.CustomData.TryFetchCustomData(offensesKey, out string offensesJSON))
                {
                    var offensesList = JsonConvert.DeserializeObject<List<CrimeSave>>(offensesJSON);
                    var wearables = new ShWearable[player.curWearables.Length];
                    foreach (var crimeSave in offensesList)
                    {
                        for (var i = 0; i < wearables.Length; i++)
                        {
                            // Future/mod-proofing
                            if (i < crimeSave.wearables.Length &&
                                SceneManager.Instance.TryGetEntity<ShWearable>(crimeSave.wearables[i], out var w))
                            {
                                wearables[i] = w;
                            }

                            if (!wearables[i]) wearables[i] = ShManager.Instance.nullWearable[i];
                        }

                        ShPlayer witness = null;

                        if (crimeSave.witnessBotID != 0)
                        {
                            witness = EntityCollections.FindByID<ShPlayer>(crimeSave.witnessBotID);
                        }
                        else if (!string.IsNullOrWhiteSpace(crimeSave.witnessPlayerAccount))
                        {
                            foreach (var p in EntityCollections.Humans)
                            {
                                if (p.username == crimeSave.witnessPlayerAccount)
                                {
                                    witness = p;
                                    break;
                                }
                            }
                        }

                        var offense = new Offense(LifeUtility.crimeTypes[(int)crimeSave.crimeIndex], wearables, witness, crimeSave.timeSinceLast);
                        pluginPlayer.offenses.Add(offense.GetHashCode(), offense);
                    }

                    pluginPlayer.UpdateWantedLevel(true);
                }

                if (player.svPlayer.PlayerData.Character.CustomData.TryFetchCustomData(jailtimeKey, out float jailtime) && jailtime > 0f)
                {
                    pluginPlayer.StartJailTimer(jailtime);
                }
            }

            return true;
        }

        private const string offensesKey = "Offenses";
        private const string jailtimeKey = "Jailtime";

        [Execution(ExecutionMode.Additive)]
        public override bool Save(ShPlayer player) {
            if (LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                var offensesList = new List<CrimeSave>();

                foreach (var offense in pluginPlayer.offenses.Values)
                {
                    offensesList.Add(new CrimeSave(offense.crime.index, offense.wearables, Time.time - offense.commitTime, offense.witness));
                }

                var CustomData = player.svPlayer.PlayerData.Character.CustomData;

                CustomData.AddOrUpdate(offensesKey, JsonConvert.SerializeObject(offensesList));
                CustomData.AddOrUpdate(jailtimeKey, Mathf.Max(0f, pluginPlayer.jailExitTime - Time.time));
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TransferItem(ShEntity entity, byte deltaType, int itemIndex, int amount, bool dispatch)
        {
            var player = entity.Player;

            switch (deltaType)
            {
                case DeltaInv.OtherToMe:
                    var otherPlayer = player.otherEntity as ShPlayer;

                    if (otherPlayer)
                    {
                        if (((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement)
                        {
                            if (SceneManager.Instance.TryGetEntity<ShItem>(itemIndex, out var item) && item.illegal)
                            {
                                if (otherPlayer && otherPlayer.Shop)
                                {
                                    player.LifePlayer().AddCrime(CrimeIndex.Theft, otherPlayer);
                                }
                            }
                            else
                            {
                                player.LifePlayer().AddCrime(CrimeIndex.Theft, otherPlayer);
                            }
                        }
                        else
                        {
                            player.LifePlayer().AddCrime(CrimeIndex.Theft, otherPlayer);
                        }

                        if (!otherPlayer.isHuman && Random.value < 0.25f &&
                            otherPlayer.GamePlayer().SetAttackState(player))
                        {
                            player.svPlayer.SvStopInventory(true);
                        }
                    }
                    break;
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Spawn(ShEntity entity)
        {
            if (LifeManager.pluginPlayers.TryGetValue(entity.Player, out var pluginPlayer))
            {
                pluginPlayer.ClearCrimes();
                entity.StartCoroutine(Maintenance(entity.Player));
            }
            return true;
        }

        public IEnumerator Maintenance(ShPlayer player)
        {
            yield return null;

            var delay = new WaitForSeconds(5f);

            LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer);

            var removeOffenseList = new List<Offense>();

            while (!player.IsDead)
            {
                foreach (var offense in pluginPlayer.offenses.Values)
                {
                    if (Time.time >= offense.commitTime + offense.AdjustedExpiration)
                    {
                        var witness = offense.witness;

                        if (witness && LifeManager.pluginPlayers.TryGetValue(witness, out var pluginWitnesS) && pluginWitnesS.witnessedPlayers.TryGetValue(player, out var value))
                        {
                            if (value <= 1) pluginWitnesS.witnessedPlayers.Remove(player);
                            else pluginWitnesS.witnessedPlayers[player] = value - 1;
                        }

                        removeOffenseList.Add(offense);
                    }
                }

                if (removeOffenseList.Count > 0)
                {
                    foreach (var offense in removeOffenseList)
                    {
                        pluginPlayer.offenses.Remove(offense.GetHashCode());
                    }
                    removeOffenseList.Clear();
                    pluginPlayer.UpdateWantedLevel(false);
                }
                
                if (player.isHuman)
                {
                    if (!player.IsOutside && Random.value < pluginPlayer.wantedNormalized * 0.08f)
                    {
                        SpawnInterior(player);
                    }
                }

                if (player.IsMount<ShMovable>(out var mount) && mount is ShEmergencyVehicle vehicle && vehicle.siren &&
                    Physics.Raycast(
                        vehicle.mainT.TransformPoint(vehicle.svTransport.frontOffset),
                        vehicle.mainT.forward,
                        out var raycastHit,
                        50f,
                        MaskIndex.physical) && raycastHit.collider.TryGetComponent(out ShTransport otherTransport))
                {
                    var otherDriver = otherTransport.controller;
                    if (otherDriver && !otherDriver.isHuman && !otherDriver.svPlayer.targetEntity &&
                        otherDriver.svPlayer.currentState.index != LifeCore.PullOver.index)
                    {
                        otherDriver.svPlayer.SetState(LifeCore.PullOver.index);
                    }
                }

                yield return delay;
            }
        }

        public ShPlayer SpawnInterior(ShPlayer target)
        {
            var spawnBot = LifeManager.GetAvailable<ShPlayer>(LifeCore.policeIndex, WaypointType.Player);

            if (spawnBot)
            {
                var spawnT = target.svEntity.GetDoor.spawnPoint;

                if (spawnT)
                {
                    spawnBot.svPlayer.SpawnBot(
                        spawnT.position,
                        spawnT.rotation,
                        target.GetPlace,
                        null,
                        target,
                        target);

                    return spawnBot;
                }
            }

            return null;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool Death(ShDestroyable destroyable, ShPlayer attacker)
        {
            if (LifeManager.pluginPlayers.TryGetValue(destroyable, out var pluginPlayer))
            {
                pluginPlayer.ClearWitnessed();
            }

            return true;
        }

        private const string securityPanel = "securityPanel";
        private const string enterPasscode = "enterPasscode";
        private const string setPasscode = "setPasscode";
        private const string clearPasscode = "clearPasscode";
        private const string upgradeSecurity = "upgradeSecurity";
        private const string hackPanel = "hackPanel";
        private const string crackPanel = "crackPanel";
        private const string crackInventoryOption = "crackInventory";
        //private const string crackTransportOption = "crackTransport";
        private const string videoPanel = "videoPanel";
        private const string customVideo = "customVideo";
        private const string stopVideo = "stopVideo";

        private const float securityCutoff = 0.99f;

        [Execution(ExecutionMode.Additive)]
        public override bool SecurityPanel(ShPlayer player, ShApartment apartment)
        {
            var options = new List<LabelID>
            {
                new LabelID("Enter Passcode", enterPasscode),
                new LabelID("Set Passcode", setPasscode),
                new LabelID("Clear Passcode", clearPasscode),
                new LabelID("Hack Panel", hackPanel)
            };

            var title = "&7Security Panel";
            if (player.ownedApartments.TryGetValue(apartment, out var apartmentPlace))
            {
                title += ": Level " + apartmentPlace.security.ToPercent();
                if (apartmentPlace.security < securityCutoff)
                    options.Add(new LabelID($"Upgrade Security (Cost: ${SecurityUpgradeCost(apartmentPlace.security)})", upgradeSecurity));
            }

            player.svPlayer.SendOptionMenu(title, apartment.ID, securityPanel, options.ToArray(), new LabelID[] { new LabelID("Select", string.Empty) });

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool VideoPanel(ShPlayer player, ShEntity videoEntity)
        {
            var options = new List<LabelID>();

            if (VideoPermission(player, videoEntity, PermEnum.VideoCustom))
            {
                options.Add(new LabelID("&6Custom Video URL", customVideo));
            }

            if (VideoPermission(player, videoEntity, PermEnum.VideoStop))
            {
                options.Add(new LabelID("&4Stop Video", stopVideo));
            }

            if (VideoPermission(player, videoEntity, PermEnum.VideoDefault))
            {
                int index = 0;
                foreach (var option in player.manager.svManager.videoOptions)
                {
                    options.Add(new LabelID(option.label, index.ToString()));
                    index++;
                }
            }

            player.svPlayer.SendOptionMenu("&7Video Panel", videoEntity.ID, videoPanel, options.ToArray(), new LabelID[] { new LabelID("Select", string.Empty) });

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool BuyApartment(ShPlayer player, ShApartment apartment)
        {
            if (player.ownedApartments.ContainsKey(apartment))
            {
                player.svPlayer.SendGameMessage("Already owned");
            }
            else if (apartment.svApartment.BuyEntity(player))
            {
                player.svPlayer.BuyApartment(apartment);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SellApartment(ShPlayer player, ShApartment apartment)
        {
            if (!LifeUtility.trySell.Limit(player))
            {
                player.svPlayer.SendGameMessage("Are you sure? Sell again to confirm..");
            }
            else if (player.ownedApartments.TryGetValue(apartment, out var place))
            {
                player.TransferMoney(DeltaInv.AddToMe, apartment.value / 2);
                player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.SellApartment, apartment.ID);
                player.svPlayer.SellApartment(place);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Invite(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && other.IsUp && player.IsMobile && !player.InOwnApartment)
            {
                foreach (var apartment in player.ownedApartments.Keys)
                {
                    if (apartment.DistanceSqr(other) <= Util.inviteDistanceSqr)
                    {
                        other.svPlayer.SvEnterDoor(apartment.ID, player, true);
                        return true;
                    }
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool KickOut(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && other.IsUp && player.IsMobile && player.InOwnApartment && other.GetPlace == player.GetPlace)
            {
                other.svPlayer.SvEnterDoor(other.GetPlace.mainDoor.ID, player, true);
            }

            return true;
        }


        [Execution(ExecutionMode.Additive)]
        public override bool Collect(ShPlayer player, ShEntity e, bool consume)
        {
            if (!e.IsOutside && e.svEntity.respawnable && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.AddCrime(CrimeIndex.Theft, null);
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool StopInventory(ShPlayer player, bool sendToSelf)
        {
            if (!player.otherEntity && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer)) // Only null if being searched
            {
                foreach (var p in player.viewers)
                {
                    if (((MyJobInfo)p.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement)
                    {
                        pluginPlayer.AddCrime(CrimeIndex.Obstruction, null);
                        return true;
                    }
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Bomb(ShPlayer player, ShVault vault)
        {
            if (LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                pluginPlayer.AddCrime(CrimeIndex.Bombing, null);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Lockpick(ShPlayer player, ShTransport transport)
        {
            if (LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                pluginPlayer.AddCrime(CrimeIndex.AutoTheft, null);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool ViewInventory(ShPlayer player, ShEntity searchee, bool force)
        {
            if (searchee is ShPlayer playerSearchee && ((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement &&
                ((MyJobInfo)playerSearchee.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement && !searchee.Shop &&
                LifeManager.pluginPlayers.TryGetValue(playerSearchee, out var pluginSearchee))
            {
                foreach (var i in searchee.myItems.Values)
                {
                    var item = i.item;

                    if (item.illegal && (!item.license || !searchee.HasItem(item.license)))
                    {
                        pluginSearchee.AddCrime(CrimeIndex.Contraband, null);
                        player.svPlayer.SendGameMessage("Contraband Found - Take unlicensed items");
                        break;
                    }
                }
            }

            return true;
        }

        private IEnumerator CheckValidMinigame(MinigameContainer minigameContainer)
        {
            while (minigameContainer.Active && minigameContainer.IsValid())
            {
                yield return null;
            }

            if (minigameContainer.Active) minigameContainer.player.svPlayer.SvMinigameStop(true);
        }

        private int SecurityUpgradeCost(float currentLevel) => (int)(15000f * currentLevel * currentLevel + 200f);

        [Execution(ExecutionMode.Additive)]
        public override bool OptionAction(ShPlayer player, int targetID, string menuID, string optionID, string actionID)
        {
            switch (menuID)
            {
                case securityPanel:
                    var apartment = EntityCollections.FindByID<ShApartment>(targetID);

                    if (!apartment) return true;

                    switch (optionID)
                    {
                        case enterPasscode:
                            player.svPlayer.DestroyMenu(securityPanel);
                            player.svPlayer.SendInputMenu("Enter Passcode", targetID, enterPasscode, InputField.ContentType.Password);
                            break;
                        case setPasscode:
                            player.svPlayer.DestroyMenu(securityPanel);
                            player.svPlayer.SendInputMenu("Set Passcode", targetID, setPasscode, InputField.ContentType.Password);
                            break;
                        case clearPasscode:
                            if (player.ownedApartments.TryGetValue(apartment, out var apartmentPlace))
                            {
                                apartmentPlace.passcode = null;
                                player.svPlayer.SendGameMessage("Apartment Passcode Cleared");
                            }
                            else player.svPlayer.SendGameMessage("No Apartment Owned");
                            break;
                        case upgradeSecurity:
                            if (player.ownedApartments.TryGetValue(apartment, out var securityPlace) && securityPlace.security < securityCutoff)
                            {
                                var upgradeCost = SecurityUpgradeCost(securityPlace.security);

                                if (player.MyMoneyCount >= upgradeCost)
                                {
                                    player.TransferMoney(DeltaInv.RemoveFromMe, upgradeCost);
                                    securityPlace.security += 0.1f;
                                    player.svPlayer.SendGameMessage("Apartment Security Upgraded");
                                    player.svPlayer.SvSecurityPanel(apartment.ID);
                                }
                                else
                                {
                                    player.svPlayer.SendGameMessage("Insufficient funds");
                                }
                            }
                            else player.svPlayer.SendGameMessage("Unable");
                            break;
                        case hackPanel:
                            var options = new List<LabelID>();
                            foreach (var clone in apartment.GetPlace.clones)
                            {
                                if (clone.owner)
                                {
                                    options.Add(new LabelID($"{clone.owner.username} - Difficulty: {clone.security.ToPercent()}", clone.owner.username));
                                }
                            }
                            player.svPlayer.DestroyMenu(securityPanel);
                            player.svPlayer.SendOptionMenu("&7Places", targetID, hackPanel, options.ToArray(), new LabelID[] { new LabelID("Hack", string.Empty) });
                            break;
                    }
                    break;

                case hackPanel:
                    var hackingContainer = new HackingContainer(player, targetID, optionID);
                    if (hackingContainer.IsValid())
                    {
                        player.svPlayer.DestroyMenu(hackPanel);
                        player.svPlayer.StartHackingMenu("Hack Security Panel", targetID, menuID, optionID, hackingContainer.GetPlace.security);
                        player.StartCoroutine(CheckValidMinigame(hackingContainer));
                    }
                    break;

                case videoPanel:
                    var videoEntity = EntityCollections.FindByID(targetID);

                    if (optionID == customVideo && VideoPermission(player, videoEntity, PermEnum.VideoCustom))
                    {
                        player.svPlayer.SendGameMessage("Only direct video links supported - Can upload to Imgur or Discord and link that");
                        player.svPlayer.DestroyMenu(videoPanel);
                        player.svPlayer.SendInputMenu("Direct MP4/WEBM URL", targetID, customVideo, InputField.ContentType.Standard, 128);
                    }
                    else if (optionID == stopVideo && VideoPermission(player, videoEntity, PermEnum.VideoStop))
                    {
                        videoEntity.svEntity.SvStopVideo();
                        player.svPlayer.DestroyMenu(videoPanel);
                    }
                    else if (VideoPermission(player, videoEntity, PermEnum.VideoDefault, true) && int.TryParse(optionID, out var index))
                    {
                        videoEntity.svEntity.SvStartDefaultVideo(index);
                        player.svPlayer.DestroyMenu(videoPanel);
                    }
                    break;

                case LifeEvents.crimesMenu:
                    var criminal = EntityCollections.FindByID<ShPlayer>(targetID);

                    if (criminal && LifeManager.pluginPlayers.TryGetValue(criminal, out var pluginCriminal) && int.TryParse(optionID, out var offenseHash) &&
                        pluginCriminal.offenses.TryGetValue(offenseHash, out var o))
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(o.crime.crimeName);
                        sb.AppendLine("Time since Offense: " + o.ElapsedTime);
                        sb.AppendLine("Expires in: " + o.TimeLeft.TimeStringFromSeconds());
                        sb.AppendLine("Witness: " + (o.witness ? "&c" + o.witness.username : "&aNo Witness"));
                        sb.AppendLine(o.disguised ? "&aDisguised" : "&cNo Disguise");
                        sb.AppendLine("&fClothing during crime:");
                        foreach (var wearableIndex in o.wearables)
                        {
                            if (SceneManager.Instance.TryGetEntity<ShWearable>(wearableIndex, out var wearable))
                            {
                                sb.AppendLine(" - " + wearable.itemName);
                            }
                        }
                        player.svPlayer.DestroyMenu(LifeEvents.crimesMenu);
                        player.svPlayer.SendTextMenu(o.crime.crimeName, sb.ToString());
                    }

                    break;

                default:
                    if (targetID >= 0)
                    {
                        player.svPlayer.job.OnOptionMenuAction(targetID, menuID, optionID, actionID);
                    }
                    else
                    {
                        var target = EntityCollections.FindByID<ShPlayer>(-targetID);
                        if (target) target.svPlayer.job.OnOptionMenuAction(player.ID, menuID, optionID, actionID);
                    }
                    break;
            }

            return true;
        }

        // Change Video permissions handling here: Default allows video controls in own apartment (else follow group permission settings)
        private bool VideoPermission(ShPlayer player, ShEntity videoPlayer, PermEnum permission, bool checkLimit = false)
        {
            if (!videoPlayer) return false;

            if (checkLimit)
            {
                const int videoLimit = 3;

                int videoCount = 0;
                foreach (var e in videoPlayer.svEntity.sector.controlled)
                {
                    if (e != videoPlayer && !string.IsNullOrWhiteSpace(e.svEntity.videoURL))
                        videoCount++;
                }

                if (videoCount >= videoLimit)
                {
                    player.svPlayer.SendGameMessage($"Video limit of {videoLimit} reached");
                    return false;
                }
            }

            return player.InActionRange(videoPlayer) && (player.InOwnApartment || player.svPlayer.HasPermissionBP(permission));
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SubmitInput(ShPlayer player, int targetID, string menuID, string input)
        {
            switch (menuID)
            {
                case enterPasscode:
                    var a1 = EntityCollections.FindByID<ShApartment>(targetID);

                    foreach (var a in a1.GetPlace.clones)
                    {
                        if (a.passcode != null && a.passcode == input)
                        {
                            player.svPlayer.SvEnterDoor(targetID, a.owner, true);
                            return true;
                        }
                    }
                    player.svPlayer.SendGameMessage("Passcode: No Match");
                    break;

                case setPasscode:
                    var a2 = EntityCollections.FindByID<ShApartment>(targetID);
                    if (a2 && player.ownedApartments.TryGetValue(a2, out var ap2))
                    {
                        ap2.passcode = input;
                        player.svPlayer.SendGameMessage("Apartment Passcode Set");
                        return true;
                    }
                    player.svPlayer.SendGameMessage("No Apartment Owned");
                    break;

                case customVideo:
                    var videoEntity = EntityCollections.FindByID(targetID);

                    // Do URL validation input here **MUST CHECK FOR HTTPS** Non-Secure Protocol isn't allowed on Android
                    // Maybe only allow StartsWith("https://i.imgur.com") for safety
                    if (VideoPermission(player, videoEntity, PermEnum.VideoCustom, true) && input.StartsWith("https://"))
                    {
                        videoEntity.svEntity.SvStartCustomVideo(input);
                    }
                    else
                    {
                        player.svPlayer.SendGameMessage("Must have permission and start with 'https://'");
                    }
                    break;
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool MinigameFinished(ShPlayer player, bool successful, int targetID, string menuID, string optionID)
        {
            if (!LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                return true;

            switch (menuID)
            {
                case hackPanel:
                    if (EntityCollections.TryGetPlayerByNameOrID(optionID, out var owner))
                    {
                        if (successful)
                        {
                            player.StartCoroutine(EnterDoorDelay(player, targetID, optionID, true, 1f));
                        }
                        else if (pluginPlayer.ApartmentUnlawful(owner))
                        {
                            pluginPlayer.AddCrime(CrimeIndex.Trespassing, owner);
                        }
                    }
                    break;

                case crackPanel:
                    if (successful)
                    {
                        player.StartCoroutine(OpenInventoryDelay(player, targetID, 1f, true));
                    }
                    else
                    {
                        player.TransferItem(DeltaInv.RemoveFromMe, player.manager.lockpick);
                    }

                    // To stop Active/Valid() checks for a lockpick
                    player.svPlayer.minigame = null;

                    pluginPlayer.AddCrime(CrimeIndex.Robbery, null);
                    break;
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool CrackStart(ShPlayer player, int entityID)
        {
            var crackingContainer = new CrackingContainer(player, entityID);
            if (crackingContainer.IsValid())
            {
                player.svPlayer.StartCrackingMenu("Crack Inventory Lock", entityID, crackPanel, crackInventoryOption,
                    Mathf.Clamp01(crackingContainer.targetEntity.InventoryValue() / 30000f));
                player.StartCoroutine(CheckValidMinigame(crackingContainer));
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Mount(ShPlayer player, ShMountable mount, byte seat)
        {
            if (player.isHuman && player.IsMountController && mount.svMountable.mountLicense && !player.HasItem(mount.svMountable.mountLicense) &&
                LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.AddCrime(CrimeIndex.NoLicense, null);
            }

            return true;
        }


        private IEnumerator EnterDoorDelay(ShPlayer player, int doorID, string senderName, bool trespassing, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (EntityCollections.TryGetPlayerByNameOrID(senderName, out var sender) && 
                LifeManager.pluginPlayers.TryGetValue(player, out var lifeSourcePlayer))
            {
                lifeSourcePlayer.trespassing = trespassing;
                player.svPlayer.SvEnterDoor(doorID, sender, true);
            }
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SetParent(ShEntity entity, Transform parent)
        {
            if (parent == SceneManager.Instance.ExteriorT && 
                LifeManager.pluginPlayers.TryGetValue(entity, out var lifeSourcePlayer))
                lifeSourcePlayer.trespassing = false;

            return true;
        }

        private IEnumerator OpenInventoryDelay(ShPlayer player, int entityID, float delay, bool force = false)
        {
            yield return new WaitForSeconds(delay);
            player.svPlayer.SvView(entityID, force);
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SetWearable(ShPlayer player, ShWearable wearable)
        {
            if (LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                pluginPlayer.UpdateWantedLevel(true);

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool RestrainOther(ShPlayer player, ShPlayer hitPlayer, ShRestraint restraint)
        {
            if (LifeManager.pluginPlayers.TryGetValue(hitPlayer, out var hitPluginPlayer))
            {
                hitPlayer.svPlayer.Restrain(player, restraint.restrained);
                if (hitPluginPlayer.wantedLevel == 0 && LifeManager.pluginPlayers.TryGetValue(player, out var pluginPlayer) &&
                    (((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement || ((MyJobInfo)hitPlayer.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement))
                {
                    pluginPlayer.AddCrime(CrimeIndex.FalseArrest, hitPlayer);
                }
                else if (!player.isHuman && ((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement)
                {
                    hitPluginPlayer.GoToJail();
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool SameSector(ShEntity entity)
        {
            if (entity.isHuman)
            {
                foreach (var s in entity.svEntity.localSectors.Values)
                {
                    if (s.humans.Count == 0)
                    {
                        SpawnSector(entity.Player, s);
                    }
                }
            }

            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool NewSector(ShEntity entity, List<Sector> newSectors)
        {
            if (entity.isHuman)
            {
                foreach (var s in newSectors)
                {
                    if (s.humans.Count == 0)
                    {
                        SpawnSector(entity.Player, s);
                    }
                }
            }

            return true;
        }
        [Execution(ExecutionMode.Additive)]
        public override bool Fire(ShPlayer player)
        {
            if (player.curEquipable is ShGun)
            {
                foreach (var p in player.svPlayer.GetLocalInRange<ShPlayer>(20f))
                {
                    if (p && !p.isHuman && p.svPlayer.currentState.index == Core.Waypoint.index)
                    {
                        p.svPlayer.SetState(Core.Flee.index);
                    }
                }
            }

            return true;
        }

        public void SpawnSector(ShPlayer player, Sector sector)
        {
            foreach (var g in LifeManager.waypointGroups)
            {
                g.SpawnRandom(player, sector);
            }
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Deposit(ShPlayer player, int entityID, int amount)
        {
            if (!player.svPlayer.CanUseApp(entityID, AppIndex.Deposit) ||
                player.LifePlayer().wantedLevel > 0 || amount <= 0 || player.MyMoneyCount < amount)
            {
                player.svPlayer.SendGameMessage("Fraudulent activity detected");
            }
            else
            {
                player.TransferMoney(DeltaInv.RemoveFromMe, amount, true);
                player.svPlayer.bankBalance += amount;
                player.svPlayer.AppendTransaction(amount);
                player.svPlayer.SvAppDeposit(entityID);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool Withdraw(ShPlayer player, int entityID, int amount)
        {
            if (!player.svPlayer.CanUseApp(entityID, AppIndex.Withdraw) ||
                player.LifePlayer().wantedLevel > 0 || amount <= 0 || player.svPlayer.bankBalance < amount)
            {
                player.svPlayer.SendGameMessage("Fraudulent activity detected");
            }
            else
            {
                player.TransferMoney(DeltaInv.AddToMe, amount, true);
                player.svPlayer.bankBalance -= amount;
                player.svPlayer.AppendTransaction(-amount);
                player.svPlayer.SvAppWithdraw(entityID);
            }
            return true;
        }

        [Execution(ExecutionMode.Additive)]
        public override bool TryGetJob(ShPlayer player, ShPlayer employer)
        {
            if (employer.svPlayer.job.info.characterType != CharacterType.All && player.characterType != employer.svPlayer.job.info.characterType)
            {
                player.svPlayer.SendGameMessage("You're not the correct mob type for this job");
            }
            else if (((MyJobInfo)employer.svPlayer.job.info).groupIndex != GroupIndex.Criminal && player.LifePlayer().wantedLevel > 0)
            {
                player.svPlayer.SendGameMessage("We don't accept wanted criminals");
            }
            else
            {
                player.svPlayer.SvTrySetJob(employer.svPlayer.spawnJobIndex, true, true);
            }

            return true;
        }
    }
}

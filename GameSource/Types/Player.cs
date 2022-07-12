using BrokeProtocol.API;
using BrokeProtocol.Collections;
using BrokeProtocol.CustomEvents;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.AI;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BrokeProtocol.GameSource.Types
{
    public class PluginPlayer
    {
        ShPlayer player;

        public float jailExitTime;

        public List<Offense> offenses = new List<Offense>();

        public int wantedLevel;
        public float wantedNormalized;

        public PluginPlayer(ShPlayer player)
        {
            this.player = player;
        }

        public void StartJailTimer(float jailtime)
        {
            player.svPlayer.SvShowTimer(jailtime);
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

                if (player.svPlayer.job.info.shared.groupIndex != GroupIndex.Prisoner)
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

        public int GetFineAmount()
        {
            var fine = 0;
            foreach (var o in offenses) fine += o.crime.fine;
            return fine;
        }

        public bool InvalidCrime(byte crimeIndex)
        {
            foreach (var o in offenses)
            {
                if (o.crime.index == crimeIndex && (Time.time - o.commitTime) < o.crime.repeatDelay)
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearCrimes()
        {
            if (SceneManager.Instance.isServer)
            {
                foreach (var o in offenses)
                {
                    if (o.witness) o.witness.svPlayer.witnessedPlayers.Remove(player);
                }
            }
            else if (player.clPlayer.isMain)
            {
                ClManager.Instance.ShowGameMessage("Crimes Cleared");
            }

            offenses.Clear();
            UpdateWantedLevel(false);
        }

        public void ClearWitnessed()
        {
            foreach (var criminal in player.svPlayer.witnessedPlayers.Keys)
            {
                if(Manager.pluginPlayers.TryGetValue(criminal, out var pluginCriminal))
                {
                    pluginCriminal.RemoveWitness(player);
                    //criminal.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.RemoveWitness, player.ID);
                }
            }

            player.svPlayer.witnessedPlayers.Clear();
        }

        public void RemoveWitness(ShPlayer witness)
        {
            foreach (var offense in offenses)
            {
                if (offense.witness == witness) offense.witness = null;
            }

            UpdateWantedLevel(false);
        }

        public void UpdateWantedLevel(bool updateDisguised)
        {
            var totalExpiration = 0f;

            foreach (var o in offenses)
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

                totalExpiration += o.AdjustedExpiration();
            }

            wantedLevel = Mathf.CeilToInt(Mathf.Min(totalExpiration / 360f, Utility.maxWantedLevel));
        }

        public void AddCrime(byte crimeIndex, ShPlayer victim)
        {
            if (player.svPlayer.godMode || InvalidCrime(crimeIndex)) return;

            var crime = Utility.crimeTypes[crimeIndex];

            ShPlayer witness;
            if (!crime.witness)
            {
                witness = victim; // May be null which is fine in this case
            }
            else
            {
                witness = player.svPlayer.GetWitness(victim);
                if (!witness) return;
            }

            if (witness)
            {
                witness.svPlayer.AddWitnessedCriminal(player);
            }

            offenses.Add(new Offense(crime, player.curWearables, witness));
            UpdateWantedLevel(false);

            if (SceneManager.Instance.isClient)
            {
                ClManager.Instance.ShowGameMessage("Committed Crime: " + crime.crimeName);
            }

            for (int i = 1; i <= Utility.maxWantedLevel; i++)
            {
                player.svPlayer.VisualElementVisibility(Utility.starName, i <= wantedLevel);
            }

            // Don't hand out crime penalties for criminal jobs and default job
            if (player.svPlayer.job.info.shared.groupIndex != GroupIndex.Criminal && player.svPlayer.job.info.shared.jobIndex > 0)
            {
                player.svPlayer.Reward(-crime.experiencePenalty, -crime.fine);
            }
        }

        public void RemoveCrime(int index)
        {
            offenses.RemoveAt(index);
            UpdateWantedLevel(false);
        }

        public bool ApartmentUnlawful(ShPlayer apartmentOwner) => apartmentOwner != player && (player.svPlayer.job.info.shared.groupIndex != GroupIndex.LawEnforcement || Manager.pluginPlayers[apartmentOwner].wantedLevel == 0);

        public bool ApartmentTrespassing(ShPlayer apartmentOwner) => player.svPlayer.trespassing && ApartmentUnlawful(apartmentOwner);

        public int GoToJail()
        {
            if (player.IsDead || !player.IsRestrained || player.svPlayer.job.info.shared.groupIndex == GroupIndex.Prisoner)
            {
                return 0;
            }

            var time = 0f;
            var fine = 0;
            foreach (var o in offenses)
            {
                time += o.crime.jailtime;
                fine += o.crime.fine;
            }

            if (fine <= 0) return 0;

            player.svPlayer.SvSetJob(BPAPI.Jobs[Core.prisonerIndex], true, false);
            var jailSpawn = Manager.jails.GetRandom().mainT;
            player.svPlayer.SvRestore(jailSpawn.position, jailSpawn.rotation, jailSpawn.parent.GetSiblingIndex());
            player.svPlayer.SvForceEquipable(player.Hands.index);
            ClearCrimes();

            // Remove jail items
            foreach (var i in player.myItems.Values.ToArray())
            {
                if (i.item.illegal)
                {
                    player.TransferItem(DeltaInv.RemoveFromMe, i.item.index, i.count, true);
                }
            }

            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.StartJailTimer(time);
            }

            return fine;
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

        public ApartmentPlace GetPlace => targetPlayer.ownedApartments.TryGetValue(targetApartment, out var apartmentPlace) ? apartmentPlace : null;
    }

    public class CrackingContainer : MinigameContainer
    {
        public CrackingContainer(ShPlayer player, int entityID) : base(player, entityID)
        {
        }

        public override bool IsValid()
        {
            if (!base.IsValid()) return false;

            if (!player.HasItem(player.manager.lockpick))
            {
                player.svPlayer.SendGameMessage($"Missing {player.manager.lockpick.itemName} item");
                return false;
            }

            return true;
        }
    }

    public class Player : Movable
    {
        [Target(GameSourceEvent.PlayerDestroy, ExecutionMode.Override)]
        public void OnDestroy(ShPlayer player)
        {
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.ClearWitnessed();
                pluginPlayer.ClearCrimes();
            }

            Manager.pluginPlayers.Remove(player);
        }

        [Target(GameSourceEvent.PlayerLoad, ExecutionMode.Override)]
        public void OnLoad(ShPlayer player)
        {
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                if (player.svPlayer.PlayerData.Character.CustomData.TryFetchCustomData(offensesKey, out List<CrimeSave> offensesList))
                {
                    var wearables = new ShWearable[player.curWearables.Length];
                    foreach (var crimeSave in offensesList)
                    {
                        for (int i = 0; i < wearables.Length; i++)
                        {
                            // Future/mod-proofing
                            if (i < crimeSave.Wearables.Length &&
                                SceneManager.Instance.TryGetEntity<ShWearable>(crimeSave.Wearables[i], out var w))
                            {
                                wearables[i] = w;
                            }

                            if (!wearables[i]) wearables[i] = ShManager.Instance.nullWearable[i];
                        }

                        ShPlayer witness = null;

                        if (crimeSave.WitnessBotID != 0)
                        {
                            witness = EntityCollections.FindByID<ShPlayer>(crimeSave.WitnessBotID);
                        }
                        else if (!string.IsNullOrWhiteSpace(crimeSave.WitnessPlayerAccount))
                        {
                            foreach (var p in EntityCollections.Humans)
                            {
                                if (p.username == crimeSave.WitnessPlayerAccount)
                                {
                                    witness = p;
                                    break;
                                }
                            }
                        }

                        pluginPlayer.offenses.Add(new Offense(Utility.crimeTypes[crimeSave.Index], wearables, witness, crimeSave.TimeSinceLast));
                    }

                    pluginPlayer.UpdateWantedLevel(true);
                }

                if (player.svPlayer.PlayerData.Character.CustomData.TryFetchCustomData(offensesKey, out float jailtime) && jailtime > 0f)
                {
                    pluginPlayer.StartJailTimer(jailtime);
                }
            }
        }

        //[Target(GameSourceEvent.PlayerAddItem, ExecutionMode.Override)]
        //public void OnAddItem(ShPlayer player, int itemIndex, int amount, bool dispatch) { }

        //[Target(GameSourceEvent.PlayerRemoveItem, ExecutionMode.Override)]
        //public void OnRemoveItem(ShPlayer player, int itemIndex, int amount, bool dispatch) { }

        //[Target(GameSourceEvent.PlayerCommand, ExecutionMode.Override)]
        //public void OnCommand(ShPlayer player, string message) { }

        private const string offensesKey = "Offenses";
        private const string jailtimeKey = "Jailtime";

        [Target(GameSourceEvent.PlayerSave, ExecutionMode.Override)]
        public void OnSave(ShPlayer player) {
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                var offensesList = new List<CrimeSave>();

                foreach (var offense in pluginPlayer.offenses)
                {
                    offensesList.Add(new CrimeSave(offense.crime.index, offense.wearables, Time.time - offense.commitTime, offense.witness));
                }

                player.svPlayer.PlayerData.Character.CustomData.AddOrUpdate(offensesKey, offensesList);
                player.svPlayer.PlayerData.Character.CustomData.AddOrUpdate(jailtimeKey, Mathf.Max(0f, pluginPlayer.jailExitTime - Time.time));
            }
        }

        [Target(GameSourceEvent.PlayerTransferItem, ExecutionMode.Override)]
        public void OnTransferItem(ShPlayer player, byte deltaType, int itemIndex, int amount, bool dispatch)
        {
            switch(deltaType)
            {
                case DeltaInv.OtherToMe:
                    var otherPlayer = player.otherEntity as ShPlayer;

                    if (player.svPlayer.job.info.shared.groupIndex == GroupIndex.LawEnforcement)
                    {
                        if (SceneManager.Instance.TryGetEntity<ShItem>(itemIndex, out var item) && item.illegal)
                        {
                            if (otherPlayer && otherPlayer.Shop && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                            {
                                pluginPlayer.AddCrime(CrimeIndex.Theft, otherPlayer);
                            }
                        }
                        else if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                        {
                            pluginPlayer.AddCrime(CrimeIndex.Theft, otherPlayer);
                        }
                    }
                    else if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                    {
                        pluginPlayer.AddCrime(CrimeIndex.Theft, otherPlayer);
                    }

                    if (otherPlayer && !otherPlayer.isHuman && Random.value < 0.25f && otherPlayer.svPlayer.SetAttackState(player))
                    {
                        player.svPlayer.SvStopInventory(true);
                    }
                    break;
            }
            
        }

        //[Target(GameSourceEvent.PlayerMenuClosed, ExecutionMode.Override)]
        //public void OnMenuClosed(ShPlayer player, string menuID, bool manualClose) => player.svPlayer.SvGlobalChatMessage("[Menu Closed Event] " + menuID + " " + manualClose);

        [Target(GameSourceEvent.PlayerInitialize, ExecutionMode.Override)]
        public void OnInitialize(ShPlayer player)
        {
            Manager.pluginPlayers.Add(player, new PluginPlayer(player));
            player.svPlayer.SvAddSelfAction("MyCrimes", "My Crimes");
            //player.svPlayer.SvAddInventoryAction("StealItem", "ShItem", ButtonType.Buyable, "Steal");
        }

        [Target(GameSourceEvent.PlayerSpawn, ExecutionMode.Override)]
        public void OnSpawn(ShPlayer player)
        {
            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.ClearCrimes();
            }
            player.StartCoroutine(Maintenance(player));
        }

        public IEnumerator Maintenance(ShPlayer player)
        {
            yield return null;

            var delay = new WaitForSeconds(5f);

            Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer);

            while (!player.IsDead)
            {
                for (int i = pluginPlayer.offenses.Count - 1; i >= 0; i--)
                {
                    if (Time.time >= pluginPlayer.offenses[i].commitTime + pluginPlayer.offenses[i].AdjustedExpiration())
                    {
                        var witness = pluginPlayer.offenses[i].witness;

                        if (witness && witness.svPlayer.witnessedPlayers.TryGetValue(player, out var value))
                        {
                            if (value <= 1) witness.svPlayer.witnessedPlayers.Remove(player);
                            else witness.svPlayer.witnessedPlayers[player] = value - 1;
                        }
                        
                        pluginPlayer.RemoveCrime(i);
                        //player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.RemoveCrime, i);
                    }
                }

                if (player.GetStanceIndex == StanceIndex.Sleep)
                {
                    player.svPlayer.UpdateStatsDelta(0f, 0f, 0.02f);
                }
                else
                {
                    var injuryDelta = 0.05f / Util.hundredF;
                    var stomachLoss = player.injuryAmount[(int)BodyPart.Abdomen] * injuryDelta;
                    var energyLoss = player.injuryAmount[(int)BodyPart.Chest] * injuryDelta;
                    const float statsDelta = -0.0018f;
                    player.svPlayer.UpdateStatsDelta(statsDelta - stomachLoss, statsDelta - stomachLoss, statsDelta - energyLoss);
                }

                var totalDamage = 0f;
                foreach (var f in player.stats)
                {
                    if (f >= 0.75f) totalDamage -= 2f;
                    else if (f == 0f) totalDamage += 2f;
                }

                if (totalDamage > 0f) player.svPlayer.Damage(DamageIndex.Null, totalDamage);
                else if (totalDamage < 0f && player.health < player.maxStat * 0.5f) player.svPlayer.SvHeal(-totalDamage);

                if (player.isHuman)
                {
                    if (!player.IsOutside && Random.value < pluginPlayer.wantedNormalized * 0.08f)
                    {
                        SpawnAttack(player);
                    }

                    if (player.otherEntity && (!player.otherEntity.isActiveAndEnabled || !player.InActionRange(player.otherEntity)))
                    {
                        player.svPlayer.SvStopInventory(true);
                    }
                }
                else if (Random.value < 0.1f)
                {
                    if (player.injuries.Count > 0)
                    {
                        player.svPlayer.SvConsume(SceneManager.Instance.healerCollection.GetRandom().Value);
                    }
                    else
                    {
                        player.svPlayer.SvConsume(SceneManager.Instance.consumablesCollection.GetRandom().Value);
                    }
                }

                yield return delay;
            }
        }

        public ShPlayer SpawnAttack(ShPlayer target)
        {
            var spawnEntity = target.svEntity.svManager.GetAvailable(Core.policeIndex);

            if (spawnEntity)
            {
                var spawnBot = spawnEntity.Player;
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
                            null,
                            target);

                        return spawnBot;
                    }
                }
            }

            return null;
        }

        [Target(GameSourceEvent.PlayerGlobalChatMessage, ExecutionMode.Override)]
        public void OnGlobalChatMessage(ShPlayer player, string message)
        {
            if (Utility.chatted.Limit(player)) return;

            message = message.CleanMessage();

            if (string.IsNullOrWhiteSpace(message)) return;

            Debug.Log($"[CHAT] {player.username}:{message}");

            // 'true' if message starts with command prefix
            if (CommandHandler.OnEvent(player, message)) return;

            player.svPlayer.Send(SvSendType.All, Channel.Reliable, ClPacket.GlobalChatMessage, player.ID, message);
        }

        [Target(GameSourceEvent.PlayerLocalChatMessage, ExecutionMode.Override)]
        public void OnLocalChatMessage(ShPlayer player, string message)
        {
            if (Utility.chatted.Limit(player)) return;

            message = message.CleanMessage();

            if (string.IsNullOrWhiteSpace(message)) return;

            Debug.Log($"[CHAT] {player.username}:{message}");

            // 'true' if message starts with command prefix
            if (CommandHandler.OnEvent(player, message)) return;

            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.LocalChatMessage, player.ID, message);
        }

        [Target(GameSourceEvent.PlayerDamage, ExecutionMode.Override)]
        public void OnDamage(ShPlayer player, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            if (player.svPlayer.godMode || player.IsDead || player.IsShielded(damageIndex, collider)) return;

            if (damageIndex != DamageIndex.Null)
            {
                BodyEffect effect;
                var random = Random.value;

                if (random < 0.6f)
                    effect = BodyEffect.Null;
                else if (random < 0.8f)
                    effect = BodyEffect.Pain;
                else if (random < 0.925f)
                    effect = BodyEffect.Bloodloss;
                else
                    effect = BodyEffect.Fracture;

                BodyPart part;

                var capsuleHeight = player.capsule.direction == 1 ? player.capsule.height : player.capsule.radius * 2f;

                var hitY = player.GetLocalY(hitPoint);

                if (damageIndex == DamageIndex.Random)
                {
                    part = (BodyPart)Random.Range(0, (int)BodyPart.Count);
                }
                else if (damageIndex == DamageIndex.Melee && player.IsBlocking(damageIndex))
                {
                    part = BodyPart.Arms;
                    amount *= 0.3f;
                }
                else if (collider == player.headCollider) // Headshot
                {
                    part = BodyPart.Head;
                    amount *= 2f;
                }
                else if (hitY >= capsuleHeight * 0.75f)
                {
                    part = Random.value < 0.5f ? BodyPart.Arms : BodyPart.Chest;
                }
                else if (hitY >= capsuleHeight * 0.5f)
                {
                    part = BodyPart.Abdomen;
                    amount *= 0.8f;
                }
                else
                {
                    part = BodyPart.Legs;
                    amount *= 0.5f;
                }

                if (effect != BodyEffect.Null)
                {
                    player.svPlayer.SvAddInjury(part, effect, (byte)Random.Range(10, 50));
                }
            }

            if (!player.isHuman)
            {
                amount /= player.svPlayer.svManager.settings.difficulty;
            }

            amount -= amount * (player.armorLevel / 200f);

            base.OnDamage(player, damageIndex, amount, attacker, collider, source, hitPoint);

            if (player.IsDead) return;

            // Still alive, do knockdown and AI retaliation

            if (player.stance.setable)
            {
                if (player.isHuman && player.health < 15f)
                {
                    player.svPlayer.SvForceStance(StanceIndex.KnockedOut);
                    // If knockout AI, set AI state Null
                }
                else if (Random.value < player.manager.damageTypes[(int)damageIndex].fallChance)
                {
                    player.StartCoroutine(player.svPlayer.KnockedDown());
                }
            }

            if (attacker && attacker != player)
            {
                if (!player.isHuman)
                {
                    player.svPlayer.SetAttackState(attacker);
                }
                else
                {
                    var playerFollower = player.svPlayer.follower;
                    if (playerFollower) playerFollower.svPlayer.SetAttackState(attacker);
                }

                var attackerFollower = attacker.svPlayer.follower;

                if (attackerFollower)
                {
                    attackerFollower.svPlayer.SetAttackState(player);
                }
            }
        }

        private IEnumerator SpectateDelay(ShPlayer player, ShPlayer target)
        {
            yield return new WaitForSeconds(2f);
            player.svPlayer.SvSpectate(target);
        }

        [Target(GameSourceEvent.PlayerDeath, ExecutionMode.Override)]
        public void OnDeath(ShPlayer player, ShPlayer attacker)
        {
            if (attacker && attacker != player)
            {
                if (player.isHuman) player.StartCoroutine(SpectateDelay(player, attacker));

                player.svPlayer.RemoveItemsDeath(true);
            }
            else
            {
                player.svPlayer.RemoveItemsDeath(false);
            }

            if (Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.ClearWitnessed();
            }

            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.ShowTimer, player.svPlayer.RespawnTime);

            player.SetStance(StanceIndex.Dead);
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

        [Target(GameSourceEvent.PlayerSecurityPanel, ExecutionMode.Override)]
        public void OnSecurityPanel(ShPlayer player, ShApartment apartment)
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
                title += ": Level " + apartmentPlace.svSecurity.ToPercent();
                if (apartmentPlace.svSecurity < securityCutoff)
                    options.Add(new LabelID($"Upgrade Security (Cost: ${SecurityUpgradeCost(apartmentPlace.svSecurity).ToString()})", upgradeSecurity));
            }

            player.svPlayer.SendOptionMenu(title, apartment.ID, securityPanel, options.ToArray(), new LabelID[] { new LabelID("Select", string.Empty) });
        }

        [Target(GameSourceEvent.PlayerVideoPanel, ExecutionMode.Override)]
        public void OnVideoPanel(ShPlayer player, ShEntity videoEntity)
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
        }

        [Target(GameSourceEvent.PlayerBuyApartment, ExecutionMode.Override)]
        public void OnBuyApartment(ShPlayer player, ShApartment apartment)
        {
            if (player.ownedApartments.ContainsKey(apartment))
            {
                player.svPlayer.SendGameMessage("Already owned");
            }
            else if (apartment.svApartment.BuyEntity(player))
            {
                apartment.svApartment.SvSetApartmentOwner(player);
            }
        }

        [Target(GameSourceEvent.PlayerSellApartment, ExecutionMode.Override)]
        public void OnSellApartment(ShPlayer player, ShApartment apartment)
        {
            if (!Utility.trySell.Limit(player))
            {
                player.svPlayer.SendGameMessage("Are you sure? Sell again to confirm..");
                return;
            }

            if (player.ownedApartments.TryGetValue(apartment, out var place))
            {
                player.TransferMoney(DeltaInv.AddToMe, apartment.value / 2);
                player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.SellApartment, apartment.ID);
                player.svPlayer.CleanupApartment(place);
            }
        }

        [Target(GameSourceEvent.PlayerInvite, ExecutionMode.Override)]
        public void OnInvite(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && other.IsUp && player.IsMobile && !player.InOwnApartment)
            {
                foreach (var apartment in player.ownedApartments.Keys)
                {
                    if (apartment.DistanceSqr(other) <= Util.inviteDistanceSqr)
                    {
                        other.svPlayer.SvEnterDoor(apartment.ID, player, true);
                        return;
                    }
                }
            }
        }

        [Target(GameSourceEvent.PlayerKickOut, ExecutionMode.Override)]
        public void OnKickOut(ShPlayer player, ShPlayer other)
        {
            if (other.isHuman && other.IsUp && player.IsMobile && player.InOwnApartment && other.GetPlace == player.GetPlace)
            {
                other.svPlayer.SvEnterDoor(other.GetPlace.mainDoor.ID, player, true);
            }
        }

        [Target(GameSourceEvent.PlayerRespawn, ExecutionMode.Override)]
        public void OnRespawn(ShPlayer player)
        {
            if (player.isHuman)
            {
                var newSpawn = Manager.spawnLocations.GetRandom().mainT;
                player.svPlayer.originalPosition = newSpawn.position;
                player.svPlayer.originalRotation = newSpawn.rotation;
                player.svPlayer.originalParent = newSpawn.parent;
            }

            base.OnRespawn(player);

            if (player.isHuman)
            {
                // Back to spectate self on Respawn
                player.svPlayer.SvSpectate(player);
            }

            player.svPlayer.SvForceEquipable(player.Hands.index);
        }

        [Target(GameSourceEvent.PlayerReward, ExecutionMode.Override)]
        public void OnReward(ShPlayer player, int experienceDelta, int moneyDelta)
        {
            if (!player.isHuman) return;

            // Player rank affects money rewards (can adjust for modding)
            moneyDelta *= player.rank + 1;

            if (moneyDelta > 0)
            {
                player.TransferMoney(DeltaInv.AddToMe, moneyDelta);
            }
            else if (moneyDelta < 0)
            {
                player.TransferMoney(DeltaInv.RemoveFromMe, -moneyDelta);
            }

            if (player.svPlayer.job.info.upgrades.Length <= 1) return;

            var experience = player.experience + experienceDelta;

            if (experience > Util.maxExperience)
            {
                if (player.rank >= player.svPlayer.job.info.upgrades.Length - 1)
                {
                    if (player.experience != Util.maxExperience)
                    {
                        player.svPlayer.SetExperience(Util.maxExperience, true);
                    }
                }
                else
                {
                    int newRank = player.rank + 1;
                    player.svPlayer.AddJobItems(player.svPlayer.job.info, player.rank, newRank, false);
                    player.svPlayer.SetRank(newRank);
                    player.svPlayer.SetExperience(experience - Util.maxExperience, false);
                }
            }
            else if (experience <= 0)
            {
                if (player.rank <= 0)
                {
                    player.svPlayer.SendGameMessage("You lost your job");
                    player.svPlayer.SvResetJob();
                }
                else
                {
                    player.svPlayer.SetRank(player.rank - 1);
                    player.svPlayer.SetExperience(experience + Util.maxExperience, false);
                }
            }
            else
            {
                player.svPlayer.SetExperience(experience, true);
            }
        }

        [Target(GameSourceEvent.PlayerCollect, ExecutionMode.Override)]
        public void OnCollect(ShPlayer player, ShEntity e, bool consume)
        {
            if (consume && e is ShConsumable consumable)
            {
                if (!player.svPlayer.SvConsume(consumable)) return;
            }
            else
            {
                var collectedItems = e.CollectedItems;

                for (int i = 0; i < collectedItems.Length; i++)
                {
                    var inv = collectedItems[i];

                    var itemIndex = inv.itemName.GetPrefabIndex();
                    if (SceneManager.Instance.TryGetEntity<ShItem>(itemIndex, out var item))
                    {
                        player.svPlayer.CollectItem(item, inv.count);
                        if (i == 0) player.svPlayer.SvTrySetEquipable(itemIndex);
                    }
                }
            }

            if (!e.IsOutside && e.svEntity.respawnable && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.AddCrime(CrimeIndex.Theft, null);
            }

            e.Destroy();
        }

        [Target(GameSourceEvent.PlayerStopInventory, ExecutionMode.Override)]
        public void OnStopInventory(ShPlayer player, bool sendToSelf)
        {
            if (!player.otherEntity && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer)) // Only null if being searched
            {
                foreach (var p in player.viewers)
                {
                    if (p.svPlayer.job.info.shared.groupIndex == GroupIndex.LawEnforcement)
                    {
                        pluginPlayer.AddCrime(CrimeIndex.Obstruction, null);
                        return;
                    }
                }
            }
        }

        [Target(GameSourceEvent.PlayerBomb, ExecutionMode.Override)]
        public void OnBomb(ShPlayer player, ShVault vault)
        {
            player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.bomb.index);

            player.svPlayer.SvShowTimer(vault.svVault.bombTimer);

            if(Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                pluginPlayer.AddCrime(CrimeIndex.Bombing, null);

            vault.svVault.SvSetVault(VaultState.Bombing);
            vault.svVault.instigator = player;
        }

        [Target(GameSourceEvent.PlayerRepair, ExecutionMode.Override)]
        public void OnRepair(ShPlayer player, ShTransport transport)
        {
            if (transport.svTransport.SvHeal(transport.maxStat, player))
                player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.toolkit);
        }

        [Target(GameSourceEvent.PlayerLockpick, ExecutionMode.Override)]
        public void OnLockpick(ShPlayer player, ShTransport transport)
        {
            if (player.CanMount(transport, false, true, out _) && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                player.TransferItem(DeltaInv.RemoveFromMe, ShManager.Instance.lockpick);
                transport.state = EntityState.Unlocked;
                transport.svTransport.SendTransportState();

                player.svPlayer.SvTryMount(transport.ID, true);

                pluginPlayer.AddCrime(CrimeIndex.AutoTheft, null);
            }
        }

        [Target(GameSourceEvent.PlayerViewInventory, ExecutionMode.Override)]
        public void OnViewInventory(ShPlayer player, ShEntity searchee, bool force)
        {
            if (searchee is ShPlayer playerSearchee && player.svPlayer.job.info.shared.groupIndex == GroupIndex.LawEnforcement &&
                playerSearchee.svPlayer.job.info.shared.groupIndex != GroupIndex.LawEnforcement && !searchee.Shop &&
                Manager.pluginPlayers.TryGetValue(playerSearchee, out var pluginSearchee))
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
        }

        [Target(GameSourceEvent.PlayerInjury, ExecutionMode.Override)]
        public void OnInjury(ShPlayer player, BodyPart part, BodyEffect effect, byte amount)
        {
            player.AddInjury(part, effect, amount);
            player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.AddInjury, (byte)part, (byte)effect, amount);
        }

        [Target(GameSourceEvent.PlayerKick, ExecutionMode.Override)]
        public void OnKick(ShPlayer player, ShPlayer target, string reason)
        {
            ChatHandler.SendToAll($"{target.displayName} Kicked: {reason}");

            player.manager.svManager.KickConnection(target.svPlayer.connection);
        }

        [Target(GameSourceEvent.PlayerBan, ExecutionMode.Override)]
        public void OnBan(ShPlayer player, ShPlayer target, string reason)
        {
            ChatHandler.SendToAll($"{target.displayName} Banned: {reason}");

            player.svPlayer.SvBanDatabase(target.username, reason);
            player.manager.svManager.Disconnect(target.svPlayer.connection, DisconnectTypes.Banned);
        }

        [Target(GameSourceEvent.PlayerRemoveItemsDeath, ExecutionMode.Override)]
        public void OnRemoveItemsDeath(ShPlayer player, bool dropItems)
        {
            var removedItems = new List<InventoryItem>();

            // Allows players to keep items/rewards from job ranks
            foreach (var myItem in player.myItems.Values.ToArray())
            {
                int extra = myItem.count;

                if (player.svPlayer.job.info.upgrades.Length > player.rank)
                {
                    for (int rankIndex = player.rank; rankIndex >= 0; rankIndex--)
                    {
                        foreach (var i in player.svPlayer.job.info.upgrades[rankIndex].items)
                        {
                            if (myItem.item.name == i.itemName)
                            {
                                extra = Mathf.Max(0, myItem.count - i.count);
                            }
                        }
                    }
                }

                // Remove everything except legal items currently worn
                if (extra > 0 && (myItem.item.illegal || !(myItem.item is ShWearable w) || player.curWearables[(int)w.type].index != w.index))
                {
                    removedItems.Add(new InventoryItem(myItem.item, extra));
                    player.TransferItem(DeltaInv.RemoveFromMe, myItem.item.index, extra, true);
                }
            }

            // Only drop items if attacker present, to prevent AI suicide item farming
            if (dropItems && removedItems.Count > 0)
            {
                var briefcase = player.svPlayer.SpawnBriefcase();

                if (briefcase)
                {
                    foreach (var invItem in removedItems)
                    {
                        if (Random.value < 0.8f)
                        {
                            invItem.count = Mathf.CeilToInt(invItem.count * Random.Range(0.05f, 0.3f));
                            briefcase.myItems.Add(invItem.item.index, invItem);
                        }
                    }
                }
            }
        }

        [Target(GameSourceEvent.PlayerRestrain, ExecutionMode.Override)]
        public void OnRestrain(ShPlayer player, ShPlayer initiator, ShRestrained restrained)
        {
            if (player.svPlayer.godMode) return;

            if (player.curMount) player.svPlayer.SvDismount();

            player.svPlayer.SvSetEquipable(restrained);

            if (!player.isHuman)
            {
                player.svPlayer.SetState(StateIndex.Restrained);
            }
            else
            {
                player.svPlayer.SendGameMessage("You've been restrained");
            }
        }

        [Target(GameSourceEvent.PlayerUnrestrain, ExecutionMode.Override)]
        public void OnUnrestrain(ShPlayer player, ShPlayer initiator)
        {
            player.svPlayer.SvSetEquipable(player.Hands);

            if (!player.isHuman)
            {
                player.svPlayer.SvDismount();
                player.svPlayer.ResetAI();
            }
            else
            {
                player.svPlayer.SendGameMessage("You've been freed");
            }
        }

        [Target(GameSourceEvent.PlayerServerInfo, ExecutionMode.Override)]
        public void OnServerInfo(ShPlayer player)
        {
            player.svPlayer.SendTextMenu("&7Server Info", player.svPlayer.svManager.serverInfo);
        }

        [Target(GameSourceEvent.PlayerDisplayName, ExecutionMode.Override)]
        public void OnDisplayName(ShPlayer player, string username)
        {
            player.username = username;

            if (player.isHuman)
            {
                player.svPlayer.tagname = player.svPlayer.PrimaryGroup?.Tag ?? string.Empty;
            }
            else
            {
                player.svPlayer.tagname = string.Empty;
            }

            // &f to reset back to white after any tag colors
            player.displayName = $"{player.svPlayer.tagname}&f{player.username}";
        }

        [Target(GameSourceEvent.PlayerEnterDoor, ExecutionMode.Override)]
        public void OnEnterDoor(ShPlayer player, ShDoor door, ShPlayer sender, bool forceEnter)
        {
            if (!forceEnter)
            {
                if (player.IsRestrained)
                {
                    player.svPlayer.SendGameMessage("You are restrained");
                    return;
                }

                if (door.svDoor.key && !player.HasItem(door.svDoor.key))
                {
                    player.svPlayer.SendGameMessage("Need " + door.svDoor.key.itemName + " to enter");
                    return;
                }

                if (!player.InActionRange(door))
                {
                    return;
                }
            }

            ShMountable baseEntity;

            if (player.curMount is ShPlayer mountPlayer)
            {
                baseEntity = mountPlayer;
            }
            else
            {
                baseEntity = player;
                if (player.curMount) player.svPlayer.SvDismount();
            }

            if (door is ShApartment apartment && sender.ownedApartments.TryGetValue(apartment, out var place))
            {
                baseEntity.svMountable.SvRelocate(place.mainDoor.spawnPoint, place.mTransform);
            }
            else
            {
                var otherDoor = door.svDoor.other;
                baseEntity.svMountable.SvRelocate(otherDoor.spawnPoint, otherDoor.GetPlace.mTransform);
            }
        }


        [Target(GameSourceEvent.PlayerFollower, ExecutionMode.Override)]
        public void OnFollower(ShPlayer player, ShPlayer other)
        {
            if (player.svPlayer.follower)
            {
                if (player.svPlayer.follower != other)
                {
                    player.svPlayer.SendGameMessage("Already have a follower");
                }
                else
                {
                    other.svPlayer.SvDismount();
                    other.svPlayer.ClearLeader();
                    other.svPlayer.ResetAI();
                }
            }
            else if (!other.svPlayer.leader && other.CanFollow && !other.svPlayer.IsBusy)
            {
                other.svPlayer.SetFollowState(player);
            }
            else
            {
                player.svPlayer.SendGameMessage("NPC is occupied");
            }
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

        [Target(GameSourceEvent.PlayerOptionAction, ExecutionMode.Override)]
        public void OnOptionAction(ShPlayer player, int targetID, string menuID, string optionID, string actionID)
        {
            switch (menuID)
            {
                case securityPanel:
                    var apartment = EntityCollections.FindByID<ShApartment>(targetID);

                    if (!apartment) return;

                    switch (optionID)
                    {
                        case enterPasscode:
                            player.svPlayer.SendInputMenu("Enter Passcode", targetID, enterPasscode, InputField.ContentType.Password);
                            break;
                        case setPasscode:
                            player.svPlayer.SendInputMenu("Set Passcode", targetID, setPasscode, InputField.ContentType.Password);
                            break;
                        case clearPasscode:
                            if (player.ownedApartments.TryGetValue(apartment, out var apartmentPlace))
                            {
                                apartmentPlace.svPasscode = null;
                                player.svPlayer.SendGameMessage("Apartment Passcode Cleared");
                            }
                            else player.svPlayer.SendGameMessage("No Apartment Owned");
                            break;
                        case upgradeSecurity:
                            if (player.ownedApartments.TryGetValue(apartment, out var securityPlace) && securityPlace.svSecurity < securityCutoff)
                            {
                                var upgradeCost = SecurityUpgradeCost(securityPlace.svSecurity);

                                if (player.MyMoneyCount >= upgradeCost)
                                {
                                    player.TransferMoney(DeltaInv.RemoveFromMe, upgradeCost);
                                    securityPlace.svSecurity += 0.1f;
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
                            foreach (var clone in apartment.svApartment.clones.Values)
                            {
                                options.Add(new LabelID($"{clone.svOwner.username} - Difficulty: {clone.svSecurity.ToPercent()}", clone.svOwner.username));
                            }
                            player.svPlayer.SendOptionMenu("&7Places", targetID, hackPanel, options.ToArray(), new LabelID[] { new LabelID("Hack", string.Empty) });
                            break;
                    }
                    break;

                case hackPanel:
                    var hackingContainer = new HackingContainer(player, targetID, optionID);
                    if (hackingContainer.IsValid())
                    {
                        player.svPlayer.StartHackingMenu("Hack Security Panel", targetID, menuID, optionID, hackingContainer.GetPlace.svSecurity);
                        player.StartCoroutine(CheckValidMinigame(hackingContainer));
                    }
                    break;

                case videoPanel:
                    var videoEntity = EntityCollections.FindByID(targetID);

                    if (optionID == customVideo && VideoPermission(player, videoEntity, PermEnum.VideoCustom))
                    {
                        player.svPlayer.SendGameMessage("Only direct video links supported - Can upload to Imgur or Discord and link that");
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

        [Target(GameSourceEvent.PlayerSubmitInput, ExecutionMode.Override)]
        public void OnSubmitInput(ShPlayer player, int targetID, string menuID, string input)
        {
            switch (menuID)
            {
                case enterPasscode:
                    var a1 = EntityCollections.FindByID<ShApartment>(targetID);

                    foreach (var a in a1.svApartment.clones.Values)
                    {
                        if (a.svPasscode != null && a.svPasscode == input)
                        {
                            player.svPlayer.SvEnterDoor(targetID, a.svOwner, true);
                            return;
                        }
                    }
                    player.svPlayer.SendGameMessage("Passcode: No Match");
                    break;

                case setPasscode:
                    var a2 = EntityCollections.FindByID<ShApartment>(targetID);
                    if (a2 && player.ownedApartments.TryGetValue(a2, out var ap2))
                    {
                        ap2.svPasscode = input;
                        player.svPlayer.SendGameMessage("Apartment Passcode Set");
                        return;
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
        }

        [Target(GameSourceEvent.PlayerTextPanelButton, ExecutionMode.Override)]
        public void OnTextPanelButton(ShPlayer player, string menuID, string optionID)
        {
            switch (menuID)
            {
                case ExampleCommand.coinFlip:
                    switch (optionID)
                    {
                        case ExampleCommand.heads:
                            player.StartCoroutine(DelayCoinFlip(player, ExampleCommand.heads));
                            break;
                        case ExampleCommand.tails:
                            player.StartCoroutine(DelayCoinFlip(player, ExampleCommand.tails));
                            break;
                        case ExampleCommand.cancel:
                            player.svPlayer.DestroyTextPanel(ExampleCommand.coinFlip);
                            break;
                    }
                    break;
            }
        }

        private IEnumerator DelayCoinFlip(ShPlayer player, string prediction)
        {
            const int coinFlipCost = 100;
            var delay = new WaitForSeconds(1f);

            player.svPlayer.SendTextPanel("Flipping coin..", ExampleCommand.coinFlip);
            yield return delay;

            if (player.MyMoneyCount < coinFlipCost)
            {
                player.svPlayer.SendTextPanel($"Need ${coinFlipCost} to play", ExampleCommand.coinFlip);
            }
            else
            {
                var coin = Random.value >= 0.5f ? ExampleCommand.heads : ExampleCommand.tails;

                if (coin == prediction)
                {
                    player.svPlayer.SendTextPanel($"Flipped {coin}!\n\n&aYou guessed right!", ExampleCommand.coinFlip);
                    player.TransferMoney(DeltaInv.AddToMe, coinFlipCost);
                }
                else
                {
                    player.svPlayer.SendTextPanel($"Flipped {coin}!\n\n&4You guessed wrong :(", ExampleCommand.coinFlip);
                    player.TransferMoney(DeltaInv.RemoveFromMe, coinFlipCost);
                }
            }

            yield return delay;
            player.svPlayer.DestroyTextPanel(ExampleCommand.coinFlip);
        }

        [Target(GameSourceEvent.PlayerReady, ExecutionMode.Override)]
        public void OnReady(ShPlayer player)
        {
            player.svPlayer.SendServerInfo();
        }

        [Target(GameSourceEvent.PlayerPoint, ExecutionMode.Override)]
        public void OnPoint(ShPlayer player, bool pointing)
        {
            player.pointing = pointing;
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.Point, player.ID, pointing);

            if (pointing && player.svPlayer.follower &&
                Physics.Raycast(player.GetOrigin, player.GetRotationT.forward, out var hit, Util.visibleRange, MaskIndex.hard) &&
                player.svPlayer.follower.svPlayer.NodeNear(hit.point) != null)
            {
                player.svPlayer.follower.svPlayer.SetGoToState(hit.point, Quaternion.LookRotation(hit.point - player.svPlayer.follower.GetPosition), player.GetParent);
            }
        }

        [Target(GameSourceEvent.PlayerAlert, ExecutionMode.Override)]
        public void OnAlert(ShPlayer player)
        {
            player.svPlayer.Send(SvSendType.LocalOthers, Channel.Reliable, ClPacket.Alert, player.ID);
            if (player.svPlayer.follower)
            {
                player.svPlayer.follower.svPlayer.SetFollowState(player);
            }
        }

        [Target(GameSourceEvent.PlayerMinigameFinished, ExecutionMode.Override)]
        public void OnHackFinished(ShPlayer player, bool successful, int targetID, string menuID, string optionID)
        {
            if (!Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                return;

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
        }

        [Target(GameSourceEvent.PlayerDestroySelf, ExecutionMode.Override)]
        public void OnDestroySelf(ShPlayer player)
        {
            if (!player.isHuman || !player.IsRestrained || !player.IsUp) base.OnDestroySelf(player);
        }

        [Target(GameSourceEvent.PlayerHandsUp, ExecutionMode.Override)]
        public void OnHandsUp(ShPlayer player, ShPlayer victim)
        {
            if (player.svPlayer.job.info.shared.groupIndex != GroupIndex.LawEnforcement && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.AddCrime(CrimeIndex.Intimidation, victim);
            }

            if (!victim.isHuman)
            {
                if (victim.svPlayer.targetEntity) return;

                if (victim.svPlayer.job.info.shared.groupIndex != GroupIndex.Citizen || Random.value < 0.2f)
                {
                    victim.svPlayer.SetAttackState(player);
                }
                else
                {
                    victim.svPlayer.SetState(StateIndex.Freeze);
                }
            }
            else
            {
                victim.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.HandsUp);
            }
        }

        [Target(GameSourceEvent.PlayerSetEquipable, ExecutionMode.Override)]
        public void OnSetEquipable(ShPlayer player, ShEquipable equipable)
        {
            if (!player.curEquipable || player.curEquipable.index != equipable.index)
            {
                player.svPlayer.SvForceEquipable(equipable.index);
            }
        }

        [Target(GameSourceEvent.PlayerCrackStart, ExecutionMode.Override)]
        public void OnCrackStart(ShPlayer player, int entityID)
        {
            var crackingContainer = new CrackingContainer(player, entityID);
            if (crackingContainer.IsValid())
            {
                player.svPlayer.StartCrackingMenu("Crack Inventory Lock", entityID, crackPanel, crackInventoryOption,
                    Mathf.Clamp01(crackingContainer.targetEntity.InventoryValue() / 30000f));
                player.StartCoroutine(CheckValidMinigame(crackingContainer));
            }
        }

        [Target(GameSourceEvent.PlayerMount, ExecutionMode.Override)]
        public void OnMount(ShPlayer player, ShMountable mount, byte seat)
        {
            player.svPlayer.SvDismount();
            player.Mount(mount, seat);
            player.SetStance(mount.seats[seat].stanceIndex);

            if (!player.isHuman)
            {
                player.svPlayer.ResetAI();
            }
            else if (seat == 0 && mount.svMountable.mountLicense && !player.HasItem(mount.svMountable.mountLicense) && 
                Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
            {
                pluginPlayer.AddCrime(CrimeIndex.NoLicense, null);
            }

            player.svPlayer.Send(SvSendType.Local, Channel.Reliable, ClPacket.Mount, player.ID, mount.ID, seat, mount.CurrentClip);
        }

        [Target(GameSourceEvent.PlayerDismount, ExecutionMode.Override)]
        public void OnDismount(ShPlayer player)
        {
            if (player.IsDriving)
            {
                // Send serverside transport position to override client-side predicted location while it was driven
                player.curMount.svMountable.SvRepositionSelf();
            }

            player.SetStance(StanceIndex.Stand);
            player.Dismount();

            // Start locking behavior after exiting vehicle
            if (player.curEquipable.ThrownHasGuidance)
            {
                player.svPlayer.StartLocking(player.curEquipable);
            }

            player.svPlayer.Send(SvSendType.Local, Channel.Reliable, ClPacket.Dismount, player.ID);
        }

        private IEnumerator EnterDoorDelay(ShPlayer player, int doorID, string senderName, bool trespassing, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (EntityCollections.TryGetPlayerByNameOrID(senderName, out var sender))
            {
                player.svPlayer.trespassing = trespassing;
                player.svPlayer.SvEnterDoor(doorID, sender, true);
            }
        }

        private IEnumerator OpenInventoryDelay(ShPlayer player, int entityID, float delay, bool force = false)
        {
            yield return new WaitForSeconds(delay);
            player.svPlayer.SvView(entityID, force);
        }

        [Target(GameSourceEvent.PlayerPlaceItem, ExecutionMode.Override)]
        public void OnPlaceItem(ShPlayer player, ShEntity placeableEntity, Vector3 position, Quaternion rotation, float spawnDelay)
        {
            if (spawnDelay > 0f)
            {
                player.svPlayer.svManager.AddNewEntityDelay(
                    placeableEntity,
                    player.GetPlace,
                    position,
                    rotation,
                    false,
                    spawnDelay);
            }
            else
            {
                player.svPlayer.svManager.AddNewEntity(
                    placeableEntity,
                    player.GetPlace,
                    position,
                    rotation,
                    false);
            }

            player.svPlayer.placementValid = true;
        }

        [Target(GameSourceEvent.PlayerResetAI, ExecutionMode.Override)]
        public void OnResetAI(ShPlayer player)
        {
            if (player.svPlayer.targetPlayer && !player.svPlayer.preFrame)
            {
                player.svPlayer.targetPlayer = null;
                player.svPlayer.Respawn();
                return;
            }

            if (player.IsKnockedOut && player.svPlayer.SetState(StateIndex.Null)) return;
            if (player.IsRestrained && player.svPlayer.SetState(StateIndex.Restrained)) return;
            player.svPlayer.SvTrySetEquipable(player.Hands.index);
            if (player.svPlayer.leader && player.svPlayer.SetFollowState(player.svPlayer.leader)) return;
            if (player.IsPassenger && player.svPlayer.SetState(StateIndex.Null)) return;

            player.svPlayer.targetEntity = null;

            if (player.IsDriving && player.svPlayer.SetState(StateIndex.Waypoint)) return;
            if (player.svPlayer.currentState.index == StateIndex.Freeze && !player.svPlayer.stop && player.svPlayer.SetState(StateIndex.Flee)) return;
            if (player.svPlayer.targetPlayer && player.svPlayer.SetAttackState(player.svPlayer.targetPlayer)) return;

            if (player.GetParent != player.svPlayer.originalParent)
            {
                player.svPlayer.ResetOriginal();
            }

            player.svPlayer.job.ResetJobAI();
        }

        [Target(GameSourceEvent.PlayerSetWearable, ExecutionMode.Override)]
        public void OnSetWearable(ShPlayer player, ShWearable wearable)
        {
            if(Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                pluginPlayer.UpdateWantedLevel(true);
        }

        [Target(GameSourceEvent.PlayerRestrainOther, ExecutionMode.Override)]
        public void OnRestrainOther(ShPlayer player, ShPlayer hitPlayer, ShRestraint restraint)
        {
            restraint.RemoveAmmo();

            if (Manager.pluginPlayers.TryGetValue(hitPlayer, out var hitPluginPlayer))
            {
                hitPlayer.svPlayer.Restrain(player, restraint.restrained);
                if (hitPluginPlayer.wantedLevel == 0 && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) &&
                    (player.svPlayer.job.info.shared.groupIndex != GroupIndex.LawEnforcement || hitPlayer.svPlayer.job.info.shared.groupIndex == GroupIndex.LawEnforcement))
                {
                    pluginPlayer.AddCrime(CrimeIndex.FalseArrest, hitPlayer);
                }
                else if (!player.isHuman && player.svPlayer.job.info.shared.groupIndex == GroupIndex.LawEnforcement)
                {
                    hitPluginPlayer.GoToJail();
                }
            }
        }

        [Target(GameSourceEvent.PlayerDeposit, ExecutionMode.Override)]
        public void OnDeposit(ShPlayer player, int entityID, int amount)
        {
            if (!player.svPlayer.CanUseApp(entityID, AppIndex.Withdraw) || !Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) || pluginPlayer.wantedLevel > 0 || amount <= 0 || player.svPlayer.bankBalance < amount)
            {
                player.svPlayer.SendGameMessage("Fraudulent activity detected");
                return;
            }

            player.TransferMoney(DeltaInv.AddToMe, amount, true);
            player.svPlayer.bankBalance -= amount;
            player.svPlayer.AppendTransaction(-amount);
            player.svPlayer.SvAppWithdraw(entityID);
        }

        [Target(GameSourceEvent.PlayerWithdraw, ExecutionMode.Override)]
        public void OnWithdraw(ShPlayer player, int entityID, int amount)
        {
            if (!player.svPlayer.CanUseApp(entityID, AppIndex.Withdraw) || !Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) || pluginPlayer.wantedLevel > 0 || amount <= 0 || player.svPlayer.bankBalance < amount)
            {
                player.svPlayer.SendGameMessage("Fraudulent activity detected");
                return;
            }

            player.TransferMoney(DeltaInv.AddToMe, amount, true);
            player.svPlayer.bankBalance -= amount;
            player.svPlayer.AppendTransaction(-amount);
            player.svPlayer.SvAppWithdraw(entityID);
        }

        [Target(GameSourceEvent.PlayerTryGetJob, ExecutionMode.Override)]
        public void OnTryGetJob(ShPlayer player, ShPlayer employer)
        {
            if (employer.svPlayer.job.info.characterType != CharacterType.All && player.characterType != employer.svPlayer.job.info.characterType)
            {
                player.svPlayer.SendGameMessage("You're not the correct mob type for this job");
            }
            else if (employer.svPlayer.job.info.shared.groupIndex == GroupIndex.Criminal && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer) && pluginPlayer.wantedLevel > 0)
            {
                player.svPlayer.SendGameMessage("We don't accept wanted criminals");
            }
            else
            {
                player.svPlayer.SvTrySetJob(employer.svPlayer.spawnJobIndex, true, true);
            }
        }
    }
}
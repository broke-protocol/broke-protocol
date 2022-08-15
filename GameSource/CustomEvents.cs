using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource;
using BrokeProtocol.GameSource.Jobs;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Managers;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.Jobs;
using BrokeProtocol.Utility.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrokeProtocol.CustomEvents
{
    public class CustomEvents : IScript
    {
        [CustomTarget]
        public void GetItemValue(ShPlayer player, ShItem item)
        {
            var other = player.otherEntity;

            if (other && !other.IsDead && other.Shop)
            {
                player.svPlayer.SendGameMessage('$' + other.GetMyItemValue(item, false).ToString());
            }
        }

        public const string crimesMenu = "CrimesMenu";

        private void SendCrimes(ShPlayer player, ShPlayer criminal)
        {
            if (Manager.pluginPlayers.TryGetValue(criminal, out var pluginPlayer))
            {
                var options = new List<LabelID>();

                foreach (var pair in pluginPlayer.offenses)
                {
                    var o = pair.Value;
                    options.Add(
                        new LabelID($"{o.crime.crimeName} | {o.TimeLeft.TimeStringFromSeconds()} | {(o.witness ? "&c" + o.witness.username : "&aNo Witness")} | {(o.disguised ? "&aDisguised" : "&cNo Disguise")}",
                        pair.Key.ToString())); // Send offense HashCode for lookup later
                }

                player.svPlayer.SendOptionMenu($"{player.username}'s Criminal Record: ${pluginPlayer.GetFineAmount()}", criminal.ID, crimesMenu, options.ToArray(), new LabelID[] { new LabelID("Get Details", string.Empty) });
            }
        }

        [CustomTarget]
        public void MyCrimes(ShPlayer player)
        {
            SendCrimes(player, player);
        }

        [CustomTarget]
        public void HandsUp(ShEntity target, ShPlayer player)
        {
            if (target is ShPlayer playerTarget && playerTarget.isActiveAndEnabled && playerTarget.IsMobile)
            {
                if (((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement && Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                {
                    pluginPlayer.AddCrime(CrimeIndex.Intimidation, playerTarget);
                }

                playerTarget.svPlayer.CommandHandsUp(player);
            }
        }

        [CustomTarget]
        public void ShowCrimes(ShEntity target, ShPlayer player)
        {
            if (target is ShPlayer criminal)
            {
                SendCrimes(player, criminal);
            }
        }

        [CustomTarget]
        public void SendToJail(ShEntity target, ShPlayer player)
        {
            if (((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement && target is ShPlayer criminal &&
                Manager.pluginPlayers.TryGetValue(criminal, out var pluginCriminal))
            {
                var fine = pluginCriminal.GoToJail();
                if (fine > 0 && player.svPlayer.job is Police) player.svPlayer.Reward(3, fine);
                else player.svPlayer.SendGameMessage("Confirm criminal is cuffed and has crimes");
            }
        }

        [CustomTarget]
        public void DrugTest(ShEntity target, ShPlayer player)
        {
            if (!(player.svPlayer.job is Police) || !(target is ShPlayer testee) || 
                testee.IsDead || !Manager.pluginPlayers.TryGetValue(testee, out var pluginTestee))
                return;

            foreach (var i in testee.injuries)
            {
                if (i.effect == BodyEffect.Drugged)
                {
                    pluginTestee.AddCrime(CrimeIndex.Intoxication, null);
                    var m = "Test Positive";
                    testee.svPlayer.SendGameMessage(m);
                    player.svPlayer.SendGameMessage(m);
                    return;
                }
            }

            var message = "Test Negative";
            testee.svPlayer.SendGameMessage(message);
            player.svPlayer.SendGameMessage(message);
        }

        [CustomTarget]
        public void AreaWarning(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShPlayer player && ((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement)
            {
                player.svPlayer.SendGameMessage($"Warning! You are about to enter {trigger.data}!");
            }
        }

        [CustomTarget]
        public void RestrictedArea(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShMovable movable)
            {
                var player = movable.controller;
                if (player && player.isHuman && !player.IsDead &&
                    ((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement && 
                    Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                {
                    pluginPlayer.AddCrime(CrimeIndex.Trespassing, null);
                }
            }
        }

        [CustomTarget]
        public void ThrowableTarget(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShThrown thrown)
            {
                var thrower = thrown.svEntity.instigator;
                if (thrower)
                {
                    thrower.TransferMoney(
                        DeltaInv.AddToMe,
                        Mathf.Clamp(Mathf.CeilToInt(0.5f * thrower.DistanceSqr(trigger.mainT.position)), 10, 1000),
                        true);
                }
                thrown.Destroy();
            }
        }

        [CustomTarget]
        public void JailExit(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShMovable movable)
            {
                var player = movable.controller;
                if (player && player.isHuman && !player.IsDead &&
                    ((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.Prisoner && 
                    Manager.pluginPlayers.TryGetValue(player, out var pluginPlayer))
                {
                    pluginPlayer.AddCrime(CrimeIndex.PrisonBreak, null);
                    player.svPlayer.SvResetJob();
                    player.svPlayer.Send(SvSendType.Self, Channel.Reliable, ClPacket.DestroyTimer);
                }
            }
        }


        [CustomTarget]
        public void Rearm(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShTransport transport)
            {
                transport.StartCoroutine(RearmCoroutine(trigger, transport));
            }
        }

        private IEnumerator RearmCoroutine(Serialized trigger, ShTransport transport)
        {
            var delay = new WaitForSeconds(2f);
            while(transport.svMovable.currentTriggers.Contains(trigger))
            {
                transport.Rearm(0.1f); // Rearm 10% of ammo capacity
                yield return delay;
            }
        }

        [CustomTarget]
        public void Repair(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShTransport transport)
            {
                transport.StartCoroutine(RepairCoroutine(trigger, transport));
            }
        }

        private IEnumerator RepairCoroutine(Serialized trigger, ShTransport transport)
        {
            var delay = new WaitForSeconds(2f);
            while (transport.svMovable.currentTriggers.Contains(trigger))
            {
                transport.svTransport.SvHeal(transport.maxStat * 0.1f); // Heal 10% of maxStat
                yield return delay;
            }
        }


        bool voidRunning;
        [CustomTarget]
        public void ButtonPush(ShEntity target, ShPlayer caller)
        {
            if (voidRunning)
            {
                caller.svPlayer.SendGameMessage("Do not challenge the void");
                return;
            }

            const int cost = 500;

            if (caller.MyMoneyCount < cost)
            {
                caller.svPlayer.SendGameMessage("Not enough cash");
                return;
            }

            caller.TransferMoney(DeltaInv.RemoveFromMe, cost);

            target.StartCoroutine(EnterTheVoid());
        }

        private IEnumerator EnterTheVoid()
        {
            voidRunning = true;

            var delay = new WaitForSecondsRealtime(0.1f);

            var duration = 4f;
            var startTime = Time.time;

            var originalTimeScale = Time.timeScale;
            var targetTimeScale = 0.25f;

            var defaultEnvironment = SceneManager.Instance.defaultEnvironment;

            var originalSky = SceneManager.Instance.skyColor;
            var originalCloud = SceneManager.Instance.cloudColor;
            var originalWater = SceneManager.Instance.waterColor;

            var targetSky = Color.red;
            var targetCloud = Color.black;
            var targetWater = Color.cyan;

            var normalizedClip = 0.25f;

            while (Time.time < startTime + duration)
            {
                yield return delay;

                var normalizedTime = (Time.time - startTime) / duration;

                float lerp;

                if (normalizedTime < normalizedClip)
                {
                    lerp = normalizedTime / normalizedClip;
                }
                else if (normalizedTime >= 1f - normalizedClip)
                {
                    lerp = (1f - normalizedTime) / normalizedClip;
                }
                else
                {
                    lerp = 1f;
                }

                SvManager.Instance.SvSetTimeScale(Mathf.Lerp(originalTimeScale, targetTimeScale, lerp));
                SvManager.Instance.SvSetSkyColor(Color.LerpUnclamped(originalSky, targetSky, lerp));
                SvManager.Instance.SvSetCloudColor(Color.LerpUnclamped(originalCloud, targetCloud, lerp));
                SvManager.Instance.SvSetWaterColor(Color.LerpUnclamped(originalWater, targetWater, lerp));
            }

            SvManager.Instance.SvSetTimeScale(originalTimeScale);

            if (defaultEnvironment)
            {
                SvManager.Instance.SvSetDefaultEnvironment();
            }
            else
            {
                SvManager.Instance.SvSetSkyColor(originalSky);
                SvManager.Instance.SvSetCloudColor(originalCloud);
                SvManager.Instance.SvSetWaterColor(originalWater);
            }

            voidRunning = false;
        }

        [CustomTarget]
        public void PlaceBounty(ShEntity target, ShPlayer player)
        {
            if (target is ShPlayer targetPlayer)
                if (targetPlayer.svPlayer.job is Hitman hitmanJob)
                    hitmanJob.PlaceBountyAction(player);
        }

        [CustomTarget]
        public void BountyList(ShEntity target, ShPlayer player)
        {
            if(target is ShPlayer targetPlayer)
                if(targetPlayer.svPlayer.job is Hitman hitmanJob)
                    hitmanJob.BountyListAction(player);
        }

        [CustomTarget]
        public void BountyListSelf(ShPlayer player)
        {
            if(player.svPlayer.job is Hitman hitmanJob)
                hitmanJob.BountyListAction(player);
        }

        [CustomTarget]
        public void ResetTarget(ShPlayer player)
        {
            if (player.svPlayer.job is TargetEntityJob targetEntityJob)
                targetEntityJob.ResetTarget();
        }

        [CustomTarget]
        public void RequestItem(ShEntity target, ShPlayer player) => ((target as ShPlayer)?.svPlayer.job as Mayor)?.RequestItemAction(player);

        [CustomTarget]
        public void RequestList(ShPlayer player) => (player.svPlayer.job as Mayor)?.RequestListAction();

        [CustomTarget]
        public void DeliverItem(ShEntity target, ShPlayer player) => (player.svPlayer.job as DeliveryMan)?.DeliverItemAction(target);

        [CustomTarget]
        public void RequestHeal(ShEntity target, ShPlayer player) => ((target as ShPlayer)?.svPlayer.job as Paramedic)?.RequestHeal(player);
    }
}

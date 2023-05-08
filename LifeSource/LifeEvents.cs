using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using System.Collections.Generic;

namespace BrokeProtocol.GameSource
{
    public class LifeEvents : IScript
    {
        public const string crimesMenu = "CrimesMenu";

        public void SendCrimes(ShPlayer player, ShPlayer criminal)
        {
            if (LifeManager.pluginPlayers.TryGetValue(criminal, out var pluginPlayer))
            {
                var options = new List<LabelID>();

                foreach (var pair in pluginPlayer.offenses)
                {
                    var o = pair.Value;
                    options.Add(
                        new LabelID($"{o.crime.crimeName} | {o.TimeLeft.TimeStringFromSeconds()} | {(o.witness ? "&c" + o.witness.username : "&aNo Witness")} | {(o.disguised ? "&aDisguised" : "&cNo Disguise")}",
                        pair.Key.ToString())); // Send offense HashCode for lookup later
                }

                player.svPlayer.SendOptionMenu($"{criminal.username}'s Criminal Record: ${pluginPlayer.GetFineAmount()}", criminal.ID, crimesMenu, options.ToArray(), new LabelID[] { new LabelID("Get Details", string.Empty) });
            }
        }

        [CustomTarget]
        public void MyCrimes(ShPlayer player) => SendCrimes(player, player);

        [CustomTarget]
        public void HandsUp(ShEntity target, ShPlayer player)
        {
            if (target is ShPlayer playerTarget && playerTarget.isActiveAndEnabled && playerTarget.IsMobile)
            {
                playerTarget.LifePlayer().CommandHandsUp(player);
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
            if (((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.LawEnforcement && target is ShPlayer criminal)
            {
                var fine = criminal.LifePlayer().GoToJail();
                if (fine > 0 && player.svPlayer.job is Police) player.svPlayer.Reward(3, fine);
                else player.svPlayer.SendGameMessage("Confirm criminal is cuffed and has crimes");
            }
        }

        [CustomTarget]
        public void DrugTest(ShEntity target, ShPlayer player)
        {
            if (player.svPlayer.job is not Police || target is not ShPlayer testee || testee.IsDead)
                return;

            foreach (var i in testee.injuries)
            {
                if (i.effect == BodyEffect.Drugged)
                {
                    testee.LifePlayer().AddCrime(CrimeIndex.Intoxication, null);
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
                    ((MyJobInfo)player.svPlayer.job.info).groupIndex != GroupIndex.LawEnforcement)
                {
                    player.LifePlayer().AddCrime(CrimeIndex.Trespassing, null);
                }
            }
        }

        [CustomTarget]
        public void JailExit(Serialized trigger, ShPhysical physical)
        {
            if (physical is ShMovable movable)
            {
                var player = movable.controller;
                if (player && player.isHuman && !player.IsDead &&
                    ((MyJobInfo)player.svPlayer.job.info).groupIndex == GroupIndex.Prisoner)
                {
                    player.LifePlayer().AddCrime(CrimeIndex.PrisonBreak, null);
                    player.svPlayer.SvResetJob();
                    player.svPlayer.DestroyText();
                }
            }
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

using BrokeProtocol.Collections;
using BrokeProtocol.Entities;
using BrokeProtocol.GameSource.Types;
using UnityEngine;
using System.Collections;

namespace BrokeProtocol.GameSource
{
    public static class Utility
    {
        public static void StartDestroyDelay(this ShEntity entity, float delay) => entity.StartCoroutine(DestroyDelay(entity, delay));

        public static IEnumerator DestroyDelay(this ShEntity entity, float delay)
        {
            //Wait 2 frames so an activate is already sent
            yield return null;
            yield return new WaitForSeconds(delay);
            if (entity.go)
            {
                entity.Destroy();
            }
        }

        public static IEnumerator RespawnDelay(this ShEntity entity)
        {
            var respawnTime = Time.time + entity.svEntity.RespawnTime;
            var delay = new WaitForSeconds(1f);

            while (entity && entity.IsDead)
            {
                if (Time.time > respawnTime)
                {
                    entity.svEntity.Respawn();
                    yield break;
                }
                yield return delay;
            }
        }

        public static readonly Vector2 defaultAnchor = new (0.5f, 0.15f);

        public const float slowSpeedSqr = 6f * 6f;

        public const string adminPermission = "admin";
        public const string allPermission = "all";

        public static GameSourcePlayer GamePlayer(this ShPlayer player) => Manager.pluginPlayers[player];

        public static LimitQueue<ShPlayer> chatted = new(8, 20f);

        public static LimitQueue<string> accountWipe = new(0, 5f);

        public static LimitQueue<ShPlayer> healed = new(3, 60f);

        public static LimitQueue<ShPlayer> unstuck = new(1, 45f);

        public static bool ValidCredential(this string credential) =>
            !string.IsNullOrWhiteSpace(credential) && credential.Length >= 3 && credential.Length <= 16;
    }
}

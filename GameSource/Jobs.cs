using BrokeProtocol.GameSource.Types;
using BrokeProtocol.Utility.Jobs;
using System.Collections;
using UnityEngine;

namespace BrokeProtocol.GameSource
{
    public abstract class LoopJob : Job
    {
        public override void ResetJob()
        {
            base.ResetJob();
            RestartCoroutines();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            RestartCoroutines();
        }

        private void RestartCoroutines()
        {
            if (player.isActiveAndEnabled)
            {
                var pluginPlayer = player.GamePlayer();
                if (pluginPlayer.jobCoroutine != null) player.StopCoroutine(pluginPlayer.jobCoroutine);
                pluginPlayer.jobCoroutine = player.StartCoroutine(JobCoroutine());
            }
        }

        private IEnumerator JobCoroutine()
        {
            var delay = new WaitForSeconds(1f);
            do
            {
                yield return delay;
                Loop();
            } while (true);
        }

        public abstract void Loop();
    }
}

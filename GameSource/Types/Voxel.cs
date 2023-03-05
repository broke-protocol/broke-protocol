using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using BrokeProtocol.Utility;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Voxel : VoxelEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Damage(ShDamageable damageable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 hitPoint, Vector3 hitNormal)
        {
            var voxel = damageable as ShVoxel;

            var radius = 0.002f * amount / voxel.mainT.localScale.magnitude;

            if (damageIndex == DamageIndex.Gun)
                radius *= 10f;

            radius = Mathf.Min(radius, 8f);

            voxel.DamageVoxels(voxel.GetSphere(voxel.GetHitPoint(hitPoint, hitNormal, -0.5f), radius), amount);

            return true;
        }
    }
}

using BrokeProtocol.API;
using BrokeProtocol.Entities;
using BrokeProtocol.Required;
using UnityEngine;

namespace BrokeProtocol.GameSource.Types
{
    public class Voxel : VoxelEvents
    {
        [Execution(ExecutionMode.Additive)]
        public override bool Damage(ShDamageable damageable, DamageIndex damageIndex, float amount, ShPlayer attacker, Collider collider, Vector3 source, Vector3 hitPoint)
        {
            var voxel = damageable as ShVoxel;

            var radius = 0.002f * amount / voxel.mainT.localScale.magnitude;

            if (damageIndex == DamageIndex.Gun)
                radius *= 10f;

            radius = Mathf.Min(8f, radius); // Clamp damage radius to reduce calculations

            Vector3 offset = hitPoint;

            if (source == hitPoint)
            {
                radius = Mathf.Max(1f, radius); // Increase min radius to 1 for non-projectile damage
            }
            else
            {
                offset += 0.01f * (hitPoint - source).normalized;
            }

            voxel.DamageVoxels(voxel.GetSphere(ShVoxel.ToInt3(voxel.mainT.InverseTransformPoint(offset)), radius), amount);

            return true;
        }
    }
}

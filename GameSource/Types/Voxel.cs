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

            radius = Mathf.Min(8, radius); // Clamp damage radius to reduce calculations

            voxel.DamageVoxels(voxel.GetSphere(ShVoxel.ToInt3(voxel.mainT.InverseTransformPoint(hitPoint + 0.01f * (hitPoint - source).normalized)), radius), amount);

            return true;
        }
    }
}

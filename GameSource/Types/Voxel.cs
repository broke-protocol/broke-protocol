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

            radius = Mathf.Clamp(radius, 0f, 8f);

            var localNormal = voxel.mainT.InverseTransformDirection(hitNormal) * Util.SQRT3 / 3f;
            localNormal.Scale(new Vector3(
                Mathf.Abs(voxel.mainT.localScale.x),
                Mathf.Abs(voxel.mainT.localScale.y),
                Mathf.Abs(voxel.mainT.localScale.z)));

            voxel.DamageVoxels(voxel.GetSphere(ShVoxel.ToInt3(voxel.mainT.InverseTransformPoint(hitPoint - voxel.mainT.TransformDirection(localNormal))), radius), amount);

            return true;
        }
    }
}

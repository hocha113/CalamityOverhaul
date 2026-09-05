using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 猎标追击箭：每 8°/帧咬向标记目标的追击箭。
    /// ai[0] = 目标 NPC whoAmI（生成端选定，whoAmI 跨端一致，各端转向确定性同步）。
    /// Misc 源生成，不进路由打标流，完全自治
    /// </summary>
    internal class GsPursuitArrow : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
        }

        public override void AI() {
            Life++;
            int idx = (int)TargetIndex;
            NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            //前 6 帧直飞拉开距离再咬弯，读作「分裂而出」
            if (Life > 6f && target != null && target.active && GsHuntMarkNPC.CanMark(target)) {
                float current = Projectile.velocity.ToRotation();
                float desired = (target.Center - Projectile.Center).ToRotation();
                float turned = current.AngleTowards(desired, MathHelper.ToRadians(8f));
                Projectile.velocity = turned.ToRotationVector2() * Projectile.velocity.Length();
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}

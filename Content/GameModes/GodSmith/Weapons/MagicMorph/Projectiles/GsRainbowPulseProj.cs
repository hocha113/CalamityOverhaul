using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 彩虹枪「彩虹脉冲」：周期性从彩虹弧头滑出的七色光珠，微追踪附近敌人。<br/>
    /// 追踪为各端同式的确定转向（目标位置由 NPC 同步承载），命中裁决在 owner 端
    /// </summary>
    internal class GsRainbowPulseProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RainbowRodBullet;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = 100;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override void AI() {
            //微追踪：向 300px 内最近敌缓转，速度恒定
            NPC target = null;
            float best = 300f;
            foreach (NPC npc in Main.npc) {
                if (npc.active && npc.CanBeChasedBy()) {
                    float d = npc.Distance(Projectile.Center);
                    if (d < best) {
                        best = d;
                        target = npc;
                    }
                }
            }
            float speed = 7f;
            if (target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Lerp(cur, want, 0.09f).SafeNormalize(Vector2.UnitX) * speed;
            }
            else if (Projectile.velocity.Length() < speed * 0.9f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;
            }
            Projectile.rotation += 0.2f;
        }
    }
}

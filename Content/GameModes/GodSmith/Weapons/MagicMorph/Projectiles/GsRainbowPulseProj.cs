using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 彩虹枪「彩虹脉冲」：周期性从彩虹弧头滑出的七色光珠，微追踪附近敌人。<br/>
    /// 追踪为各端同式的确定转向（目标位置由 NPC 同步承载），命中裁决在 owner 端
    /// </summary>
    internal class GsRainbowPulseProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
            if (!VaultUtils.isServer && Projectile.timeLeft % 2 == 0) {
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.62f);
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.08f, c, 0.2f)?.Configure(c, 10, 0.2f, 0.8f);
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.15f, 0.25f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            //七色光珠：色相随 identity 定相滚动，双层辉光（A=0）
            float hue = (Main.GlobalTimeWrappedHourly * 0.35f + Projectile.identity * 0.17f) % 1f;
            Color outer = Main.hslToRgb(hue, 1f, 0.6f) * 0.7f;
            outer.A = 0;
            float pulse = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, outer, 0f,
                glow.Size() / 2f, 0.5f * pulse, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Color core = Color.White * 0.55f;
            core.A = 0;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, core, 0f,
                glow.Size() / 2f, 0.24f * pulse, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }
    }
}

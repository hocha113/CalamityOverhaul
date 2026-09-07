using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 冲击波：零伤害的扩张环，波前扫过玩家给一次朝外冲量 34×(1-r/R)。先把人推开，再开火。
    /// 半径 += 50 + 60(1-t)²，扩到 MaxRadius 消散。ai[0]=宿主 whoAmI
    /// </summary>
    internal class EmpressShockwave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float MaxRadius = 3000f;
        private const float FrontDepth = 500f;

        private ref float Radius => ref Projectile.localAI[0];
        /// <summary>已推过的玩家位掩码（≤32 人，够用）</summary>
        private ref float PushedMask => ref Projectile.localAI[1];
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            float t = Radius / MaxRadius;
            Radius += 50f + 60f * (1f - t) * (1f - t);
            if (Radius >= MaxRadius) {
                Projectile.Kill();
                return;
            }

            //推开：本地玩家被波前扫过时给一次冲量（位置客户端权威，各端只推自己）
            Player me = Main.LocalPlayer;
            if (!VaultUtils.isServer && me.Alives()) {
                int bit = 1 << (me.whoAmI & 31);
                float d = Vector2.Distance(Projectile.Center, me.Center) - Radius;
                if (d < 0f && d > -FrontDepth && ((int)PushedMask & bit) == 0) {
                    PushedMask = (int)PushedMask | bit;
                    me.velocity += me.DirectionFrom(Projectile.Center) * 34f * (1f - t);
                    EmpressMotion.ShakeAlong(me.Center, me.DirectionFrom(Projectile.Center), 7f, 10);
                    EmpressScreenFX.PushFlash(me.DirectionFrom(Projectile.Center), 0.3f);
                }
            }
            Lighting.AddLight(Projectile.Center, EmpressMotion.Sun(0.5f).ToVector3() * 0.5f * (1f - t));
        }

        public override bool PreDraw(ref Color lightColor) {
            float t = Radius / MaxRadius;
            float alpha = (1f - t) * 0.8f;
            Color bright = Color.Lerp(new Color(230, 200, 255), new Color(255, 246, 224), DayBlend);
            Color main = Color.Lerp(new Color(180, 120, 255), new Color(255, 200, 120), DayBlend);
            Color deep = Color.Lerp(new Color(60, 20, 90), EmpressMotion.SunDark, DayBlend);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, Radius, 40f + 60f * t, bright, main, deep, alpha, 50f, 1f, 0.12f, Projectile.identity * 0.7f);
            return false;
        }
    }
}

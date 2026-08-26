using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 彩虹魔杖 B 形态「虹桥」：光标处架起 340px 任意角度虹弧，驻场 4s。<br/>
    /// ai[0]=桥轴角度（释放时的瞄准方向，随生成包过线）；
    /// 判定为 12 段拱弧线带（宽 20px），与可见桥面同源；
    /// 敌人穿桥受 tick 伤害，友方踩桥 +8% 移速（各端本地玩家自查，见 GsMorphPlayer）
    /// </summary>
    internal class GsRainbowBridgeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTicks = 240;
        private const float HalfLength = 170f;
        private const float ArchHeight = 30f;
        private const int Segments = 12;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = LifeTicks;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>桥面第 i 个采样点（0..Segments），抛物拱：中点最高</summary>
        private static Vector2 SegPoint(Projectile proj, int i) {
            float t = i / (float)Segments * 2f - 1f;
            Vector2 axis = proj.ai[0].ToRotationVector2();
            return proj.Center + axis * (t * HalfLength) - Vector2.UnitY * ArchHeight * (1f - t * t);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            for (int i = 0; i < Segments; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    SegPoint(Projectile, i), SegPoint(Projectile, i + 1), 20f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>本端玩家是否踩在任意虹桥带内（移速增益判定，纯本地读取）</summary>
        internal static bool LocalPlayerOnAnyBridge(Player player) {
            int type = ModContent.ProjectileType<GsRainbowBridgeProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != type) {
                    continue;
                }
                for (int s = 0; s <= Segments; s++) {
                    if (player.Center.DistanceSQ(SegPoint(proj, s)) < 40f * 40f) {
                        return true;
                    }
                }
            }
            return false;
        }

        private float LifeFade {
            get {
                int lived = LifeTicks - Projectile.timeLeft;
                return MathHelper.Clamp(lived / 10f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            }
        }

        public override void AI() {
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //桥面微光尘：1 粒/3t 随机段点上浮（identity 换相由 rand 承担，纯表现）
            if (Projectile.timeLeft % 3 == 0) {
                int s = Main.rand.Next(Segments + 1);
                Vector2 p = SegPoint(Projectile, s);
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.65f);
                PRTLoader.NewParticle<PRT_Sparkle>(p, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    c, 0.22f)?.Configure(c, 14, 0.1f, 0.9f);
            }
            Lighting.AddLight(Projectile.Center, 0.25f, 0.2f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = LifeFade;
            //桥面：12 段拉伸光辉，色相沿桥长滚动（A=0 加色安全）
            for (int i = 0; i < Segments; i++) {
                Vector2 a = SegPoint(Projectile, i);
                Vector2 b = SegPoint(Projectile, i + 1);
                Vector2 mid = (a + b) * 0.5f;
                float len = a.Distance(b);
                float rot = (b - a).ToRotation();
                Color c = Main.hslToRgb((i / (float)Segments + Main.GlobalTimeWrappedHourly * 0.08f) % 1f, 1f, 0.62f);
                c *= 0.55f * fade;
                c.A = 0;
                Main.EntitySpriteDraw(glow, mid - Main.screenPosition, null, c, rot,
                    glow.Size() / 2f, new Vector2(len / glow.Width * 1.15f, 0.34f),
                    Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
                //亮芯一层，收窄提亮
                Color core = Color.White * (0.28f * fade);
                core.A = 0;
                Main.EntitySpriteDraw(glow, mid - Main.screenPosition, null, core, rot,
                    glow.Size() / 2f, new Vector2(len / glow.Width, 0.16f),
                    Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

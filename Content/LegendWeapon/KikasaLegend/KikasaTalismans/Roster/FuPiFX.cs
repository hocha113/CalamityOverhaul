using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霹的演出集中处：引雷窗开窗提示，纯表现各端本地</summary>
    internal static class FuPiFX
    {
        /// <summary>开窗提示：一声远雷压低垫底，泼墨处迸几粒紫电星，窗起有声</summary>
        internal static void WindowOpenCue(Vector2 pos, Color accent) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(SoundID.Thunder, pos, 0.42f, -0.6f, 2);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(pos + Main.rand.NextVector2Circular(26f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.6f, 1.6f)),
                    Color.Lerp(accent, Color.White, 0.35f), Main.rand.NextFloat(0.26f, 0.44f))
                    ?.Configure(accent * 0.6f, Main.rand.Next(12, 20), 0.1f, 0.8f);
            }
        }
    }

    /// <summary>
    /// 霹·天雷：自屏顶（天花板钳制）直劈落点的一记竖雷。
    /// 仅所有者端生成（伤害自然同步），各端首帧自解雷径并本地起雷光——
    /// 闪电表现复用 <see cref="PRT_SkyBolt"/> 的 ThunderTrail 管线，
    /// 判定为雷径竖直线碰撞，只在劈落窗内有效；劈点环形墨波+骤亮
    /// </summary>
    internal class KikasaFuPiThunderStrike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>雷径判定宽（px）</summary>
        private const float BoltWidthPx = 30f;

        /// <summary>劈落判定窗（帧），之后只剩余光</summary>
        private const int StrikeWindowFrames = 10;

        /// <summary>雷径最大长度（px），无天花板时的屏顶高度</summary>
        private const float MaxBoltLenPx = 940f;

        private float life;
        private float boltLen;
        private bool struck;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 26;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //一雷一敌只咬一口：免疫窗不过期
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            life++;
            if (!struck) {
                struck = true;
                //雷径首帧一次性落定：向上探天花板，各端同地形同解
                boltLen = SolveBoltLength();
                StrikeFX();
            }
            //骤亮余光：前段猛，随寿命衰减
            float glow = 1f - MathHelper.Clamp(life / 20f, 0f, 1f);
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.4f * glow, 0.85f * glow);
        }

        /// <summary>自劈点向上逐格探实心，雷从天花板下沿劈起；旷野满长直上屏顶</summary>
        private float SolveBoltLength() {
            int x = (int)(Projectile.Center.X / 16f);
            int startY = (int)(Projectile.Center.Y / 16f) - 2;
            int endY = Math.Max(startY - (int)(MaxBoltLenPx / 16f), 1);
            for (int y = startY; y >= endY; y--) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return MathF.Max(Projectile.Center.Y - (y * 16f + 20f), 120f);
                }
            }
            return MaxBoltLenPx;
        }

        /// <summary>劈落拍：ThunderTrail 天雷+劈点脉冲环+墨珠迸散+雷声，各端本地</summary>
        private void StrikeFX() {
            if (Main.dedServ) {
                return;
            }
            Color accent = new(198, 168, 252);
            Vector2 top = Projectile.Center - new Vector2(0f, boltLen);
            PRTLoader.NewParticle<PRT_SkyBolt>(Projectile.Center, Vector2.Zero,
                accent, 1f)?.Configure(top, Projectile.Center, 26);

            //劈点脉冲环：骤亮的第一层
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                Color.Lerp(accent, Color.White, 0.4f) * 0.6f, 0.1f)?.Configure(0.1f, 0.9f, 12);

            //雷把雨劈开：墨珠混紫电屑四散
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4.5f, 3f) - Vector2.UnitY * 1.5f;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 6f), vel,
                    Main.rand.NextBool(3) ? accent : KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(16, 26));
            }
            KikasaInk.Play(SoundID.Thunder, Projectile.Center, 0.8f, 0.12f, 3);
            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.5f, -0.2f, 3);
        }

        /// <summary>雷径竖直线判定：劈点到天花板整线，窗后失能</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (life > StrikeWindowFrames) {
                return false;
            }
            float _ = 0f;
            Vector2 top = Projectile.Center - new Vector2(0f, boltLen);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, top, BoltWidthPx, ref _);
        }

        /// <summary>劈点余波：环形墨波扩散+首拍白紫骤亮（雷体在 PRT 侧）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float t = MathHelper.Clamp(life / 26f, 0f, 1f);
            Color accent = new(198, 168, 252);

            //环形墨波：快张缓收，贴地椭圆透视
            float radius = MathHelper.Lerp(14f, 98f, 1f - MathF.Pow(1f - t, 2.4f));
            float alpha = MathF.Pow(1f - t, 1.4f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 7f,
                Color.Lerp(accent, Color.White, 0.5f), accent, KikasaInk.InkDeep,
                alpha * 0.85f, squish: 0.42f, innerGlow: 0.2f,
                timeSeed: Projectile.identity * 0.37f);

            //骤亮：首拍一团白紫过曝，几帧内退潮
            float flash = 1f - MathHelper.Clamp(life / 8f, 0f, 1f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (flash > 0.01f && glow != null) {
                Color c = Color.Lerp(accent, Color.White, 0.6f) with { A = 0 };
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                    c * (0.85f * flash), 0f, glow.Size() * 0.5f,
                    new Vector2(2.6f, 1.8f) * (0.6f + 0.4f * flash), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

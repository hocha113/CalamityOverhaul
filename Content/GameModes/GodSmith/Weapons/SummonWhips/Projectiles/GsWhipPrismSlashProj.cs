using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles
{
    /// <summary>
    /// 万花筒处决「万华镜」单道折线斩：以目标为心的一道彩光折线（0.6x），
    /// 五道成环。摆角由 identity 种子 + 色相索引定，跨端一致；
    /// 各道按色相错 2f 开扫，五色琶音展开。<br/>
    /// 四相：错帧亮头出手、折线扫描（判定窗）、命中色屑迸溅、整线渐隐余痕。<br/>
    /// ai[0] = 色相索引 0~4；ai[1] = 目标 npc.whoAmI
    /// </summary>
    internal class GsWhipPrismSlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int SweepFrames = 10;
        private const int LifeFrames = 34;
        private const float ReachPx = 175f;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>本道错帧起点：色相索引 x2，五道琶音展开</summary>
        private int StartDelay => (int)Projectile.ai[0] * 2;

        /// <summary>本道局部时间</summary>
        private int LocalTime => Elapsed - StartDelay;

        /// <summary>目标失活后的路径锚点</summary>
        private Vector2 anchor;
        private bool anchorInit;

        private Color PrismColor
            => GsKaleidoscope.PrismColors[Math.Clamp((int)Projectile.ai[0], 0, GsKaleidoscope.PrismColors.Length - 1)];

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>折线三点：外围入点、偏轴拐点、对侧出点（局部偏移随目标走）</summary>
        private void GetPath(out Vector2 p0, out Vector2 p1, out Vector2 p2) {
            int idx = (int)Projectile.ai[1];
            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active) {
                anchor = Main.npc[idx].Center;
                anchorInit = true;
            }
            else if (!anchorInit) {
                anchor = Projectile.Center;
                anchorInit = true;
            }
            float theta = Projectile.identity * 0.53f + Projectile.ai[0] * (MathHelper.TwoPi / 5f);
            Vector2 dir = theta.ToRotationVector2();
            Vector2 perp = (theta + MathHelper.PiOver2).ToRotationVector2();
            p0 = anchor + dir * ReachPx;
            p1 = anchor + perp * 42f - dir * 15f;
            p2 = anchor - dir * ReachPx - perp * 18f;
        }

        public override bool? CanDamage() => LocalTime >= 3 && LocalTime < 9 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            GetPath(out Vector2 p0, out Vector2 p1, out Vector2 p2);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), p0, p1)
                || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), p1, p2);
        }

        public override void AI() {
            GetPath(out _, out Vector2 p1, out _);
            Projectile.Center = p1;   //本体锚在拐点，判定与绘制都走路径
            int lt = LocalTime;
            if (lt == 0 && !VaultUtils.isServer) {
                //五色琶音：音高随色相爬升
                SoundEngine.PlaySound(SoundID.Item9 with {
                    Volume = 0.55f, Pitch = -0.2f + 0.15f * (int)Projectile.ai[0]
                }, anchor);
            }
            //渐隐期沿线飘色屑余痕
            if (lt > SweepFrames && !VaultUtils.isServer && Main.GameUpdateCount % 3 == 0) {
                GetPath(out Vector2 a, out Vector2 b, out Vector2 c);
                Vector2 sample = Main.rand.NextBool()
                    ? Vector2.Lerp(a, b, Main.rand.NextFloat())
                    : Vector2.Lerp(b, c, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Sparkle>(sample, Main.rand.NextVector2Circular(0.8f, 0.8f),
                    PrismColor, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(PrismColor, Main.rand.Next(12, 20), 0.05f, 0.8f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                    Main.rand.NextVector2Circular(4f, 4f),
                    PrismColor, Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(PrismColor, Main.rand.Next(14, 24), 0.08f, 1f);
            }
        }

        /// <summary>沿折线画一段拉伸光带</summary>
        private static void DrawSeg(Texture2D tex, Vector2 a, Vector2 b, Color c, float width) {
            Vector2 delta = b - a;
            float len = delta.Length();
            if (len < 2f) {
                return;
            }
            Main.EntitySpriteDraw(tex, a - Main.screenPosition, null, c, delta.ToRotation(),
                new Vector2(0f, tex.Height * 0.5f),
                new Vector2(len / tex.Width, width / tex.Height), SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            int lt = LocalTime;
            if (lt < 0) {
                return false;
            }
            Texture2D line = CWRUtils.GetT2DAsset(CWRConstant.Masking + "MaskLaserLine")?.Value;
            Texture2D flare = CWRUtils.GetT2DAsset(CWRConstant.Masking + "StarFlare02")?.Value;
            if (line == null || flare == null) {
                return false;
            }
            GetPath(out Vector2 p0, out Vector2 p1, out Vector2 p2);
            float len1 = Vector2.Distance(p0, p1);
            float len2 = Vector2.Distance(p1, p2);
            float total = MathF.Max(1f, len1 + len2);
            Color main = PrismColor with { A = 0 };
            Color core = Color.Lerp(PrismColor, Color.White, 0.55f) with { A = 0 };

            if (lt <= SweepFrames) {
                //扫描相：head 沿折线快进（先快后缓），画已显露段 + 亮头
                float s = 1f - (1f - lt / (float)SweepFrames) * (1f - lt / (float)SweepFrames);
                float headDist = s * total;
                Vector2 head;
                if (headDist <= len1) {
                    head = Vector2.Lerp(p0, p1, headDist / MathF.Max(1f, len1));
                    DrawSeg(line, p0, head, main * 0.85f, 26f);
                    DrawSeg(line, p0, head, core * 0.9f, 12f);
                }
                else {
                    head = Vector2.Lerp(p1, p2, (headDist - len1) / MathF.Max(1f, len2));
                    DrawSeg(line, p0, p1, main * 0.85f, 26f);
                    DrawSeg(line, p0, p1, core * 0.9f, 12f);
                    DrawSeg(line, p1, head, main * 0.85f, 26f);
                    DrawSeg(line, p1, head, core * 0.9f, 12f);
                }
                Main.EntitySpriteDraw(flare, head - Main.screenPosition, null, core * 0.95f,
                    Projectile.identity * 0.6f + lt * 0.3f, flare.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);
                return false;
            }
            //余痕相：整线渐隐收窄
            float t = (lt - SweepFrames) / (float)(LifeFrames - StartDelay - SweepFrames);
            float fade = 1f - t;
            float width = MathHelper.Lerp(26f, 8f, t);
            DrawSeg(line, p0, p1, main * (0.7f * fade), width);
            DrawSeg(line, p1, p2, main * (0.7f * fade), width);
            DrawSeg(line, p0, p1, core * (0.6f * fade), width * 0.45f);
            DrawSeg(line, p1, p2, core * (0.6f * fade), width * 0.45f);
            //首道兼画中心五色旋光，五道只画一遍
            if ((int)Projectile.ai[0] == 0) {
                float rot = Main.GlobalTimeWrappedHourly * 2.2f + Projectile.identity * 0.5f;
                for (int i = 0; i < 5; i++) {
                    Color c = GsKaleidoscope.PrismColors[i] with { A = 0 };
                    Vector2 offset = (rot + i * MathHelper.TwoPi / 5f).ToRotationVector2() * 26f * fade;
                    Main.EntitySpriteDraw(flare, anchor + offset - Main.screenPosition, null,
                        c * (0.65f * fade), rot + i, flare.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}

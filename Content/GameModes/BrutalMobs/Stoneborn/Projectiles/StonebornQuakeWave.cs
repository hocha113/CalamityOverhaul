using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 花岗岩魔像·共振地表波：ai[0]=行进方向(±1) ai[1]=存续帧（档位只调射程不调形状）。
    /// 沿地表爬行的实体波，逐帧贴地吸附：上坡最多爬 <see cref="MaxStepUpTiles"/> 格、
    /// 落差超过 <see cref="MaxDropTiles"/> 格即碎（悬崖与高墙是天然反制）。
    /// 波高即判定高（<see cref="WaveCrestHeightPx"/>，约 1.5 格）——跳过即躲，缺口在纵向；
    /// 淡入完成才有杀伤（伤害窗=可见窗，波峰可见=判定窗）。
    /// 遮挡体：Extra_98 真透暗石壳 ×1.18 全 alpha + 电蓝 A=0 亮芯（M5），拖尾同材质 ≥0.5× 横轴
    /// </summary>
    internal class StonebornQuakeWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 公平阀门（具名，判定与视觉同读） ====
        /// <summary>波峰判定高（像素，约 1.5 格）：既是 hitbox 高也是绘制高，跳跃可越</summary>
        internal const int WaveCrestHeightPx = 24;
        /// <summary>波体判定宽（像素）</summary>
        internal const int WaveBodyWidthPx = 26;
        /// <summary>行进速度（中慢，跳跃反应窗充足；档位不加速）</summary>
        private const float WaveSpeed = 3.6f;
        /// <summary>淡入帧：完成前无判定（伤害窗=可见窗）</summary>
        private const int FadeInFrames = 6;
        /// <summary>淡出帧：进入即无害</summary>
        private const int FadeOutFrames = 8;
        /// <summary>上坡最大爬升（瓦格），超出视为撞墙即碎</summary>
        private const int MaxStepUpTiles = 2;
        /// <summary>下坡最大跟随落差（瓦格），超出视为悬崖即碎</summary>
        private const int MaxDropTiles = 4;

        /// <summary>暗石壳色（真 alpha 层，A 满值才能压出遮挡感）</summary>
        private static readonly Color ShellDark = new Color(20, 26, 46);

        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private bool FadingOut => Projectile.timeLeft <= FadeOutFrames;
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = WaveBodyWidthPx;
            Projectile.height = WaveCrestHeightPx;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;//贴地吸附自行管理，不走原版碰撞
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 110;
            Projectile.netImportant = true;
        }

        /// <summary>淡入完成才有杀伤，淡出即无害（波峰可见=判定窗）</summary>
        public override bool? CanDamage() => Age > FadeInFrames && !FadingOut ? null : false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Age == 0f) {
                //存续帧随生成包带入（ai[1]），不产生生成后改 timeLeft 的漏同步窗口
                int life = (int)Projectile.ai[1];
                if (life > 0) {
                    Projectile.timeLeft = life;
                }
            }
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));

            //各端确定性推进：速度与地形吸附都由同步的出生态推得，无逐帧同步需求
            if (!FadingOut && !AdvanceAlongGround()) {
                //撞墙/坠崖：碎裂收场（进入淡出而非立即消失，保证可读的消散）
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, FadeOutFrames);
            }

            if (!Main.dedServ) {
                //波峰电弧尘 + 基座碎石（≤3 粒/帧）
                if (Main.rand.NextBool(2)) {
                    Dust arc = Dust.NewDustPerfect(
                        Projectile.Top + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(6f)),
                        DustID.Electric, new Vector2(Dir * 0.6f, -Main.rand.NextFloat(0.4f, 1.4f)), 80, default, 0.9f);
                    arc.noGravity = true;
                }
                if (Main.rand.NextBool(3)) {
                    Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f),
                        DustID.Stone, new Vector2(Dir * 0.8f, -Main.rand.NextFloat(0.5f, 1.8f)), 60, default, 1f);
                }
                Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.22f);
            }
        }

        /// <summary>
        /// 沿地表推进一帧：先横移，再向下寻可站立面吸附。
        /// 返回 false 表示地形中断（爬升超限或落差超限）
        /// </summary>
        private bool AdvanceAlongGround() {
            Vector2 next = Projectile.Center + new Vector2(Dir * WaveSpeed, 0f);
            Point column = new Vector2(next.X, Projectile.Bottom.Y - 4f).ToTileCoordinates();
            if (!WorldGen.InWorld(column.X, column.Y, 10)) {
                return false;
            }

            //从允许爬升的最高格向下扫，找第一张固体面（守卫式循环，M8）
            int scanTop = column.Y - MaxStepUpTiles;
            int scanBottom = column.Y + MaxDropTiles;
            for (int tileY = scanTop; tileY <= scanBottom; tileY++) {
                if (!WorldGen.InWorld(column.X, tileY, 10)) {
                    return false;
                }
                if (!WorldGen.SolidTile(column.X, tileY)) {
                    continue;
                }
                //落点上方仍是固体＝这是一面高墙的墙腰而非台阶，波在墙脚碎掉
                if (WorldGen.SolidTile(column.X, tileY - 1)) {
                    return false;
                }
                Projectile.Bottom = new Vector2(next.X, tileY * 16f);
                return true;
            }
            return false;//扫描窗内无地面＝悬崖
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center,
                    Main.rand.NextBool() ? DustID.Stone : DustID.Electric,
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 2f)), 90, default, 1f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shell = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float opacity = 1f - Projectile.alpha / 255f;
            if (FadingOut) {
                opacity *= Projectile.timeLeft / (float)FadeOutFrames;
            }
            float flicker = 0.75f + 0.25f * MathF.Sin((Projectile.timeLeft * 1.7f + Projectile.identity) * 2.3f);
            Color coreBlue = GraniteMarbleVFX.GraniteCore with { A = 0 };

            //旧位残迹：同材质拖尾，横轴 0.55×（M5 ≥0.5 契约）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawCrest(shell, old, opacity * 0.35f * t, 0.55f, coreBlue, flicker);
            }
            //本体波峰
            Vector2 pos = Projectile.Center - Main.screenPosition;
            DrawCrest(shell, pos, opacity, 1f, coreBlue, flicker);
            //峰顶辉光（加色，读作放电的能量脊线）
            Main.EntitySpriteDraw(glow, pos - new Vector2(0f, Projectile.height * 0.28f), null,
                coreBlue * (0.5f * opacity * flicker), 0f, glow.Size() / 2f,
                new Vector2(Projectile.width * 1.5f / glow.Width, Projectile.height * 0.9f / glow.Height),
                SpriteEffects.None, 0);
            return false;
        }

        /// <summary>暗石壳 ×1.18 全 alpha 打底 + 电蓝 A=0 亮芯（M5 双层配方）</summary>
        private void DrawCrest(Texture2D shell, Vector2 pos, float alpha, float scaleMul, Color coreBlue, float flicker) {
            Vector2 scale = new Vector2(Projectile.width / (float)shell.Width, Projectile.height / (float)shell.Height) * scaleMul;
            Main.EntitySpriteDraw(shell, pos, null, ShellDark * (0.9f * alpha), 0f,
                shell.Size() / 2f, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shell, pos, null, coreBlue * (0.75f * alpha * flicker), 0f,
                shell.Size() / 2f, scale * 0.72f, SpriteEffects.None, 0);
        }
    }
}

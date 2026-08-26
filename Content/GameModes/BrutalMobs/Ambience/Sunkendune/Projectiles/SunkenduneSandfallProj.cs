using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sunkendune.Projectiles
{
    /// <summary>
    /// 「沙瀑」场地实体（地形驱动）。ai[0]=倾泻长度（像素，生成端按净空实测）。
    /// 生成位置锁定：顶缝先漏细沙 + 簌簌声 70 帧（公平契约 ≥45）→ 沙柱倾泻 150 帧
    /// （被淋者轻推 + 微量伤害，判定窗=可见柱体）→ 自顶排空 + 沙尘余韵 35 帧。
    /// 柱体三位置各有物理答案：源头=石缝暗口带漏流球根感、落点=溅尘丘、排空=自顶撕开断流；
    /// 沙是漫反射材质，柱段逐点乘本地光照。Boss 在场时伤害与推力暂停
    /// </summary>
    internal class SunkenduneSandfallProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>渗漏预告帧数（视觉+听觉双通道，公平契约 ≥45）</summary>
        private const int LeakFrames = 70;
        /// <summary>倾泻帧数</summary>
        private const int PourFrames = 150;
        /// <summary>排空余韵帧数</summary>
        private const int FadeFrames = 35;
        /// <summary>柱半宽（像素，机制形状不随档位改变）</summary>
        private const float HalfWidthPx = 26f;
        /// <summary>倾泻前沿贯通用时（帧，加速下坠签名）</summary>
        private const int FrontFrames = 12;
        /// <summary>柱段视觉行进速度（像素/帧）</summary>
        private const float FallSpeed = 13f;
        /// <summary>柱段间距（像素）</summary>
        private const float SegStep = 88f;
        /// <summary>Fog 贴图有效内容直径（256 画布内约 0.7）</summary>
        private const float FogContentPx = 180f;

        /// <summary>
        /// 倾泻伤害（normal/expert 双值，语义对齐 NPC.GetAttackDamage_ForProjectiles）。
        /// 基准=本群系原版敌怪接触伤害 × 约 0.5（镜像 DamageFrac 公约）：
        /// 困难前锚墓穴爬虫（约 20/40），困难后锚沙丘尸虫（约 60/120）
        /// </summary>
        internal static int PourDamage => Main.hardMode
            ? (Main.expertMode ? 50 : 28)
            : (Main.expertMode ? 18 : 10);

        private float LenPx => Projectile.ai[0];
        private int TotalLife => LeakFrames + PourFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>倾泻前沿 0~1（t² 加速下坠签名）</summary>
        private float FrontProgress {
            get {
                int t = Elapsed - LeakFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= FrontFrames) {
                    return 1f;
                }
                float x = t / (float)FrontFrames;
                return x * x;
            }
        }

        /// <summary>排空进度 0~1：可见柱体自顶向下撕开</summary>
        private float DrainProgress {
            get {
                int t = Elapsed - LeakFrames - PourFrames;
                if (t <= 0) {
                    return 0f;
                }
                return MathHelper.Clamp(t / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 820;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//倾泻窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LeakFrames + PourFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            bool pouring = elapsed >= LeakFrames && elapsed < LeakFrames + PourFrames;
            //Boss 在场：伤害与推力暂停，倾泻视觉照常收尾
            bool harmAllowed = !CWRWorld.HasBoss;

            //判定窗=可见倾泻窗（前沿贯通两成后才咬人）
            Projectile.hostile = pouring && harmAllowed && FrontProgress > 0.12f;

            //声音节拍
            if (!Main.dedServ) {
                if (elapsed == 0) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed < LeakFrames && elapsed % 20 == 0) {
                    float progress = elapsed / (float)LeakFrames;
                    SoundEngine.PlaySound(SoundID.Dig with {
                        Volume = 0.16f + 0.22f * progress, Pitch = 0.5f, MaxInstances = 5
                    }, Projectile.Center);
                }
                else if (elapsed == LeakFrames) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 5 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.55f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (pouring && elapsed % 18 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.32f, Pitch = 0.55f, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed == LeakFrames + PourFrames) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.35f, Pitch = -0.25f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //被淋者轻推：只点名本机玩家（移动是本机权威）
            if (pouring && harmAllowed && !Main.dedServ) {
                Player localPlayer = Main.LocalPlayer;
                if (localPlayer.active && !localPlayer.dead && ColumnRect().Intersects(localPlayer.Hitbox)) {
                    localPlayer.GetModPlayer<SunkendunePlayer>().fallSoak = 2;
                    if (Main.rand.NextBool(3)) {
                        Dust soak = Dust.NewDustPerfect(localPlayer.Top + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1f, 2.5f)),
                            110, default, Main.rand.NextFloat(0.9f, 1.3f));
                        soak.noGravity = true;
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }

            float front = FrontProgress;
            if (elapsed < LeakFrames) {
                //渗漏期：顶缝细沙下滴（约 1 粒/6 帧）
                if (elapsed % 3 == 0 && Main.rand.NextBool(2)) {
                    Dust grain = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-2f, 2f), 2f),
                        DustID.Sand, new Vector2(0f, Main.rand.NextFloat(2f, 4f)), 120, default,
                        Main.rand.NextFloat(0.6f, 0.9f));
                    grain.noGravity = false;
                }
            }
            else if (pouring) {
                //柱缘剥离沙屑（≤1 粒/3 帧）
                if (Main.rand.NextBool(3)) {
                    float along = Main.rand.NextFloat(0.1f, 0.95f) * LenPx * front;
                    int side = Main.rand.NextBool() ? 1 : -1;
                    Dust shed = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(side * HalfWidthPx * 0.8f, along),
                        DustID.Sand, new Vector2(side * Main.rand.NextFloat(0.3f, 0.9f), Main.rand.NextFloat(2f, 4f)),
                        120, default, Main.rand.NextFloat(0.7f, 1f));
                    shed.noGravity = false;
                }
                //落点溅尘（前沿贯通后，约 1 粒/3 帧）
                if (front >= 0.95f) {
                    Vector2 basePos = Projectile.Center + new Vector2(0f, LenPx);
                    if (Main.rand.NextBool(3)) {
                        Dust splash = Dust.NewDustPerfect(basePos + new Vector2(Main.rand.NextFloat(-HalfWidthPx, HalfWidthPx), -4f),
                            DustID.Sand, new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.6f, 2.2f)),
                            100, default, Main.rand.NextFloat(0.9f, 1.4f));
                        splash.noGravity = Main.rand.NextBool();
                    }
                }
                //柱身读作发力的微光（沙尘反光，弱）
                Lighting.AddLight(Projectile.Center + new Vector2(0f, LenPx * 0.5f),
                    new Vector3(0.18f, 0.14f, 0.07f));
            }
            else if (Main.rand.NextBool(3)) {
                //余韵：落点沙尘慢慢散
                Dust haze = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidthPx * 1.6f, HalfWidthPx * 1.6f), LenPx - 6f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.1f, 0.5f)),
                    150, default, Main.rand.NextFloat(0.6f, 0.9f));
                haze.noGravity = true;
            }
        }

        /// <summary>可见柱体判定矩形（宽取可见核心的八成，判定不宽于可见体）</summary>
        private Rectangle ColumnRect() {
            float top = LenPx * DrainProgress;
            float bottom = LenPx * FrontProgress;
            if (bottom <= top) {
                return Rectangle.Empty;
            }
            return new Rectangle((int)(Projectile.Center.X - HalfWidthPx * 0.8f), (int)(Projectile.Center.Y + top),
                (int)(HalfWidthPx * 1.6f), (int)(bottom - top));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            return ColumnRect().Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float front = FrontProgress;
            float drain = DrainProgress;

            //石缝暗口（真 alpha 暗形，源头有出处）
            Texture2D sheet = CWRAsset.Extra_98.Value;
            float crackEnv = MathHelper.Clamp(elapsed / 20f, 0f, 1f) * (1f - drain * 0.7f);
            Main.EntitySpriteDraw(sheet, Projectile.Center + new Vector2(0f, -3f) - Main.screenPosition,
                null, new Color(26, 20, 12) * (0.55f * crackEnv), 0f, sheet.Size() / 2f,
                new Vector2(0.55f, 0.12f), SpriteEffects.None, 0);

            if (elapsed < LeakFrames) {
                //渗漏细流 + 警示光斑（家族警示语汇）
                float progress = elapsed / (float)LeakFrames;
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity) * 1.2f;
                Color leakLit = Color.Lerp(lightColor, new Color(210, 182, 124), 0.4f) * (0.15f + 0.3f * progress);
                Main.EntitySpriteDraw(sheet, Projectile.Center + new Vector2(sway, 30f) - Main.screenPosition,
                    null, leakLit, 0f, sheet.Size() / 2f, new Vector2(0.2f, 1.25f), SpriteEffects.None, 0);

                Texture2D glow = CWRAsset.SoftGlow.Value;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);
                Color warn = new Color(255, 205, 120, 0) * (0.35f * progress * pulse);
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, warn, 0f,
                    glow.Size() / 2f, new Vector2(1f, 0.35f), SpriteEffects.None, 0);
                return false;
            }

            float visTop = LenPx * drain;
            float visBottom = LenPx * front;
            if (visBottom - visTop < 8f) {
                return false;
            }
            float fadeDim = 1f - drain * 0.6f;

            //柱体：雾团段沿流向匀速行进（真 alpha，逐段乘本地光照；段窗内首尾自然收口）
            Texture2D fog = CWRAsset.Fog.Value;
            Vector2 fogOrig = fog.Size() / 2f;
            float widthScale = HalfWidthPx * 2f / FogContentPx;
            float offset = (elapsed - LeakFrames) * FallSpeed % SegStep;
            for (float y = visTop - SegStep + offset; y < visBottom + SegStep * 0.5f; y += SegStep) {
                //段中心限制在可见窗内做包络：顶端自缝生出、底端压进溅丘
                float clampedY = MathHelper.Clamp(y, visTop, visBottom);
                float bandT = (clampedY - visTop) / MathF.Max(visBottom - visTop, 1f);
                float endCap = MathF.Pow(MathF.Sin(MathHelper.Pi * MathHelper.Clamp(bandT, 0.02f, 0.98f)), 0.55f);
                float jig = MathF.Sin(Projectile.identity * 1.7f + y * 0.03f + Main.GlobalTimeWrappedHourly * 3f) * 3f;
                Vector2 segPos = Projectile.Center + new Vector2(jig, clampedY);
                Color segLight = Lighting.GetColor((int)(segPos.X / 16f), (int)(segPos.Y / 16f));
                float lum = MathHelper.Clamp(0.12f + 0.88f * (segLight.R + segLight.G + segLight.B) / 765f, 0f, 1f);
                Color segColor = new Color((byte)(224 * lum), (byte)(196 * lum), (byte)(130 * lum))
                    * (0.42f * endCap * fadeDim);
                Main.EntitySpriteDraw(fog, segPos - Main.screenPosition, null, segColor, 0f, fogOrig,
                    new Vector2(widthScale, SegStep * 1.25f / FogContentPx), SpriteEffects.None, 0);
            }

            //柱内沙团（实体感锚点，快速下行 + 自旋）
            Texture2D clumpTex = TextureAssets.Projectile[Type].Value;
            Vector2 clumpOrig = clumpTex.Size() / 2f;
            float span = MathF.Max(visBottom - visTop, 1f);
            for (int i = 0; i < 2; i++) {
                float fall = ((elapsed - LeakFrames) * 18f + i * span * 0.5f) % span;
                Vector2 pos = Projectile.Center + new Vector2(
                    MathF.Sin(Projectile.identity + i * 2.4f + fall * 0.02f) * 5f, visTop + fall);
                Color clumpColor = Color.Lerp(lightColor, new Color(216, 186, 118), 0.4f) * (0.75f * fadeDim);
                Main.EntitySpriteDraw(clumpTex, pos - Main.screenPosition, null, clumpColor,
                    fall * 0.08f + i, clumpOrig, 0.7f, SpriteEffects.None, 0);
            }

            //落点溅丘（前沿贯通后浮现，排空期渐散）
            if (front >= 0.9f) {
                Vector2 basePos = Projectile.Center + new Vector2(0f, LenPx - 4f);
                Color baseLightC = Lighting.GetColor((int)(basePos.X / 16f), (int)(basePos.Y / 16f) - 1);
                float baseLum = MathHelper.Clamp(0.12f + 0.88f * (baseLightC.R + baseLightC.G + baseLightC.B) / 765f, 0f, 1f);
                float breathe = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);
                Color plume = new Color((byte)(212 * baseLum), (byte)(182 * baseLum), (byte)(120 * baseLum))
                    * (0.34f * fadeDim * (drain > 0f ? 1f - drain * 0.5f : 1f));
                Main.EntitySpriteDraw(fog, basePos - Main.screenPosition, null, plume, 0f, fogOrig,
                    new Vector2(HalfWidthPx * 3.4f / FogContentPx * breathe, HalfWidthPx * 1.7f / FogContentPx),
                    SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余韵定格：落点最后一圈浮尘
            Vector2 basePos = Projectile.Center + new Vector2(0f, LenPx - 8f);
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    basePos + new Vector2(Main.rand.NextFloat(-HalfWidthPx * 1.5f, HalfWidthPx * 1.5f), 0f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.7f)),
                    140, default, Main.rand.NextFloat(0.7f, 1f));
                dust.noGravity = true;
            }
        }
    }
}

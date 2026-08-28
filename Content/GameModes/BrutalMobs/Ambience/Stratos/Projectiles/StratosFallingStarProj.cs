using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos.Projectiles
{
    /// <summary>
    /// 「坠星」环境流星。ai[0]=天穹起点横向偏斜 ai[1]=空爆标记（1=无地面云层空爆）。
    /// 生成位置即锁定落点（区域随机，调度端保证永不点名玩家坐标）：
    /// 天际亮点增大+呼啸渐强+落点光圈收拢 52 帧 → 坠落 34 帧 → 小爆炸 8 帧（仅此窗口有判定）
    /// → 烟尘碎星余韵 36 帧。预告期出现 Boss 则取消爆炸（伤害机制暂停）。
    /// 坠落体=真 alpha 暗石壳+A=0 外缘热边（层序对标烬羽 PRT），拖尾是壳体自身层叠的残影重绘。
    /// 各端从各自 timeLeft 展开同一时间轴，伤害值恒定不清零，判定窗只由 hostile 门控
    /// </summary>
    internal class StratosFallingStarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 52;
        /// <summary>坠落帧数（执行段公平契约 ≥30：预告后仍有整段反应窗）</summary>
        private const int FallFrames = 34;
        /// <summary>爆炸判定窗</summary>
        private const int BurstFrames = 8;
        /// <summary>余韵帧数（纯视觉，无伤害残留——陨石坑灼热余烬地归 Starfall 槽）</summary>
        private const int AfterglowFrames = 36;
        /// <summary>天穹起点高度（随坠落段拉长抬高，进场仍自屏幕上方）</summary>
        private const float SkyDropHeight = 760f;
        /// <summary>爆炸半径（小于调度端的最小落点偏移 180，原地站立的玩家永不被点名命中）</summary>
        private const float BlastRadius = 104f;
        /// <summary>预告圈起始半径，随预告向爆炸半径收拢</summary>
        private const float RingStartRadius = 150f;

        /// <summary>呼啸：暴风雪嘶声定位循环，音量音调随逼近渐强渐尖</summary>
        private static readonly SoundStyle WhistleStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 4 };

        /// <summary>熔石壳贴图：真 alpha 梭形（有 alpha 才能遮挡，读作石体而非光点）</summary>
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Extra_98 = null;

        /// <summary>石壳暗体（带 A，承担轮廓与遮挡）</summary>
        private static readonly Color ShellDark = new(56, 36, 30);
        /// <summary>熔缘炽色（A=0 加色敷边）</summary>
        private static readonly Color RimHot = new(255, 118, 46);
        /// <summary>拖尾末端冷却色</summary>
        private static readonly Color RimCool = new(150, 52, 26);

        /// <summary>拖尾残影数与采样帧距：重绘壳体自身层叠，横轴恒 ≥0.5 体宽</summary>
        private const int TrailGhosts = 6;
        private const float TrailSpacing = 0.75f;

        private float SkySlant => Projectile.ai[0];
        private bool AirBurst => Projectile.ai[1] == 1f;
        private static int TotalLife => TelegraphFrames + FallFrames + BurstFrames + AfterglowFrames;
        private static int ImpactFrame => TelegraphFrames + FallFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;
        private Vector2 SkyOrigin => Projectile.Center + new Vector2(SkySlant, -SkyDropHeight);

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>本机呼啸声槽（客户端私产）</summary>
        private SlotId whistleSlot;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//爆炸窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>流星头位置：预告期钉在天穹起点，坠落期二次加速扑向落点（浮点采样供拖尾回看）</summary>
        private Vector2 HeadPos(float elapsed) {
            if (elapsed <= TelegraphFrames) {
                return SkyOrigin;
            }
            float t = MathHelper.Clamp((elapsed - TelegraphFrames) / FallFrames, 0f, 1f);
            return Vector2.Lerp(SkyOrigin, Projectile.Center, t * t);
        }

        public override void AI() {
            int elapsed = Elapsed;

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    //预告起点：远星轻响（双通道预告的听觉端从第一帧就有）
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.55f, MaxInstances = 5 }, SkyOrigin);
                }
            }

            //爆炸前出现 Boss：伤害机制暂停，本次坠星熄火（各端读同步的 boss 在场态，结论一致）
            if (!Cancelled && elapsed < ImpactFrame && CWRWorld.HasBoss) {
                Cancelled = true;
            }
            if (Cancelled && elapsed >= TelegraphFrames) {
                Projectile.Kill();
                return;
            }

            //判定窗=可见爆炸窗
            Projectile.hostile = !Cancelled && elapsed >= ImpactFrame && elapsed < ImpactFrame + BurstFrames;

            if (Main.dedServ) {
                return;
            }

            //呼啸渐强：预告+坠落期挂定位循环，音量音调在回调里随进度爬升
            if (!Cancelled && elapsed < ImpactFrame && !SoundEngine.TryGetActiveSound(whistleSlot, out _)) {
                whistleSlot = SoundEngine.PlaySound(WhistleStyle, HeadPos(elapsed), UpdateWhistle);
            }

            if (Cancelled) {
                return;
            }

            if (elapsed < TelegraphFrames) {
                //预告期：落点圈内星屑上浮（≤0.5 粒/帧）
                float progress = elapsed / (float)TelegraphFrames;
                if (Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-BlastRadius, BlastRadius) * progress, 4f),
                        DustID.YellowStarDust, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.8f)),
                        140, default, 0.7f + 0.5f * progress);
                    dust.noGravity = true;
                }
                Lighting.AddLight(SkyOrigin, new Vector3(0.42f, 0.36f, 0.22f) * progress);
                return;
            }

            if (elapsed < ImpactFrame) {
                //坠落期：熔壳剥落烬屑+断续烟迹（石质通道，2 粒/帧短促）
                Vector2 head = HeadPos(elapsed);
                Vector2 dir = (Projectile.Center - SkyOrigin).SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < 2; i++) {
                    Dust ember = Dust.NewDustPerfect(head + Main.rand.NextVector2Circular(9f, 9f),
                        DustID.Meteorite, -dir * Main.rand.NextFloat(1f, 3f)
                        + Main.rand.NextVector2Circular(1f, 1f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                    ember.noGravity = true;
                }
                if (Main.rand.NextBool(2)) {
                    Dust smoke = Dust.NewDustPerfect(head - dir * 18f, DustID.Smoke,
                        -dir * Main.rand.NextFloat(0.4f, 1.1f), 150, default, Main.rand.NextFloat(1f, 1.5f));
                    smoke.noGravity = true;
                }
                Lighting.AddLight(head, new Vector3(0.85f, 0.6f, 0.35f));
                return;
            }

            if (elapsed == ImpactFrame) {
                //落地拍：小爆炸+石屑迸溅+熔烬+烟团+近距离屏震（随距离衰减）
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = 0.1f, MaxInstances = 5 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                        Main.rand.NextVector2Circular(3.5f, 3f) - new Vector2(0f, 1f), 120, default,
                        Main.rand.NextFloat(1.2f, 2f));
                    smoke.noGravity = Main.rand.NextBool();
                }
                for (int i = 0; i < 14; i++) {
                    Dust rock = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 6f),
                        DustID.Stone, new Vector2(Main.rand.NextFloat(-5.5f, 5.5f), -Main.rand.NextFloat(1.5f, 7f)),
                        30, default, Main.rand.NextFloat(1f, 1.7f));
                    rock.noGravity = false;
                }
                for (int i = 0; i < 8; i++) {
                    Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Meteorite,
                        Main.rand.NextVector2Circular(6f, 5f) - new Vector2(0f, 2f), 60, default,
                        Main.rand.NextFloat(1f, 1.5f));
                    ember.noGravity = Main.rand.NextBool();
                }
                for (int i = 0; i < 3; i++) {
                    Gore.NewGore(Projectile.GetSource_FromAI(), Projectile.Center + Main.rand.NextVector2Circular(14f, 8f),
                        new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.4f, 1.8f)),
                        Main.rand.Next(61, 64));
                }
                float dist = Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center);
                float shake = MathHelper.Lerp(4.2f, 0f, MathHelper.Clamp(dist / 640f, 0f, 1f));
                if (shake > 0.4f) {
                    Main.LocalPlayer.CWR()?.GetScreenShake(shake);
                }
            }
            else if (elapsed >= ImpactFrame + BurstFrames) {
                //余韵期：烟尘缓升与零星碎星坠落（≤0.6 粒/帧）
                if (elapsed % 3 == 0) {
                    Dust smoke = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-14f, 4f)),
                        DustID.Smoke, new Vector2(Main.windSpeedCurrent, -Main.rand.NextFloat(0.5f, 1.4f)),
                        170, default, Main.rand.NextFloat(1f, 1.6f));
                    smoke.noGravity = true;
                }
                if (Main.rand.NextBool(4)) {
                    Dust twinkle = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(50f, 24f),
                        DustID.YellowStarDust, new Vector2(0f, Main.rand.NextFloat(0.4f, 1.4f)),
                        120, default, Main.rand.NextFloat(0.6f, 1f));
                    twinkle.noGravity = false;
                }
            }

            float burstGlow = 1f - MathHelper.Clamp((elapsed - ImpactFrame) / (float)(BurstFrames + AfterglowFrames), 0f, 1f);
            Lighting.AddLight(Projectile.Center, new Vector3(1.1f, 0.9f, 0.55f) * burstGlow);
        }

        private bool UpdateWhistle(ActiveSound sound) {
            //槽位对象会被新弹幕复用：实例校验防旧回调误读新弹幕
            if (Projectile.ModProjectile != this || !Projectile.active || Cancelled || Main.gameMenu) {
                return false;
            }
            int elapsed = Elapsed;
            if (elapsed >= ImpactFrame) {
                return false;
            }
            float progress = elapsed / (float)ImpactFrame;
            sound.Position = HeadPos(elapsed);
            sound.Volume = 0.12f + 0.55f * progress * progress;
            sound.Pitch = -0.15f + 0.9f * progress;
            return true;
        }

        /// <summary>圆形爆炸判定：仅爆炸窗口，几何与可见闪爆同源</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) <= BlastRadius * BlastRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float cancelDim = Cancelled ? 0.35f : 1f;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D fogTex = CWRAsset.Fog?.Value;
            Texture2D sparkle = CWRAsset.StarGlow01?.Value;
            Texture2D shell = Extra_98?.Value;
            if (star == null || glow == null || fogTex == null || sparkle == null || shell == null) {
                return false;
            }
            Vector2 starOrig = star.Size() * 0.5f;
            Vector2 glowOrig = glow.Size() * 0.5f;
            Vector2 shellOrig = shell.Size() * 0.5f;
            float squish = AirBurst ? 0.8f : 0.42f;

            if (elapsed < TelegraphFrames) {
                float progress = elapsed / (float)TelegraphFrames;
                //落点光圈：向爆炸半径收拢+脉动，圈即未来判定圈（可读可学）
                float pulse = 1f + 0.05f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
                float ringRadius = MathHelper.Lerp(RingStartRadius, BlastRadius, progress) * pulse;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringRadius, 9f,
                    new Color(255, 218, 150), new Color(235, 150, 70), new Color(110, 58, 30),
                    (0.26f + 0.34f * progress) * cancelDim, -1f, squish, 0.12f * progress,
                    Projectile.identity * 0.31f);

                //天际亮点增大：四芒星本体+斜置副星+暖晕衬底
                Vector2 skyPos = SkyOrigin - Main.screenPosition;
                float twinkle = 1f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity * 1.7f);
                float starScale = (0.05f + 0.17f * progress) * twinkle;
                Color starCol = new Color(255, 228, 170) * ((0.3f + 0.7f * progress) * cancelDim);
                Main.EntitySpriteDraw(glow, skyPos, null, new Color(255, 196, 120, 0) * (0.5f * progress * cancelDim),
                    0f, glowOrig, 1.6f * progress + 0.4f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, skyPos, null, starCol, 0f, starOrig, starScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, skyPos, null, starCol * 0.55f, MathHelper.PiOver4, starOrig,
                    starScale * 0.7f, SpriteEffects.None, 0);
                return false;
            }

            if (elapsed < ImpactFrame) {
                //坠落期：熔石体=暗石壳剪影+外缘热边（层序对标烬羽 PRT），拖尾=壳体自身层叠的残影回放
                float fallT = (elapsed - TelegraphFrames) / (float)FallFrames;
                Vector2 dir = (Projectile.Center - SkyOrigin).SafeNormalize(Vector2.UnitY);
                float rockRot = dir.ToRotation() + MathHelper.PiOver2;//梭形贴图长轴竖直，转到运动向
                float fallFlicker = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 30f + Projectile.identity);

                //拖尾残影：由远及近重绘暗壳+热边，缩放 0.5→0.7、透明度递减，末端热色冷却
                for (int k = TrailGhosts; k >= 1; k--) {
                    float kt = (k - 1) / (float)(TrailGhosts - 1);
                    Vector2 ghostPos = HeadPos(elapsed - k * TrailSpacing) - Main.screenPosition;
                    float mul = MathHelper.Lerp(0.7f, 0.5f, kt);
                    float fade = MathHelper.Lerp(0.5f, 0.14f, kt) * cancelDim;
                    Vector2 ghostScale = new Vector2(0.55f, 1.35f) * mul;//沿运动向拉长补缝
                    Color ghostRim = Color.Lerp(RimHot, RimCool, kt) with { A = 0 };
                    Main.EntitySpriteDraw(shell, ghostPos, null, ghostRim * fade, rockRot,
                        shellOrig, ghostScale * 1.25f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(shell, ghostPos, null, ShellDark * fade, rockRot,
                        shellOrig, ghostScale, SpriteEffects.None, 0);
                }

                Vector2 headScreen = HeadPos(elapsed) - Main.screenPosition;
                //热晕衬底（弱底光，不当本体）
                Main.EntitySpriteDraw(glow, headScreen, null, new Color(255, 150, 70, 0) * (0.45f * cancelDim),
                    0f, glowOrig, 0.85f, SpriteEffects.None, 0);
                //外缘热边：略大于壳体垫底，只露一圈灼烧缘
                Main.EntitySpriteDraw(shell, headScreen, null, (RimHot with { A = 0 }) * (0.85f * fallFlicker * cancelDim),
                    rockRot, shellOrig, new Vector2(0.72f, 1.24f), SpriteEffects.None, 0);
                //暗石壳：真 alpha 剪影，可见体的遮挡主体
                Main.EntitySpriteDraw(shell, headScreen, null, ShellDark * (0.92f * cancelDim),
                    rockRot, shellOrig, new Vector2(0.55f, 1.05f), SpriteEffects.None, 0);
                //熔核内芒：壳内余温透出（对标烬羽内敷层）
                Main.EntitySpriteDraw(shell, headScreen, null, (RimHot with { A = 0 }) * (0.5f * fallFlicker * cancelDim),
                    rockRot, shellOrig, new Vector2(0.3f, 0.62f), SpriteEffects.None, 0);
                //迎风燃点：前端最热
                Main.EntitySpriteDraw(glow, headScreen + dir * 20f, null, new Color(255, 210, 130, 0) * (0.6f * cancelDim),
                    0f, glowOrig, 0.4f, SpriteEffects.None, 0);
                //点火眩光：预告星点熄灭成岩体的衔接拍（坠落前 1/4 段渐隐）
                if (fallT < 0.25f) {
                    float ig = 1f - fallT / 0.25f;
                    Main.EntitySpriteDraw(star, headScreen, null, new Color(255, 228, 170) * (0.85f * ig * cancelDim),
                        0f, starOrig, 0.22f * ig + 0.03f, SpriteEffects.None, 0);
                }
                return false;
            }

            if (elapsed < ImpactFrame + BurstFrames) {
                //爆炸窗：热闪核+扩张冲击环+破壳石屑外抛，可见闪爆=判定圈
                float pb = (elapsed - ImpactFrame) / (float)BurstFrames;
                float fade = 1f - pb;
                ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                    MathHelper.Lerp(BlastRadius * 0.8f, BlastRadius * 1.9f, pb), 13f,
                    new Color(255, 230, 180), new Color(250, 170, 90), new Color(120, 60, 30),
                    0.55f * fade, -1f, squish, 0.25f, Projectile.identity * 0.31f);
                Vector2 center = Projectile.Center - Main.screenPosition;
                Main.EntitySpriteDraw(glow, center, null, new Color(255, 214, 150, 0) * (0.9f * fade),
                    0f, glowOrig, 3.4f * (0.6f + 0.8f * pb), SpriteEffects.None, 0);
                //破壳石屑：暗壳碎块带热边外抛旋转（确定性布点，各端画面一致）
                for (int k = 0; k < 6; k++) {
                    float ang = Projectile.identity * 0.31f + k * (MathHelper.TwoPi / 6f) + pb * (0.35f + 0.11f * k);
                    float dist = MathHelper.Lerp(12f, BlastRadius * (0.72f + 0.1f * (k % 3)), pb);
                    Vector2 fragPos = center + ang.ToRotationVector2() * dist * new Vector2(1f, squish)
                        - new Vector2(0f, 30f * MathF.Sin(pb * MathHelper.Pi) * (k % 2 == 0 ? 1f : 0.55f));
                    float spin = ang * 3f + pb * (3f + k * 0.8f);
                    Vector2 fragScale = new Vector2(0.2f, 0.3f) * (1f - 0.3f * pb) * (0.8f + 0.12f * (k % 3));
                    Main.EntitySpriteDraw(shell, fragPos, null, (RimHot with { A = 0 }) * (0.55f * fade),
                        spin, shellOrig, fragScale * 1.3f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(shell, fragPos, null, ShellDark * (0.9f * fade),
                        spin, shellOrig, fragScale, SpriteEffects.None, 0);
                }
                return false;
            }

            //余韵期：烟团缓升+烬光明灭+碎星闪点（活得比爆炸久，这里被砸过的唯一证据）
            float pa = (elapsed - ImpactFrame - BurstFrames) / (float)AfterglowFrames;
            float linger = 1f - pa;
            Vector2 impact = Projectile.Center - Main.screenPosition;
            float flicker = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity);
            Main.EntitySpriteDraw(glow, impact, null, new Color(255, 140, 70, 0) * (0.35f * linger * flicker),
                0f, glowOrig, 0.8f, SpriteEffects.None, 0);
            Vector2 fogOrig = fogTex.Size() * 0.5f;
            for (int k = 0; k < 3; k++) {
                float drift = MathF.Sin(Projectile.identity * 1.9f + k * 2.4f);
                Vector2 puffPos = impact + new Vector2(drift * 26f, -16f - 62f * pa - k * 15f);
                Color puffCol = new Color(70, 62, 58) * (0.33f * linger);
                Main.EntitySpriteDraw(fogTex, puffPos, null, puffCol, drift * 0.6f + k, fogOrig,
                    0.45f + 0.5f * pa + 0.12f * k, k % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }
            for (int k = 0; k < 4; k++) {
                Vector2 sparkPos = impact + new Vector2(
                    MathF.Sin(Projectile.identity * 0.7f + k * 1.9f) * 42f, -8f + 66f * pa + k * 6f);
                Main.EntitySpriteDraw(sparkle, sparkPos, null, new Color(255, 226, 160, 0) * (0.5f * linger),
                    k * 0.9f, sparkle.Size() * 0.5f, 0.14f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rotmire.Projectiles
{
    /// <summary>
    /// 「瘴气涌泉」瘴柱。ai[0]=体型 ai[1]=谷底旗标（谷口涌泉更高）。
    /// 生成位置即锁定柱位（预告即承诺）：地面紫泡聚集+咕噜 52 帧 → 瘴柱自地缝缓慢上升
    /// （生长与滞留窗口内有判定，触碰者原版虚弱+微量伤害）→ 向上散逸收场。
    /// 材质=腐沼气：源头攒泡溢出、柱身缓升受风微斜、顶冠蘑菇状稀薄散开；
    /// 暗层用 Fog 真 alpha 承担，亮芯 A=0 加色。各端由 timeLeft 确定性推演，无追加同步
    /// </summary>
    internal class RotmireVentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，档位一律不缩短）</summary>
        private const int TelegraphFrames = 52;
        /// <summary>瘴柱生长帧数（缓慢上升）</summary>
        private const int RiseFrames = 46;
        /// <summary>满高滞留帧数</summary>
        private const int HoldFrames = 70;
        /// <summary>散逸帧数（判定关闭）</summary>
        private const int FadeFrames = 34;
        /// <summary>柱高（×体型；谷底涌泉另 ×1.18）</summary>
        private const float BaseHeight = 200f;
        /// <summary>判定半宽（×体型），藏在可见瘴体之内</summary>
        private const float BaseHalfWidth = 26f;
        /// <summary>命中后的原版虚弱时长（短暂）</summary>
        private const int WeakFrames = 150;

        //配色只读引用邪地风味表（腐化紫底绿芯），保持家族一致
        private static readonly Color GasDeep = EvilBiomeFX.Deep(EvilBiomeFX.FlavorCorrupt);
        private static readonly Color GasBright = EvilBiomeFX.Bright(EvilBiomeFX.FlavorCorrupt);

        private float Scale => Projectile.ai[0];
        private bool Gorge => Projectile.ai[1] == 1f;
        private float FullHeight => BaseHeight * Scale * (Gorge ? 1.18f : 1f);

        private const int TotalLife = TelegraphFrames + RiseFrames + HoldFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>柱体生长 0~1（缓升，收尾减速）</summary>
        private float RiseProgress {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= RiseFrames) {
                    return 1f;
                }
                float x = t / (float)RiseFrames;
                return 1f - (1f - x) * (1f - x);
            }
        }

        /// <summary>散逸 0~1</summary>
        private float FadeT {
            get {
                int t = Elapsed - (TelegraphFrames + RiseFrames + HoldFrames);
                return t <= 0 ? 0f : MathHelper.Clamp(t / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 360;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//瘴柱在场窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //判定窗=瘴柱在场窗（生长+滞留），散逸期无害；各端由本地 timeLeft 一致推演。
            //Boss 登场瞬间已在场瘴柱一并缴械（视觉走完）；HasBoss 各端由同步 NPC 表自算，结论一致
            Projectile.hostile = GameModeSystem.BrutalActive && !CWRWorld.HasBoss
                && elapsed >= TelegraphFrames
                && elapsed < TelegraphFrames + RiseFrames + HoldFrames;

            if (elapsed == 0 && !Main.dedServ) {
                //预告起手：地底咕噜（听觉通道①）
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 },
                    Projectile.Center);
            }
            if (Main.dedServ) {
                return;
            }

            if (elapsed < TelegraphFrames) {
                //预告期：紫泡聚集（视觉通道）+ 断续气泡声（听觉通道②）
                if (elapsed % 13 == 0 && elapsed > 0) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.22f, Pitch = -0.6f + Main.rand.NextFloat(0.12f), MaxInstances = 4
                    }, Projectile.Center);
                }
                if (Main.rand.NextBool(2)) {
                    Dust bubble = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-18f, 18f) * Scale, 2f),
                        DustID.CorruptGibs, new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)),
                        140, default, Main.rand.NextFloat(0.7f, 1.1f));
                    bubble.noGravity = true;
                }
                return;
            }

            if (elapsed == TelegraphFrames) {
                //破缝帧：闷响+气浪
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.55f, Pitch = -0.55f, MaxInstances = 3 },
                    Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 3 },
                    Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center,
                        EvilBiomeFX.DustFor(EvilBiomeFX.FlavorCorrupt),
                        new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1.5f, 4f)) * Scale,
                        120, default, Main.rand.NextFloat(1f, 1.6f));
                    burst.noGravity = true;
                }
            }

            float fade = FadeT;
            float height = FullHeight * RiseProgress;
            if (fade < 1f && Main.rand.NextBool(2)) {
                //柱内缓升腐孢（散逸期变稀）
                if (fade <= 0f || Main.rand.NextBool(2)) {
                    Dust gas = Dust.NewDustPerfect(
                        Projectile.Center - new Vector2(
                            Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale * 0.8f,
                            Main.rand.NextFloat(0f, Math.Max(height, 16f))),
                        EvilBiomeFX.DustFor(EvilBiomeFX.FlavorCorrupt),
                        new Vector2(Main.windSpeedCurrent * 0.6f, -Main.rand.NextFloat(1f, 2.2f)),
                        130, default, Main.rand.NextFloat(0.8f, 1.3f));
                    gas.noGravity = true;
                }
            }
            //滞留期偶发气泡声
            if (elapsed % 26 == 0 && fade <= 0f) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.14f, Pitch = -0.68f, MaxInstances = 3
                }, Projectile.Center);
            }

            float bodyLight = RiseProgress * (1f - fade);
            if (bodyLight > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * height * 0.5f,
                    new Vector3(0.16f, 0.22f, 0.1f) * bodyLight);
            }
        }

        /// <summary>柱形判定：沿柱轴分三段取样（窗口由 hostile 门控，判定窄于可见瘴体）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float height = FullHeight * RiseProgress;
            if (height < 24f) {
                return false;
            }
            float halfWidth = BaseHalfWidth * Scale;
            for (int i = 0; i < 3; i++) {
                Vector2 point = Projectile.Center - new Vector2(0f, height * (0.17f + 0.33f * i));
                Rectangle sample = Utils.CenteredRectangle(point, new Vector2(halfWidth * 2f, height * 0.4f));
                if (sample.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //短暂原版虚弱（受击端本机结算，原生同步）
            target.AddBuff(EvilBiomeFX.BuffFor(EvilBiomeFX.FlavorCorrupt), WeakFrames);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || fog.IsDisposed) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            int elapsed = Elapsed;
            float time = Main.GlobalTimeWrappedHourly;
            float seed = Projectile.identity * 1.37f;

            if (elapsed < TelegraphFrames) {
                DrawTelegraph(fog, glow, fogOrigin, elapsed, time, seed);
                return false;
            }

            float rise = RiseProgress;
            float fade = FadeT;
            float alphaK = 1f - fade;
            if (rise <= 0.01f || alphaK <= 0.01f) {
                return false;
            }
            float height = FullHeight * rise;
            //散逸期整柱上飘并变宽稀薄
            float drift = fade * 56f;
            float widen = 1f + fade * 0.55f;
            //受风微斜：越高偏移越大
            float lean = Main.windSpeedCurrent * 26f;

            //柱身 6 节雾团：根部球根、中段收窄、顶冠散开（三个端点各有物理答案，无平切）
            for (int i = 0; i < 6; i++) {
                float seg = (i + 0.5f) / 6f;
                float segY = height * seg + drift;
                float wobble = MathF.Sin(time * (1.6f + i * 0.13f) + seed + i * 1.9f) * (3f + 5f * seg);
                float segWidth = seg < 0.2f ? 1.2f : seg > 0.8f ? 1.3f : 0.92f;
                float sizePx = 74f * Scale * segWidth * widen * MathHelper.Clamp(rise * 1.4f - seg * 0.3f, 0.3f, 1f);
                Vector2 pos = Projectile.Center
                    + new Vector2(wobble + lean * seg, -segY) - Main.screenPosition;
                float segAlpha = (0.42f - 0.1f * seg) * alphaK;
                Color veil = Color.Lerp(GasDeep, Color.Black, 0.25f * (1f - seg));
                Main.EntitySpriteDraw(fog, pos, null, veil * segAlpha,
                    time * (0.14f + i * 0.05f) + seed, fogOrigin, sizePx / fog.Width, SpriteEffects.None, 0);
            }
            //亮芯节点：孢光在柱内明灭（A=0 加色敷料）
            if (glow != null && !glow.IsDisposed) {
                Vector2 glowOrigin = glow.Size() * 0.5f;
                for (int i = 0; i < 3; i++) {
                    float seg = 0.22f + 0.3f * i;
                    float pulse = 0.6f + 0.4f * MathF.Sin(time * 5f + seed + i * 2.4f);
                    Vector2 pos = Projectile.Center + new Vector2(
                        MathF.Sin(time * 1.8f + seed + i * 2.1f) * 5f + lean * seg,
                        -(height * seg + drift)) - Main.screenPosition;
                    Color core = new Color(GasBright.R, GasBright.G, GasBright.B, (byte)0)
                        * (0.2f * pulse * alphaK * rise);
                    Main.EntitySpriteDraw(glow, pos, null, core, 0f, glowOrigin,
                        0.5f * Scale, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        //预告绘制：地面警示光斑（脉动）+ 逐渐聚集、鼓起又瘪下的紫泡
        private void DrawTelegraph(Texture2D fog, Texture2D glow, Vector2 fogOrigin,
            int elapsed, float time, float seed) {
            float progress = elapsed / (float)TelegraphFrames;
            if (glow != null && !glow.IsDisposed) {
                float pulse = 0.7f + 0.3f * MathF.Sin(time * 13f + seed);
                Color warn = new Color(GasBright.R, GasBright.G, GasBright.B, (byte)0)
                    * (0.4f * progress * pulse);
                Main.EntitySpriteDraw(glow, Projectile.Center + new Vector2(0f, 2f) - Main.screenPosition,
                    null, warn, 0f, glow.Size() * 0.5f,
                    new Vector2(1.5f * Scale, 0.38f), SpriteEffects.None, 0);
            }
            //紫泡：数量随预告推进增多，各自按错相周期鼓起
            int bubbleCount = 1 + (int)(progress * 3f);
            for (int b = 0; b < bubbleCount; b++) {
                float cycle = (time * 0.9f + b * 0.37f + seed * 0.11f) % 1f;
                float grow = MathF.Sin(cycle * MathHelper.Pi);
                float x = MathF.Sin(seed + b * 2.6f) * 16f * Scale;
                float sizePx = (14f + 14f * grow) * Scale * (0.7f + 0.3f * progress);
                Vector2 pos = Projectile.Center + new Vector2(x, 4f - grow * 5f) - Main.screenPosition;
                Main.EntitySpriteDraw(fog, pos, null,
                    Color.Lerp(GasDeep, Color.Black, 0.2f) * (0.5f * progress),
                    time * 0.3f + b, fogOrigin, sizePx / fog.Width, SpriteEffects.None, 0);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散逸收尾：残余腐孢向上飘散
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center - new Vector2(Main.rand.NextFloat(-BaseHalfWidth, BaseHalfWidth) * Scale,
                        Main.rand.NextFloat(0.3f, 1f) * FullHeight),
                    EvilBiomeFX.DustFor(EvilBiomeFX.FlavorCorrupt),
                    new Vector2(Main.windSpeedCurrent * 0.8f + Main.rand.NextFloat(-0.4f, 0.4f),
                        -Main.rand.NextFloat(0.6f, 1.4f)),
                    150, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = true;
            }
        }
    }
}

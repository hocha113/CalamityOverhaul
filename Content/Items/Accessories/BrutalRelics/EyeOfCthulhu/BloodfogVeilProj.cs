using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾裹体锚点：本身无伤害，全屏着色器按其位置合成体积血雾(BloodfogIrisRender)。<br/>
    /// owner 端生成后经原生弹幕同步，跨端演出(起手爆发/雾态视觉/仇恨压制)全由本弹幕承载。<br/>
    /// ai[0]=模式：0 突进随身雾裹 / 1 原地雾爆 / 2 重凝随身雾裹；ai[1]=总寿命(帧)
    /// </summary>
    internal class BloodfogVeilProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal int Mode => (int)Projectile.ai[0];
        internal float TotalLife => Math.Max(Projectile.ai[1], 1f);
        /// <summary>本端存活帧数</summary>
        private float Age => Projectile.localAI[1];

        internal float BloomProgress
            => MathHelper.Clamp(Age / (Mode == 1 ? 6f : 10f), 0f, 1f);

        /// <summary>当前雾半径 px，渲染句柄读取</summary>
        internal float CurrentRadius {
            get {
                float bloom = VaultUtils.EaseOutCubic(BloomProgress);
                if (Mode == 1) {
                    float lifeFrac = MathHelper.Clamp(Projectile.timeLeft / TotalLife, 0f, 1f);
                    return 150f * bloom * (0.55f + 0.45f * lifeFrac);
                }
                float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
                return 112f * bloom * (0.6f + 0.4f * fade);
            }
        }

        /// <summary>当前遮蔽密度 0~1，渲染句柄读取</summary>
        internal float CurrentDensity {
            get {
                float bloom = VaultUtils.EaseOutCubic(BloomProgress);
                if (Mode == 1) {
                    float lifeFrac = MathHelper.Clamp(Projectile.timeLeft / TotalLife, 0f, 1f);
                    return 0.9f * bloom * MathF.Pow(lifeFrac, 1.5f);
                }
                float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
                return 0.72f * bloom * MathF.Pow(fade, 0.75f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
            //伏击窗口全程存在，晚入场玩家也要收到
            Projectile.netImportant = true;
        }

        public override void AI() {
            //首帧：按 ai[1] 定寿命 + 起手演出(各端本地执行一次)
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = (int)TotalLife;
                PlaySpawnPerformance();
            }
            Projectile.localAI[1]++;

            if (Mode == 1) {
                Projectile.velocity = Vector2.Zero;
                UpdateAmbientWisps(null);
                return;
            }

            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.velocity = Vector2.Zero;

            //雾态视觉计时：各端(含服务端)每帧点亮，
            //服务端由 PostUpdateEquips 读它压仇恨，客户端驱动本体褪色
            owner.GetModPlayer<BloodfogIrisPlayer>().VeilVisualTimer = 2;

            UpdateAmbientWisps(owner);
        }

        /// <summary>首帧演出：模式各异，粒子/音效内部自带服务端守卫</summary>
        private void PlaySpawnPerformance() {
            Player owner = Main.player[Projectile.owner];
            switch (Mode) {
                case 0: {
                    //突进起手：沿冲刺方向血爆(含震屏与湿吼)
                    Vector2 dir = owner.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
                    EocMotion.LaunchBurst(Projectile.Center, dir, 0.95f);
                    break;
                }
                case 1: {
                    //消隐点：躯体溶解成血雾，表皮碎屑翻滚坠落
                    EocMotion.BloodBurst(Projectile.Center, 1.5f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.75f, Pitch = -0.45f }, Projectile.Center);
                        for (int i = 0; i < 7; i++) {
                            Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f);
                            vel.Y -= 2.2f;
                            PRTLoader.NewParticle<PRT_EocSkinShred>(
                                Projectile.Center + Main.rand.NextVector2Circular(16f, 24f), vel,
                                Color.Lerp(EocMotion.Arterial, EocMotion.VenousDark, Main.rand.NextFloat()),
                                Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(26, 44));
                        }
                    }
                    break;
                }
                case 2: {
                    //重凝落点：血丝向心收拢 + 湿咽音 + 瞳孔亮起
                    EocMotion.MistPuff(Projectile.Center, 5, 1.4f, 0.5f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.7f, Pitch = -0.25f }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.55f, Pitch = -0.6f }, Projectile.Center);
                        for (int i = 0; i < 6; i++) {
                            EocMotion.ConvergeStreaks(Projectile.Center, 0.35f, 150f);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>常驻雾息：边缘游丝、高速甩血、心跳微光，纯客户端</summary>
        private void UpdateAmbientWisps(Player owner) {
            if (VaultUtils.isServer || !EocMotion.OnScreen(Projectile.Center, 520f)) {
                return;
            }

            float radius = CurrentRadius;
            if (Main.rand.NextBool(9) && radius > 30f) {
                EocMotion.MistPuff(Projectile.Center
                    + Main.rand.NextVector2CircularEdge(radius * 0.7f, radius * 0.7f), 1, 0.85f, 0.3f);
            }

            //随身雾裹：高速位移时向后甩血滴(各端按本地看到的速度自播，纯表现)
            if (owner != null) {
                float speed = owner.velocity.Length();
                if (speed > 16f && Main.GameUpdateCount % 2 == 0) {
                    Vector2 back = -owner.velocity.SafeNormalize(Vector2.Zero);
                    Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2f, 6f);
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        owner.Center + Main.rand.NextVector2Circular(18f, 22f), vel,
                        Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(16, 26), 0.3f, 0.98f);
                    if (Main.rand.NextBool(3)) {
                        EocMotion.MistPuff(owner.Center - owner.velocity * 0.4f, 1, 0.75f, 0.3f);
                    }
                }
            }

            //心跳微光
            float pulse = 0.55f + 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.19f);
            Lighting.AddLight(Projectile.Center, EocMotion.MistWine.ToVector3() * pulse * CurrentDensity);
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) {
            //雾体由全屏着色器绘制；这里画血带拖尾与雾中瞳光
            if (Mode == 1) {
                return false;
            }
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                return false;
            }
            if (owner.TryGetModPlayer(out BloodfogIrisPlayer mp)) {
                DrawPlayerTrail(owner, mp);
                DrawPupilGlint(owner, mp);
            }
            return false;
        }

        /// <summary>雾中瞳光：常亮微光(位置的公平线索) + 间歇一闪；突进冷却期微暗</summary>
        private void DrawPupilGlint(Player owner, BloodfogIrisPlayer mp) {
            float bloom = VaultUtils.EaseOutCubic(BloomProgress);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            float blink = MathF.Pow(MathF.Max(MathF.Sin(
                (float)Main.timeForVisualEffects * 0.11f + Projectile.whoAmI * 1.7f), 0f), 7f);
            float glow = (0.2f + blink * 0.8f) * bloom * fade;
            //可见冷却：突进转好前瞳光收敛(计时仅所有者端非零，远端不暗)
            if (mp.DashCooldown > 0) {
                glow *= 0.6f;
            }
            if (glow < 0.03f) {
                return;
            }

            Texture2D soft = CWRAsset.SoftGlow.Value;
            Texture2D flare = CWRAsset.StarFlare02.Value;
            Vector2 pos = owner.Center + new Vector2(owner.direction * 7f, -5f) - Main.screenPosition;
            //黑底贴图在预乘批里走 A=0 加色
            Color glintColor = EocMotion.IrisRed with { A = 0 };
            Main.spriteBatch.Draw(soft, pos, null, glintColor * (glow * 0.8f), 0f,
                soft.Size() / 2f, 0.55f * glow + 0.2f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(flare, pos, null, glintColor * (glow * 0.65f),
                Main.GlobalTimeWrappedHourly * 1.4f, flare.Size() / 2f, 0.14f * glow + 0.03f, SpriteEffects.None, 0f);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer && EocMotion.OnScreen(Projectile.Center, 600f)) {
                EocMotion.MistPuff(Projectile.Center, 3, 1.1f, 0.35f);
            }
        }

        #region 血带拖尾(静态资源，Unload 释放)
        private const int TrailBufCount = 24;
        private static Trail trailRenderer;
        private static readonly Vector2[] trailBuf = new Vector2[TrailBufCount];
        private static float trailWidth;
        private static float trailAlpha;

        internal static void UnloadTrailResources() {
            trailRenderer?.Dispose();
            trailRenderer = null;
        }

        /// <summary>玩家血带：EocBloodTrail 三层液体截面，缺 fxc 回退 GradientTrail</summary>
        private static void DrawPlayerTrail(Player owner, BloodfogIrisPlayer mp) {
            float intensity = mp.TrailHeat;
            if (intensity <= 0.06f || mp.TrailPoints.Count < 3) {
                return;
            }
            Effect effect = EffectLoader.EocBloodTrail?.Value;
            bool bespoke = effect != null;
            if (!bespoke) {
                effect = EffectLoader.GradientTrail?.Value;
            }
            if (effect == null) {
                return;
            }

            //头在前：玩家当前位置 + 由新到旧的采样点，超长段(传送)截断
            Span<Vector2> gathered = stackalloc Vector2[TrailBufCount];
            int count = 0;
            gathered[count++] = owner.Center;
            for (int i = mp.TrailPoints.Count - 1; i >= 0 && count < TrailBufCount; i--) {
                Vector2 pos = mp.TrailPoints[i].Pos;
                if (Vector2.DistanceSquared(pos, gathered[count - 1]) > 380f * 380f) {
                    break;
                }
                gathered[count++] = pos;
            }
            if (count < 4) {
                return;
            }

            Vector2 oldest = gathered[count - 1];
            int pad = TrailBufCount - count;
            for (int i = 0; i < pad; i++) {
                trailBuf[i] = oldest;
            }
            for (int i = 0; i < count; i++) {
                trailBuf[pad + i] = gathered[count - 1 - i];
            }

            trailWidth = 40f * intensity;
            trailAlpha = 0.85f * intensity;

            trailRenderer ??= new Trail(new Vector2[TrailBufCount],
                f => trailWidth * (0.14f + f * 0.86f),
                texCoord => Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, texCoord.X)
                    * (trailAlpha * (0.2f + texCoord.X * 0.8f)));
            trailRenderer.TrailPositions = trailBuf;

            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            if (bespoke) {
                effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.035f);
                effect.Parameters["uIntensity"]?.SetValue(intensity);
                //噪声显式绑 s1，参数式贴图绑定在 SpriteBatch 下失效
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                gd.BlendState = BlendState.AlphaBlend;
                trailRenderer.DrawTrail(effect);
                gd.BlendState = BlendState.AlphaBlend;
                return;
            }

            //缺 fxc 回退：GradientTrail + 血红渐变
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * -0.05f);
            effect.Parameters["uTimeG"]?.SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"]?.SetValue(1f);
            effect.Parameters["uBaseImage"]?.SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uFlow"]?.SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uGradient"]?.SetValue(CWRAsset.BloodRed_Bar.Value);
            effect.Parameters["uDissolve"]?.SetValue(VaultAsset.placeholder2.Value);
            gd.BlendState = BlendState.Additive;
            for (int i = 0; i < 2; i++) {
                trailRenderer.DrawTrail(effect);
            }
            gd.BlendState = BlendState.AlphaBlend;
        }
        #endregion
    }

    /// <summary>
    /// 伏击印记：伏击命中点炸开的红色裂瞳，owner 命中钩子里生成、
    /// 经弹幕同步各端可见；纯演出无伤害
    /// </summary>
    internal class BloodfogAmbushMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int Life = 34;
        private float Progress => 1f - Projectile.timeLeft / (float)Life;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                PlayBurst();
            }
            Lighting.AddLight(Projectile.Center, EocMotion.IrisRed.ToVector3() * 0.8f * (1f - Progress));
        }

        /// <summary>命中拍：血爆 + 放射血滴 + 瞳色电花 + 湿裂响 + 血闪</summary>
        private void PlayBurst() {
            EocMotion.BloodBurst(Projectile.Center, 1.2f, playSound: false);
            EocMotion.Shake(Projectile.Center, 5f, 10);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.9f, Pitch = 0.22f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.75f, Pitch = -0.12f }, Projectile.Center);

            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.3f);
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 11f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(20, 34), 0.32f, 0.984f);
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    EocMotion.IrisRed, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, Main.rand.Next(10, 18));
            }
            if (EocMotion.OnScreen(Projectile.Center)) {
                BloodfogScreenFX.PushFlash(0.3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float progress = Progress;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Effect effect = EffectLoader.BRelicIrisMark?.Value;
            if (effect == null) {
                DrawFallbackSigil(sb, center, progress);
                return false;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uIntensity"]?.SetValue(1f);
            //噪声显式绑 s1(shader 内 register(s1))
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            gd.Textures[1] = CWRAsset.PerlinNoise.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float side = 300f * (0.85f + 0.35f * VaultUtils.EaseOutCubic(MathHelper.Clamp(progress * 2.4f, 0f, 1f)));
            Vector2 scale = new(side / pixel.Width, side / pixel.Height);
            sb.Draw(pixel, center, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>缺 fxc 简笔：血环扩张 + 竖瞳暗芯 + 星芒，杜绝无形演出</summary>
        private void DrawFallbackSigil(SpriteBatch sb, Vector2 center, float progress) {
            float fade = 1f - VaultUtils.EaseInQuad(progress);
            //DiffusionCircle 真 alpha，可正常染色
            Texture2D disc = CWRAsset.DiffusionCircle.Value;
            float discScale = (60f + progress * 90f) * 2f / disc.Width;
            sb.Draw(disc, center, null, EocMotion.Arterial * (0.7f * fade), 0f,
                disc.Size() / 2f, discScale, SpriteEffects.None, 0f);
            //Extra_98 真 alpha，允许画暗竖瞳
            Texture2D dark = CWRAsset.Extra_98.Value;
            Vector2 slitScale = new(0.12f, 0.9f * (0.4f + progress * 0.6f));
            sb.Draw(dark, center, null, EocMotion.VenousDark * (0.85f * fade), 0f,
                dark.Size() / 2f, slitScale, SpriteEffects.None, 0f);
            //星芒走 A=0 加色
            Texture2D flare = CWRAsset.StarFlare02.Value;
            sb.Draw(flare, center, null, (EocMotion.IrisRed with { A = 0 }) * fade,
                progress * 1.6f, flare.Size() / 2f, 0.3f * (0.5f + progress), SpriteEffects.None, 0f);
        }
    }
}

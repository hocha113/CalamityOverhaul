using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Destroyer
{
    /// <summary>
    /// 轨道打击：预告柱→天降贯穿死光→冲击→热浪余韵。
    /// ai[0]标定目标(仅预告期追踪用)。时间轴走localAI，各端自生成起确定性推进；
    /// 光柱两端收口由DestroyerBeam着色器承担：地面端白热喷口咬地，天空端噪声撕散
    /// </summary>
    internal class ProbeOrbitalStrike : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //时间轴
        internal const int TelegraphTime = 36;
        internal const int TrackTime = 22;
        internal const int ExpandTime = 8;
        internal const int SustainTime = 30;
        internal const int CollapseTime = 12;
        internal const int AfterTime = 80;
        internal const int TotalLife = TelegraphTime + ExpandTime + SustainTime + CollapseTime + AfterTime;
        //冲击拍(光柱展开到位后砸地)
        private const int ImpactBeat = TelegraphTime + 3;

        /// <summary>光柱满宽</summary>
        private const float MaxWidth = 126f;
        /// <summary>柱顶相对瞄准点的高度</summary>
        private const float SkyReach = 2600f;

        private ref float Timer => ref Projectile.localAI[0];

        //宽度生命周期与落地点，各端逐帧自算
        private float beamWidth;
        private Vector2 groundPos;
        private bool impactDone;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalLife + 10;
            Projectile.usesLocalNPCImmunity = true;
            //判定窗≈48帧：全程被罩住的目标恰好两跳，晚进柱的一跳(三跳不可达)
            Projectile.localNPCHitCooldown = 44;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            float t = Timer;

            //预告期前段跟踪目标，随后锁死
            if (t < TrackTime) {
                int idx = (int)Projectile.ai[0];
                if (idx >= 0 && idx < Main.maxNPCs) {
                    NPC target = Main.npc[idx];
                    if (target.active && !target.friendly) {
                        Projectile.Center = Vector2.Lerp(Projectile.Center,
                            target.Center + target.velocity * 12f, 0.16f);
                    }
                }
            }
            else if (t == TrackTime && Projectile.owner == Main.myPlayer) {
                //锁定帧所有者校正一次位置
                Projectile.netUpdate = true;
            }

            groundPos = DestroyerMotionFX.FindGroundBelow(Projectile.Center);
            UpdateWidth(t);
            PlayBeats(t);
            EmitAmbientFx(t);

            if (t >= TotalLife) {
                Projectile.Kill();
            }
        }

        /// <summary>宽度生命周期：展开→维持(呼吸)→塌缩归零</summary>
        private void UpdateWidth(float t) {
            float fireT = t - TelegraphTime;
            if (fireT <= 0f) {
                beamWidth = 0f;
                return;
            }
            if (fireT < ExpandTime) {
                beamWidth = MathHelper.Lerp(6f, MaxWidth, VaultUtils.EaseOutCubic(fireT / ExpandTime));
            }
            else if (fireT < ExpandTime + SustainTime) {
                beamWidth = MaxWidth;
            }
            else if (fireT < ExpandTime + SustainTime + CollapseTime) {
                float c = (fireT - ExpandTime - SustainTime) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(c));
            }
            else {
                beamWidth = 0f;
                return;
            }
            beamWidth *= 1f + 0.04f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);
        }

        /// <summary>时间轴节拍：预警声→释放→砸地冲击→塌缩嘶鸣</summary>
        private void PlayBeats(float t) {
            if (VaultUtils.isServer) {
                return;
            }

            if (t == 1f) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.4f, Volume = 1.05f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.75f, Pitch = -0.55f }, Projectile.Center);
            }

            if (t == TelegraphTime) {
                //释放拍：毁灭者口束嗓音+天空闪雷(机械战场景内自门控)
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                MachineEffect.TriggerSkyFlash(Projectile.Center, 1f);
                DestroyerMotionFX.CameraPunch(Projectile.Center, 6f, 10, "ProbeOrbitalRelease", Vector2.UnitY);
            }

            if (t == ImpactBeat && !impactDone) {
                impactDone = true;
                DestroyerMotionFX.SpawnImpactBlast(groundPos, 1.35f);
                DestroyerMotionFX.CameraPunch(groundPos, 12f, 24, "ProbeOrbitalImpact", Vector2.UnitY);
                PRTLoader.NewParticle<PRT_StarPulseRing>(groundPos, Vector2.Zero,
                    ProbeDroneProj.ThemeAmber, 0.08f)?.Configure(0.08f, 1.6f, 22);
            }

            if (t == TelegraphTime + ExpandTime + SustainTime) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.6f, Pitch = -0.2f }, groundPos);
            }
        }

        /// <summary>沿柱火花与落点余烬，密度随阶段衰减</summary>
        private void EmitAmbientFx(float t) {
            //沿柱与落点常亮照明
            if (beamWidth > 4f) {
                float colTop = Projectile.Center.Y - SkyReach;
                for (int i = 0; i < 6; i++) {
                    float y = MathHelper.Lerp(groundPos.Y, colTop, i / 6f);
                    Lighting.AddLight(new Vector2(Projectile.Center.X, y),
                        ProbeDroneProj.ThemeBlood.ToVector3() * 0.8f);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            //预告期向线心汇聚的采样火花
            if (t < TelegraphTime && t % 3 == 0 && DestroyerMotionFX.OnScreen(Projectile.Center)) {
                Vector2 gather = Projectile.Center + Main.rand.NextVector2CircularEdge(120f, 120f);
                PRTLoader.NewParticle<PRT_Spark>(gather, (Projectile.Center - gather) * 0.1f,
                    ProbeDroneProj.ThemeBlood, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, 14);
            }

            //光柱期沿束熔滴
            if (beamWidth > MaxWidth * 0.35f && Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(groundPos, new Vector2(Projectile.Center.X, Projectile.Center.Y - SkyReach), along);
                pos.X += Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(2f, 8f)),
                    Color.Lerp(ProbeDroneProj.ThemeAmber, Color.White, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.6f))?.Configure(true, Main.rand.Next(14, 22));
            }

            //冲击后落点余韵：升腾余烬+烟
            float afterStart = TelegraphTime + ExpandTime + SustainTime + CollapseTime;
            if (t > ImpactBeat && t < TotalLife - 20) {
                float decay = t < afterStart ? 1f : 1f - (t - afterStart) / AfterTime;
                if (Main.rand.NextFloat() < 0.55f * decay) {
                    Vector2 pos = groundPos + new Vector2(Main.rand.NextFloat(-120f, 120f), Main.rand.NextFloat(-8f, 4f));
                    PRTLoader.NewParticle<PRT_LavaFire>(pos,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3.4f)),
                        Color.White, Main.rand.NextFloat(0.6f, 1.2f) * decay)?.SetLifetime(20, 44);
                }
                if (t % 5 == 0 && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Smoke>(groundPos + new Vector2(Main.rand.NextFloat(-90f, 90f), 0f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.8f),
                        new Color(64, 58, 54), Main.rand.NextFloat(0.7f, 1.2f))
                        ?.Configure(Main.rand.Next(40, 70), 0.55f * decay, Main.rand.NextFloat(-0.04f, 0.04f));
                }
            }
        }

        #region 判定

        //展开过两成半才咬人，塌缩后即止
        public override bool? CanDamage() => beamWidth > MaxWidth * 0.25f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (beamWidth <= MaxWidth * 0.25f) {
                return false;
            }
            float p = 0f;
            Vector2 top = new(Projectile.Center.X, Projectile.Center.Y - SkyReach);
            Vector2 bottom = new(Projectile.Center.X, groundPos.Y + 16f);
            //判定窄于可见亮体
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                top, bottom, beamWidth * 0.6f, ref p);
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) {
            float t = Timer;

            //预告柱：复用DestroyerTelegraph着色器，垂直段自瞄准点上空压到地面
            if (t > 0f && t < TelegraphTime) {
                if (EffectLoader.DestroyerTelegraph?.Value != null) {
                    DrawTelegraphColumn(EffectLoader.DestroyerTelegraph.Value, t);
                }
                else {
                    DrawTelegraphFallback(t);
                }
            }

            //冲击拍起26帧的贴地冲击环
            float ringAge = t - ImpactBeat;
            if (ringAge >= 0f && ringAge < 26f) {
                float ringT = ringAge / 26f;
                ShockRingDraw.Draw(Main.spriteBatch, groundPos,
                    MathHelper.Lerp(36f, 300f, VaultUtils.EaseOutCubic(ringT)),
                    MathHelper.Lerp(26f, 10f, ringT),
                    new Color(255, 235, 200), new Color(255, 90, 45), new Color(150, 25, 12),
                    0.9f * (1f - ringT), squish: 0.42f, innerGlow: 0.25f,
                    timeSeed: Projectile.whoAmI * 0.31f);
            }
            return false;
        }

        /// <summary>预告柱：末段锁定白闪推进，宽度随锁定加粗</summary>
        private void DrawTelegraphColumn(Effect effect, float t) {
            float fadeIn = MathHelper.Clamp(t / 9f, 0f, 1f);
            float lockT = MathHelper.Clamp((t - (TelegraphTime - 14f)) / 14f, 0f, 1f);
            Vector2 top = new(Projectile.Center.X, Projectile.Center.Y - SkyReach);
            float length = groundPos.Y + 40f - top.Y;
            float width = 96f + lockT * 54f;

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(fadeIn * (0.6f + lockT * 0.5f));
            effect.Parameters["uLockProgress"]?.SetValue(lockT);
            effect.Parameters["uAspect"]?.SetValue(length / width);
            effect.Parameters["uColor"]?.SetValue(new Vector3(1f, 0.22f, 0.13f));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(length / pixel.Width, width / pixel.Height);
            sb.Draw(pixel, top - Main.screenPosition, null, Color.White,
                MathHelper.PiOver2, new Vector2(0, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺席时的预告回退：呼吸细线+锁定白闪，不许无预告落束</summary>
        private void DrawTelegraphFallback(float t) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float lockT = MathHelper.Clamp((t - (TelegraphTime - 14f)) / 14f, 0f, 1f);
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
            Vector2 top = new Vector2(Projectile.Center.X, Projectile.Center.Y - SkyReach) - Main.screenPosition;
            float length = groundPos.Y + 40f - (Projectile.Center.Y - SkyReach);

            Color warn = new Color(255, 50, 30, 0) * ((0.4f + lockT * 0.5f) * pulse);
            Main.spriteBatch.Draw(pixel, top, null, warn, MathHelper.PiOver2,
                new Vector2(0f, pixel.Height / 2f),
                new Vector2(length / pixel.Width, (2.5f + lockT * 5f) / pixel.Height), SpriteEffects.None, 0f);
        }

        /// <summary>光柱体：DestroyerBeam着色器quad，口器端埋进地面咬地，尾端上天撕散</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1.5f) {
                return;
            }
            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                //回退层画在加色装点里，判定期不许无形
                return;
            }

            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Vector2 dir = -Vector2.UnitY;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            //口器端(uv.x=1)下沉埋进地面，硬边藏进地形
            Vector2 origin = new(Projectile.Center.X, groundPos.Y + beamWidth * 0.3f + 30f);
            Vector2 tip = new(Projectile.Center.X, Projectile.Center.Y - SkyReach);
            float halfW = beamWidth * 3.2f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            //EX白热档，与Boss口束区分档次
            effect.Parameters["exMode"]?.SetValue(1f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>加色装点：落点白热核+行进脉冲+余韵熔斑，真加色批A随强度走</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch sb) {
            Texture2D glow = CWRAsset.DiffusionCircle.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 screenGround = groundPos - Main.screenPosition;
            float t = Timer;

            //光柱期落点装点
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            if (opacity > 0.02f) {
                float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

                //着色器缺席回退：三层拉伸柱，杜绝无形判定
                if (EffectLoader.DestroyerBeam?.Value == null) {
                    Texture2D pixel = VaultAsset.placeholder2.Value;
                    Vector2 fallTop = new Vector2(Projectile.Center.X, Projectile.Center.Y - SkyReach) - Main.screenPosition;
                    float length = groundPos.Y + 30f - (Projectile.Center.Y - SkyReach);
                    Vector2 lineOrigin = new(0f, pixel.Height / 2f);
                    sb.Draw(pixel, fallTop, null, ProbeDroneProj.ThemeBlood * (0.55f * opacity), MathHelper.PiOver2,
                        lineOrigin, new Vector2(length / pixel.Width, beamWidth * 1.6f / pixel.Height), SpriteEffects.None, 0f);
                    sb.Draw(pixel, fallTop, null, ProbeDroneProj.ThemeAmber * (0.75f * opacity), MathHelper.PiOver2,
                        lineOrigin, new Vector2(length / pixel.Width, beamWidth * 0.8f / pixel.Height), SpriteEffects.None, 0f);
                    sb.Draw(pixel, fallTop, null, ProbeDroneProj.ThemeCore * opacity, MathHelper.PiOver2,
                        lineOrigin, new Vector2(length / pixel.Width, beamWidth * 0.3f / pixel.Height), SpriteEffects.None, 0f);
                }

                //自天向地推进的能量脉冲
                Vector2 top = new Vector2(Projectile.Center.X, Projectile.Center.Y - SkyReach) - Main.screenPosition;
                for (int i = 0; i < 4; i++) {
                    float along = (Main.GlobalTimeWrappedHourly * 1.1f + i / 4f) % 1f;
                    Vector2 pPos = Vector2.Lerp(top, screenGround, along);
                    float pScale = opacity * (0.5f + 0.5f * (float)Math.Sin(along * MathHelper.Pi));
                    sb.Draw(glow, pPos, null, ProbeDroneProj.ThemeAmber * (0.65f * opacity), 0f,
                        glow.Size() / 2f, pScale * 0.42f, SpriteEffects.None, 0f);
                }

                //落点白热核，横向压扁贴地
                sb.Draw(glow, screenGround, null, ProbeDroneProj.ThemeBlood * (0.95f * opacity), 0f,
                    glow.Size() / 2f, new Vector2(2.6f, 1.2f) * opacity * flicker, SpriteEffects.None, 0f);
                sb.Draw(glow, screenGround, null, ProbeDroneProj.ThemeAmber * opacity, 0f,
                    glow.Size() / 2f, new Vector2(1.5f, 0.7f) * opacity, SpriteEffects.None, 0f);
                sb.Draw(glow, screenGround, null, ProbeDroneProj.ThemeCore * (0.9f * opacity), 0f,
                    glow.Size() / 2f, new Vector2(0.85f, 0.45f) * opacity, SpriteEffects.None, 0f);
                sb.Draw(star, screenGround, null, ProbeDroneProj.ThemeAmber * (0.85f * opacity),
                    Main.GlobalTimeWrappedHourly * 3.2f, star.Size() / 2f, opacity * 0.7f * flicker, SpriteEffects.None, 0f);
            }

            //余韵熔斑：塌缩后贴地残光渐冷
            float afterStart = TelegraphTime + ExpandTime + SustainTime + CollapseTime;
            if (t > afterStart) {
                float afterT = MathHelper.Clamp((t - afterStart) / AfterTime, 0f, 1f);
                float glowA = (1f - afterT) * 0.5f;
                if (glowA > 0.02f) {
                    Color molten = Color.Lerp(ProbeDroneProj.ThemeAmber, ProbeDroneProj.ThemeBlood, afterT);
                    sb.Draw(glow, screenGround, null, molten * glowA, 0f, glow.Size() / 2f,
                        new Vector2(2.2f * (1f - afterT * 0.4f), 0.8f * (1f - afterT * 0.5f)), SpriteEffects.None, 0f);
                }
            }
        }

        #endregion

        #region 热浪扭曲

        public bool CanDrawCustom() => false;

        public bool DontUseBlueshiftEffect() => true;

        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>冲击后落点上方的升腾热浪柱：底宽顶窄，随余韵冷却</summary>
        public void Warp() {
            float t = Timer;
            if (t < ImpactBeat) {
                return;
            }
            float afterStart = TelegraphTime + ExpandTime + SustainTime + CollapseTime;
            float rise = MathHelper.Clamp((t - ImpactBeat) / 15f, 0f, 1f);
            float cool = t > afterStart ? 1f - MathHelper.Clamp((t - afterStart) / AfterTime, 0f, 1f) : 1f;
            float intensity = 0.5f * rise * cool;
            if (intensity <= 0.04f) {
                return;
            }

            const float HeatLen = 540f;
            float width = 230f + beamWidth;
            Vector2 center = groundPos - Vector2.UnitY * (HeatLen * 0.5f - 36f);
            //rotation指向上：前端(窄)在顶，尾端(宽)贴地——热自地面升腾
            DestroyerMotionFX.DrawHeatWakeWarp(center, HeatLen, width, -MathHelper.PiOver2,
                intensity, 0.35f + 0.65f * cool);
        }

        #endregion
    }
}

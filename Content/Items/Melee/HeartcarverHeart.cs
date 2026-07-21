using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 剜出心脏，悬浮搏动实体；每搏尖叫+血色脉冲，间隔缩短后吸收开狂热并凝环绕血刃；HeartcarverOrgan.fx SDF
    /// </summary>
    internal class HeartcarverExcisedHeart : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Heartcarver>();

        private Player Owner => Main.player[Projectile.owner];

        //==== 生命阶段 ====
        private const int ExtractTime = 12;
        /// <summary>搏动期结束、开始被刀吸收的时刻</summary>
        private const int AbsorbStart = 118;
        private const int MaxLife = 260;

        //==== 搏动调度（间隔缩短）====
        private const float FirstBeatDelay = 14f;
        private const float FirstInterval = 40f;
        private const float IntervalStep = 8f;
        private const float MinInterval = 24f;

        private ref float Timer => ref Projectile.ai[0];
        /// <summary>距下一次搏动的倒计时</summary>
        private ref float BeatCountdown => ref Projectile.localAI[0];
        /// <summary>当前搏动间隔</summary>
        private ref float BeatInterval => ref Projectile.localAI[1];

        //==== 演出状态（客户端各自演进）====
        private float beatPunch;
        private float mouthOpen;
        private float fade;
        private Vector2 hoverAnchor;
        private bool anchored;
        private bool absorbed;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!anchored) {
                anchored = true;
                hoverAnchor = Projectile.Center + new Vector2(0f, -44f);
                BeatCountdown = FirstBeatDelay;
                BeatInterval = FirstInterval;
            }

            if (Timer < ExtractTime) {
                //剜出瞬间上飘
                float t = Timer / ExtractTime;
                fade = t;
                Projectile.velocity *= 0.86f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, hoverAnchor, 0.10f);
            }
            else if (Timer < AbsorbStart) {
                //悬浮搏动期
                fade = 1f;
                Projectile.velocity = Vector2.Zero;
                float bob = MathF.Sin(Timer * 0.07f) * 5f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, hoverAnchor + new Vector2(0f, bob), 0.12f);

                BeatCountdown--;
                if (BeatCountdown <= 0f) {
                    DoBeat();
                    BeatInterval = MathF.Max(MinInterval, BeatInterval - IntervalStep);
                    BeatCountdown = BeatInterval;
                }

                //持续滴血
                if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 12f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.5f, 1.4f)),
                        HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.5f))
                        ?.Configure(Main.rand.Next(26, 40));
                }
            }
            else {
                //吸收期飞向持刀者
                float t = MathHelper.Clamp((Timer - AbsorbStart) / 26f, 0f, 1f);
                fade = 1f - t * 0.55f;
                float pull = 0.08f + MathF.Pow(t, 3f) * 0.42f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.GetPlayerStabilityCenter(), pull);
                Projectile.scale = MathHelper.Lerp(1f, 0.35f, t);

                //吸入尾流
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 toOwner = (Owner.GetPlayerStabilityCenter() - Projectile.Center).SafeNormalize(Vector2.Zero);
                    PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                        toOwner * Main.rand.NextFloat(4f, 9f),
                        HeartcarverPalette.Heat(Main.rand.NextFloat(0.3f)), Main.rand.NextFloat(1f, 1.7f))
                        ?.Configure(false, Main.rand.Next(8, 14));
                }

                if (Projectile.Distance(Owner.GetPlayerStabilityCenter()) < 26f) {
                    CompleteAbsorb();
                    return;
                }
            }

            //演出包络衰减
            beatPunch *= 0.86f;
            mouthOpen *= 0.90f;

            Lighting.AddLight(Projectile.Center, HeartcarverPalette.Arterial.ToVector3() * (0.5f + beatPunch * 0.8f) * fade);
            Timer++;
        }

        /// <summary>一次搏动，尖叫+脉冲伤害+溅血</summary>
        private void DoBeat() {
            beatPunch = 1f;
            mouthOpen = 1f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<HeartcarverPulseProj>(), (int)(Projectile.damage * 0.5f),
                    2f, Projectile.owner);
            }

            if (VaultUtils.isServer) {
                return;
            }

            //心音重锤 + 嘴的尖叫
            SoundEngine.PlaySound(SoundID.DrumKick with { Pitch = -1f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.9f, Volume = 0.40f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.4f, Volume = 0.45f }, Projectile.Center);

            //搏动喷血
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f);
                vel.Y -= 1.5f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, vel,
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.9f))
                    ?.Configure(Main.rand.Next(24, 40));
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new(Projectile.Center, Main.rand.NextVector2Unit(), 3f, 4f, 8, 700f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        /// <summary>吸收完成，开狂热+凝环绕血刃</summary>
        private void CompleteAbsorb() {
            absorbed = true;

            if (Owner.TryGetModPlayer(out HeartcarverPlayer hcPlayer)) {
                hcPlayer.NotifyAbsorb();
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                int daggerType = ModContent.ProjectileType<HeartcarverDagger>();
                if (Owner.ownedProjectileCounts[daggerType] < 3) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        daggerType, (int)(Projectile.damage * 0.45f), Projectile.knockBack,
                        Projectile.owner, ai0: Owner.ownedProjectileCounts[daggerType]);
                }
            }

            if (!VaultUtils.isServer) {
                //吸收完成音+收束粒子
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.3f, Volume = 0.6f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.1f, Volume = 0.5f }, Owner.Center);

                for (int i = 0; i < 14; i++) {
                    float ang = MathHelper.TwoPi * i / 14f;
                    PRTLoader.NewParticle<PRT_SparkAlpha>(Owner.GetPlayerStabilityCenter(),
                        ang.ToRotationVector2() * Main.rand.NextFloat(2f, 6f),
                        HeartcarverPalette.Heat(Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(1f, 1.8f))
                        ?.Configure(false, Main.rand.Next(10, 18));
                }
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Owner.GetPlayerStabilityCenter(), Vector2.Zero,
                    HeartcarverPalette.Arterial, 1f)?.Configure(0.12f, 0.55f, 16);
            }

            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (absorbed || VaultUtils.isServer) {
                return;
            }
            //非吸收死亡，血雾散尽
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    Main.rand.NextVector2Circular(4f, 4f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.8f))
                    ?.Configure(Main.rand.Next(20, 34));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //心脏底光
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float pulse = 0.9f + beatPunch * 0.8f;
            Color c = HeartcarverPalette.Arterial with { A = 0 } * (0.55f * fade);
            Main.EntitySpriteDraw(glow, pos, null, c, 0f, glow.Size() / 2f, Projectile.scale * pulse * 2.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, Color.White with { A = 0 } * (0.18f * fade * beatPunch), 0f,
                glow.Size() / 2f, Projectile.scale * pulse * 1.2f, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = HeartcarverAssets.HeartcarverOrgan?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null || fade <= 0.02f) {
                return;
            }

            Vector2 c = Projectile.Center;
            float half = 56f * Projectile.scale;

            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((c + new Vector2(-half, -half)).ToVector3(), Color.White, new Vector2(0f, 0f));
            quad[1] = new VertexPositionColorTexture((c + new Vector2(half, -half)).ToVector3(), Color.White, new Vector2(1f, 0f));
            quad[2] = new VertexPositionColorTexture((c + new Vector2(-half, half)).ToVector3(), Color.White, new Vector2(0f, 1f));
            quad[3] = new VertexPositionColorTexture((c + new Vector2(half, half)).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.313f % 1f);
            effect.Parameters["uBeat"]?.SetValue(beatPunch);
            effect.Parameters["uMouth"]?.SetValue(mouthOpen);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 搏动脉冲环伤，同圈每目标一次
    /// </summary>
    internal class HeartcarverPulseProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Heartcarver>();

        private const int RingLife = 20;
        private const float MaxRadius = 176f;
        /// <summary>判定环带厚度的一半</summary>
        private const float BandHalf = 22f;

        private ref float Timer => ref Projectile.ai[0];

        private float CurrentRadius => MaxRadius * (1f - MathF.Pow(1f - MathHelper.Clamp(Timer / RingLife, 0f, 1f), 2.6f));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = RingLife;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().HitAttribute.WormResistance = 0.4f;
        }

        public override void AI() {
            if (Timer == 0f && !VaultUtils.isServer) {
                //视觉环与碰撞环同步扩张（DiffusionCircle 素材半径约 180px）
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                    HeartcarverPalette.Arterial, 1f)?.Configure(0.05f, MaxRadius / 165f, RingLife);
                PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                    HeartcarverPalette.Myocard * 0.6f, 1f)?.Configure(0.03f, MaxRadius / 210f, RingLife - 4);
            }
            Projectile.velocity = Vector2.Zero;
            Timer++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //环带判定
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            float dist = Vector2.Distance(Projectile.Center, nearest);
            return MathF.Abs(dist - CurrentRadius) < BandHalf;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.Center.X > Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 240);
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 outward = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                    outward.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(3f, 8f),
                    HeartcarverPalette.Blood(Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.7f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}

using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>神源之刃HeldProj，三段连击挥砍，末段大斩切+巨型新月剑气</summary>
    internal class DivineSourceBladeHeld : BaseHeldProj
    {
        public override string Texture => DivineSourceBladeFX.BladeTexture;

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;

        private static readonly Vector2 GripPixel = DivineSourceBladeFX.GripPixel;
        private static readonly Vector2 TipPixel = DivineSourceBladeFX.TipPixel;
        private const float HeldScale = 0.95f;
        private static float BaseReach => (TipPixel - GripPixel).Length() * HeldScale;

        private static readonly Color LeadColor = new(255, 245, 205);
        private static readonly Color GoldColor = new(255, 200, 80);
        private static readonly Color AmberColor = new(250, 140, 35);
        private static readonly Color TailColor = new(150, 70, 15);
        private static readonly Color OutlineGold = new(255, 225, 140);
        private static readonly Color EnergyGold = new(255, 200, 90);
        private static readonly Color FlashWhite = new(255, 248, 220);

        //阶段时长与挥砍几何参数，InitStage 按连击段位写入
        private int raiseDur = 6;
        private int holdDur = 2;
        private int slashDur = 6;
        private int recoverDur = 7;
        private int totalDur = 21;
        private float raiseBack = 2.2f;
        private float follow = 1.25f;
        private float reachScale = 1f;
        private float slashEasePow = 2.6f;
        private int fanSegments = 42;

        private float baseAngle;
        private float swingDir;
        private float mainAngle;
        private float mainReach;
        private Vector2 mainTip;
        private float slashProgress;
        private float sweepT;
        private float fanFade = 1f;
        private int flashTimer;
        private bool waveSpawned;
        private bool slashSoundPlayed;
        //大斩切命中时的顿帧（冻结姿态数帧，强化打击感）
        private int hitstopTimer;
        private bool hitstopApplied;

        /// <summary>连击段位，0快斩/1回斩/2大斩</summary>
        private int ComboStage => (int)Projectile.ai[0];
        private bool IsHeavy => ComboStage >= 2;

        private int Timer {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int CurrentPhase {
            get {
                if (Timer <= raiseDur) return PhaseRaise;
                if (Timer <= raiseDur + holdDur) return PhaseHold;
                if (Timer <= raiseDur + holdDur + slashDur) return PhaseSlash;
                return PhaseRecover;
            }
        }

        private float FullReach => BaseReach * reachScale;
        private float TotalSweep => raiseBack + follow;
        private float ArcStart => baseAngle - swingDir * raiseBack;
        private float ArcEnd => baseAngle + swingDir * follow;

        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DivineSourceBlade>();

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
        }

        private void InitStage(Player owner) {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            float facing = MathF.Abs(cos) < 0.05f ? owner.direction : MathF.Sign(cos);

            if (IsHeavy) {
                raiseDur = 12;
                holdDur = 4;
                slashDur = 7;
                recoverDur = 14;
                raiseBack = 2.7f;
                follow = 1.45f;
                reachScale = 1.18f;
                slashEasePow = 4.2f;
                fanSegments = 54;
                swingDir = facing;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.7f, Volume = 0.6f }, owner.Center);
            }
            else {
                raiseDur = 6;
                holdDur = 2;
                slashDur = 6;
                recoverDur = 7;
                raiseBack = 2.2f;
                follow = 1.25f;
                reachScale = 1f;
                slashEasePow = 2.6f;
                fanSegments = 42;
                //第二段反向回斩，形成左右交替的连击观感
                swingDir = ComboStage == 1 ? -facing : facing;
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Pitch = 0.15f + ComboStage * 0.18f,
                    Volume = 0.5f
                }, owner.Center);
            }

            totalDur = raiseDur + holdDur + slashDur + recoverDur;
            Projectile.velocity = Vector2.Zero;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Timer == 0) {
                InitStage(Owner);
            }

            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                Timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            UpdateBladeTransform(Owner, phase);

            Owner.ChangeDir(MathF.Cos(baseAngle) >= 0 ? 1 : -1);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;

            Player.CompositeArmStretchAmount stretch = phase switch {
                PhaseRaise => Player.CompositeArmStretchAmount.ThreeQuarters,
                PhaseRecover => Player.CompositeArmStretchAmount.ThreeQuarters,
                _ => Player.CompositeArmStretchAmount.Full,
            };
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters,
                mainAngle - MathHelper.PiOver2 + swingDir * 0.25f);

            Projectile.Center = Vector2.Lerp(Owner.GetPlayerStabilityCenter(), mainTip, 0.6f);
            Projectile.rotation = mainAngle;

            HandlePhaseEvents(Owner, phase);
            HandleParticles(Owner, phase);
            HandleLight(phase);

            if (Timer >= totalDur) {
                Projectile.Kill();
            }
        }

        private void UpdateBladeTransform(Player owner, int phase) {
            float arcStart = ArcStart;
            float heldAngle = arcStart - swingDir * 0.07f;

            switch (phase) {
                case PhaseRaise: {
                    float p = Timer / (float)raiseDur;
                    float eased = EaseOutCubic(p);
                    float liftFrom = arcStart + swingDir * (raiseBack * 0.75f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.50f, 0.92f, eased);
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (Timer - raiseDur) / (float)holdDur;
                    float eased = EaseOutQuad(p);
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, eased);
                    if (IsHeavy) {
                        //蓄力时剑身微颤，强化张力
                        mainAngle += swingDir * 0.018f * MathF.Sin(Timer * 1.7f);
                    }
                    mainReach = FullReach * MathHelper.Lerp(0.92f, 0.97f, eased);
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (Timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    float eased = 1f - MathF.Pow(1f - p, slashEasePow);
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, eased);
                    float reachT = MathF.Sin(p * MathHelper.Pi);
                    mainReach = FullReach * MathHelper.Lerp(0.97f, 1.0f, reachT);
                    sweepT = MathHelper.Clamp(MathF.Abs((mainAngle - arcStart) / TotalSweep), 0f, 1f);
                    break;
                }
                default: {
                    float q = (Timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 1.8f));
                    mainAngle = ArcEnd + swingDir * 0.20f * settle;
                    mainReach = FullReach * MathHelper.Lerp(0.97f, 0.78f, EaseInQuad(q));
                    slashProgress = 1f;
                    sweepT = 1f;
                    float fadeDur = MathF.Max(6f, recoverDur * 0.7f);
                    fanFade = MathHelper.Clamp(1f - (Timer - raiseDur - holdDur - slashDur) / fadeDur, 0f, 1f);
                    break;
                }
            }

            mainTip = owner.Center + mainAngle.ToRotationVector2() * mainReach;
        }

        private void HandlePhaseEvents(Player owner, int phase) {
            //大斩切蓄力完成的瞬间
            if (IsHeavy && Timer == raiseDur + 1) {
                flashTimer = 12;
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                if (IsHeavy) {
                    flashTimer = 10;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 1.2f }, owner.Center);
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.45f, Volume = 0.85f }, owner.Center);
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Pitch = 0.1f + ComboStage * 0.18f,
                        Volume = 0.9f
                    }, owner.Center);
                }

                if (!Main.dedServ && CWRClientConfig.Instance.ScreenVibration) {
                    Vector2 punchDir = (baseAngle + swingDir * MathHelper.PiOver2).ToRotationVector2();
                    float strength = IsHeavy ? 9f : 3.5f;
                    int frames = IsHeavy ? 12 : 6;
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        owner.Center, punchDir, strength, 8f, frames, 1100f, FullName));
                }
            }

            if (!waveSpawned && phase == PhaseSlash && slashProgress >= 0.9f) {
                waveSpawned = true;
                Vector2 dir = baseAngle.ToRotationVector2();

                if (IsHeavy) {
                    SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.5f, Volume = 1.2f }, owner.Center);
                    SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f, Volume = 0.9f }, owner.Center);

                    if (!Main.dedServ && CWRClientConfig.Instance.ScreenVibration) {
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                            owner.Center, dir, 10f, 7f, 13, 1300f, FullName));
                    }
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item73 with { Pitch = 0.25f, Volume = 0.6f }, owner.Center);
                }

                if (Projectile.owner == Main.myPlayer) {
                    float waveScale = IsHeavy ? 1.85f : 0.95f;
                    float dmgMul = IsHeavy ? 2.4f : 0.85f;
                    float speed = IsHeavy ? 23f : 16f;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        owner.Center + dir * 46f,
                        dir * speed,
                        ModContent.ProjectileType<DivineSourceWaveProjectile>(),
                        (int)(Projectile.damage * dmgMul),
                        Projectile.knockBack * (IsHeavy ? 1.5f : 1f),
                        owner.whoAmI,
                        ai0: waveScale,
                        ai1: swingDir);
                }
            }
        }

        private void HandleParticles(Player owner, int phase) {
            if (Main.dedServ) {
                return;
            }

            switch (phase) {
                case PhaseRaise:
                case PhaseHold: {
                    if (!IsHeavy) {
                        //快斩起手极短，只点缀少量金尘
                        if (Main.rand.NextBool(2)) {
                            Vector2 at = Vector2.Lerp(owner.Center, mainTip, Main.rand.NextFloat(0.4f, 1f));
                            Dust dust = Dust.NewDustPerfect(at, DustID.GoldFlame);
                            dust.velocity = new Vector2(0, -Main.rand.NextFloat(0.4f, 1.2f));
                            dust.scale = Main.rand.NextFloat(0.6f, 1.0f);
                            dust.noGravity = true;
                        }
                        break;
                    }

                    float chargeT = phase == PhaseHold ? 1f : Timer / (float)raiseDur;
                    int count = phase == PhaseHold ? 4 : (Main.rand.NextBool(2) ? 2 : 1);
                    for (int i = 0; i < count; i++) {
                        float t = Main.rand.NextFloat(0.25f, 1.0f);
                        Vector2 at = Vector2.Lerp(owner.Center, mainTip, t);
                        Vector2 perp = (mainAngle + MathHelper.PiOver2).ToRotationVector2();
                        Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(5f, 5f), DustID.GoldFlame);
                        dust.velocity = perp * Main.rand.NextFloat(-1.2f, 1.2f) + new Vector2(0, -Main.rand.NextFloat(0.4f, 1.4f));
                        dust.scale = Main.rand.NextFloat(0.7f, 1.25f) * (0.5f + chargeT * 0.6f);
                        dust.noGravity = true;
                    }

                    //能量向剑身汇聚，营造蓄力吸入感
                    if (Main.rand.NextBool(2)) {
                        Vector2 anchor = Vector2.Lerp(owner.Center, mainTip, Main.rand.NextFloat(0.45f, 0.95f));
                        Vector2 offset = Main.rand.NextVector2CircularEdge(70f, 70f);
                        Dust dust = Dust.NewDustPerfect(anchor + offset, DustID.GoldCoin);
                        dust.velocity = -offset * 0.09f;
                        dust.scale = Main.rand.NextFloat(0.6f, 1.0f) * chargeT;
                        dust.noGravity = true;
                    }
                    break;
                }
                case PhaseSlash: {
                    Vector2 sweepVel = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2();
                    int count = IsHeavy ? 4 : 2;
                    for (int i = 0; i < count; i++) {
                        Vector2 at = Vector2.Lerp(owner.Center, mainTip, Main.rand.NextFloat(0.45f, 1.0f));
                        Dust dust = Dust.NewDustPerfect(at, DustID.GoldFlame);
                        dust.velocity = sweepVel * Main.rand.NextFloat(3f, 8f) + Main.rand.NextVector2Circular(1f, 1f);
                        dust.scale = Main.rand.NextFloat(0.9f, 1.5f) * (IsHeavy ? 1.15f : 1f);
                        dust.noGravity = true;
                    }
                    if (Main.rand.NextBool(2)) {
                        Dust dust = Dust.NewDustPerfect(mainTip, DustID.Torch);
                        dust.velocity = sweepVel * Main.rand.NextFloat(4f, 9f);
                        dust.scale = Main.rand.NextFloat(1.2f, 1.8f);
                        dust.noGravity = true;
                        dust.fadeIn = 1.1f;
                    }
                    break;
                }
                default: {
                    if (Main.rand.NextBool(5)) {
                        Vector2 at = Vector2.Lerp(owner.Center, mainTip, Main.rand.NextFloat(0.5f, 1f));
                        Dust dust = Dust.NewDustPerfect(at, DustID.GoldFlame);
                        dust.velocity = new Vector2(0, -Main.rand.NextFloat(0.3f, 1f));
                        dust.scale = Main.rand.NextFloat(0.5f, 0.9f) * fanFade;
                        dust.noGravity = true;
                    }
                    break;
                }
            }
        }

        private void HandleLight(int phase) {
            float flash = flashTimer / 12f;
            float heavyMul = IsHeavy ? 1.25f : 1f;
            switch (phase) {
                case PhaseRaise: {
                    float p = Timer / (float)raiseDur;
                    Lighting.AddLight(mainTip, new Vector3(0.7f, 0.55f, 0.22f) * (0.3f + p * 0.5f) * heavyMul);
                    break;
                }
                case PhaseHold:
                    Lighting.AddLight(mainTip, new Vector3(0.95f, 0.8f, 0.4f) * (0.8f + flash * 0.6f) * heavyMul);
                    break;
                case PhaseSlash:
                    Lighting.AddLight(Vector2.Lerp(Main.player[Projectile.owner].Center, mainTip, 0.6f),
                        new Vector3(1.0f, 0.78f, 0.32f) * heavyMul);
                    break;
                default:
                    Lighting.AddLight(mainTip, new Vector3(0.6f, 0.46f, 0.2f) * fanFade * heavyMul);
                    break;
            }
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInQuad(float t) => t * t;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CurrentPhase != PhaseSlash) {
                return false;
            }

            Player owner = Main.player[Projectile.owner];
            float collisionPoint = 0f;
            float width = IsHeavy ? 56f : 44f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(),
                owner.Center, mainTip, width, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //快斩轻、回斩轻、大斩切重，强化三段落差
            modifiers.SourceDamage *= IsHeavy ? 1.8f : 0.8f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //大斩切命中瞬间顿帧，并补一记震屏
            if (IsHeavy && CurrentPhase == PhaseSlash && !hitstopApplied) {
                hitstopApplied = true;
                hitstopTimer = 4;
                if (!Main.dedServ && CWRClientConfig.Instance.ScreenVibration) {
                    Vector2 dir = (target.Center - Main.player[Projectile.owner].Center).SafeNormalize(Vector2.UnitX);
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        target.Center, dir, 5f, 9f, 7, 900f, FullName));
                }
            }

            SoundEngine.PlaySound(SoundID.Item71 with {
                Pitch = IsHeavy ? 0.0f : 0.35f,
                Volume = IsHeavy ? 0.8f : 0.55f
            }, target.Center);
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    target.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<DivineSourceHitFXProjectile>(),
                    0, 0f, Projectile.owner,
                    ai0: IsHeavy ? 1f : 0.5f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Player owner = Main.player[Projectile.owner];
            DrawArcFan(sb, owner);
            DrawBlade(sb, owner);
            return false;
        }

        private void DrawArcFan(SpriteBatch sb, Player owner) {
            if (sweepT <= 0.03f || fanFade <= 0.02f) {
                return;
            }

            Effect effect = DivineSourceBladeFX.Arc;
            if (effect == null) {
                DrawArcFallback(sb, owner);
                return;
            }

            int segs = Math.Max(8, (int)(fanSegments * sweepT) + 2);
            var verts = new ColoredVertex[segs * 2];
            var inds = new short[(segs - 1) * 6];

            float outerR = mainReach * 1.04f;
            float innerR = mainReach * 0.30f;
            Vector2 center = owner.Center;
            float arcStart = ArcStart + swingDir * 0.3f;//加上 swingDir * 0.3f 让这个刀光和刀背衔接起来

            for (int i = 0; i < segs; i++) {
                float t = i / (float)(segs - 1);
                float u = t * sweepT;
                float ang = arcStart + swingDir * TotalSweep * u;
                Vector2 dir = ang.ToRotationVector2();
                float bulge = 1f + 0.05f * MathF.Pow(t, 3f);
                Vector2 outer = center + dir * (outerR * bulge) - Main.screenPosition;
                Vector2 inner = center + dir * innerR - Main.screenPosition;
                verts[i * 2] = new ColoredVertex(outer, Color.White, new Vector3(u, 0f, 0f));
                verts[i * 2 + 1] = new ColoredVertex(inner, Color.White, new Vector3(u, 1f, 0f));
            }
            for (int i = 0; i < segs - 1; i++) {
                int vi = i * 2;
                int ii = i * 6;
                inds[ii] = (short)vi;
                inds[ii + 1] = (short)(vi + 1);
                inds[ii + 2] = (short)(vi + 2);
                inds[ii + 3] = (short)(vi + 2);
                inds[ii + 4] = (short)(vi + 1);
                inds[ii + 5] = (short)(vi + 3);
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            sb.End();

            BlendState prevBlend = device.BlendState;
            SamplerState prevSampler = device.SamplerStates[0];
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;

            device.BlendState = BlendState.AlphaBlend;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["SweepT"]?.SetValue(sweepT);
            effect.Parameters["FadeOut"]?.SetValue(fanFade);
            effect.Parameters["HeatBoost"]?.SetValue((IsHeavy ? 1.3f : 1.1f) + slashProgress * (IsHeavy ? 0.7f : 0.45f));
            effect.Parameters["RimIntensity"]?.SetValue(IsHeavy ? 1.45f : 1.15f);
            effect.Parameters["LeadColor"]?.SetValue(LeadColor.ToVector4());
            effect.Parameters["GoldColor"]?.SetValue(GoldColor.ToVector4());
            effect.Parameters["AmberColor"]?.SetValue(AmberColor.ToVector4());
            effect.Parameters["TailColor"]?.SetValue(TailColor.ToVector4());
            Texture2D noise = DivineSourceBladeFX.Noise;
            if (noise != null) {
                effect.Parameters["NoiseTexture"]?.SetValue(noise);
            }

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Trail.DrawUserPrimitives(verts, inds, device);
            }

            device.BlendState = prevBlend;
            device.SamplerStates[0] = prevSampler;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawArcFallback(SpriteBatch sb, Player owner) {
            Texture2D wave = DivineSourceBladeFX.WaveFallback;
            if (wave == null) {
                return;
            }

            float alpha = fanFade * (0.35f + slashProgress * 0.45f);
            Vector2 arcCenter = owner.Center + mainAngle.ToRotationVector2() * (mainReach * 0.6f);
            Color c = GoldColor * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c,
                mainAngle + swingDir * 0.35f, wave.Size() / 2f, new Vector2(0.5f, 0.22f), SpriteEffects.None, 0f);
            Color c2 = LeadColor * alpha * 0.7f;
            c2.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c2,
                mainAngle + swingDir * 0.35f, wave.Size() / 2f, new Vector2(0.45f, 0.10f), SpriteEffects.None, 0f);
        }

        private void ComputeBladeDrawXform(Player owner, Texture2D tex, float angle,
            out Vector2 origin, out float bladeRot, out SpriteEffects flip) {
            bool facingLeft = owner.direction == -1;
            flip = facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            origin = facingLeft
                ? new Vector2(tex.Width - GripPixel.X, GripPixel.Y)
                : GripPixel;

            Vector2 tipVec = TipPixel - GripPixel;
            if (facingLeft) {
                tipVec.X *= -1f;
            }
            bladeRot = angle - tipVec.ToRotation();
        }

        private void DrawBlade(SpriteBatch sb, Player owner) {
            Asset<Texture2D> asset = TextureAssets.Projectile[Projectile.type];
            if (asset == null || !asset.IsLoaded) {
                return;
            }
            Texture2D tex = asset.Value;

            int phase = CurrentPhase;
            float flash = flashTimer / 12f;

            float glowStrength = phase switch {
                PhaseRaise => IsHeavy
                    ? 0.35f + 0.6f * (Timer / (float)raiseDur)
                    : 0.55f,
                PhaseHold => IsHeavy
                    ? 1.0f + 0.1f * MathF.Sin(Timer * 0.6f)
                    : 0.7f,
                PhaseSlash => IsHeavy ? 1.15f : 0.95f,
                _ => MathHelper.Lerp(0.25f, 0.9f, fanFade),
            };

            Effect effect = DivineSourceBladeFX.BladeGlow;
            Vector2 handPos = owner.Center;
            Color light = Lighting.GetColor((int)(handPos.X / 16), (int)(handPos.Y / 16));
            Color bladeCol = Color.Lerp(light, new Color(255, 238, 200), 0.35f + flash * 0.5f);

            if (effect != null) {
                effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
                effect.Parameters["GlowStrength"]?.SetValue(glowStrength);
                effect.Parameters["FlashBoost"]?.SetValue(flash * flash);
                effect.Parameters["TexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                effect.Parameters["BladeDir"]?.SetValue(Vector2.Normalize(TipPixel - GripPixel));
                effect.Parameters["OutlineColor"]?.SetValue(OutlineGold.ToVector4());
                effect.Parameters["EnergyColor"]?.SetValue(EnergyGold.ToVector4());
                effect.Parameters["FlashColor"]?.SetValue(FlashWhite.ToVector4());
                Texture2D noise = DivineSourceBladeFX.Noise;
                if (noise != null) {
                    effect.Parameters["NoiseTexture"]?.SetValue(noise);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

                DrawBladeInstances(sb, owner, tex, phase, handPos, bladeCol);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                DrawBladeInstances(sb, owner, tex, phase, handPos, bladeCol);
                if (flash > 0.05f) {
                    ComputeBladeDrawXform(owner, tex, mainAngle, out Vector2 o, out float r, out SpriteEffects f);
                    Color silhouette = FlashWhite * (flash * 0.8f);
                    silhouette.A = 0;
                    sb.Draw(tex, handPos - Main.screenPosition, null, silhouette, r, o, HeldScale, f, 0f);
                }
            }
        }

        private void DrawBladeInstances(SpriteBatch sb, Player owner, Texture2D tex, int phase,
            Vector2 handPos, Color bladeCol) {

            if (phase == PhaseSlash && slashProgress > 0.1f) {
                int ghostCount = IsHeavy ? 3 : 2;
                float ghostSpacing = IsHeavy ? 0.24f : 0.19f;
                for (int g = ghostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - swingDir * ghostSpacing * g;
                    ComputeBladeDrawXform(owner, tex, ghostAngle, out Vector2 gOrigin, out float gRot, out SpriteEffects gFlip);
                    float ghostAlpha = g switch { 1 => 0.40f, 2 => 0.18f, _ => 0.08f };
                    sb.Draw(tex, handPos - Main.screenPosition, null, bladeCol * ghostAlpha,
                        gRot, gOrigin, HeldScale, gFlip, 0f);
                }
            }

            ComputeBladeDrawXform(owner, tex, mainAngle, out Vector2 origin, out float rot, out SpriteEffects flip);
            sb.Draw(tex, handPos - Main.screenPosition, null, bladeCol, rot, origin, HeldScale, flip, 0f);
        }
    }
}

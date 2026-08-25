using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Everdeeps
{
    /// <summary>
    /// 永渊水环:环形深渊水流。出射复合加速带微幅蛇摆;
    /// 穿透敌人后余势滑行一小段,减速中掰出折返弧,再度加速扑回目标补击;
    /// 折返段充能发亮且无视地形。空放飞满射程或撞地则散成水。<br/>
    /// ai0=状态 ai1=种子 ai2=折返目标 whoAmI+1(0=无)
    /// </summary>
    internal class EverdeepRing : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private enum RingState
        {
            /// <summary>出射:复合加速+蛇摆</summary>
            Outbound,
            /// <summary>穿透余势:水劲被身体泄掉,滑行一小段</summary>
            Coast,
            /// <summary>折返弧:减速中把速度掰向目标</summary>
            Brake,
            /// <summary>折返:充能加速扑回目标</summary>
            Return,
            /// <summary>散成水:判定关闭,余韵交给液滴</summary>
            Dissolve,
        }

        /// <summary>可见环带外缘 ≈ quad半宽 × 0.73(shader 画布契约)</summary>
        private const float QuadBasePx = 128f;
        private const int CoastTime = 15;
        private const int BrakeTime = 20;
        private const int DissolveTime = 14;
        /// <summary>空放飞行帧数,飞满散水</summary>
        private const int OutboundLife = 78;

        private RingState State {
            get => (RingState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        private ref float Seed => ref Projectile.ai[1];
        /// <summary>折返目标 whoAmI+1,0=无</summary>
        private ref float ReturnTargetIndex => ref Projectile.ai[2];
        private ref float StateTimer => ref Projectile.localAI[0];

        /// <summary>循环角积分,随速度增长(纯视觉)</summary>
        private float spin;
        /// <summary>折返充能度 0~1(纯视觉)</summary>
        private float charge;
        /// <summary>命中白闪帧(纯视觉)</summary>
        private int flashTimer;
        /// <summary>折返段是否已贴近过目标,贴近后再远离即散水</summary>
        private bool reachedTarget;

        private readonly List<Vector2> wakeTrail = new();
        private const int MaxWakeTrail = 14;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 260;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
        }

        public override void SetStaticDefaults() {
            //quad 宽出命中盒,离屏余量防近屏瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 220;
        }

        public override void AI() {
            float speed = Projectile.velocity.Length();
            spin += 0.045f + speed * 0.0135f;
            StateTimer++;

            //出生成形:前 8 帧从小胀到微过冲,再落回 1
            if (Projectile.scale < 1f && State == RingState.Outbound && StateTimer <= 9) {
                Projectile.scale = MathHelper.Lerp(0.45f, 1.06f, MathHelper.Clamp(StateTimer / 8f, 0f, 1f));
            }
            else if (Projectile.scale > 1f && State != RingState.Dissolve) {
                Projectile.scale = MathF.Max(Projectile.scale - 0.015f, 1f);
            }

            switch (State) {
                case RingState.Outbound:
                    //复合加速:出手后水压持续挤,不给匀速直线
                    if (speed < 18f) {
                        Projectile.velocity *= 1.014f;
                    }
                    //微幅蛇摆,水流的时域签名
                    Projectile.velocity = Projectile.velocity.RotatedBy(
                        MathF.Sin(StateTimer * 0.33f + Seed * 9f) * 0.012f);
                    if (StateTimer >= OutboundLife) {
                        BeginDissolve();
                    }
                    break;

                case RingState.Coast:
                    Projectile.velocity *= 0.965f;
                    if (StateTimer >= CoastTime) {
                        State = RingState.Brake;
                        StateTimer = 0;
                    }
                    break;

                case RingState.Brake: {
                    Projectile.velocity *= 0.93f;
                    Vector2 toTarget = ReturnTargetPos() - Projectile.Center;
                    float turnRate = MathHelper.Lerp(0.05f, 0.22f, StateTimer / BrakeTime);
                    float newAngle = Projectile.velocity.ToRotation()
                        .AngleTowards(toTarget.ToRotation(), turnRate);
                    Projectile.velocity = newAngle.ToRotationVector2() * MathF.Max(speed, 2.8f);
                    if (StateTimer >= BrakeTime) {
                        State = RingState.Return;
                        StateTimer = 0;
                        Projectile.tileCollide = false;//折返的水无孔不入
                        ReturnBeat();
                    }
                    break;
                }

                case RingState.Return: {
                    Vector2 toTarget = ReturnTargetPos() - Projectile.Center;
                    float dist = toTarget.Length();
                    float retSpeed = MathF.Min(speed + 0.55f, 19f);
                    float newAngle = Projectile.velocity.ToRotation()
                        .AngleTowards(toTarget.ToRotation(), 0.15f);
                    Projectile.velocity = newAngle.ToRotationVector2() * retSpeed;

                    if (dist < 52f) {
                        reachedTarget = true;
                    }
                    //贴近过目标又甩远,或折返超时:散水
                    if ((reachedTarget && dist > 150f) || StateTimer >= 70) {
                        BeginDissolve();
                    }
                    break;
                }

                case RingState.Dissolve:
                    Projectile.velocity *= 0.86f;
                    Projectile.friendly = false;
                    Projectile.scale += 0.012f;//散水时环体微涨,随 fade 化开
                    if (StateTimer >= DissolveTime) {
                        Projectile.Kill();
                    }
                    break;
            }

            charge = MathHelper.Clamp(charge + (State == RingState.Return ? 0.12f : -0.05f), 0f, 1f);
            if (flashTimer > 0) {
                flashTimer--;
            }

            UpdateWakeTrail();
            FlightFX(speed);
        }

        /// <summary>折返目标位置:目标失效时先找近处敌人,再不然回主人</summary>
        private Vector2 ReturnTargetPos() {
            int who = (int)ReturnTargetIndex - 1;
            if (who >= 0 && who < Main.maxNPCs) {
                NPC npc = Main.npc[who];
                if (npc.active && npc.CanBeChasedBy(Projectile)) {
                    return npc.Center + npc.velocity * 5f;
                }
            }
            //目标没了:主人端重选 500px 内最近可追目标并同步
            if (Projectile.IsOwnedByLocalPlayer()) {
                int best = -1;
                float bestDist = 500f * 500f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                    if (d < bestDist) {
                        bestDist = d;
                        best = npc.whoAmI;
                    }
                }
                if (best >= 0) {
                    ReturnTargetIndex = best + 1;
                    Projectile.netUpdate = true;
                    return Main.npc[best].Center;
                }
            }
            return Main.player[Projectile.owner].Center;
        }

        /// <summary>折返拍:调头完成的一声低啸+一圈水痕</summary>
        private void ReturnBeat() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item84 with {
                Volume = 0.35f,
                Pitch = -0.3f,
                MaxInstances = 3,
            }, Projectile.Center);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_OceanCurrentWake>(Projectile.Center, Vector2.Zero
                , EverdeepVFX.AbyssGlow, 0.055f)
                ?.Configure(dir, new Vector2(1f, 0.6f), 0.4f, Main.rand.Next(10, 15));
            for (int i = 0; i < 5; i++) {
                EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f)
                    , dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4f), 0.8f);
            }
        }

        private void BeginDissolve() {
            if (State == RingState.Dissolve) {
                return;
            }
            State = RingState.Dissolve;
            StateTimer = 0;
            Projectile.netUpdate = true;

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.15f }, Projectile.Center);
            //环体散成一圈水:沿环带均匀甩滴,余韵活得比弹体久
            float ringR = QuadBasePx * 0.5f * 0.62f * Projectile.scale;
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Seed * 6f;
                Vector2 rim = ang.ToRotationVector2();
                EverdeepVFX.ShedDroplet(Projectile.Center + rim * ringR * Main.rand.NextFloat(0.7f, 1f)
                    , rim * Main.rand.NextFloat(1.2f, 3.2f) + Projectile.velocity * 0.3f
                    , Main.rand.NextFloat(0.7f, 1.1f));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(
                    Projectile.Center + Main.rand.NextVector2Circular(ringR, ringR)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                    , EverdeepVFX.AbyssFoam, Main.rand.NextFloat(0.05f, 0.10f))
                    ?.Configure(Main.rand.Next(24, 40), 0.035f);
            }
        }

        private void UpdateWakeTrail() {
            wakeTrail.Insert(0, Projectile.Center);
            if (wakeTrail.Count > MaxWakeTrail) {
                wakeTrail.RemoveAt(wakeTrail.Count - 1);
            }
        }

        /// <summary>飞行途中:环缘甩滴,速度越快甩得越密;折返段带青辉</summary>
        private void FlightFX(float speed) {
            if (VaultUtils.isServer || State == RingState.Dissolve) {
                return;
            }
            Lighting.AddLight(Projectile.Center
                , EverdeepVFX.AbyssGlow.ToVector3() * (0.22f + charge * 0.34f));

            if (speed > 5f && (int)StateTimer % (speed > 12f ? 2 : 3) == 0) {
                float ringR = QuadBasePx * 0.5f * 0.62f * Projectile.scale;
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = ang.ToRotationVector2();
                //切向甩出:水珠沿循环方向离心飞出,再被重力接管
                Vector2 fling = rim.RotatedBy(MathHelper.PiOver2) * (speed * 0.10f)
                    + Projectile.velocity * Main.rand.NextFloat(0.2f, 0.45f);
                EverdeepVFX.ShedDroplet(Projectile.Center + rim * ringR, fling
                    , Main.rand.NextFloat(0.6f, 0.9f) + charge * 0.25f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 240);
            flashTimer = 4;

            //出射相首次穿透:登记折返目标,进入余势滑行
            if (State == RingState.Outbound) {
                State = RingState.Coast;
                StateTimer = 0;
                ReturnTargetIndex = target.whoAmI + 1;
                Projectile.netUpdate = true;
            }

            //共鸣上报,只在主人端记账
            if (Projectile.IsOwnedByLocalPlayer()) {
                Player owner = Main.player[Projectile.owner];
                if (owner.CWR().TryGetHeldProjInds(out EverdeepHeld held)) {
                    held.AddResonance(target);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item85 with {
                Volume = 0.45f,
                Pitch = 0.1f + charge * 0.2f,
                MaxInstances = 3,
            }, Projectile.Center);
            EverdeepVFX.SplashBurst(target.Center, Projectile.velocity, 0.85f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State != RingState.Dissolve) {
                if (!VaultUtils.isServer) {
                    EverdeepVFX.SplashBurst(Projectile.Center, oldVelocity, 1.05f);
                    SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
                }
                Projectile.velocity = Vector2.Zero;
                Projectile.tileCollide = false;
                BeginDissolve();
            }
            return false;
        }

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            DrawWakeTrail();

            Effect effect = EffectLoader.EverdeepRing?.Value;
            if (effect == null || noiseTex == null) {
                DrawSpriteFallback();
                return false;
            }

            float fade = MathHelper.Clamp((StateTimer + (State == RingState.Outbound ? 0f : 20f)) / 5f, 0f, 1f);
            if (State == RingState.Dissolve) {
                fade = 1f - MathHelper.Clamp(StateTimer / DissolveTime, 0f, 1f);
            }
            float speed = Projectile.velocity.Length();
            float stretch = 1f + MathHelper.Clamp(speed * 0.022f, 0f, 0.42f);
            float visCharge = MathHelper.Clamp(charge + flashTimer * 0.2f, 0f, 1f);

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uSpin"]?.SetValue(spin);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uCharge"]?.SetValue(visCharge);
            effect.Parameters["uDeepColor"]?.SetValue(EverdeepVFX.AbyssDeep.ToVector3());
            effect.Parameters["uGlowColor"]?.SetValue(EverdeepVFX.AbyssGlow.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(EverdeepVFX.AbyssFoam.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑到 s1:SpriteBatch.Draw 会把 s0 覆写成画布贴图(合同同 ShockRingDraw)
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float quad = QuadBasePx * Projectile.scale;
            //先缩放后旋转:x 轴携带速度拉伸,再旋到运动方向 → 沿速度拉长的环
            Vector2 scale = new(quad * stretch / pixel.Width, quad / pixel.Height);
            sb.Draw(pixel, Projectile.Center - Main.screenPosition, null, Color.White
                , Projectile.velocity.ToRotation(), pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>加色水迹:环身后的拖尾核线,亮头暗尾</summary>
        private void DrawWakeTrail() {
            if (wakeTrail.Count < 2) {
                return;
            }
            Texture2D core = CWRAsset.LightShot.Value;
            SpriteBatch sb = Main.spriteBatch;
            for (int i = 0; i < wakeTrail.Count - 1; i++) {
                float progress = 1f - i / (float)wakeTrail.Count;
                Vector2 pos = wakeTrail[i] - Main.screenPosition;
                float rotation = (wakeTrail[i + 1] - wakeTrail[i]).ToRotation();
                Color col = Color.Lerp(EverdeepVFX.AbyssGlow, EverdeepVFX.AbyssDeep, 1f - progress)
                    with { A = 0 };
                float alpha = progress * progress * (0.48f + charge * 0.34f);
                float width = progress * (0.095f + charge * 0.035f);
                sb.Draw(core, pos, null, col * alpha, rotation
                    , core.Size() * 0.5f, new Vector2(width * 3.2f, width), SpriteEffects.None, 0f);
            }
        }

        /// <summary>着色器缺失兜底:双层旋筒剪影+缘光</summary>
        private void DrawSpriteFallback() {
            Texture2D cyclone = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Cyclone")?.Value;
            if (cyclone == null) {
                return;
            }
            float fade = State == RingState.Dissolve
                ? 1f - MathHelper.Clamp(StateTimer / DissolveTime, 0f, 1f) : 1f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float size = QuadBasePx * Projectile.scale / cyclone.Width * 0.8f;
            Color deep = EverdeepVFX.AbyssBlue with { A = 128 };
            Color glow = EverdeepVFX.AbyssGlow with { A = 0 };
            Main.EntitySpriteDraw(cyclone, pos, null, deep * (0.8f * fade), spin
                , cyclone.Size() / 2f, size, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(cyclone, pos, null, glow * (0.5f * fade), -spin * 0.7f
                , cyclone.Size() / 2f, size * 0.82f, SpriteEffects.FlipHorizontally, 0);
        }
        #endregion

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || State == RingState.Dissolve) {
                return;
            }
            //非溶解路径的意外销毁也要留水花
            EverdeepVFX.SplashBurst(Projectile.Center, Projectile.velocity, 0.8f);
        }
    }
}

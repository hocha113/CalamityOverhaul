using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 无头鬼影夺身「影的处决」。<br/>
    /// 前兆：脚下的影子拽住玩家、自地面立起一具无头暗体；<br/>
    /// 显形：暗体在身后弓身蓄势，骨白瞄线在要害上闪烁；<br/>
    /// 处决：穿体一闪，主刀 + 两道交叉切口按 0/3/6 帧错拍落下，躯体撕成影屑；<br/>
    /// 余韵：暗体沉回地面，斩痕自行针尖捏合。<br/>
    /// 材质：影——无彩黑吸光暗体 + 骨白结构细线，复用 <see cref="ShadeStrikeField"/> 斩痕场。
    /// </summary>
    internal sealed class ShadeSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 44;
        public override int ExecuteFrame => 122;
        //斩痕寿命约 70 帧，余韵放宽到能看完针尖捏合
        public override int TotalFrames => 200;

        private static readonly Color ShadeBody = new(7, 7, 10);
        private static readonly Color ShadeFray = new(18, 15, 26);
        private static readonly Color BoneRim = new(184, 204, 217);

        private readonly ShadeStrikeField field = new();
        //影体所在侧（相对玩家）与升起进度
        private int side = -1;
        private float rise;
        private bool dashDone;
        private int dashTrail;
        private float mainCutAngle;

        //无头暗体的横宽剖面（自颈口到腿），无头是身份
        private static readonly float[] SliceWidths =
            [15f, 30f, 34f, 31f, 26f, 22f, 20f, 18f, 16f, 13f];
        private const float SliceHeight = 8.6f;

        private Vector2 SilhouetteBase {
            get {
                Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;
                float dist = dashDone ? -58f : 52f;
                return anchor + new Vector2(side * dist, 4f);
            }
        }

        public override void OnBegin() {
            side = Player.direction != 0 ? -Player.direction : Seed % 2 == 0 ? -1 : 1;
            SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                Pitch = -0.9f,
                Volume = 0.5f,
                MaxInstances = 1,
            }, Player.Center);
        }

        public override void Update() {
            field.Update();
            if (dashTrail > 0) {
                dashTrail--;
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    //影子先醒，再立起来
                    if (Timer == 16) {
                        SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with {
                            Pitch = -0.45f,
                            Volume = 0.55f,
                            MaxInstances = 1,
                        }, Player.Center);
                    }
                    rise = MathHelper.Clamp((Timer - 14f) / 28f, 0f, 1f);
                    rise = rise * rise * (3f - 2f * rise);
                    if (Timer % 4 == 0 && rise > 0.1f) {
                        SpawnRiseSmoke();
                    }
                    break;

                case WraithSeizePhase.Manifest:
                    rise = 1f;
                    if (Timer == OmenEndFrame + 1) {
                        SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                            Pitch = -0.5f,
                            Volume = 0.6f,
                            MaxInstances = 1,
                        }, SilhouetteBase);
                    }
                    //蓄势末尾一声闷响，随后处决
                    if (PhaseProgress >= 0.82f && PhaseProgress < 0.84f && Timer % 2 == 0) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                            Pitch = -0.8f,
                            Volume = 0.45f,
                            MaxInstances = 1,
                        }, SilhouetteBase);
                    }
                    if (Timer % 5 == 0) {
                        SpawnRiseSmoke();
                    }
                    break;

                case WraithSeizePhase.Linger:
                    //交叉刀的错拍残响
                    if (Timer == ExecuteFrame + 3 || Timer == ExecuteFrame + 6) {
                        SoundEngine.PlaySound(SoundID.Item71 with {
                            Pitch = -0.15f - (Timer - ExecuteFrame) * 0.03f,
                            Volume = 0.4f,
                            MaxInstances = 2,
                        }, DeathAnchor);
                    }
                    //暗体沉回地面
                    rise = MathHelper.Clamp(1f - (PhaseProgress - 0.15f) / 0.6f, 0f, 1f);
                    if (Timer % 6 == 0 && rise > 0.05f) {
                        SpawnRiseSmoke();
                    }
                    break;
            }
        }

        public override void OnExecute() {
            dashDone = true;
            dashTrail = 8;
            Vector2 anchor = Player.Center;
            Vector2 dashDir = new(-side, Main.rand.NextFloat(-0.12f, 0.12f));
            dashDir = dashDir.SafeNormalize(Vector2.UnitX);
            mainCutAngle = dashDir.ToRotation();

            //主刀 + 两道交叉切口：0/3/6 帧错拍，同一场撕碎
            field.AddCut(anchor, mainCutAngle, 128f, 44f, 70);
            float crossA = mainCutAngle + MathHelper.ToRadians(52f + Seed % 14);
            float crossB = mainCutAngle - MathHelper.ToRadians(50f + Seed % 11);
            field.AddCut(anchor + dashDir.RotatedBy(MathHelper.PiOver2) * 12f, crossA,
                88f, 36f, 66, 3);
            field.AddCut(anchor - dashDir * 16f, crossB, 78f, 33f, 62, 6);

            //躯体撕成影屑：大片暗板携新鲜骨白撕口飞散
            for (int i = 0; i < 7; i++) {
                Vector2 velocity = Main.rand.NextVector2Unit()
                    * Main.rand.NextFloat(2.6f, 7.4f);
                velocity.Y -= Main.rand.NextFloat(0.5f, 2f);
                field.AddShard(anchor + Main.rand.NextVector2Circular(14f, 20f), velocity,
                    Main.rand.NextFloat(30f, 58f), Main.rand.NextFloat(4.5f, 8.5f),
                    Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(30, 48));
            }
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = -dashDir.RotatedByRandom(1.1f)
                    * Main.rand.NextFloat(2f, 6.2f);
                field.AddShard(anchor + Main.rand.NextVector2Circular(20f, 20f), velocity,
                    Main.rand.NextFloat(18f, 40f), Main.rand.NextFloat(2.4f, 5.4f),
                    Main.rand.NextBool(3) ? Main.rand.NextFloat(0.5f, 0.9f) : 0f,
                    Main.rand.Next(22, 38));
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(anchor + Main.rand.NextVector2Circular(20f, 20f),
                    -dashDir * Main.rand.NextFloat(0.8f, 2.4f)
                    + Main.rand.NextVector2Circular(1.2f, 1.2f),
                    new Color(17, 17, 22), Main.rand.NextFloat(0.1f, 0.17f))
                    ?.Configure(Main.rand.Next(26, 42), 0.46f, Main.rand.NextFloat(-0.02f, 0.02f));
            }

            SoundEngine.PlaySound(SoundID.Item71 with {
                Pitch = -0.35f,
                Volume = 0.95f,
                MaxInstances = 1,
            }, anchor);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                Pitch = -0.2f,
                Volume = 0.7f,
                MaxInstances = 1,
            }, anchor);
        }

        public override void Draw(SpriteBatch sb) {
            Texture2D glow = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;

            //地面影潭：暗体自其中立起/沉回
            float pool = Phase == WraithSeizePhase.Omen
                ? MathHelper.Clamp(Timer / 14f, 0f, 1f)
                : Phase == WraithSeizePhase.Linger ? MathHelper.Clamp(rise + 0.25f, 0f, 1f) : 1f;
            if (pool > 0.03f) {
                Vector2 poolPos = SilhouetteBase + new Vector2(0f, 42f) - Main.screenPosition;
                sb.Draw(glow, poolPos, null, ShadeBody * (0.85f * pool), 0f, glowOrigin,
                    new Vector2(0.52f * pool + 0.08f, 0.1f), SpriteEffects.None, 0f);
                sb.Draw(glow, poolPos, null, ShadeFray * (0.4f * pool), 0f, glowOrigin,
                    new Vector2(0.66f * pool + 0.1f, 0.13f), SpriteEffects.None, 0f);
            }

            if (rise > 0.02f) {
                DrawSilhouette(sb, SilhouetteBase, rise, 1f);
            }
            //穿体残像：处决后几帧沿冲线留三道渐淡影
            if (dashTrail > 0) {
                float trailAlpha = dashTrail / 8f;
                Vector2 dashDir = mainCutAngle.ToRotationVector2();
                for (int i = 1; i <= 3; i++) {
                    Vector2 ghostPos = anchor + new Vector2(side * 52f, 4f) + dashDir * (i * 38f);
                    DrawSilhouette(sb, ghostPos, 1f, trailAlpha * (0.36f - i * 0.09f));
                }
            }

            //瞄线：显形后段，三道骨白细线在要害上闪
            if (Phase == WraithSeizePhase.Manifest && PhaseProgress > 0.6f) {
                float flicker = 0.5f + 0.5f * MathF.Sin(Timer * 0.9f + Seed);
                float alpha = (PhaseProgress - 0.6f) / 0.4f * flicker * 0.4f;
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle src = new(0, 0, 1, 1);
                float baseAngle = new Vector2(-side, 0f).ToRotation();
                Span<float> angles = [
                    baseAngle,
                    baseAngle + MathHelper.ToRadians(52f + Seed % 14),
                    baseAngle - MathHelper.ToRadians(50f + Seed % 11),
                ];
                foreach (float angle in angles) {
                    Vector2 dir = angle.ToRotationVector2();
                    Vector2 start = anchor - dir * 80f - Main.screenPosition;
                    sb.Draw(pixel, start, src, BoneRim * alpha, angle, new Vector2(0f, 0.5f),
                        new Vector2(160f, 1.1f), SpriteEffects.None, 0f);
                }
            }
        }

        public override void DrawPrimitive(GraphicsDevice device) {
            if (!field.HasCuts) {
                return;
            }
            Effect cutEffect = EffectLoader.HeadlessShadeCut?.Value;
            Effect bodyEffect = EffectLoader.HeadlessShadeBody?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (noise == null) {
                return;
            }

            BlendState previousBlend = device.BlendState;
            RasterizerState previousRasterizer = device.RasterizerState;
            DepthStencilState previousDepth = device.DepthStencilState;
            SamplerState previousSampler = device.SamplerStates[0];
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            try {
                field.DrawCuts(device, cutEffect, noise);
                field.DrawShards(device, bodyEffect, noise, 1f);
            } finally {
                device.BlendState = previousBlend;
                device.RasterizerState = previousRasterizer;
                device.DepthStencilState = previousDepth;
                device.SamplerStates[0] = previousSampler;
            }
        }

        /// <summary>无头暗体：横切片堆出的黑吸光剪影，顶口一线骨白。</summary>
        private void DrawSilhouette(SpriteBatch sb, Vector2 basePos, float riseAmount, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            int slices = SliceWidths.Length;
            //从影潭里升：只画露出地面的部分
            int visible = (int)MathF.Ceiling(slices * riseAmount);
            //蓄势前倾：显形期上身压向玩家
            float lean = Phase == WraithSeizePhase.Manifest
                ? MathHelper.Clamp(PhaseProgress * 1.2f, 0f, 1f) * 0.32f : 0f;

            for (int i = 0; i < visible; i++) {
                //i=0 是颈口，从下往上升
                int fromBottom = slices - 1 - i;
                if (fromBottom >= visible) {
                    continue;
                }
                float sliceY = basePos.Y + 34f - (slices - i) * SliceHeight * riseAmount;
                float swayAmp = (slices - i) / (float)slices * 2.4f;
                float sway = MathF.Sin(Timer * 0.085f + i * 0.68f + Seed * 0.3f) * swayAmp;
                float leanShift = -side * lean * (slices - i) * 2.6f;
                Vector2 pos = new(basePos.X + sway + leanShift, sliceY);
                float width = SliceWidths[i];
                //暗体本体 + 毛口紫边
                sb.Draw(pixel, pos - Main.screenPosition, src, ShadeFray * (0.4f * alpha), 0f,
                    new Vector2(0.5f), new Vector2(width + 3.4f, SliceHeight + 1.6f),
                    SpriteEffects.None, 0f);
                sb.Draw(pixel, pos - Main.screenPosition, src, ShadeBody * (0.96f * alpha), 0f,
                    new Vector2(0.5f), new Vector2(width, SliceHeight + 0.8f),
                    SpriteEffects.None, 0f);
                //颈口骨白细线：没有头，是这具影的身份
                if (i == 0 && riseAmount > 0.92f) {
                    float boneFlick = 0.55f + 0.45f * MathF.Sin(Timer * 0.31f + Seed);
                    sb.Draw(pixel, pos + new Vector2(0f, -SliceHeight * 0.5f) - Main.screenPosition,
                        src, BoneRim * (0.5f * alpha * boneFlick), 0f, new Vector2(0.5f),
                        new Vector2(width * 0.72f, 1.2f), SpriteEffects.None, 0f);
                }
            }
        }

        private void SpawnRiseSmoke() {
            Vector2 pos = SilhouetteBase + new Vector2(Main.rand.NextFloat(-16f, 16f),
                Main.rand.NextFloat(10f, 40f));
            PRTLoader.NewParticle<PRT_Smoke>(pos,
                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.2f, -0.4f)),
                new Color(18, 17, 23), Main.rand.NextFloat(0.08f, 0.14f))
                ?.Configure(Main.rand.Next(22, 36), 0.42f, Main.rand.NextFloat(-0.02f, 0.02f));
        }

        public override Vector2 CameraFocus {
            get {
                Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;
                return Phase switch {
                    WraithSeizePhase.Manifest => Vector2.Lerp(anchor, SilhouetteBase, 0.3f),
                    WraithSeizePhase.Linger => DeathAnchor,
                    _ => anchor,
                };
            }
        }

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.14f,
            WraithSeizePhase.Manifest => 1.38f,
            WraithSeizePhase.Linger => 1.15f,
            _ => 1f,
        };

        public override float ShakeIntensity => Phase switch {
            WraithSeizePhase.Omen => 1.2f * PhaseProgress,
            WraithSeizePhase.Manifest => 1.8f + PhaseProgress * 1.6f,
            WraithSeizePhase.Linger => dashTrail > 0 ? 5.5f : 0f,
            _ => 0f,
        };
    }
}

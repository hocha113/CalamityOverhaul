using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 鬼雨夺身「被雨认领」。<br/>
    /// 前兆：四下的雨改向朝头顶收拢，脸痕压在雨里，喉口在上方成形；<br/>
    /// 显形：雨喉垂下，末端绞索缠住人把他抽离地面吞进去，喉身一道颈缩自下而上跑过；<br/>
    /// 处决：喉口把人吐回来，尸身坠地那一刻才算死，砸出一圈水花并顿五帧；<br/>
    /// 余韵：喉体自上而下抽干散去，原地那片急雨迟迟不散。<br/>
    /// 材质：湿墨阴幕，喉体走 <c>GhostRain.fx</c> 的 <c>TechThroat</c>，与常驻雨幕同一套水；
    /// 禁喜堂墨红、雷电狂欢与 SoftGlow 雨体。
    /// </summary>
    internal sealed class RainSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 40;
        public override int ExecuteFrame => 138;
        public override int TotalFrames => 192;

        /// <summary>尸身砸地那一下卡住的帧数，总长因此约 197 帧</summary>
        private const int LandHoldFrames = 5;

        //节拍锚点，全部是绝对帧
        private const int ThroatDropFrame = 41;
        private const int GrabFrame = 62;
        private const int SwallowFrame = 96;
        private const int GulpFrame = 116;
        private const int EjectFrame = 120;

        /// <summary>喉口悬在落点上方这么高</summary>
        private const float ThroatHeight = 330f;
        /// <summary>喉体 quad 宽度，噪声按此与高度折算等比</summary>
        private const float ThroatWidth = 250f;
        /// <summary>无地面时的坠落行程</summary>
        private const float AirborneDrop = 260f;

        private static readonly Color RainPale = new(170, 185, 190);
        private static readonly Color RainCorpse = new(140, 170, 165);
        private static readonly Color MistDamp = new(58, 66, 70);

        //三层处决音：低频体感层是抽吸风压，与其余五只互不相同
        private static readonly SeizureCue GrabCue = new(
            SeizureCue.Custom("RainGrab", SoundID.DD2_BetsyWindAttack with {
                Pitch = -0.55f,
                Volume = 0.7f,
                MaxInstances = 3,
            }),
            SoundID.SplashWeak with { Pitch = -0.25f, Volume = 0.5f, MaxInstances = 3 },
            SoundID.DD2_DrakinBreathIn with { Pitch = -0.3f, Volume = 0.35f, MaxInstances = 3 });

        private static readonly SeizureCue GulpCue = new(
            SeizureCue.Custom("RainGulp", SoundID.DD2_BetsyWindAttack with {
                Pitch = -0.95f,
                Volume = 0.9f,
                MaxInstances = 3,
            }),
            SoundID.DD2_DrakinBreathIn with { Pitch = -0.7f, Volume = 0.6f, MaxInstances = 3 },
            SoundID.SplashWeak with { Pitch = 0.15f, Volume = 0.4f, MaxInstances = 3 });

        private static readonly SeizureCue LandCue = new(
            SeizureCue.Custom("RainLand", SoundID.DD2_BetsyWindAttack with {
                Pitch = -1f,
                Volume = 0.55f,
                MaxInstances = 3,
            }),
            SoundID.SplashWeak with { Pitch = -0.45f, Volume = 0.95f, MaxInstances = 3 });

        private Vector2 groundAnchor;
        private bool anchorSet;
        private float landingY;
        private float throatOpen;
        private float throatGrip;
        private float swallow;
        private float drain;
        private float liftAmount;
        private float fallAmount;
        private int gulpFlash;

        public override void OnBegin() {
            ResolveAnchors();
            //远处一声闷雷，不带闪电
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.85f,
                Volume = 0.4f,
                MaxInstances = 3,
            }, groundAnchor);
        }

        public override void BuildBeats(SeizureBeatTable beats) {
            beats.Add(16, SpawnFaceStreak);
            beats.Add(30, () => {
                SpawnFaceStreak();
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with {
                    Pitch = -0.85f,
                    Volume = 0.4f,
                    MaxInstances = 3,
                }, MouthPoint);
            });
            //喉体自喉口垂下
            beats.Add(ThroatDropFrame, () => {
                SoundEngine.PlaySound(SoundID.DD2_DrakinBreathIn with {
                    Pitch = -0.55f,
                    Volume = 0.5f,
                    MaxInstances = 3,
                }, MouthPoint);
                YankBurst(groundAnchor);
            });
            //绞索缠身，人被抽离地面
            beats.Add(GrabFrame, () => {
                GrabCue.Play(Player?.Center ?? groundAnchor);
                YankBurst(Player?.Center ?? groundAnchor);
                gulpFlash = 10;
            });
            //吞：颈缩自下而上跑
            beats.Add(SwallowFrame, () => {
                SoundEngine.PlaySound(SoundID.DD2_DrakinBreathIn with {
                    Pitch = -0.2f,
                    Volume = 0.55f,
                    MaxInstances = 3,
                }, MouthPoint);
            });
            //喉口一合
            beats.Add(GulpFrame, () => {
                GulpCue.Play(MouthPoint);
                gulpFlash = 16;
                MouthSpray();
            });
            //吐回：尸身自喉口下端掉出来
            beats.Add(EjectFrame, () => {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Pitch = -0.6f,
                    Volume = 0.6f,
                    MaxInstances = 3,
                }, ThroatEnd);
            });
        }

        /// <summary>砸地那一下卡住，让「落定」有重量</summary>
        public override int HoldFramesAt(int frame)
            => frame == ExecuteFrame ? LandHoldFrames : 0;

        //---- 几何 ----

        private void ResolveAnchors() {
            Vector2 center = Player?.Center ?? DeathAnchor;
            groundAnchor = center;
            landingY = Ground.HasGround ? Ground.GroundY : center.Y + AirborneDrop;
            anchorSet = true;
        }

        private Vector2 MouthPoint {
            get {
                Vector2 basePos = anchorSet ? groundAnchor : (Player?.Center ?? DeathAnchor);
                return new Vector2(basePos.X, landingY - ThroatHeight);
            }
        }

        /// <summary>人被抽到的高度：喉口内侧一截</summary>
        private Vector2 HoldPoint => MouthPoint + new Vector2(0f, 118f);

        /// <summary>喉体下端：抓取期跟着人，吞下后自下而上缩回喉口</summary>
        private Vector2 ThroatEnd {
            get {
                Vector2 mouth = MouthPoint;
                Vector2 low = liftAmount > 0f
                    ? Vector2.Lerp(new Vector2(mouth.X, landingY), HoldPoint, liftAmount)
                    : new Vector2(mouth.X, landingY);
                //吞咽把下端一路收回喉口
                return Vector2.Lerp(low, mouth + new Vector2(0f, 60f), swallow);
            }
        }

        /// <summary>尸身坠回的位置</summary>
        private Vector2 FallPoint {
            get {
                Vector2 from = MouthPoint + new Vector2(0f, 140f);
                Vector2 to = new(MouthPoint.X, landingY - 16f);
                //自由落体读作加速，不是匀速下滑
                return Vector2.Lerp(from, to, fallAmount * fallAmount);
            }
        }

        //---- 推进 ----

        public override void Update() {
            if (!anchorSet) {
                ResolveAnchors();
            }
            if (gulpFlash > 0) {
                gulpFlash--;
            }

            //喉体宽度生命周期：张开 → 维持 → 吞咽颈缩 → 自喉口抽干
            throatOpen = MathHelper.Clamp((Timer - ThroatDropFrame) / 22f, 0f, 1f);
            throatGrip = MathHelper.Clamp((Timer - GrabFrame + 6f) / 20f, 0f, 1f);
            liftAmount = VaultUtils.EaseOutCubic(
                MathHelper.Clamp((Timer - GrabFrame) / 32f, 0f, 1f));
            swallow = MathHelper.Clamp((Timer - SwallowFrame) / (float)(GulpFrame - SwallowFrame),
                0f, 1f);
            if (Timer >= EjectFrame) {
                //吐回之后喉体不再吞，改为自上而下抽干
                swallow = MathHelper.Clamp(1f - (Timer - EjectFrame) / 10f, 0f, 1f);
                drain = MathHelper.Clamp((Timer - EjectFrame - 6f) / 46f, 0f, 1f);
                fallAmount = MathHelper.Clamp((Timer - EjectFrame) / (float)(ExecuteFrame - EjectFrame),
                    0f, 1f);
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    //改向的雨：四下的雨丝全部朝喉口收
                    SpawnConvergingRain(3);
                    if (Timer % 6 == 0) {
                        SpawnMist(2);
                    }
                    break;

                case WraithSeizePhase.Manifest:
                    SpawnConvergingRain(4);
                    if (Timer % 5 == 0) {
                        SpawnMist(1);
                    }
                    //人在喉里被淋透：身上不断挂水线往下淌
                    if (Timer >= GrabFrame && Timer < SwallowFrame && Timer % 3 == 0) {
                        SpawnBodyDrips(Player?.Center ?? HoldPoint);
                    }
                    if (Timer >= EjectFrame && Timer % 2 == 0) {
                        SpawnBodyDrips(FallPoint);
                    }
                    break;

                case WraithSeizePhase.Linger:
                    //原地这片急雨迟迟不散
                    if (Timer % 3 == 0) {
                        SpawnLocalDownpour(2);
                    }
                    if (Timer % 7 == 0) {
                        SpawnMist(1);
                    }
                    break;
            }
        }

        public override void OnExecute() {
            gulpFlash = 14;
            LandCue.Play(new Vector2(MouthPoint.X, landingY));
            //落地砸开一圈水花：贴地横向铺开，不是球形爆
            Vector2 impact = new(MouthPoint.X, landingY - 8f);
            for (int i = 0; i < 26; i++) {
                float spread = Main.rand.NextFloat(-1f, 1f);
                Vector2 vel = new(spread * Main.rand.NextFloat(3.5f, 8.5f),
                    -Main.rand.NextFloat(1.5f, 5f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    impact + new Vector2(Main.rand.NextFloat(-22f, 22f), 0f), vel,
                    (Main.rand.NextBool(5) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.5f, 0.75f),
                    Main.rand.NextFloat(0.6f, 1.05f))
                    ?.Configure(Main.rand.Next(22, 38), vel.X);
            }
            for (int i = 0; i < 4; i++) {
                SpawnMist(1);
            }
            //残体顺着坠势往下摊，不是原版的随机上抛
            if (Player != null) {
                SeizurePuppet.ThrowBody(Player, new Vector2(0f, 1.6f), 1.8f, 0.1f);
            }
        }

        //---- 玩家 ----

        //吞进喉口到吐回来之间不画本体
        public override bool HidesPlayer => Timer >= SwallowFrame && Timer < EjectFrame;

        public override void UpdatePlayerMotion() {
            if (Player == null || Player.dead) {
                return;
            }
            if (Timer < GrabFrame) {
                //还站着，只是被往上抽着
                SeizurePuppet.Brake(Player, 0.6f, freeze: Timer > 10);
                return;
            }
            if (Timer < EjectFrame) {
                SeizurePuppet.Anchor(Player, Vector2.Lerp(
                    new Vector2(MouthPoint.X, landingY - 24f), HoldPoint, liftAmount), 0.32f);
                return;
            }
            SeizurePuppet.Anchor(Player, FallPoint, 0.5f);
        }

        public override void ApplyPose() {
            if (Player == null) {
                return;
            }
            if (Timer < GrabFrame) {
                SeizurePuppet.LeanFromFeet(Player, -Player.direction * 0.1f * PhaseProgress);
                return;
            }
            //被吊在雨喉里：并腿悬挂，随水流轻摆
            float sway = MathF.Sin(Timer * 0.13f + Seed) * 0.16f;
            if (Timer >= EjectFrame) {
                //吐回来的人是软的，越掉越翻
                sway += fallAmount * fallAmount * 1.15f * (Seed % 2 == 0 ? 1f : -1f);
            }
            SeizurePuppet.Hang(Player, sway);
        }

        public override void UpdateDeathBody() {
            if (Player == null) {
                return;
            }
            //尸身落定，摊在原地被雨浇着
            SeizurePuppet.SettleBody(Player, 0.78f);
        }

        //---- 绘制 ----

        public override void Draw(SpriteBatch sb) {
            if (!anchorSet) {
                return;
            }
            Vector2 mouth = MouthPoint;
            Vector2 low = ThroatEnd;
            float height = low.Y - mouth.Y;
            if (throatOpen <= 0.01f || drain >= 0.999f || height < 24f) {
                return;
            }

            Effect effect = EffectLoader.GhostRain?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || white == null || noise == null) {
                return;
            }

            float width = ThroatWidth * (1f + gulpFlash / 16f * 0.08f);
            Rectangle dest = new(
                (int)(mouth.X - Main.screenPosition.X - width * 0.5f),
                (int)(mouth.Y - Main.screenPosition.Y),
                (int)width, (int)height);

            //喉体自开一批：TechThroat 与天幕 TechSky 同为 ps-only，共用一个 Effect 实例安全
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Seed * 0.037f);
            effect.Parameters["uIntensity"]?.SetValue(1f);
            effect.Parameters["uOpen"]?.SetValue(throatOpen);
            effect.Parameters["uSwallow"]?.SetValue(swallow);
            effect.Parameters["uDrain"]?.SetValue(drain);
            effect.Parameters["uGrip"]?.SetValue(throatGrip);
            effect.Parameters["uAspect"]?.SetValue(width / MathF.Max(height, 1f));
            effect.CurrentTechnique = effect.Techniques["TechThroat"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, dest, Color.White);
            sb.End();

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        //---- 粒子 ----

        /// <summary>改向的雨：从四周朝喉口收拢的雨丝。</summary>
        private void SpawnConvergingRain(int count) {
            Vector2 mouth = MouthPoint;
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = mouth + angle.ToRotationVector2()
                    * Main.rand.NextFloat(150f, 340f);
                Vector2 vel = (mouth - pos).SafeNormalize(-Vector2.UnitY)
                    * Main.rand.NextFloat(4.5f, 9f);
                PRTLoader.NewParticle<PRT_GhostRainYank>(pos, vel,
                    (Main.rand.NextBool(6) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.42f, 0.62f),
                    Main.rand.NextFloat(0.8f, 1.15f))
                    ?.Configure(mouth, Main.rand.Next(20, 32));
            }
        }

        /// <summary>拽入爆点：漏斗收束丝 + 上抽碎珠。</summary>
        private void YankBurst(Vector2 target) {
            Vector2 mouth = MouthPoint;
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 pos = target + angle.ToRotationVector2()
                    * Main.rand.NextFloat(26f, 74f);
                Vector2 vel = (mouth - pos).SafeNormalize(-Vector2.UnitY)
                    * Main.rand.NextFloat(3.5f, 7f);
                PRTLoader.NewParticle<PRT_GhostRainYank>(pos, vel,
                    RainPale * Main.rand.NextFloat(0.45f, 0.62f),
                    Main.rand.NextFloat(0.8f, 1.15f))
                    ?.Configure(mouth, Main.rand.Next(18, 30));
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    target + Main.rand.NextVector2Circular(20f, 26f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f),
                        Main.rand.NextFloat(-8f, -4f)),
                    RainPale * 0.5f, Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(Main.rand.Next(16, 26), 0f);
            }
        }

        /// <summary>喉口合上时自口沿甩出的水。</summary>
        private void MouthSpray() {
            Vector2 mouth = MouthPoint + new Vector2(0f, 40f);
            for (int i = 0; i < 18; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 vel = new(side * Main.rand.NextFloat(2.5f, 7f),
                    Main.rand.NextFloat(-2f, 3.5f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    mouth + new Vector2(side * Main.rand.NextFloat(20f, 90f),
                        Main.rand.NextFloat(-18f, 18f)), vel,
                    RainPale * Main.rand.NextFloat(0.45f, 0.7f),
                    Main.rand.NextFloat(0.55f, 0.95f))
                    ?.Configure(Main.rand.Next(20, 34), vel.X);
            }
        }

        /// <summary>挂在身上往下淌的水线：雨是落在东西上的。</summary>
        private void SpawnBodyDrips(Vector2 at) {
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    at + Main.rand.NextVector2Circular(13f, 20f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(2.5f, 5f)),
                    RainPale * Main.rand.NextFloat(0.4f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.72f))
                    ?.Configure(Main.rand.Next(16, 28), 0f);
            }
        }

        /// <summary>余韵的一小片急雨：只落在死点周围，迟迟不散。</summary>
        private void SpawnLocalDownpour(int count) {
            Vector2 anchor = new(MouthPoint.X, landingY);
            for (int i = 0; i < count; i++) {
                Vector2 pos = anchor + new Vector2(Main.rand.NextFloat(-96f, 96f),
                    -Main.rand.NextFloat(180f, 320f));
                Vector2 vel = new(Main.rand.NextFloat(-0.4f, 0.4f),
                    Main.rand.NextFloat(12f, 17f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel,
                    (Main.rand.NextBool(6) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.45f, 0.68f),
                    Main.rand.NextFloat(0.85f, 1.25f))
                    ?.Configure(Main.rand.Next(40, 70), vel.X);
            }
        }

        private void SpawnMist(int count) {
            Vector2 anchor = new(MouthPoint.X, landingY);
            for (int i = 0; i < count; i++) {
                Vector2 pos = anchor + new Vector2(Main.rand.NextFloat(-130f, 130f),
                    Main.rand.NextFloat(-26f, 8f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                    new Vector2(Main.rand.NextFloat(-0.35f, 0.35f),
                        Main.rand.NextFloat(-0.08f, 0f)),
                    MistDamp * Main.rand.NextFloat(0.75f, 1f),
                    Main.rand.NextFloat(0.7f, 1.25f))
                    ?.Configure(Main.rand.Next(90, 160));
            }
        }

        private void SpawnFaceStreak() {
            Vector2 anchor = anchorSet ? groundAnchor : DeathAnchor;
            PRTLoader.NewParticle<PRT_GhostRainFaceStreak>(
                anchor + new Vector2(Main.rand.NextFloat(-120f, 120f),
                    -Main.rand.NextFloat(120f, 240f)),
                new Vector2(0f, Main.rand.NextFloat(1.6f, 2.4f)),
                RainPale * 0.55f, Main.rand.NextFloat(0.9f, 1.2f))
                ?.Configure(Main.rand.Next(50, 74));
        }

        //---- 运镜 ----

        public override Vector2 CameraFocus {
            get {
                if (Timer < GrabFrame) {
                    return Vector2.Lerp(Player?.Center ?? DeathAnchor, MouthPoint, 0.28f);
                }
                if (Timer < EjectFrame) {
                    //跟着人往喉口抬
                    return Vector2.Lerp(HoldPoint, MouthPoint, 0.3f);
                }
                if (Phase == WraithSeizePhase.Linger) {
                    return new Vector2(MouthPoint.X, landingY - 40f);
                }
                //坠落段镜头跟着尸身下压
                return Vector2.Lerp(FallPoint, new Vector2(MouthPoint.X, landingY), 0.35f);
            }
        }

        public override float CameraZoom {
            get {
                if (Phase == WraithSeizePhase.Omen) {
                    return 1.08f;
                }
                if (Timer < EjectFrame) {
                    return MathHelper.Lerp(1.14f, 1.02f, liftAmount);
                }
                return Phase == WraithSeizePhase.Linger ? 1.1f : 1.06f;
            }
        }

        public override float CameraFocusLerp => Timer >= EjectFrame ? 0.2f : 0.12f;

        public override float ShakeIntensity {
            get {
                if (gulpFlash > 0) {
                    return gulpFlash * 0.42f;
                }
                return Phase switch {
                    WraithSeizePhase.Omen => 0.7f * PhaseProgress,
                    WraithSeizePhase.Manifest => 1.2f,
                    _ => 0f,
                };
            }
        }
    }
}

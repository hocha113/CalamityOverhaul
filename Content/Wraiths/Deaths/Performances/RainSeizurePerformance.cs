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
    /// 前兆：四下的雨改向，全部朝玩家头顶收拢，一张脸痕压在雨里；<br/>
    /// 显形：雨丝收束成一条向上的雨喉，把人整个提离地面吸进去；<br/>
    /// 处决：雨喉一合，人不见了；<br/>
    /// 余韵：尸身随雨落回原地，那一小片急雨迟迟不散。<br/>
    /// 材质：湿墨阴幕（冷灰青尸雨/贴地潮雾/脸痕/雨喉），不混喜堂墨红或雷电狂欢。
    /// </summary>
    internal sealed class RainSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 40;
        public override int ExecuteFrame => 122;
        public override int TotalFrames => 198;

        private static readonly Color RainPale = new(170, 185, 190);
        private static readonly Color RainCorpse = new(140, 170, 165);
        private static readonly Color MistDamp = new(58, 66, 70);

        //雨喉起点：玩家头顶上方的喉口
        private const float ThroatHeight = 300f;
        //被吸上去的高度
        private const float SwallowLift = 210f;

        private Vector2 groundAnchor;
        private bool anchorSet;
        private float liftAmount;
        private int throatFlash;
        private bool corpseDropped;
        private Vector2 corpsePos;
        private float corpseFall;

        private Vector2 ThroatMouth {
            get {
                Vector2 anchor = anchorSet ? groundAnchor : Player.Center;
                return anchor - new Vector2(0f, ThroatHeight);
            }
        }

        public override void OnBegin() {
            groundAnchor = Player.Center;
            anchorSet = true;
            //远处一声闷雷，不带闪电
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.85f,
                Volume = 0.4f,
                MaxInstances = 3,
            }, Player.Center);
        }

        public override void Update() {
            if (!anchorSet) {
                groundAnchor = Player.Center;
                anchorSet = true;
            }
            if (throatFlash > 0) {
                throatFlash--;
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    //改向的雨：四下的雨丝全部朝喉口收
                    SpawnConvergingRain(3);
                    if (Timer % 6 == 0) {
                        SpawnMist(2);
                    }
                    //一张脸痕压在雨里
                    if (Timer == 18 || Timer == 32) {
                        SpawnFaceStreak();
                    }
                    break;

                case WraithSeizePhase.Manifest: {
                    //雨喉成形并开始上提
                    if (Timer == OmenEndFrame + 1) {
                        SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                            Pitch = -0.75f,
                            Volume = 0.55f,
                            MaxInstances = 3,
                        }, Player.Center);
                        GhostRainYankBurst(Player.Center);
                        throatFlash = 12;
                    }
                    liftAmount = VaultUtils.EaseOutCubic(
                        MathHelper.Clamp((PhaseProgress - 0.15f) / 0.75f, 0f, 1f));
                    SpawnConvergingRain(4);
                    if (Timer % 5 == 0) {
                        SpawnMist(1);
                    }
                    //被吞进喉口前的最后一记收紧
                    if (PhaseProgress is >= 0.82f and < 0.85f && Timer % 2 == 0) {
                        SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                            Pitch = -0.5f,
                            Volume = 0.5f,
                            MaxInstances = 3,
                        }, ThroatMouth);
                        throatFlash = 10;
                    }
                    break;
                }

                case WraithSeizePhase.Linger:
                    liftAmount = 1f;
                    //尸身随雨落回：处决后 18 帧从喉口掉下来
                    if (Timer > ExecuteFrame + 16) {
                        if (!corpseDropped) {
                            corpseDropped = true;
                            corpsePos = ThroatMouth;
                        }
                        corpseFall = MathHelper.Clamp(corpseFall + 0.075f, 0f, 1f);
                        corpsePos = Vector2.Lerp(ThroatMouth, groundAnchor,
                            corpseFall * corpseFall);
                        if (corpseFall >= 1f && Timer % 3 == 0) {
                            //落地那一刻起，原地这片急雨迟迟不散
                            SpawnLocalDownpour(2);
                        }
                        else if (Timer % 2 == 0) {
                            SpawnTrailDrips(corpsePos);
                        }
                    }
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
            throatFlash = 16;
            //雨喉一合
            SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                Pitch = -0.95f,
                Volume = 0.75f,
                MaxInstances = 3,
            }, ThroatMouth);
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.95f,
                Volume = 0.35f,
                MaxInstances = 3,
            }, ThroatMouth);
            GhostRainYankBurst(SwallowPoint);
            //吞下时喉口炸开一圈水花
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                PRTLoader.NewParticle<PRT_GhostRainDrop>(SwallowPoint,
                    angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6.5f)
                    + new Vector2(0f, -2f),
                    RainPale * Main.rand.NextFloat(0.45f, 0.7f),
                    Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(20, 34), 0f);
            }
        }

        /// <summary>被提到的位置：喉口正下方，随上提量接近喉口。</summary>
        private Vector2 SwallowPoint {
            get {
                Vector2 anchor = anchorSet ? groundAnchor : Player.Center;
                return anchor - new Vector2(0f, SwallowLift * liftAmount);
            }
        }

        //吞入喉口后到尸身落回之间不画本体
        public override bool HidesPlayer => Phase == WraithSeizePhase.Linger
            && Timer < ExecuteFrame + 16;

        public override void Draw(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            Vector2 mouth = ThroatMouth;

            //雨喉：自喉口垂到猎物的一束收窄水柱，越靠喉口越窄
            float throatAlpha = Phase switch {
                WraithSeizePhase.Omen => PhaseProgress * 0.3f,
                WraithSeizePhase.Manifest => 0.35f + PhaseProgress * 0.35f,
                WraithSeizePhase.Linger => MathHelper.Clamp(1.1f - PhaseProgress * 1.7f, 0f, 1f) * 0.6f,
                _ => 0f,
            };
            if (throatAlpha > 0.01f) {
                Vector2 low = Phase == WraithSeizePhase.Omen
                    ? (Player.dead ? DeathAnchor : Player.Center)
                    : SwallowPoint;
                float flash = throatFlash > 0 ? throatFlash / 16f * 0.4f : 0f;
                const int Bands = 13;
                for (int i = 0; i < Bands; i++) {
                    float t = i / (float)(Bands - 1);
                    Vector2 pos = Vector2.Lerp(low, mouth, t);
                    //喉壁摆动：低频湿墨，不做整条平移
                    float sway = MathF.Sin(t * 4.2f + Timer * 0.16f + Seed * 0.4f)
                        * MathHelper.Lerp(16f, 4f, t);
                    pos.X += sway;
                    float width = MathHelper.Lerp(96f, 26f, t) * (0.85f + flash);
                    float alpha = throatAlpha * MathHelper.Lerp(0.85f, 0.35f, t);
                    sb.Draw(pixel, pos - Main.screenPosition, src,
                        MistDamp * alpha, 0f, new Vector2(0.5f),
                        new Vector2(width, ThroatHeight / Bands + 4f), SpriteEffects.None, 0f);
                    //喉内的冷灰青水线
                    sb.Draw(pixel, pos - Main.screenPosition, src,
                        RainCorpse * (alpha * 0.45f), 0f, new Vector2(0.5f),
                        new Vector2(width * 0.42f, ThroatHeight / Bands + 2f),
                        SpriteEffects.None, 0f);
                }
                //喉口：一圈压暗的收束环
                float mouthAlpha = throatAlpha * (0.7f + flash);
                sb.Draw(pixel, mouth - Main.screenPosition, src, MistDamp * mouthAlpha, 0f,
                    new Vector2(0.5f), new Vector2(112f, 14f), SpriteEffects.None, 0f);
                sb.Draw(pixel, mouth - Main.screenPosition, src,
                    RainPale * (mouthAlpha * 0.3f), 0f, new Vector2(0.5f),
                    new Vector2(74f, 5f), SpriteEffects.None, 0f);
            }

            //尸身落回：一团湿暗轮廓带着水痕坠下（本体已隐藏时才画）
            if (corpseDropped && corpseFall < 1f) {
                float squash = MathHelper.Lerp(1.25f, 1f, corpseFall);
                sb.Draw(pixel, corpsePos - Main.screenPosition, src,
                    MistDamp * 0.9f, 0f, new Vector2(0.5f),
                    new Vector2(20f / squash, 34f * squash), SpriteEffects.None, 0f);
                sb.Draw(pixel, corpsePos - Main.screenPosition, src,
                    RainCorpse * 0.28f, 0f, new Vector2(0.5f),
                    new Vector2(11f / squash, 24f * squash), SpriteEffects.None, 0f);
            }
        }

        /// <summary>改向的雨：从四周朝喉口收拢的雨丝。</summary>
        private void SpawnConvergingRain(int count) {
            Vector2 mouth = ThroatMouth;
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = mouth + angle.ToRotationVector2()
                    * Main.rand.NextFloat(140f, 320f);
                Vector2 vel = (mouth - pos).SafeNormalize(-Vector2.UnitY)
                    * Main.rand.NextFloat(4.5f, 9f);
                PRTLoader.NewParticle<PRT_GhostRainYank>(pos, vel,
                    (Main.rand.NextBool(6) ? RainCorpse : RainPale)
                    * Main.rand.NextFloat(0.42f, 0.62f),
                    Main.rand.NextFloat(0.8f, 1.15f))
                    ?.Configure(mouth, Main.rand.Next(20, 32));
            }
        }

        /// <summary>雨喉拽入的爆点：漏斗收束丝 + 上抽碎珠。</summary>
        private void GhostRainYankBurst(Vector2 target) {
            Vector2 mouth = ThroatMouth;
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

        /// <summary>余韵的一小片急雨：只落在死点周围，迟迟不散。</summary>
        private void SpawnLocalDownpour(int count) {
            Vector2 anchor = anchorSet ? groundAnchor : DeathAnchor;
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

        private void SpawnTrailDrips(Vector2 pos) {
            PRTLoader.NewParticle<PRT_GhostRainDrop>(
                pos + Main.rand.NextVector2Circular(10f, 14f),
                new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(2f, 4.5f)),
                RainPale * 0.45f, Main.rand.NextFloat(0.45f, 0.7f))
                ?.Configure(Main.rand.Next(16, 26), 0f);
        }

        private void SpawnMist(int count) {
            Vector2 anchor = anchorSet ? groundAnchor : DeathAnchor;
            for (int i = 0; i < count; i++) {
                Vector2 pos = anchor + new Vector2(Main.rand.NextFloat(-130f, 130f),
                    Main.rand.NextFloat(-8f, 26f));
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

        public override void UpdatePlayerMotion() {
            if (Player == null || Player.dead) {
                return;
            }
            if (Phase == WraithSeizePhase.Manifest && liftAmount > 0f) {
                //被雨喉提离地面：直接改写坐标，不靠速度堆叠
                Vector2 target = SwallowPoint;
                Player.velocity = Vector2.Zero;
                Player.Center = Vector2.Lerp(Player.Center, target, 0.35f);
                Player.fallStart = (int)(Player.position.Y / 16f);
                return;
            }
            base.UpdatePlayerMotion();
        }

        public override Vector2 CameraFocus => Phase switch {
            //上提期镜头跟着人往喉口抬
            WraithSeizePhase.Manifest => Vector2.Lerp(SwallowPoint, ThroatMouth, 0.25f),
            WraithSeizePhase.Linger => corpseDropped
                ? Vector2.Lerp(corpsePos, groundAnchor, 0.5f)
                : Vector2.Lerp(ThroatMouth, groundAnchor, 0.35f),
            _ => Player?.Center ?? DeathAnchor,
        };

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.1f,
            WraithSeizePhase.Manifest => MathHelper.Lerp(1.16f, 1.02f, PhaseProgress),
            WraithSeizePhase.Linger => 1.08f,
            _ => 1f,
        };

        public override float CameraFocusLerp => Phase == WraithSeizePhase.Manifest ? 0.16f : 0.1f;

        public override float ShakeIntensity => Phase switch {
            WraithSeizePhase.Omen => 0.8f * PhaseProgress,
            WraithSeizePhase.Manifest => 1.4f + (throatFlash > 0 ? throatFlash * 0.35f : 0f),
            WraithSeizePhase.Linger => throatFlash > 0 ? throatFlash * 0.3f : 0f,
            _ => 0f,
        };
    }
}

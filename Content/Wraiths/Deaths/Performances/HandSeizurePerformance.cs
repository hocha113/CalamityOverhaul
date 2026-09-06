using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Projectiles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 焦黑枯手夺身「五指之内」。<br/>
    /// 前兆：地面五道裂口透出烬光，五枚爪尖破土；空中被夺身时改为虚空撕口成形。<br/>
    /// 显形：巨掌自裂口托起，把人捧离地面，五指按 32%/68% 两度碾紧，玩家随之被压弯；<br/>
    /// 处决：五指攥死成拳，顿六帧，血烬自指缝迸出；<br/>
    /// 余韵：拳带着人沉回地底，土层合拢，只留一枚焦黑掌印。<br/>
    /// 材质：焦炭枯尸手，与役使时同一套 <see cref="GhostHandRig"/> 与 <c>GhostHandSheath.fx</c>，
    /// 处决版不得比日常版还糙。
    /// </summary>
    internal sealed class HandSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 44;
        public override int ExecuteFrame => 126;
        public override int TotalFrames => 184;

        /// <summary>攥死那一下卡住的帧数，总长因此约 190 帧</summary>
        private const int FistHoldFrames = 6;

        /// <summary>巨手尺度：掌半宽约 51px、中指约 147px，刚好能把人捧在掌心</summary>
        private const float HandScale = 3.2f;
        /// <summary>肩埋在裂口之下这么深，臂由此升出</summary>
        private const float ShoulderDepth = 430f;

        private static readonly Color CharPrint = new(16, 11, 9);
        private static readonly Color EmberHot = new(212, 70, 26);
        private static readonly Color EmberDim = new(120, 38, 18);
        private static readonly Color CharSmoke = new(58, 30, 20);
        private static readonly Color DarkBlood = new(126, 16, 20);

        //三层处决音：低频体感层是巨物砸地，与其余五只互不相同
        private static readonly SeizureCue BreachCue = new(
            SeizureCue.Custom("HandBreach", SoundID.DD2_OgreGroundPound with {
                Pitch = -0.75f,
                Volume = 0.55f,
                MaxInstances = 2,
            }),
            SoundID.NPCDeath14 with { Pitch = -0.85f, Volume = 0.35f, MaxInstances = 2 });

        private static readonly SeizureCue RiseCue = new(
            SeizureCue.Custom("HandRise", SoundID.DD2_OgreGroundPound with {
                Pitch = -0.45f,
                Volume = 0.9f,
                MaxInstances = 2,
            }),
            SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 0.5f, MaxInstances = 2 },
            SoundID.DD2_KoboldExplosion with { Pitch = 0.4f, Volume = 0.22f, MaxInstances = 2 });

        private static readonly SeizureCue FistCue = new(
            SeizureCue.Custom("HandFist", SoundID.DD2_OgreGroundPound with {
                Pitch = -0.2f,
                Volume = 1f,
                MaxInstances = 2,
            }),
            SoundID.NPCDeath14 with { Pitch = -0.35f, Volume = 0.85f, MaxInstances = 2 },
            SoundID.DD2_KoboldExplosion with { Pitch = 0.15f, Volume = 0.4f, MaxInstances = 2 });

        private readonly GhostHandRig rig = new() { Scale = HandScale };

        private Vector2 breachPoint;
        private bool breachSet;
        private float palmRise;
        private float clampHeld;
        private int crushFlash;
        private float sink;
        private bool fistShut;

        public override void OnBegin() {
            rig.Identity = Seed * 0.137f;
            ResolveBreach();
            //臂先摊成直线埋在裂口下，免得第一帧从别处甩过来
            rig.Snap(PalmCenter, ShoulderPoint);
            BreachCue.Play(breachPoint);
        }

        public override void BuildBeats(SeizureBeatTable beats) {
            beats.Add(10, () => BreachBurst(2));
            beats.Add(26, () => {
                BreachCue.Play(breachPoint);
                BreachBurst(3);
            });
            //巨掌破土：显形第一拍
            beats.Add(OmenEndFrame + 2, () => {
                RiseCue.Play(breachPoint);
                SpawnCharSmoke(breachPoint, 12);
                SpawnEmber(breachPoint, 10, 4.2f);
            });
            //32% / 68%：它攥猎物的老节拍，这次落在你身上
            beats.Add(OmenEndFrame + 27, () => Crush(0.16f, bleed: false));
            beats.Add(OmenEndFrame + 56, () => Crush(0.22f, bleed: true));
            //指节咬合前的一顿吸气
            beats.Add(ExecuteFrame - 8, () => crushFlash = 10);
            //拳开始沉回裂口
            beats.Add(ExecuteFrame + 16, () => SpawnCharSmoke(breachPoint, 8));
            //土层合拢
            beats.Add(ExecuteFrame + 44, () => {
                SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with {
                    Pitch = -0.9f,
                    Volume = 0.4f,
                    MaxInstances = 2,
                }, breachPoint);
                SpawnCharSmoke(breachPoint, 6);
            });
        }

        /// <summary>攥死那一下把逻辑帧卡住，让「合上」这一拍有重量</summary>
        public override int HoldFramesAt(int frame)
            => frame == ExecuteFrame ? FistHoldFrames : 0;

        //---- 几何 ----

        /// <summary>
        /// 裂口：有地就贴地，空中被夺身则在身下开一道虚空撕口。<br/>
        /// 空中变体只换成形理由（撕口而非破土）与余韵（不留地面掌印），巨手照常成形。
        /// </summary>
        private void ResolveBreach() {
            Vector2 anchor = Player?.Center ?? DeathAnchor;
            breachPoint = Ground.FootAnchor(anchor, 104f);
            breachSet = true;
        }

        private Vector2 ShoulderPoint => breachPoint + new Vector2(0f, ShoulderDepth);

        /// <summary>掌心：自裂口下方托到人脚底，余韵再沉回去</summary>
        private Vector2 PalmCenter {
            get {
                Vector2 basePos = breachSet ? breachPoint : (Player?.Center ?? DeathAnchor);
                //掌面比裂口高出一截，人正好被捧在上面
                float lift = MathHelper.Lerp(148f, -34f, palmRise);
                return basePos + new Vector2(0f, lift + sink);
            }
        }

        /// <summary>人被捧住的位置：掌心上方半个身位</summary>
        private Vector2 CradlePoint => PalmCenter - new Vector2(0f, 46f);

        /// <summary>合拢量 0..1</summary>
        private float ClampAmount {
            get {
                if (fistShut) {
                    return 1.05f;
                }
                float baseClamp = Phase == WraithSeizePhase.Manifest ? PhaseProgress * 0.34f : 0f;
                return MathHelper.Clamp(baseClamp + clampHeld
                    + (crushFlash > 0 ? crushFlash / 12f * 0.08f : 0f), -0.1f, 1.05f);
            }
        }

        //---- 推进 ----

        public override void Update() {
            if (!breachSet) {
                ResolveBreach();
            }
            if (crushFlash > 0) {
                crushFlash--;
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    //爪尖破土：只露出指尖那一截，其余被裂口线切掉
                    palmRise = MathHelper.Clamp((Timer - 8f) / 34f, 0f, 1f) * 0.24f;
                    if (Timer % 3 == 0) {
                        SpawnCharSmoke(RandomCrackPoint(), 1);
                    }
                    if (Timer % 5 == 0) {
                        SpawnEmber(RandomCrackPoint(), 1, 1.8f);
                    }
                    break;

                case WraithSeizePhase.Manifest:
                    palmRise = MathHelper.Clamp(0.24f + PhaseProgress / 0.34f, 0f, 1f);
                    if (Timer % 4 == 0) {
                        SpawnCharSmoke(PalmCenter + Main.rand.NextVector2Circular(70f, 14f), 1);
                    }
                    if (Timer % 9 == 0) {
                        SpawnEmber(PalmCenter + Main.rand.NextVector2Circular(60f, 12f), 1, 1.6f);
                    }
                    break;

                case WraithSeizePhase.Linger:
                    palmRise = 1f;
                    //拳带着人沉回裂口
                    sink = VaultUtils.EaseOutCubic(
                        MathHelper.Clamp((PhaseProgress - 0.08f) / 0.62f, 0f, 1f)) * 240f;
                    if (Timer % 4 == 0 && PhaseProgress < 0.75f) {
                        SpawnCharSmoke(breachPoint + Main.rand.NextVector2Circular(56f, 10f), 1);
                    }
                    if (Timer % 8 == 0 && PhaseProgress < 0.5f) {
                        SpawnEmber(breachPoint + Main.rand.NextVector2Circular(46f, 10f), 1, 2.2f);
                    }
                    break;
            }

            SolveRig();
            Lighting.AddLight(PalmCenter, EmberDim.ToVector3() * (0.5f + ClampAmount * 0.4f));
        }

        private void SolveRig() {
            rig.Identity = Seed * 0.137f;
            rig.SolveArm(ShoulderPoint, PalmCenter, 0.35f + ClampAmount * 0.5f, 1);
            //五指按指位分散包拢，不再全部收敛到同一点糊成一团
            Span<float> offsets = [0.10f, 0.02f, -0.04f, 0.02f, 0.10f];
            rig.SolveFingers(MathHelper.Clamp(ClampAmount, -0.2f, 1.05f), offsets);
        }

        private void Crush(float clampGain, bool bleed) {
            crushFlash = 12;
            clampHeld += clampGain;
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with {
                Pitch = -0.55f + clampHeld * 0.35f,
                Volume = 0.8f,
                MaxInstances = 2,
            }, PalmCenter);
            SoundEngine.PlaySound(SoundID.NPCDeath14 with {
                Pitch = -0.5f,
                Volume = 0.5f,
                MaxInstances = 2,
            }, CradlePoint);
            SpawnEmber(CradlePoint, 8, 3.2f);
            //第二次碾紧起就见血
            if (!bleed) {
                return;
            }
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    CradlePoint + Main.rand.NextVector2Circular(14f, 18f),
                    Main.rand.NextVector2Circular(3.2f, 2.6f) - Vector2.UnitY * 1.4f,
                    DarkBlood, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
            }
        }

        public override void OnExecute() {
            fistShut = true;
            crushFlash = 16;
            FistCue.Play(CradlePoint);
            SpawnEmber(CradlePoint, 18, 5.5f);
            SpawnCharSmoke(CradlePoint, 10);
            //血烬自指缝迸出：沿五指的缝隙方向甩，不是均匀圆爆
            for (int k = 0; k < GhostHandRig.FingerCount; k++) {
                Vector2 gap = (rig.FingerTip(k) - CradlePoint).SafeNormalize(-Vector2.UnitY);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        CradlePoint + gap * Main.rand.NextFloat(10f, 26f),
                        gap.RotatedByRandom(0.45f) * Main.rand.NextFloat(3.5f, 8f),
                        Main.rand.NextBool(3) ? new Color(168, 22, 26) : DarkBlood,
                        Main.rand.NextFloat(0.6f, 1.05f))
                        ?.Configure(Main.rand.Next(18, 32), 0.34f);
                }
            }
        }

        //---- 玩家 ----

        public override void UpdatePlayerMotion() {
            if (Player == null || Player.dead) {
                return;
            }
            if (Phase == WraithSeizePhase.Omen) {
                SeizurePuppet.Brake(Player, 0.55f, freeze: Timer > 10);
                return;
            }
            //被掌托起：直接改写坐标，别靠速度堆叠
            SeizurePuppet.Anchor(Player, CradlePoint, 0.3f);
        }

        public override void ApplyPose() {
            if (Player == null) {
                return;
            }
            if (Phase == WraithSeizePhase.Omen) {
                //还站着，只是被钉住
                SeizurePuppet.LeanFromFeet(Player, Player.direction * 0.12f * PhaseProgress);
                return;
            }
            SeizurePuppet.FaceTowards(Player, PalmCenter);
            SeizurePuppet.Curl(Player, MathHelper.Clamp(ClampAmount, 0f, 1f), Seed);
        }

        //拳合上之后人就在拳里，不该还站在掌上，也不该在旁边散成残体。
        //正典九之二写的「拳带人沉回地底」此前从未兑现，就是缺这一句
        public override bool HidesPlayer => fistShut;

        //---- 绘制 ----

        public override void DrawPrimitive(GraphicsDevice device) {
            Effect fx = EffectLoader.GhostHandSheath?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return;
            }

            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            try {
                float grip = MathHelper.Clamp(ClampAmount, 0f, 1f);
                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uOpacity"]?.SetValue(1f);
                fx.Parameters["uGrip"]?.SetValue(grip);
                fx.Parameters["uSeed"]?.SetValue(Seed * 0.137f * 10f);
                fx.Parameters["uEmber"]?.SetValue(
                    0.7f + grip * 0.5f + (crushFlash > 0 ? crushFlash / 16f * 0.8f : 0f));
                fx.Parameters["uNoiseTex"]?.SetValue(noise);

                //裂口线以下的几何压平到线上，读作被土掩着，而不是浮在地表上
                float cut = breachPoint.Y;
                var arm = ClipBelow(rig.BuildArmStrip(grip, Main.GlobalTimeWrappedHourly), cut);
                var palm = ClipBelow(rig.BuildPalmStrip(grip), cut);
                foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, arm, 0, arm.Length - 2);
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, palm, 0, palm.Length - 2);
                    for (int k = 0; k < GhostHandRig.FingerCount; k++) {
                        var finger = ClipBelow(rig.BuildFingerStrip(k), cut);
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip,
                            finger, 0, finger.Length - 2);
                    }
                }
            } finally {
                device.BlendState = prevBlend;
                device.RasterizerState = prevRaster;
                device.DepthStencilState = prevDepth;
            }
        }

        /// <summary>把裂口线以下的顶点压到线上，得到一条干净的地平切口。</summary>
        private static VertexPositionColorTexture[] ClipBelow(
            VertexPositionColorTexture[] verts, float cutY) {
            for (int i = 0; i < verts.Length; i++) {
                if (verts[i].Position.Y > cutY) {
                    verts[i].Position.Y = cutY;
                }
            }
            return verts;
        }

        public override void Draw(SpriteBatch sb) {
            //焦黑掌印：拳沉走后地上留的痕。暗层必须走真 alpha 贴图，
            //黑底亮度型贴图无论加色还是 A=0 都画不出暗东西
            if (Phase != WraithSeizePhase.Linger || !Ground.HasGround) {
                return;
            }
            Texture2D dark = CWRAsset.Extra_98?.Value;
            if (dark == null) {
                return;
            }
            float appear = MathHelper.Clamp((PhaseProgress - 0.35f) / 0.25f, 0f, 1f);
            float fade = appear * MathHelper.Clamp(1.4f - PhaseProgress, 0f, 1f);
            if (fade <= 0.01f) {
                return;
            }
            Vector2 origin = dark.Size() * 0.5f;
            Vector2 ground = breachPoint - Main.screenPosition;
            sb.Draw(dark, ground, null, CharPrint * (0.85f * fade), 0f, origin,
                new Vector2(3.6f, 0.5f), SpriteEffects.None, 0f);
            for (int k = 0; k < GhostHandRig.FingerCount; k++) {
                Vector2 mark = ground + new Vector2((k - 2) * 46f, -10f);
                sb.Draw(dark, mark, null, CharPrint * (0.7f * fade),
                    MathHelper.PiOver2, origin, new Vector2(0.9f, 0.28f), SpriteEffects.None, 0f);
            }
        }

        //---- 粒子 ----

        /// <summary>裂口上随机一处：五道裂缝的其中一条</summary>
        private Vector2 RandomCrackPoint() {
            int crack = Main.rand.Next(GhostHandRig.FingerCount);
            return breachPoint + new Vector2((crack - 2) * 46f + Main.rand.NextFloat(-10f, 10f),
                Main.rand.NextFloat(-6f, 6f));
        }

        private void BreachBurst(int cracks) {
            for (int i = 0; i < cracks; i++) {
                Vector2 pos = RandomCrackPoint();
                SpawnCharSmoke(pos, 3);
                SpawnEmber(pos, 3, 2.6f);
            }
        }

        private static void SpawnCharSmoke(Vector2 pos, int count) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(12f, 7f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f)
                    + Main.rand.NextVector2Circular(0.6f, 0.3f),
                    CharSmoke, Main.rand.NextFloat(0.1f, 0.18f))
                    ?.Configure(Main.rand.Next(24, 42), 0.45f, Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        private static void SpawnEmber(Vector2 pos, int count, float speed) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    pos + Main.rand.NextVector2Circular(12f, 9f),
                    Main.rand.NextVector2Circular(speed, speed) - Vector2.UnitY * speed * 0.45f,
                    Main.rand.NextBool() ? EmberHot : new Color(222, 82, 30),
                    Main.rand.NextFloat(0.4f, 0.9f))
                    ?.Configure(Main.rand.Next(14, 26), 0.05f);
            }
        }

        //---- 运镜 ----

        public override Vector2 CameraFocus => Phase switch {
            WraithSeizePhase.Omen => Vector2.Lerp(Player?.Center ?? DeathAnchor, breachPoint, 0.35f),
            WraithSeizePhase.Manifest => Vector2.Lerp(CradlePoint, PalmCenter, 0.35f),
            _ => Vector2.Lerp(PalmCenter, breachPoint, 0.5f),
        };

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.1f,
            WraithSeizePhase.Manifest => MathHelper.Lerp(1.18f, 1.34f, PhaseProgress),
            WraithSeizePhase.Linger => 1.14f,
            _ => 1f,
        };

        public override float ShakeIntensity => Phase switch {
            WraithSeizePhase.Omen => 1.8f * PhaseProgress,
            WraithSeizePhase.Manifest => 2.2f + (crushFlash > 0 ? crushFlash * 0.55f : 0f),
            WraithSeizePhase.Linger => crushFlash > 0 ? crushFlash * 0.5f : 1.2f * (1f - PhaseProgress),
            _ => 0f,
        };
    }
}

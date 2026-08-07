using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Cinematics;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 入雨演出的相位状态机：镜面浮现→驻留→180°翻转→落定，纯本地演出量。<br/>
    /// 节拍常量是唯一时钟，<see cref="OniRainWorldCutscene"/> 与 <see cref="OniRainWorldRender"/> 都从这里取数；
    /// 运镜失败不致命，演出照走。
    /// </summary>
    internal static class OniRainWorldTransition
    {
        //节拍表（60fps）：压镜0-40 → 镜面浮现40-150 → 驻留150-190 → 翻转190-280 → 落定280-312
        public const int ApproachEnd = 40;
        public const int RevealEnd = 150;
        public const int DwellEnd = 190;
        public const int RollEnd = 280;
        public const int TotalFrames = 312;
        /// <summary>入雨结算帧，翻转过半（θ≈90°）白闪处，世界状态在此切换</summary>
        public const int CommitFrame = 235;

        private const int GlimpseStart = 156;
        private const int GlimpseFrames = 20;

        public static bool Active { get; private set; }
        public static int Timer { get; private set; }

        /// <summary>缝线焦点：伞的地面锚点略上抬，运镜聚焦于此使缝线落屏幕中线</summary>
        public static Vector2 FocusWorld { get; private set; }
        /// <summary>伞的世界坐标，镜面波前的扩散圆心</summary>
        public static Vector2 UmbrellaWorld { get; private set; }

        //渲染包络，Update 逐帧推进
        public static float Reveal { get; private set; }
        public static float RollAngle { get; private set; }
        public static float Swallow { get; private set; }
        public static float Grade { get; private set; }
        public static float Glimpse { get; private set; }
        public static float Flash { get; private set; }
        public static float SeamGlow { get; private set; }

        /// <summary>翻转角速度（弧度/帧），旋转拖影用</summary>
        public static float RollVelocity { get; private set; }
        /// <summary>鬼影涟漪环的扩散进度 0-1</summary>
        public static float GlimpseRingProgress { get; private set; }
        /// <summary>伞的躁动包络：压镜段起颤，浮现段拉满</summary>
        public static float UmbrellaAgitation { get; private set; }
        /// <summary>结算前真实世界的前兆稀雨密度，结算后由世界状态接管</summary>
        public static float PreRainDensity { get; private set; }

        /// <summary>渲染合成是否需要介入</summary>
        public static bool RenderActive => Active
            && (Reveal > 0.0005f || RollAngle > 0.0005f || Flash > 0.0005f);

        /// <summary>结算前的预压顶：压镜段微弱起步，镜面浮现期加深，结算后由世界状态接管</summary>
        public static float AmbientPreGloom => Active && Timer < CommitFrame
            ? MathF.Max(Reveal * 0.25f, Smooth01(Timer / (float)ApproachEnd) * 0.08f) : 0f;

        /// <summary>开始入雨演出，仅本地玩家生效；重复调用无效</summary>
        public static void Begin(Player player, Vector2 umbrellaGround) {
            if (Active || Main.dedServ || player == null
                || player.whoAmI != Main.myPlayer || !player.Alives()) {
                return;
            }

            Active = true;
            Timer = 0;
            UmbrellaWorld = umbrellaGround;
            FocusWorld = umbrellaGround + new Vector2(0f, -8f);
            ZeroEnvelopes();
        }

        internal static void Update() {
            if (!Active) {
                return;
            }
            if (Main.gameMenu) {
                HardReset();
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.Alives()) {
                Abort();
                return;
            }

            //演出全程短无敌
            player.GivePlayerImmuneState(4);

            Timer++;
            AdvanceEnvelopes();
            SpawnStageFx(player);
            PlayBeats(player);

            if (Timer == CommitFrame) {
                Commit(player);
            }
            if (Timer >= TotalFrames) {
                Finish();
            }
        }

        private static void AdvanceEnvelopes() {
            int t = Timer;
            float prevRoll = RollAngle;

            //镜面从伞下扩散
            Reveal = t <= ApproachEnd ? 0f
                : Smooth01((t - ApproachEnd) / (float)(RevealEnd - ApproachEnd));

            //翻转角：先反向蓄势一小口（预备动作），再 0→π 先慢后快再慢
            if (t <= DwellEnd) {
                RollAngle = 0f;
            }
            else {
                float p = (t - DwellEnd) / (float)(RollEnd - DwellEnd);
                const float antic = 0.10f;
                RollAngle = p < antic
                    ? -0.03f * MathHelper.Pi * Smooth01(p / antic)
                    : MathHelper.Lerp(-0.03f * MathHelper.Pi, MathHelper.Pi,
                        CubicInOut((p - antic) / (1f - antic)));
            }
            RollVelocity = RollAngle - prevRoll;

            //结算后镜面向上吞没旧世界半屏，θ=π 时全屏皆镜、画面自动对上真实渲染
            Swallow = t < CommitFrame ? 0f : Smooth01((t - CommitFrame) / 37f);

            //镜像侧调色增益：结算后让位给真实世界的鬼雨氛围，避免双重压暗
            Grade = t < CommitFrame ? 1f : 1f - Smooth01((t - CommitFrame) / 41f);

            //镜中异样一闪
            Glimpse = t >= GlimpseStart && t < GlimpseStart + GlimpseFrames
                ? MathF.Sin(MathHelper.Pi * (t - GlimpseStart) / GlimpseFrames) : 0f;

            //结算白闪：短促起势，长尾退潮
            if (t >= CommitFrame - 2 && t < CommitFrame) {
                Flash = (t - (CommitFrame - 2)) / 2f;
            }
            else if (t >= CommitFrame) {
                Flash = MathHelper.Clamp(1f - (t - CommitFrame) / 18f, 0f, 1f);
            }
            else {
                Flash = 0f;
            }

            //缝线水膜辉光，落定段消隐
            float settleFade = t <= RollEnd ? 1f
                : 1f - Smooth01((t - RollEnd) / (float)(TotalFrames - RollEnd));
            SeamGlow = Math.Min(Reveal, settleFade);

            //鬼影涟漪环：随异样脉冲从伞荡开一圈
            GlimpseRingProgress = t >= GlimpseStart && t < GlimpseStart + GlimpseFrames + 14
                ? MathHelper.Clamp((t - GlimpseStart) / (float)(GlimpseFrames + 14), 0f, 1f) : 0f;

            //伞的躁动：压镜段起颤到 0.5，浮现段拉满
            UmbrellaAgitation = t <= ApproachEnd
                ? Smooth01(t / (float)ApproachEnd) * 0.5f
                : 0.5f + Reveal * 0.5f;

            //前兆稀雨：浮现段零星几根丝，翻转段渐密，结算后交给世界状态
            if (t <= ApproachEnd || t >= CommitFrame) {
                PreRainDensity = 0f;
            }
            else if (t <= DwellEnd) {
                PreRainDensity = MathF.Min(Reveal * 0.07f, 0.08f);
            }
            else {
                PreRainDensity = MathHelper.Lerp(0.08f, 0.2f,
                    (t - DwellEnd) / (float)(CommitFrame - DwellEnd));
            }
        }

        /// <summary>相位粒子：波前水花、脚下涟漪、落定溅圈</summary>
        private static void SpawnStageFx(Player player) {
            //湿墨色板，与鬼雨体系一致
            Color pale = new(170, 185, 190);
            Color damp = new(58, 66, 70);

            //波前推进的两侧水花：镜面漫开时缝线上被"顶开"的小水珠
            if (Timer > ApproachEnd && Reveal > 0.02f && Reveal < 0.99f && Timer % 3 == 0) {
                float zoom = MathF.Max(Main.GameViewMatrix.Zoom.X, 0.5f);
                float halfSpan = Reveal * (Main.screenWidth * 0.55f + 160f) / zoom;
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 pos = new(UmbrellaWorld.X + side * halfSpan,
                        FocusWorld.Y + Main.rand.NextFloat(-2f, 4f));
                    Vector2 vel = new(side * Main.rand.NextFloat(0.2f, 0.8f),
                        -Main.rand.NextFloat(1.8f, 3.6f));
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel,
                        pale * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(Main.rand.Next(22, 36), vel.X);

                    if (Main.rand.NextBool(5)) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(pos,
                            new Vector2(side * 0.3f, -0.06f),
                            damp * Main.rand.NextFloat(0.5f, 0.8f),
                            Main.rand.NextFloat(0.5f, 0.9f))
                            ?.Configure(Main.rand.Next(70, 110));
                    }
                }
            }

            //镜面成形后，脚下水膜周期性荡出小水花
            if (Reveal > 0.5f && Timer < DwellEnd && Timer % 18 == 0) {
                for (int i = 0; i < 4; i++) {
                    float vx = Main.rand.NextFloat(-1.4f, 1.4f);
                    Vector2 pos = new(player.Center.X + Main.rand.NextFloat(-14f, 14f),
                        FocusWorld.Y + 2f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                        new Vector2(vx, -Main.rand.NextFloat(1.2f, 2.6f)),
                        pale * Main.rand.NextFloat(0.35f, 0.5f),
                        Main.rand.NextFloat(0.4f, 0.6f))
                        ?.Configure(Main.rand.Next(16, 28), vx);
                }
            }

            //落定确认拍：脚下水花溅开一圈，人是"落"进雨里的
            if (Timer == RollEnd) {
                for (int i = 0; i < 16; i++) {
                    float angle = -MathHelper.Pi * (0.15f + 0.7f * i / 15f);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        player.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), -2f),
                        vel, pale * Main.rand.NextFloat(0.45f, 0.65f),
                        Main.rand.NextFloat(0.5f, 0.85f))
                        ?.Configure(Main.rand.Next(20, 34), vel.X);
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        player.Bottom + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.08f),
                        damp * Main.rand.NextFloat(0.6f, 0.9f),
                        Main.rand.NextFloat(0.7f, 1.1f))
                        ?.Configure(Main.rand.Next(80, 130));
                }
                player.CWR()?.GetScreenShake(4f);
            }
        }

        private static void PlayBeats(Player player) {
            switch (Timer) {
                case 6:
                    //远处一声闷雷，预兆
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -0.85f, Volume = 0.38f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case ApproachEnd:
                    //镜面起：水膜漫开
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.6f, Volume = 0.85f, MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 104:
                    //第二声更沉的闷雷
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -0.95f, Volume = 0.3f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case GlimpseStart + 4:
                    //镜中异样：布被扯紧的闷吸声
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                        Pitch = -0.9f, Volume = 0.42f, MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case DwellEnd:
                    //翻转起势
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                        Pitch = -0.7f, Volume = 0.5f, MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 208:
                    //翻转中段，世界滚动的极低闷响
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -1f, Volume = 0.34f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case 255:
                    //新世界的雨开始砸下来
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.35f, Volume = 0.55f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case RollEnd:
                    //落定一记压低的闷锣，雨声由常驻雨幕接管
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Pitch = -0.9f, Volume = 0.4f, MaxInstances = 3,
                    }, player.Center);
                    break;
                case RollEnd + 12:
                    //落定后再弹字，翻转中段的世界文本会被镜像采样翻转
                    OniRainWorldState.ShowEnterText(player);
                    break;
            }
        }

        /// <summary>结算：白闪掩护下切入鬼雨世界状态，真实渲染从此带鬼雨调色</summary>
        private static void Commit(Player player) {
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.6f, Volume = 0.85f, MaxInstances = 3,
            }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.7f, Volume = 0.6f, MaxInstances = 3,
            }, player.Center);
            player.CWR()?.GetScreenShake(9f);
            OniRainWorldState.EnterLocal(player);
        }

        private static void Finish() {
            //θ=π、全屏皆镜、调色归零时输出等于输入，直接停用无跳变
            Active = false;
            ZeroEnvelopes();
        }

        /// <summary>玩家中途失效：结算前取消不入雨，结算后直接收尾（世界已切换）</summary>
        private static void Abort() {
            bool committed = Timer >= CommitFrame;
            Active = false;
            ZeroEnvelopes();
            if (!committed) {
                StopOwnCutscene();
            }
        }

        /// <summary>世界卸载/回主菜单的硬复位，不回滚已结算的世界状态</summary>
        internal static void HardReset() {
            Active = false;
            Timer = 0;
            ZeroEnvelopes();
        }

        private static void ZeroEnvelopes() {
            Reveal = RollAngle = Swallow = Glimpse = Flash = SeamGlow = 0f;
            RollVelocity = GlimpseRingProgress = UmbrellaAgitation = PreRainDensity = 0f;
            Grade = 1f;
        }

        private static void StopOwnCutscene() {
            //if (CutsceneDirector.CurrentClip is OniRainWorldCutscene) {
            //    CutsceneDirector.Stop();
            //}
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float CubicInOut(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
        }
    }
}

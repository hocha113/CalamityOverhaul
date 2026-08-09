using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 入雨演出的相位状态机：涨水浮镜→驻留→180°翻转→落定，纯本地演出量。<br/>
    /// 节拍常量是唯一时钟，<see cref="OniRainWorldCutscene"/> 与 <see cref="OniRainWorldRender"/> 都从这里取数；
    /// 运镜失败不致命，演出照走。
    /// </summary>
    internal static class OniRainWorldTransition
    {
        //节拍表（60fps）：压镜0-40 → 涨水浮镜40-150 → 驻留150-190 → 翻转190-280 → 落定280-312
        public const int ApproachEnd = 40;
        public const int RevealEnd = 150;
        public const int DwellEnd = 190;
        public const int RollEnd = 280;
        public const int TotalFrames = 312;
        /// <summary>入雨结算帧，翻转过半（θ≈90°）白闪处，世界状态在此切换</summary>
        public const int CommitFrame = 235;
        /// <summary>水面触脚帧：水位线抵达缝线的确认拍</summary>
        public const int WaterContactFrame = RevealEnd - 2;

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
        /// <summary>涨水期泡沫/浮渣增强包络，触脚后回落到静水</summary>
        public static float FoamBoost { get; private set; }
        /// <summary>水位线噪声波动幅度（uv 空间），涨水期大、锁定后静水微澜</summary>
        public static float WaterWobble { get; private set; }

        /// <summary>涨水进度 0-1：对 <see cref="Reveal"/> 再套前快后慢，水逼近脚底时减速</summary>
        public static float RiseProgress => 1f - MathF.Pow(1f - Reveal, 1.6f);

        /// <summary>渲染合成是否需要介入</summary>
        public static bool RenderActive => Active
            && (Reveal > 0.0005f || RollAngle > 0.0005f || Flash > 0.0005f);

        /// <summary>结算前的预压顶：压镜段微弱起步，镜面浮现期加深，结算后由世界状态接管</summary>
        public static float AmbientPreGloom => Active && Timer < CommitFrame
            ? MathF.Max(Reveal * 0.25f, Smooth01(Timer / (float)ApproachEnd) * 0.08f) : 0f;

        /// <summary>开始入雨演出，仅本地玩家生效；重复调用无效，深潜演出期间不可再入</summary>
        public static void Begin(Player player, Vector2 umbrellaGround) {
            if (Active || OniRainDescentTransition.Active || Main.dedServ || player == null
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

            //涨水进度：水位线从屏底涨到缝线，Render 取 RiseProgress 再套前快后慢
            Reveal = t <= ApproachEnd ? 0f
                : Smooth01((t - ApproachEnd) / (float)(RevealEnd - ApproachEnd));

            //涨水期泡沫拉满，触脚后 40 帧内退到静水；水位线波动随之收敛
            FoamBoost = t <= ApproachEnd ? 0f
                : t <= RevealEnd ? MathHelper.Clamp((t - ApproachEnd) / 30f, 0f, 1f)
                : MathHelper.Clamp(1f - (t - RevealEnd) / 40f, 0f, 1f);
            WaterWobble = 0.0025f + 0.011f * FoamBoost;

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

        /// <summary>相位粒子：水位线碎水花与气泡、触脚溅圈、脚下涟漪、落定溅圈</summary>
        private static void SpawnStageFx(Player player) {
            //湿墨色板，与鬼雨体系一致
            Color pale = new(170, 185, 190);
            Color damp = new(58, 66, 70);

            //涨水段：沿水位线的碎水花与上浮潮气，线进屏才生
            if (Timer > ApproachEnd && Timer < WaterContactFrame && Timer % 3 == 0) {
                float waterUv = MathHelper.Lerp(1.15f, SeamUv(), RiseProgress);
                if (waterUv < 1.02f) {
                    Matrix inv = Matrix.Invert(Main.GameViewMatrix.TransformationMatrix);
                    for (int i = 0; i < 3; i++) {
                        Vector2 screenPx = new(
                            Main.rand.NextFloat(0.04f, 0.96f) * Main.screenWidth,
                            waterUv * Main.screenHeight);
                        Vector2 world = Vector2.Transform(screenPx, inv) + Main.screenPosition;

                        //水面被顶破的小水珠
                        Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f),
                            -Main.rand.NextFloat(1.2f, 2.8f));
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(world, vel,
                            pale * Main.rand.NextFloat(0.35f, 0.55f),
                            Main.rand.NextFloat(0.45f, 0.7f))
                            ?.Configure(Main.rand.Next(20, 32), vel.X);

                        //水下上浮的潮气泡
                        if (Main.rand.NextBool(4)) {
                            PRTLoader.NewParticle<PRT_GhostRainMist>(
                                world + new Vector2(0f, Main.rand.NextFloat(30f, 140f)),
                                new Vector2(Main.rand.NextFloat(-0.15f, 0.15f),
                                    -Main.rand.NextFloat(0.25f, 0.6f)),
                                damp * Main.rand.NextFloat(0.5f, 0.8f),
                                Main.rand.NextFloat(0.5f, 0.85f))
                                ?.Configure(Main.rand.Next(60, 100));
                        }
                    }
                }
            }

            //水面触脚确认拍：脚下溅花一圈 + 轻震屏，水从此贴住缝线
            if (Timer == WaterContactFrame) {
                for (int i = 0; i < 10; i++) {
                    float angle = -MathHelper.Pi * (0.15f + 0.7f * i / 9f);
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.8f, 4f);
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        player.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), -2f),
                        vel, pale * Main.rand.NextFloat(0.4f, 0.6f),
                        Main.rand.NextFloat(0.45f, 0.75f))
                        ?.Configure(Main.rand.Next(18, 30), vel.X);
                }
                player.CWR()?.GetScreenShake(3f);
            }

            //水面锁定缝线后，脚下水膜周期性荡出小水花
            if (Timer >= WaterContactFrame && Timer < DwellEnd && Timer % 18 == 0) {
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

        /// <summary>缝线焦点的屏幕 uv.y（与 Render 的枢轴同一夹取），涨水段 roll=0 两处一致</summary>
        private static float SeamUv() {
            Vector2 screen = Vector2.Transform(FocusWorld - Main.screenPosition,
                Main.GameViewMatrix.TransformationMatrix);
            return MathHelper.Clamp(screen.Y / Main.screenHeight, 0.3f, 0.7f);
        }

        private static void PlayBeats(Player player) {
            switch (Timer) {
                case 6:
                    //远处一声闷雷，预兆
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -0.85f,
                        Volume = 0.38f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case ApproachEnd:
                    //水起：阴冷的水从深处渗上来
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.9f,
                        Volume = 0.6f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 104:
                    //水涌第二拍，比上一声更近
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.7f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 126:
                    //水涌第三拍，已经贴近脚下
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.5f,
                        Volume = 0.6f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case WaterContactFrame:
                    //水面触脚
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.15f,
                        Volume = 0.75f,
                        MaxInstances = 3,
                    }, player.Bottom);
                    break;
                case GlimpseStart + 4:
                    //镜中异样：布被扯紧的闷吸声
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with {
                        Pitch = -0.9f,
                        Volume = 0.42f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case DwellEnd:
                    //翻转起势
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                        Pitch = -0.7f,
                        Volume = 0.5f,
                        MaxInstances = 3,
                    }, FocusWorld);
                    break;
                case 208:
                    //翻转中段，世界滚动的极低闷响
                    SoundEngine.PlaySound(SoundID.Thunder with {
                        Pitch = -1f,
                        Volume = 0.34f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case 255:
                    //新世界的雨开始砸下来
                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Pitch = -0.35f,
                        Volume = 0.55f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
                case RollEnd:
                    //落定一记压低的闷锣，雨声由常驻雨幕接管
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Pitch = -0.9f,
                        Volume = 0.4f,
                        MaxInstances = 3,
                    }, player.Center);
                    break;
            }
        }

        /// <summary>结算：白闪掩护下切入鬼雨世界状态，真实渲染从此带鬼雨调色</summary>
        private static void Commit(Player player) {
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.6f,
                Volume = 0.85f,
                MaxInstances = 3,
            }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.7f,
                Volume = 0.6f,
                MaxInstances = 3,
            }, player.Center);
            player.CWR()?.GetScreenShake(9f);
            OniRainWorldState.DescendLocal(player);
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
            FoamBoost = WaterWobble = 0f;
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

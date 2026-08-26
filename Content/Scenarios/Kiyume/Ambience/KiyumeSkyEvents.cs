using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Ambience
{
    /// <summary>
    /// 鬼梦天幕事件（KIY-P5-D）：E4 云后月轮（S 级敬畏拍）与 E5 远山火把队列（B 级点缀）
    /// 的包络状态机。只产数据（<see cref="MoonReveal"/> / <see cref="TorchLine"/>），
    /// 渲染消费在 KiyumeSky.fx，参数由 KiyumeSky.Draw 上载。<br/>
    /// 权威端+同步字段：无（裁决 23 本地时刻表）。天幕在鬼梦语义里是"你看见的幻象"，
    /// 各端进入时间不同、月轮与队列各端不同时出现，恰是梦的逻辑；潮汐钟各端不同相
    /// 是既有先例。零包零同步；本类 static 只是本地演出进度，非 per-player 游戏状态，
    /// netcode 静态禁令不适用（DungeonworldSnuff 同款口径）。
    /// </summary>
    internal class KiyumeSkyEvents : ModSystem
    {
        //与 KiyumeSky.fx 近脊几何耦合的换算常量（改 fx 必改这里）：
        //xRidgeNear = uv.x * aspect * 1.05 + uCamX * 0.000110
        private const float RidgeDomainScale = 1.05f;
        private const float RidgeNearParallax = 0.000110f;
        /// <summary>屏缘外余量（近脊噪声域）：盖住缩放平移误差，火点不在画内起灭</summary>
        private const float OffscreenMargin = 0.06f;

        //==== E4 月轮包络 ====
        private static int moonPhase = -1;    //-1 闲 / 0 入 / 1 持 / 2 出
        private static int moonTimer;
        private static int moonDwell;
        private static float moonReveal;
        /// <summary>月轮显形 0..1（KiyumeSky 上载 uMoonReveal）</summary>
        internal static float MoonReveal => moonReveal;

        //==== E5 火把队列 ====
        private static int torchWait = -1;    //-1=首个周期未掷
        private static int torchRun = -1;     //-1 闲，否则=已行进帧数
        private static float torchHead;       //队头位置（近脊噪声域，世界锚定）
        private static int torchCount;
        private static float torchDir;
        private static float torchSeed;
        /// <summary>火把队列参数（KiyumeSky 上载 uTorchLine；闲时全零=层数学归零）</summary>
        internal static Vector4 TorchLine => torchRun >= 0
            ? new Vector4(torchHead, torchCount, torchDir, torchSeed)
            : Vector4.Zero;

        public override void OnWorldLoad() => HardReset();
        public override void ClearWorld() => HardReset();
        public override void Unload() => HardReset();

        private static void HardReset() {
            //ShouldSave=false：每次进入是新的一夜，月轮预算由导演回满
            moonPhase = -1;
            moonTimer = 0;
            moonDwell = 0;
            moonReveal = 0f;
            torchRun = -1;
            torchWait = -1;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            if (!KiyumeWorld.Active || Main.gameMenu || KiyumeAmbienceSystem.Presence < 0.01f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }
            int band = KiyumeMetrics.BandIndexForColumn((int)(player.Center.X / 16f));
            UpdateMoon(band);
            UpdateTorch(band);
        }

        //==================== E4 云后月轮（S 级，裁决 20 A 案）====================

        private static void UpdateMoon(int band) {
            if (moonPhase >= 0) {
                AdvanceMoon();
                return;
            }
            //驻留门：村/枯林/远山带连续驻留计时（湖上视线交给雾墙），离带清零
            moonDwell = band >= 2 ? moonDwell + 1 : 0;
            //带位是物理门（武装也拦——与 C/E 包及 DungeonworldSnuff 口径统一）；驻留是铺垫门（武装跳过）
            if (band < 2) {
                return;
            }
            if (moonDwell < KiyumeScore.MoonDwellTicks && !KiyumeDirectorDebug.PeekArm(KiyumeScareId.Moon)) {
                return;
            }
            if (!KiyumeDirector.TryClaimScare(KiyumeScareId.Moon,
                KiyumeScore.MoonWindowLo, KiyumeScore.MoonWindowHi, KiyumeScore.MoonLottery)) {
                return;
            }
            //触发帧世界屏息：床层压半 12s（=包络全长），无 stinger，月不出声
            KiyumeSoundscape.PushDuck(KiyumeScore.MoonDuckAmount, KiyumeScore.MoonDuckFrames);
            moonPhase = 0;
            moonTimer = 0;
        }

        private static void AdvanceMoon() {
            moonTimer++;
            if (moonPhase == 0) {
                moonReveal = Smooth01(moonTimer / (float)KiyumeScore.MoonRiseTicks);
                if (moonTimer >= KiyumeScore.MoonRiseTicks) {
                    moonPhase = 1;
                    moonTimer = 0;
                }
                return;
            }
            if (moonPhase == 1) {
                moonReveal = 1f;
                if (moonTimer >= KiyumeScore.MoonHoldTicks) {
                    moonPhase = 2;
                    moonTimer = 0;
                }
                return;
            }
            moonReveal = Smooth01(1f - moonTimer / (float)KiyumeScore.MoonFadeTicks);
            if (moonTimer >= KiyumeScore.MoonFadeTicks) {
                moonPhase = -1;
                moonReveal = 0f;
                KiyumeDirector.ReleaseScare(KiyumeScareId.Moon);
            }
        }

        //==================== E5 远山火把队列（B 级点缀）====================

        private static void UpdateTorch(int band) {
            if (torchRun >= 0) {
                torchRun++;
                torchHead += torchDir * KiyumeScore.TorchSpeedUv * RidgeDomainScale;
                //自然收尾=队尾翻过对侧屏缘（"翻过脊线消失"）；硬帽防镜头追队不散场
                if (torchRun >= KiyumeScore.TorchCapTicks || QueueFullyPast()) {
                    torchRun = -1;
                    torchWait = NextTorchWait();
                }
                return;
            }
            if (torchWait < 0) {
                torchWait = NextTorchWait();    //进世界后首掷
                return;
            }
            //B 级 debug 直通（ArmScare(TorchLine)）：即刻起队，无视带位与周期
            if (KiyumeDirectorDebug.ConsumeArm(KiyumeScareId.TorchLine)) {
                StartTorch();
                return;
            }
            if (torchWait > 0) {
                torchWait--;
                return;
            }
            //只在枯林/远山带投放（近脊可见，村带不投）；出带短重试
            if (band != 3 && band != 4) {
                torchWait = 600;
                return;
            }
            StartTorch();
        }

        private static void StartTorch() {
            torchCount = Main.rand.Next(KiyumeScore.TorchCountMin, KiyumeScore.TorchCountMax + 1);
            torchDir = Main.rand.NextBool() ? 1f : -1f;
            torchSeed = Main.rand.NextFloat(0f, 8f);
            //队头先入画：从行进方向的后侧屏缘外起步，队尾拖在更外侧
            (float left, float right) = ViewSpan();
            torchHead = torchDir > 0f ? left - OffscreenMargin : right + OffscreenMargin;
            torchRun = 0;
        }

        private static bool QueueFullyPast() {
            (float left, float right) = ViewSpan();
            float span = (torchCount - 1) * KiyumeScore.TorchSpacingUv * RidgeDomainScale;
            float tail = torchHead - torchDir * span;
            return torchDir > 0f ? tail > right + OffscreenMargin : tail < left - OffscreenMargin;
        }

        /// <summary>当前视口在近脊噪声域里的左右缘（与 shader xRidgeNear 折算一致）</summary>
        private static (float left, float right) ViewSpan() {
            float camDomain = Main.screenPosition.X * RidgeNearParallax;
            float aspect = Main.screenWidth / (float)Main.screenHeight;
            return (camDomain, camDomain + aspect * RidgeDomainScale);
        }

        private static int NextTorchWait() {
            int wait = Main.rand.Next(KiyumeScore.TorchPeriodMin, KiyumeScore.TorchPeriodMax + 1);
            //犬让位期 B 级周期 ×2：真威胁在场，点缀退后（导演门 7 的 B 级条款）
            if (KiyumeDirector.HoundYieldActive) {
                wait *= 2;
            }
            return wait;
        }

        private static float Smooth01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}

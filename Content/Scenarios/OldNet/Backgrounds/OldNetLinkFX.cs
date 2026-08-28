using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds
{
    /// <summary>
    /// 旧网氛围渐层状态板（client-only，纯演出，玩法状态只读不写）：
    /// 集中承载"链路劣化 strain"等连续渐层量的每帧计算与平滑，
    /// 消费端（OldNetGradeRender/OldNetSky/OldNetDeco/OldNetAmbience）只读。
    /// 复位纪律照 <see cref="OldNetSkyEvents.ResetAll"/> 先例：ClearWorld + 离世检测双保险
    /// </summary>
    internal class OldNetLinkFX : ModSystem
    {
        //═══════ ② 链路劣化（RAM 见底渐层）═══════

        /// <summary>链路劣化强度 0~1：RAM 余量 35% 起步、8% 满幅，含耗速紧张项；0.15 lerp 平滑防抖</summary>
        internal static float Strain01 { get; private set; }

        //单帧撕裂脉冲：strain>0.45 后随机 1~2 帧横向撕裂，走 uGlitch 合成通道（不与疯域事件抢写）
        private static int tearTimer;
        private static float tearAmp;
        //闷响节拍器（间隔随 strain 缩短）
        private static int thumpTimer;
        //调试强制 strain 满幅的剩余帧数（验收用）
        private static int debugStrainTicks;

        //═══════ ③ 网的注视（威胁档位环境化）═══════

        /// <summary>注视度 0~4 连续量：NoiseTier + 档内进度，0.1 lerp 平滑（档位迟滞已内置，环境语言天然不抖）</summary>
        internal static float Watch { get; private set; }

        /// <summary>档位跃迁入场脉冲 0~1（6 帧，角标闪现一拍再落定，与 HUD 跃迁白闪同拍不同介质）</summary>
        internal static float WatchPulse01 => watchPulseTimer / 6f;

        /// <summary>T4 清剿波边缘脉动幅度（含被追数加权），Grade uWatch.z 消费</summary>
        internal static float T4EdgeAmp { get; private set; }

        private static int watchPulseTimer;
        private static int lastTier;
        //调试强制 watch（验收用）
        private static int debugWatchTicks;
        private static float debugWatchValue;

        //═══════ ⑧ 深层剖面（纵向低保真渐层，处女轴）═══════

        /// <summary>
        /// 纵深 0~1：相机中心行自 FloorRow+20（地表之下）起步，
        /// 深层厅地板 UnderDeepFloorRow 满幅——地表以上恒为 0，纵向场不是滤镜
        /// </summary>
        internal static float Depth01 { get; private set; }

        //═══════ 契约 C3：外部故障贡献位 ═══════

        /// <summary>
        /// 外部演出向屏幕故障通道的贡献位 0~1（如回收官在场/入场尖峰，Wave B 由 P7 接线）。
        /// 写者以取 max 方式写入；本系统每帧向 uGlitch 合成取 max 后自衰减
        /// （×0.88，低于 0.01 清零），需要持续故障的写者每帧续写
        /// </summary>
        public static float ExternalGlitch01;

        /// <summary>
        /// uGlitch 合成值：疯域事件 / 劣化撕裂 / 外部贡献三写者一律取 max 不取和（防双源爆表）
        /// </summary>
        internal static float ComposedGlitch01 => MathF.Max(
            MathF.Max(OldNetSkyEvents.Glitch, tearTimer > 0 ? tearAmp : 0f),
            ExternalGlitch01);

        /// <summary>验收辅助：强制 strain 满幅若干秒（TestItem 触发用）</summary>
        internal static void DebugForceStrain(int seconds = 8) => debugStrainTicks = seconds * 60;

        /// <summary>验收辅助：强制 watch 值若干秒（TestItem 触发用，逐档看环境变脸）</summary>
        internal static void DebugForceWatch(float watch, int seconds = 10) {
            debugWatchValue = MathHelper.Clamp(watch, 0f, 4f);
            debugWatchTicks = seconds * 60;
        }

        internal static void ResetAll() {
            Strain01 = 0f;
            tearTimer = 0;
            tearAmp = 0f;
            thumpTimer = 0;
            debugStrainTicks = 0;
            Watch = 0f;
            T4EdgeAmp = 0f;
            watchPulseTimer = 0;
            lastTier = 0;
            debugWatchTicks = 0;
            Depth01 = 0f;
            ExternalGlitch01 = 0f;
        }

        public override void ClearWorld() => ResetAll();

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            //外部贡献位自衰减：先于合成消费的下一帧生效，写者每帧续写维持
            ExternalGlitch01 *= 0.88f;
            if (ExternalGlitch01 < 0.01f) {
                ExternalGlitch01 = 0f;
            }
            if (!OldNetWorld.Active) {
                if (Strain01 > 0f || tearTimer > 0 || Watch > 0f) {
                    ResetAll();
                }
                return;
            }
            UpdateStrain();
            UpdateWatch();
            UpdateDepth();
        }

        //──── ⑧ 深层剖面：相机中心行 → 纵深 0~1 ────
        private static void UpdateDepth() {
            float camRow = (Main.screenPosition.Y + Main.screenHeight * 0.5f) / 16f;
            float lo = OldNetMetrics.FloorRow + 20f;
            float hi = OldNetMetrics.UnderDeepFloorRow;
            float t = MathHelper.Clamp((camRow - lo) / (hi - lo), 0f, 1f);
            Depth01 = MathHelper.Lerp(Depth01, t * t * (3f - 2f * t), 0.08f);
            if (Depth01 < 0.003f && t <= 0f) {
                Depth01 = 0f;
            }
        }

        //──── ③ 网的注视：噪音档位 → 0~4 连续注视度 + 跃迁脉冲 + T4 边缘幅度 ────
        private static void UpdateWatch() {
            float target;
            if (debugWatchTicks > 0) {
                debugWatchTicks--;
                target = debugWatchValue;
            }
            else {
                OldNetPlayer session = OldNetPlayer.Get(Main.LocalPlayer);
                int tier = session.NoiseTier;
                //档内进度：TierThreshold 口径；T4 档内以 [T4,100] 归一
                float lo = OldNetPlayer.TierThreshold(tier);
                float hi = tier >= 4 ? 100f : OldNetPlayer.TierThreshold(tier + 1);
                float inTier = MathHelper.Clamp(
                    (session.Noise - lo) / MathF.Max(hi - lo, 1f), 0f, 1f);
                target = tier + inTier;

                //跨档跃迁帧：6 帧入场脉冲；T4 进入帧一声故障低鸣（全网都听到了这声档位）
                if (tier != lastTier) {
                    watchPulseTimer = 6;
                    if (tier == 4 && lastTier < 4) {
                        SoundEngine.PlaySound(CWRSound.FaultOccurred with {
                            Pitch = -0.5f,
                            Volume = 0.55f
                        });
                    }
                    lastTier = tier;
                }
            }
            Watch = MathHelper.Lerp(Watch, target, 0.1f);
            if (Watch < 0.003f && target <= 0f) {
                Watch = 0f;
            }
            if (watchPulseTimer > 0) {
                watchPulseTimer--;
            }

            //T4 边缘脉动：watch [3.8,4] 窗口，被追数加权（封顶 3 只 +105%）
            float t4Band = MathHelper.Clamp((Watch - 3.8f) / 0.2f, 0f, 1f);
            T4EdgeAmp = t4Band * (1f + 0.35f * Math.Min(OldNetICEDirector.ActiveHunterCount, 3));
        }

        //──── ② 链路劣化：RAM 余量 → strain 包络 + 撕裂脉冲 + 闷响节拍 ────
        private static void UpdateStrain() {
            Player player = Main.LocalPlayer;
            RAMPlayer ram = player.GetModPlayer<RAMPlayer>();

            //避开未初始化档案的假读数
            bool ramReady = ram.ProfileInitialized && ram.MaxRam > 0;
            float ratio = ramReady ? MathHelper.Clamp(ram.CurrentRam / ram.MaxRam, 0f, 1f) : 1f;

            //烧断弹出接管：RAM 见零即 ForceEject 起跳，strain 快速归零给 EjectFlash 红闪让位
            //（ejectDelay 是 OldNetPlayer 私有态，这里用"RAM 见零"同义近似，不改共享文件）
            if (ramReady && ratio <= 0.005f && debugStrainTicks <= 0) {
                Strain01 = MathHelper.Lerp(Strain01, 0f, 0.4f);
                tearTimer = 0;
                return;
            }

            float target;
            if (debugStrainTicks > 0) {
                debugStrainTicks--;
                target = 1f;
            }
            else if (!ramReady) {
                target = 0f;
            }
            else {
                //35% 起步、8% 满幅（下降沿 smoothstep）
                float t = MathHelper.Clamp((0.35f - ratio) / (0.35f - 0.08f), 0f, 1f);
                float baseStrain = t * t * (3f - 2f * t);
                //耗速紧张项：同余量下越深越紧张；乘 base 起步门保证余量充足时恒零（渐层零点真为零）
                float rate = MathHelper.Clamp(OldNetMetrics.DrainPerSecondAt(
                    (int)(player.Center.X / 16f)) / 0.5f, 0f, 1f) * 0.25f;
                target = MathHelper.Clamp(
                    baseStrain + rate * MathHelper.Clamp(baseStrain / 0.15f, 0f, 1f), 0f, 1f);
            }
            Strain01 = MathHelper.Lerp(Strain01, target, 0.15f);
            if (Strain01 < 0.003f && target <= 0f) {
                Strain01 = 0f;
            }

            //单帧撕裂：0.45 起，概率 strain²×0.06/帧，每次 1~2 帧
            if (tearTimer > 0) {
                tearTimer--;
            }
            else if (Strain01 > 0.45f
                && Main.rand.NextFloat() < Strain01 * Strain01 * 0.06f) {
                tearTimer = Main.rand.Next(1, 3);
                tearAmp = 0.45f + Main.rand.NextFloat(0.3f);
            }

            //闷响节拍：0.5 起，间隔 90→40 tick 线性缩短，越急促越接近烧断
            //音源候选 WormDigQuiet 低变调（未确认-待实机试听；备选 CWRSound.Fault 家族低量变调）
            if (thumpTimer > 0) {
                thumpTimer--;
            }
            if (Strain01 > 0.5f && thumpTimer <= 0) {
                float k = (Strain01 - 0.5f) / 0.5f;
                thumpTimer = (int)MathHelper.Lerp(90f, 40f, k);
                SoundEngine.PlaySound(SoundID.WormDigQuiet with {
                    Volume = 0.3f + 0.2f * k,
                    Pitch = -0.9f,
                    MaxInstances = 2
                });
            }
        }
    }
}

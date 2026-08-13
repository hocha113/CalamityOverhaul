using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.UI
{
    /// <summary>
    /// 过渡链路修复：进入/退出的「先遮再冻」压黑门<br/>
    /// 已核实 SubworldLibrary 时序：BeginEntering 当帧设 gameMenu=true（SubworldSystem.cs:223）后
    /// 存档/卸载/生成全在后台 Task（:225→ExitWorldCallBack :1067），主线程理论上持续绘制加载屏——
    /// 但 12M tile 世界的分配与 GC 会造成主线程长帧，暴露出未经修饰的冻结黑。<br/>
    /// 本门在真正调 SubworldSystem.Enter/Exit 之前先播 0.45s 客户端压黑（入井意象），
    /// 全黑再多呈现一帧后才提交过渡，把「突兀冻结黑」变成「有意为之的入井压黑」；
    /// 联机客户端提交后 gameMenu 由服务器回包才翻转，期间黑幕持续保持（带超时撤防）
    /// </summary>
    internal class DungeonworldTransitionGate : ModSystem
    {
        private const float FadeSeconds = 0.45f;
        //提交后等待 SLib 接管（联机回包）的超时，超过则撤幕放行
        private const float TakeoverTimeoutSeconds = 6f;

        //阶段：0=闲置 1=压黑渐入 2=已提交等待 gameMenu 接管 3=撤幕渐出（提交被拒绝）
        private static int phase;
        private static bool entering;
        private static Func<bool> commit;
        private static float fadeT;
        private static float awaitT;
        private static int coveredFrames;
        private static long lastTick;

        /// <summary>
        /// 启动压黑门：渐入全黑后的下一帧再执行 commit（内部应调 SubworldSystem.Enter/Exit）<br/>
        /// commit 返回 false（如已在过渡中被 SLib 拒绝）则快速撤幕；重复触发/已在菜单时忽略
        /// </summary>
        internal static void Begin(bool enterDirection, Func<bool> commitAction) {
            if (phase != 0 || Main.gameMenu || Main.dedServ) {
                return;
            }
            phase = 1;
            entering = enterDirection;
            commit = commitAction;
            fadeT = 0f;
            awaitT = 0f;
            coveredFrames = 0;
            lastTick = Environment.TickCount64;
            DungeonworldTransitionLog.NewEpoch(enterDirection
                ? "EnterWorld() 调用,压黑渐入开始" : "ExitWorld() 调用,压黑渐入开始");
            //棺门合拢意象的风底音,后续第一响钟声由加载屏负责
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.9f, Volume = 0.3f });
        }

        public override void OnWorldUnload() {
            phase = 0;
            commit = null;
        }

        public override void PostUpdateEverything() {
            if (phase == 0 || Main.dedServ) {
                return;
            }
            long now = Environment.TickCount64;
            float dt = MathHelper.Clamp((now - lastTick) / 1000f, 0f, 0.1f);
            lastTick = now;

            if (phase == 1) {
                fadeT += dt;
                if (fadeT < FadeSeconds) {
                    return;
                }
                //已全黑:多呈现一帧纯黑再提交,保证冻结前屏幕上是黑幕而非世界
                if (coveredFrames++ < 1) {
                    return;
                }
                var doCommit = commit;
                commit = null;
                DungeonworldTransitionLog.Mark("压黑完成,提交过渡");
                bool ok = doCommit?.Invoke() ?? false;
                if (!ok) {
                    DungeonworldTransitionLog.Mark("过渡提交被拒绝,撤幕放行");
                    phase = 3;
                    return;
                }
                DungeonworldTransitionLog.Mark($"过渡已受理 gameMenu={Main.gameMenu}");
                //单人 BeginEntering 当帧翻 gameMenu;联机客户端要等服务器回包,黑幕保持
                phase = Main.gameMenu ? 0 : 2;
                return;
            }

            if (phase == 2) {
                if (Main.gameMenu) {
                    //本帧后 DrawSetup 接管画面,黑幕使命完成(实际到不了这里,菜单期不跑更新,留兜底)
                    phase = 0;
                    return;
                }
                awaitT += dt;
                if (awaitT >= TakeoverTimeoutSeconds) {
                    DungeonworldTransitionLog.Mark("等待 SLib 接管超时,撤幕放行");
                    phase = 3;
                }
                return;
            }

            //phase==3 撤幕渐出(3 倍速)
            fadeT -= dt * 3f;
            if (fadeT <= 0f) {
                phase = 0;
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (phase == 0 || Main.dedServ) {
                return;
            }
            layers.Add(new LegacyGameInterfaceLayer(
                "CWRMod: Dungeonworld Transition Gate",
                delegate {
                    var px = VaultAsset.placeholder2?.Value;
                    if (px != null && !px.IsDisposed) {
                        float alpha = phase == 2 ? 1f
                            : MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(fadeT / FadeSeconds, 0f, 1f));
                        Main.spriteBatch.Draw(px,
                            new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * alpha);
                    }
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }

    /// <summary>
    /// [DungeonworldTransition] 时间线日志：一次过渡为一个纪元（Begin 时归零），
    /// 每条日志带距纪元起点的毫秒偏移，是黑屏问题的验收工具
    /// </summary>
    internal static class DungeonworldTransitionLog
    {
        private static long epoch;

        /// <summary>开启新纪元（一次过渡的 t=0）并记录首条日志</summary>
        internal static void NewEpoch(string evt) {
            epoch = Environment.TickCount64;
            Mark(evt);
        }

        /// <summary>记录一条带纪元偏移的时间线日志</summary>
        internal static void Mark(string evt) {
            long delta = epoch == 0 ? 0 : Environment.TickCount64 - epoch;
            CWRMod.Instance?.Logger.Info($"[DungeonworldTransition] +{delta}ms {evt}");
        }
    }
}

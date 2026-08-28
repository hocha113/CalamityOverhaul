using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Lumindepth
{
    /// <summary>
    /// 沉沦之海「澄光」环境总控（残酷模式）。
    /// 本地在场强度、「荧潮」低频呼吸与 Boss 让位系数在此汇总，供描画层与音量取用；
    /// 环境声两条循环（水晶泛音+深水浸润）镜像 OldNetAmbience 的槽位管理；
    /// 权威端在此逐玩家推进静谧涡流的调度时钟。
    /// 沉沦之海在灾厄里是和平群系：氛围占比最高，唯一机制是无伤涡流，禁伤害
    /// </summary>
    internal class LumindepthAmbience : ModSystem
    {
        /// <summary>本地玩家的在场强度 0~1（含渐出尾巴，纯本地演出量）</summary>
        internal static float Presence { get; private set; }
        /// <summary>荧潮因子 0.44~1：整片水域荧光的低频潮汐起伏（约 34 秒一周期）</summary>
        internal static float Tide { get; private set; } = 1f;
        /// <summary>Boss 在场时的氛围让位系数（描画与音量统一乘）</summary>
        internal static float BossDim { get; private set; } = 1f;

        private static float tidePhase;
        private static int zoneRecheck;
        private static bool zoneCached;
        private static int accentIn = 300;

        //环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例）
        private static SlotId chimeLoopSlot;
        private static SlotId hushLoopSlot;
        /// <summary>水晶泛音：空灵微鸣的高频底</summary>
        private static readonly SoundStyle ChimeLoopStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>深水浸润：闷化的水息低频底</summary>
        private static readonly SoundStyle HushLoopStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        public override void PostUpdateEverything() {
            bool enabled = CWRRef.Has && GameModeSystem.BrutalActive;

            //权威端：涡流调度（Boss 在场时一切位移机制停摆，不再起新涡）
            if (enabled && Main.netMode != NetmodeID.MultiplayerClient && !CWRWorld.HasBoss) {
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead) {
                        player.GetModPlayer<LumindepthPlayer>().TickVortexClock();
                    }
                }
            }

            if (Main.dedServ) {
                return;
            }
            UpdateClient(enabled);
        }

        private static void UpdateClient(bool enabled) {
            Player lp = Main.LocalPlayer;
            if (--zoneRecheck <= 0) {
                zoneRecheck = 12;
                zoneCached = enabled && !Main.gameMenu && lp.active && lp.GetPlayerZoneSunkenSea();
            }
            float target = zoneCached ? 1f : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.035f);
            if (Presence < 0.004f && target <= 0f) {
                Presence = 0f;
            }
            BossDim = MathHelper.Lerp(BossDim, CWRWorld.HasBoss ? 0.55f : 1f, 0.05f);

            //荧潮：慢正弦呼吸（暂停时本钩不跑，自然冻结）
            tidePhase += MathHelper.TwoPi / (34f * 60f);
            if (tidePhase > MathHelper.TwoPi) {
                tidePhase -= MathHelper.TwoPi;
            }
            Tide = 0.72f + 0.28f * MathF.Sin(tidePhase);

            if (Presence <= 0f) {
                return;
            }
            UpdateAmbientLoops();
            UpdateAccents(lp);
            LumindepthCrystalChime.Update(lp, Presence);
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(chimeLoopSlot, out _)) {
                chimeLoopSlot = SoundEngine.PlaySound(ChimeLoopStyle, null, UpdateChimeLoop);
            }
            if (!SoundEngine.TryGetActiveSound(hushLoopSlot, out _)) {
                hushLoopSlot = SoundEngine.PlaySound(HushLoopStyle, null, UpdateHushLoop);
            }
        }

        //水晶泛音：音量随在场与荧潮一起呼吸
        private static bool UpdateChimeLoop(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0f) {
                return false;
            }
            sound.Volume = 0.20f * Presence * (0.75f + 0.25f * Tide) * BossDim;
            sound.Pitch = 0.45f;
            sound.Position = null;
            return true;
        }

        //深水浸润：稳定的低语水息
        private static bool UpdateHushLoop(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0f) {
                return false;
            }
            sound.Volume = 0.16f * Presence * BossDim;
            sound.Pitch = -0.62f;
            sound.Position = null;
            return true;
        }

        //零星点缀：远处的水滴轻响与偶发的晶体微光声
        private static void UpdateAccents(Player lp) {
            if (Presence < 0.5f || --accentIn > 0) {
                return;
            }
            accentIn = Main.rand.Next(240, 480);
            Vector2 pos = lp.Center + Main.rand.NextVector2Circular(360f, 240f);
            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.12f, Pitch = 0.55f, MaxInstances = 2 }, pos);
            }
            else {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.22f,
                    Pitch = -0.55f + Main.rand.NextFloat(0.35f),
                    MaxInstances = 3
                }, pos);
            }
        }

        public override void ClearWorld() {
            Presence = 0f;
            BossDim = 1f;
            accentIn = 300;
            zoneRecheck = 0;
            zoneCached = false;
            LumindepthCrystalChime.Reset();
        }
    }
}

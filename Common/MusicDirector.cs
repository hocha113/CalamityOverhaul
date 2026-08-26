using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 音乐覆盖档位，数值越大越优先。Boss 曲/音乐盒/场景曲的让位关系
    /// 用 <see cref="MusicClaim.YieldToBossMusic"/> 等旗位声明，不在站点互相特判
    /// </summary>
    internal enum MusicTier
    {
        /// <summary>菜单/致谢 ED，仅 gameMenu 下可参选（游戏内档位反之，杜绝 #97 类菜单曲泄漏）</summary>
        MenuOverlay = 0,
        /// <summary>传奇主题（鬼伞雨曲）</summary>
        LegendTheme = 1,
        /// <summary>子世界环境（赛博教程）</summary>
        SubworldAmbience = 2,
        /// <summary>叙事场景（机械/奸奇/德拉多/老公爵/终灾/逢魔黄昏）</summary>
        NarrativeScene = 3,
        /// <summary>全屏演出/仪式（永燃当下/海妖诅咒）</summary>
        Ceremony = 4,
    }

    /// <summary>
    /// 一条音乐认领：与功能同文件共置（镜像 <see cref="CWRNetChannel"/> 惯例），
    /// 加载期经 <see cref="MusicDirector"/> 自动发现，游戏内不再有任何直写
    /// <see cref="Main.newMusic"/>/<see cref="Main.musicBox2"/> 的站点
    /// （例外：三处子世界加载屏在加载期直写 newMusic=0 静音，不属仲裁域）。<br/>
    /// <see cref="ShouldPlay"/> 必须每帧现算、可证伪，禁止闩锁字段；
    /// 功能自身的状态旗（演出 IsActive 等）由功能生命周期负责设与清，认领只读
    /// </summary>
    internal abstract class MusicClaim
    {
        /// <summary>优先级档位</summary>
        public abstract MusicTier Tier { get; }
        /// <summary>同档平手裁决，大者先；再平按类型全名序，次序确定。
        /// 迁移站点的权重按旧同帧写序折算（后写者胜），改动前先核对旧胜负关系</summary>
        public virtual int SubWeight => 0;
        /// <summary>true 时给 Boss 曲让位，AnyActiveBossNPC 由仲裁器统一判；属性每帧取值，可随状态切换</summary>
        public virtual bool YieldToBossMusic => false;
        /// <summary>true 时给灾厄 BossRush 让位（仲裁器统一判）</summary>
        public virtual bool YieldToBossRush => false;
        /// <summary>守护栏：&gt;0 时在场连续超过该帧数强制离场，条件归假后重新武装</summary>
        public virtual int HardTimeoutFrames => 0;
        /// <summary>灭声认领（奸奇）：胜出时不写曲目，musicVolume 压 0，存取还原由仲裁器统一做</summary>
        public virtual bool MuteAll => false;
        /// <summary>音量地板（永燃当下 0.6）：胜出时用户音量低于此值则抬升，负值表示不干预</summary>
        public virtual float VolumeFloor => -1f;
        /// <summary>每帧现算的在场证明；gameMenu 门禁由仲裁器按档位统一处理，无需自查</summary>
        public abstract bool ShouldPlay();
        /// <summary>胜出帧曲目槽位；<see cref="MuteAll"/> 认领返回 -1。
        /// 必须返回模组曲目槽（≥原版曲目数）：游戏内经 musicBox2 承载，
        /// 原版范围的值会被音乐盒映射表错译成别的曲目</summary>
        public abstract int GetMusicSlot();
        /// <summary>胜出帧回调（致谢 ED 的 musicFade 交叉淡出挂这里）</summary>
        public virtual void OnWinFrame() { }

        //以下为仲裁器簿记，认领自身不要读写
        internal int ActiveFrames;
        internal bool TimedOut;
        internal bool Errored;
    }

    /// <summary>
    /// 音乐中央仲裁器：全库唯一写点挂 <see cref="IUpdateAudio.DecideMusic"/>
    /// （原版 DecideOnNewMusic 之后、消费之前，菜单内同样运行——菜单档认领依赖此点，
    /// PostUpdateEverything 在 gameMenu 下不跑故不可用作写点）。<br/>
    /// 游戏内写 newMusic+musicBox2 借音乐盒层压过 Boss/事件/场景曲（与旧直写站点同层级），
    /// 菜单内只写 newMusic。赢家切换打 Debug 日志，"音乐不停"类反馈有账可查
    /// </summary>
    internal static class MusicDirector
    {
        private static MusicClaim[] claims = [];
        private static MusicClaim lastWinner;
        //音量干预的统一存取还原（灭声/地板共用一个基准槽）
        private static float savedVolume = -1f;
        private static float lastAppliedVolume = -1f;

        internal static void Load(Mod mod) {
            //只扫描本模组程序集（镜像 CWRNetWork 惯例）
            List<MusicClaim> list = VaultUtils.GetDerivedInstances<MusicClaim>(AssemblyManager.GetLoadableTypes(mod.Code));
            list.Sort(static (a, b) => {
                int byTier = b.Tier.CompareTo(a.Tier);
                if (byTier != 0) {
                    return byTier;
                }
                int byWeight = b.SubWeight.CompareTo(a.SubWeight);
                if (byWeight != 0) {
                    return byWeight;
                }
                return string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
            });
            claims = [.. list];
        }

        internal static void Unload() {
            RestoreVolume();
            claims = [];
            lastWinner = null;
        }

        /// <summary>世界卸载/进出菜单的统一收场：清计时、还原音量、忘掉赢家，杜绝残留到标题界面</summary>
        internal static void ClearState() {
            foreach (MusicClaim claim in claims) {
                claim.ActiveFrames = 0;
                claim.TimedOut = false;
            }
            RestoreVolume();
            if (lastWinner != null) {
                Log($"世界收场，释放 {lastWinner.GetType().Name}");
                lastWinner = null;
            }
        }

        /// <summary>唯一写点，由 <see cref="MusicDirectorAudio"/> 每帧驱动</summary>
        internal static void Arbitrate() {
            if (Main.dedServ) {
                return;
            }
            bool menu = Main.gameMenu;
            bool bossNpc = !menu && Main.CurrentFrameFlags.AnyActiveBossNPC;
            bool bossRush = !menu && CWRRef.GetBossRushActive();

            MusicClaim winner = null;
            foreach (MusicClaim claim in claims) {
                //菜单档只在菜单参选，其余档只在游戏内参选
                bool eligible = menu == (claim.Tier == MusicTier.MenuOverlay);
                if (!eligible || !SafeShouldPlay(claim)) {
                    claim.ActiveFrames = 0;
                    claim.TimedOut = false;
                    continue;
                }
                //守护栏按在场帧数计（与旧站点 CekTimer 口径一致：让位期间照计、
                //暂停帧冻结——旧计时挂 PostUpdateEverything，暂停时不跑，此处保持同口径）
                if (!Main.gamePaused) {
                    claim.ActiveFrames++;
                }
                if (claim.HardTimeoutFrames > 0 && claim.ActiveFrames > claim.HardTimeoutFrames && !claim.TimedOut) {
                    claim.TimedOut = true;
                    Log($"{claim.GetType().Name} 在场超过 {claim.HardTimeoutFrames} 帧，硬超时强制离场");
                }
                if (claim.TimedOut || winner != null) {
                    continue;//已定胜负或已超时，只维护计时
                }
                if (claim.YieldToBossMusic && bossNpc) {
                    continue;
                }
                if (claim.YieldToBossRush && bossRush) {
                    continue;
                }
                winner = claim;
            }

            if (!ReferenceEquals(winner, lastWinner)) {
                Log(winner == null
                    ? $"{lastWinner.GetType().Name} 离场，交还原版判定"
                    : $"{winner.GetType().Name} 接管音乐（档位 {winner.Tier}，子权重 {winner.SubWeight}）");
                lastWinner = winner;
            }

            if (winner == null) {
                RestoreVolume();
                return;
            }

            if (!winner.MuteAll) {
                int slot = SafeSlot(winner);
                if (slot >= 0) {
                    Main.newMusic = slot;
                    if (!menu) {
                        Main.musicBox2 = slot;
                    }
                }
            }
            ApplyVolumePolicy(winner);
            SafeWinFrame(winner);
        }

        private static void ApplyVolumePolicy(MusicClaim winner) {
            //干预期间当前值≠上次施加值 = 用户动了滑条，改记为新基准
            if (savedVolume >= 0f && Main.musicVolume != lastAppliedVolume) {
                savedVolume = Main.musicVolume;
            }
            float baseVolume = savedVolume >= 0f ? savedVolume : Main.musicVolume;
            float target;
            if (winner.MuteAll) {
                target = 0f;
            }
            else if (winner.VolumeFloor >= 0f && baseVolume < winner.VolumeFloor) {
                target = winner.VolumeFloor;
            }
            else {
                RestoreVolume();//赢家不干预音量，归还此前基准
                return;
            }
            if (savedVolume < 0f) {
                savedVolume = Main.musicVolume;
            }
            Main.musicVolume = target;
            lastAppliedVolume = target;
        }

        private static void RestoreVolume() {
            if (savedVolume < 0f) {
                return;
            }
            //无条件还原：旧 EBN 站点对"用户音量为 0"不还原属 bug，此处顺带修复
            Main.musicVolume = savedVolume;
            savedVolume = -1f;
            lastAppliedVolume = -1f;
        }

        //单个认领抛异常只废它自己，不拖垮整个仲裁帧
        private static bool SafeShouldPlay(MusicClaim claim) {
            if (claim.Errored) {
                return false;
            }
            try {
                return claim.ShouldPlay();
            } catch (Exception ex) {
                MarkErrored(claim, ex);
                return false;
            }
        }

        private static int SafeSlot(MusicClaim claim) {
            try {
                return claim.GetMusicSlot();
            } catch (Exception ex) {
                MarkErrored(claim, ex);
                return -1;
            }
        }

        private static void SafeWinFrame(MusicClaim claim) {
            try {
                claim.OnWinFrame();
            } catch (Exception ex) {
                MarkErrored(claim, ex);
            }
        }

        private static void MarkErrored(MusicClaim claim, Exception ex) {
            claim.Errored = true;
            CWRMod.Instance?.Logger?.Error($"[MusicDirector] 认领 {claim.GetType().Name} 抛异常，永久停用该认领：{ex}");
        }

        private static void Log(string text) => CWRMod.Instance?.Logger?.Debug($"[MusicDirector] {text}");
    }

    /// <summary>写点壳：InnoVault 加载期单实例，挂在 DecideOnNewMusic 之后</summary>
    internal sealed class MusicDirectorAudio : IUpdateAudio
    {
        void IUpdateAudio.DecideMusic() => MusicDirector.Arbitrate();
    }

    /// <summary>生命周期壳：加载发现认领，世界卸载统一收场</summary>
    internal sealed class MusicDirectorSystem : ModSystem
    {
        public override void Load() => MusicDirector.Load(Mod);
        public override void Unload() => MusicDirector.Unload();
        public override void ClearWorld() => MusicDirector.ClearState();
        public override void OnWorldUnload() => MusicDirector.ClearState();
    }
}

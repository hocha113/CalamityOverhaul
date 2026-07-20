using CalamityOverhaul.Content.HackTimes.Targets;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 厉鬼扫描体：可扫不可骇（无任何协议声明支持 Wraith 位）。信息缺损纪律：
    /// 不暴露真名，威胁评估报 ERR://-∞；状态字段如实映射阶段（鬼律 8 扫描可映射 + 鬼律 14 科技失效）。
    /// (whoAmI, generation) 防槽位复用
    /// </summary>
    internal class WraithScannable : IHackTarget
    {
        public int ActorWho { get; }
        public ushort ActorGeneration { get; }

        public WraithScannable(int actorWho, ushort generation) {
            ActorWho = actorWho;
            ActorGeneration = generation;
        }

        /// <summary>解析回实体，失效/换代返回 null</summary>
        private WraithActor Resolve() {
            if (ActorWho < 0 || ActorWho >= ActorLoader.MaxActorCount) {
                return null;
            }
            return ActorLoader.Actors[ActorWho] is WraithActor wraith && wraith.Active
                && wraith.Generation == ActorGeneration ? wraith : null;
        }

        /// <summary>是否指向同一实体（高亮层比对用）</summary>
        public bool Matches(WraithActor wraith)
            => wraith != null && wraith.WhoAmI == ActorWho && wraith.Generation == ActorGeneration;

        #region IScannable

        public Vector2 WorldCenter => Resolve()?.Center ?? Vector2.Zero;

        public bool IsValid => Resolve() != null;

        /// <summary>不可骇入：科技失效范式（鬼律 14）</summary>
        public bool IsHackable => false;

        public int ScanRowCount => 6;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            WraithActor wraith = Resolve();
            if (wraith == null) {
                return;
            }

            //标识:未知厉鬼,不暴露真名
            labels[0] = HackTime.WraithScanName.Value;
            values[0] = HackTime.WraithScanNameValue.Value;
            colors[0] = HackTheme.TextBright;

            //TYPE:灵异对象
            labels[1] = HackTime.TypeLabel.Value;
            values[1] = HackTime.WraithScanType.Value;
            colors[1] = HackTheme.Danger;

            //THREAT:无法评估 ERR://-∞
            labels[2] = HackTime.ThreatLabel.Value;
            values[2] = HackTime.WraithScanThreat.Value;
            colors[2] = HackTheme.Danger;

            //状态:如实映射阶段
            labels[3] = HackTime.WraithScanStatus.Value;
            values[3] = ResolveStatus(wraith, out Color statusColor);
            colors[3] = statusColor;

            //数据完整性:0x??% 损坏
            labels[4] = HackTime.WraithScanIntegrity.Value;
            values[4] = HackTime.WraithScanIntegrityValue.Value;
            colors[4] = HackTheme.TextDim;

            //来源:查无此档
            labels[5] = HackTime.WraithScanOrigin.Value;
            values[5] = HackTime.WraithScanOriginValue.Value;
            colors[5] = HackTheme.TextDim;
        }

        /// <summary>状态映射：死机 → 被凝视中（本地玩家自评）→ 裂解/成形过渡 → 潜行追猎</summary>
        private static string ResolveStatus(WraithActor wraith, out Color color) {
            if (wraith.IsHalted) {
                color = HackTheme.Accent;
                return HackTime.WraithScanStatusHalt.Value;
            }
            Player local = Main.LocalPlayer;
            if (local != null && local.active && !local.dead
                && WraithSensors.IsGazedBy(local, wraith, wraith.Definition?.GazeRange ?? 900f)) {
                color = HackTheme.AccentAlt;
                return HackTime.WraithScanStatusWatched.Value;
            }
            if (wraith.Presence == WraithPresence.Dematerializing) {
                color = HackTheme.TextDim;
                return HackTime.WraithScanStatusDismember.Value;
            }
            if (wraith.Presence == WraithPresence.Materializing) {
                color = HackTheme.Uploading;
                return HackTime.WraithScanStatusMemory.Value;
            }
            color = HackTheme.Uploading;
            return HackTime.WraithScanStatusStalking.Value;
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<WraithTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                WraithActor wraith = Resolve();
                if (wraith == null) {
                    return Vector2.Zero;
                }
                return new Vector2(
                    Math.Max(wraith.Width, 32) * 0.6f + 28f,
                    Math.Max(wraith.Height, 32) * 0.6f + 28f);
            }
        }

        public string LockFrameTitle => IsValid ? HackTime.WraithScanNameValue.Value : string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            WraithActor wraith = Resolve();
            if (wraith == null) {
                return false;
            }
            text = ResolveStatus(wraith, out color);
            return true;
        }

        /// <summary>骇入对灵异永远无效</summary>
        public bool ApplyHack(QuickHackDef hack, Player caster) => false;

        public bool TargetEquals(IHackTarget other) {
            return other is WraithScannable w && w.ActorWho == ActorWho && w.ActorGeneration == ActorGeneration;
        }

        #endregion
    }
}

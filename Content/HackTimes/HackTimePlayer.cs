using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇入协议持有集，跟玩家存档。<br/>
    /// 与 <see cref="HackTimeAuthorityPlayer"/> 分家：那边是服务端上传队列，Initialize 会无条件清空，
    /// 持有集放进去会在每次进世界丢光
    /// </summary>
    internal sealed class HackTimePlayer : ModPlayer
    {
        private const string OwnedTag = "HackOwnedProtocols";
        //补发间隔与次数：上行是单向的，没有回执可等，只能在进世界后的头二十秒里多敲几下
        private const int SnapshotResendInterval = 300;
        private const int SnapshotResendCount = 4;

        private int snapshotResendTimer;
        private int snapshotResendsLeft;

        /// <summary>已持有协议的 FullName 集合，种子含全部出厂协议</summary>
        internal HashSet<string> OwnedProtocols = [];

        /// <summary>
        /// 服务端是否已收到该玩家的持有快照。
        /// 进世界时快照与首个骇入请求存在竞态，没收到就拿持有集拒请求会全员误杀
        /// </summary>
        internal bool OwnedSnapshotReceived;

        public override void Initialize() {
            OwnedProtocols = [];
            OwnedSnapshotReceived = false;
            snapshotResendTimer = 0;
            snapshotResendsLeft = 0;
        }

        public override void PlayerDisconnect() {
            OwnedProtocols = [];
            OwnedSnapshotReceived = false;
            snapshotResendsLeft = 0;
        }

        public override void OnEnterWorld() {
            HackProtocolOwned.EnsureSeed(this);
            HackTimeNetSync.SendOwnedSnapshot(Player);
            snapshotResendTimer = SnapshotResendInterval;
            snapshotResendsLeft = SnapshotResendCount;
        }

        //死亡时 PostUpdate 不跑，进世界即阵亡的场合要靠 UpdateDead 兜住
        public override void PostUpdate() => TickSnapshotResend();

        public override void UpdateDead() => TickSnapshotResend();

        /// <summary>
        /// 进世界那一帧的上行与服务端建号存在竞态：包先到，玩家还没生成，
        /// 服务端就永远收不到快照，持有校验整局静默放行。补发几次把这个窗口填掉
        /// </summary>
        private void TickSnapshotResend() {
            if (snapshotResendsLeft <= 0
                || Main.netMode != NetmodeID.MultiplayerClient
                || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (--snapshotResendTimer > 0) return;
            snapshotResendTimer = SnapshotResendInterval;
            snapshotResendsLeft--;
            HackTimeNetSync.SendOwnedSnapshot(Player);
        }

        public override void SaveData(TagCompound tag) {
            HackProtocolOwned.EnsureSeed(this);
            List<string> keys = OwnedProtocols
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .OrderBy(k => k, System.StringComparer.Ordinal)
                .ToList();
            tag[OwnedTag] = keys;
        }

        public override void LoadData(TagCompound tag) {
            OwnedProtocols = [];
            if (tag.TryGet(OwnedTag, out List<string> keys) && keys != null) {
                foreach (string key in keys) {
                    //协议被删或改名的旧档条目直接丢弃，别留一条点不出东西的幽灵持有
                    if (QuickHackDef.GetByFullName(key) != null) {
                        OwnedProtocols.Add(key);
                    }
                }
            }
            HackProtocolOwned.EnsureSeed(this);
        }
    }
}

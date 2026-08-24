using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>游戏模式种类</summary>
    internal enum GameModeKind : byte
    {
        /// <summary>残酷模式：解锁 BrutalNPCs 全部 AI 重制</summary>
        Brutal,
        /// <summary>修罗模式：残酷模式的上位，敌怪自适应免疫 + 伤害下限镜像</summary>
        Asura,
    }

    /// <summary>
    /// 游戏模式世界状态（残酷模式/修罗模式）。
    /// 世界级旗标：随世界存档持久化，进档由 tML 世界数据同步；
    /// 运行时切换走 <see cref="CWRMessageType.GameModeToggle"/>（客户端请求，服务端校验后广播）。
    /// AI 覆盖在 NPC 生成时绑定（InnoVault <c>NPCOverride.SetDefaults</c>），
    /// 因此 Boss 在场时拒绝切换，避免战中切换无效造成困惑
    /// </summary>
    internal class GameModeSystem : ModSystem
    {
        /// <summary>残酷模式已开启（世界级，主端权威）</summary>
        public static bool BrutalActive { get; internal set; }

        /// <summary>修罗模式已开启（依赖残酷模式，残酷关闭时强制随关）</summary>
        public static bool AsuraActive { get; internal set; }

        public override void ClearWorld() {
            BrutalActive = false;
            AsuraActive = false;
            GameModeCeremony.Reset();
        }

        public override void SaveWorldData(TagCompound tag) {
            if (BrutalActive) {
                tag[nameof(BrutalActive)] = true;
            }
            if (AsuraActive) {
                tag[nameof(AsuraActive)] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            BrutalActive = tag.TryGet(nameof(BrutalActive), out bool brutal) && brutal;
            AsuraActive = tag.TryGet(nameof(AsuraActive), out bool asura) && asura;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(BrutalActive);
            writer.Write(AsuraActive);
        }

        public override void NetReceive(BinaryReader reader) {
            BrutalActive = reader.ReadBoolean();
            AsuraActive = reader.ReadBoolean();
        }

        /// <summary>当前是否允许切换模式（Boss 在场时锁定）</summary>
        public static bool CanToggleNow() => !CWRWorld.HasBoss;

        /// <summary>
        /// 本地玩家请求切换指定模式。返回是否受理；
        /// 未受理（Boss 在场/依赖不满足）由调用方给出拒绝反馈
        /// </summary>
        public static bool RequestToggle(GameModeKind kind) {
            if (!CanToggleNow()) {
                return false;
            }

            bool target;
            if (kind == GameModeKind.Brutal) {
                target = !BrutalActive;
            }
            else {
                if (!BrutalActive) {
                    return false;//上位模式依赖残酷模式
                }
                target = !AsuraActive;
            }

            if (VaultUtils.isSinglePlayer) {
                Apply(kind, target);
                return true;
            }

            //联机：客户端只发意图，等服务端广播回执后再落地演出
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.GameModeToggle);
            packet.Write((byte)OpRequest);
            packet.Write((byte)kind);
            packet.Write(target);
            packet.Send();
            return true;
        }

        /// <summary>落地一次模式变更并播放演出；关残酷时修罗静默随关（只播残酷的谢幕词）</summary>
        private static void Apply(GameModeKind kind, bool enabled) {
            if (kind == GameModeKind.Brutal) {
                if (BrutalActive == enabled) {
                    return;
                }
                BrutalActive = enabled;
                if (!enabled) {
                    AsuraActive = false;
                }
            }
            else {
                if (AsuraActive == enabled) {
                    return;
                }
                AsuraActive = enabled;
            }
            GameModeCeremony.Play(kind, enabled);
        }

        private const byte OpRequest = 0;
        private const byte OpApply = 1;

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.GameModeToggle) {
                return;
            }

            byte op = reader.ReadByte();
            GameModeKind kind = (GameModeKind)reader.ReadByte();
            bool enabled = reader.ReadBoolean();

            if (op == OpRequest) {
                if (!VaultUtils.isServer) {
                    return;
                }
                //服务端权威校验：Boss 在场、修罗依赖、重复请求一律静默丢弃
                if (!CanToggleNow()) {
                    return;
                }
                if (kind == GameModeKind.Asura && enabled && !BrutalActive) {
                    return;
                }
                bool current = kind == GameModeKind.Brutal ? BrutalActive : AsuraActive;
                if (current == enabled) {
                    return;
                }

                Apply(kind, enabled);

                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.GameModeToggle);
                packet.Write(OpApply);
                packet.Write((byte)kind);
                packet.Write(enabled);
                packet.Send();
            }
            else if (op == OpApply) {
                Apply(kind, enabled);
            }
        }
    }
}

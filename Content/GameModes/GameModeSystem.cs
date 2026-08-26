using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>游戏模式种类（旗标与网络协议层面只有两种，毁灭是修罗的派生态）</summary>
    internal enum GameModeKind : byte
    {
        /// <summary>残酷模式：解锁 BrutalNPCs 全部 AI 重制</summary>
        Brutal,
        /// <summary>修罗模式：残酷模式的上位，敌怪自适应免疫 + 伤害下限镜像</summary>
        Asura,
    }

    /// <summary>
    /// 模式的表现脸：名字/台词/色板/纹样按脸取。
    /// 毁灭是修罗在天顶世界的呈现，只存在于表现层与数值层，不进旗标与网络
    /// </summary>
    internal enum GameModeFace : byte
    {
        /// <summary>残酷世界</summary>
        Brutal,
        /// <summary>修罗地狱</summary>
        Asura,
        /// <summary>死神永生（天顶世界的修罗）</summary>
        Annihilation,
    }

    /// <summary>
    /// 游戏模式世界状态（残酷模式/修罗模式）。
    /// 世界级旗标：随世界存档持久化，进档由 tML 世界数据同步；
    /// 运行时切换走 <see cref="GameModeToggleNet"/>（客户端请求，服务端校验后广播）。
    /// AI 覆盖在 NPC 生成时绑定（InnoVault <c>NPCOverride.SetDefaults</c>），
    /// 因此 Boss 在场时拒绝切换，避免战中切换无效造成困惑
    /// </summary>
    internal class GameModeSystem : ModSystem
    {
        /// <summary>残酷模式已开启（世界级，主端权威）</summary>
        public static bool BrutalActive { get; internal set; }

        /// <summary>修罗模式已开启（依赖残酷模式，残酷关闭时强制随关）</summary>
        public static bool AsuraActive { get; internal set; }

        /// <summary>毁灭模式：修罗在天顶世界的派生终相（隐藏难度，无独立旗标与网络协议）</summary>
        public static bool AnnihilationActive => AsuraActive && Main.zenithWorld;

        /// <summary>当前生效档位：0 无 / 1 残酷 / 2 修罗 / 3 毁灭</summary>
        public static int EffectiveTier {
            get {
                if (!BrutalActive) {
                    return 0;
                }
                if (!AsuraActive) {
                    return 1;
                }
                return Main.zenithWorld ? 3 : 2;
            }
        }

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

        /// <summary>模式种类在本世界的表现脸：天顶世界里修罗恒以毁灭示人（含休眠态）</summary>
        public static GameModeFace FaceOf(GameModeKind kind) {
            if (kind == GameModeKind.Brutal) {
                return GameModeFace.Brutal;
            }
            return Main.zenithWorld ? GameModeFace.Annihilation : GameModeFace.Asura;
        }

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
            ModPacket packet = CWRNetWork.GetPacket<GameModeToggleNet>();
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

        internal static void HandleNet(BinaryReader reader, int whoAmI) {
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

                ModPacket packet = CWRNetWork.GetPacket<GameModeToggleNet>();
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

    /// <summary>游戏模式切换信道：客户端请求，服务端校验落地后广播全端各自演出</summary>
    internal sealed class GameModeToggleNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => GameModeSystem.HandleNet(reader, whoAmI);
    }
}

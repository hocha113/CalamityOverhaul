using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>游戏模式种类（旗标与网络协议层面三种，毁灭是修罗的派生态）</summary>
    internal enum GameModeKind : byte
    {
        /// <summary>残酷模式：解锁 BrutalNPCs 全部 AI 重制</summary>
        Brutal,
        /// <summary>修罗模式：残酷模式的上位，敌怪自适应免疫 + 伤害下限镜像</summary>
        Asura,
        /// <summary>神匠模式：内容向模式，重铸原版武器与盔甲；独立开关，不依赖残酷与修罗</summary>
        GodSmith,
    }

    /// <summary>
    /// 模式的表现脸：名字/台词/色板/纹样按脸取。
    /// 毁灭是修罗在传奇世界（FTW）的呈现，只存在于表现层与数值层，不进旗标与网络
    /// </summary>
    internal enum GameModeFace : byte
    {
        /// <summary>残酷世界</summary>
        Brutal,
        /// <summary>修罗地狱</summary>
        Asura,
        /// <summary>死神永生（传奇世界的修罗）</summary>
        Annihilation,
        /// <summary>神工开物（神匠模式，无天顶变脸）</summary>
        GodSmith,
    }

    /// <summary>
    /// 游戏模式世界状态（残酷模式/修罗模式/神匠模式）。
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

        /// <summary>神匠模式已开启（独立世界旗标，不随残酷开关联动；内容向，不进 <see cref="EffectiveTier"/>）</summary>
        public static bool GodSmithActive { get; internal set; }

        /// <summary>毁灭模式：修罗在传奇世界（FTW，天顶种子亦满足）的派生终相（隐藏难度，无独立旗标与网络协议）</summary>
        public static bool AnnihilationActive => AsuraActive && Main.getGoodWorld;

        /// <summary>创建界面预选残酷后的待生效旗标，世界生成完毕由 <see cref="PostWorldGen"/> 消费</summary>
        internal static bool PendingBrutal;

        /// <summary>创建界面预选修罗后的待生效旗标（随残酷一并落地）</summary>
        internal static bool PendingAsura;

        /// <summary>当前生效档位：0 无 / 1 残酷 / 2 修罗 / 3 毁灭</summary>
        public static int EffectiveTier {
            get {
                if (!BrutalActive) {
                    return 0;
                }
                if (!AsuraActive) {
                    return 1;
                }
                return Main.getGoodWorld ? 3 : 2;
            }
        }

        public override void ClearWorld() {
            BrutalActive = false;
            AsuraActive = false;
            GodSmithActive = false;
            GameModeCeremony.Reset();
        }

        public override void SaveWorldData(TagCompound tag) {
            if (BrutalActive) {
                tag[nameof(BrutalActive)] = true;
            }
            if (AsuraActive) {
                tag[nameof(AsuraActive)] = true;
            }
            if (GodSmithActive) {
                tag[nameof(GodSmithActive)] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            BrutalActive = tag.TryGet(nameof(BrutalActive), out bool brutal) && brutal;
            AsuraActive = tag.TryGet(nameof(AsuraActive), out bool asura) && asura;
            GodSmithActive = tag.TryGet(nameof(GodSmithActive), out bool godSmith) && godSmith;
        }

        /// <summary>
        /// 世界头部旗标：写进 .twld 头部，菜单阶段世界选择列表经
        /// <see cref="Terraria.IO.WorldFileData.TryGetHeaderData{T}(out TagCompound)"/> 读取，
        /// 键与 <see cref="SaveWorldData"/> 同名同规（仅真值写入）
        /// </summary>
        public override void SaveWorldHeader(TagCompound tag) {
            if (BrutalActive) {
                tag[nameof(BrutalActive)] = true;
            }
            if (AsuraActive) {
                tag[nameof(AsuraActive)] = true;
            }
            if (GodSmithActive) {
                tag[nameof(GodSmithActive)] = true;
            }
        }

        /// <summary>创建界面预选的模式在此落地：直写旗标不走 Apply，新世界不播切换演出；消费后必清零</summary>
        public override void PostWorldGen() {
            if (PendingBrutal) {
                BrutalActive = true;
                AsuraActive = PendingAsura;
            }
            PendingBrutal = false;
            PendingAsura = false;
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(BrutalActive);
            writer.Write(AsuraActive);
            writer.Write(GodSmithActive);
        }

        public override void NetReceive(BinaryReader reader) {
            BrutalActive = reader.ReadBoolean();
            AsuraActive = reader.ReadBoolean();
            GodSmithActive = reader.ReadBoolean();
        }

        /// <summary>当前是否允许切换模式（Boss 在场时锁定）</summary>
        public static bool CanToggleNow() => !CWRWorld.HasBoss;

        /// <summary>指定模式旗标的当前值</summary>
        public static bool FlagOf(GameModeKind kind) => kind switch {
            GameModeKind.Brutal => BrutalActive,
            GameModeKind.Asura => AsuraActive,
            _ => GodSmithActive,
        };

        /// <summary>模式种类在本世界的表现脸：传奇世界（FTW）里修罗恒以毁灭示人（含休眠态），神匠无变脸</summary>
        public static GameModeFace FaceOf(GameModeKind kind) {
            if (kind == GameModeKind.Brutal) {
                return GameModeFace.Brutal;
            }
            if (kind == GameModeKind.GodSmith) {
                return GameModeFace.GodSmith;
            }
            return Main.getGoodWorld ? GameModeFace.Annihilation : GameModeFace.Asura;
        }

        /// <summary>
        /// 本地玩家请求切换指定模式。返回是否受理；
        /// 未受理（Boss 在场/依赖不满足）由调用方给出拒绝反馈
        /// </summary>
        public static bool RequestToggle(GameModeKind kind) {
            if (!CanToggleNow()) {
                return false;
            }

            //只有修罗是残酷的上位态，开启前提是残酷已开；残酷与神匠各自独立
            if (kind == GameModeKind.Asura && !AsuraActive && !BrutalActive) {
                return false;
            }
            bool target = !FlagOf(kind);

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

        /// <summary>落地一次模式变更并播放演出；关残酷时修罗静默随关（只播残酷的谢幕词），神匠独立不受影响</summary>
        private static void Apply(GameModeKind kind, bool enabled) {
            switch (kind) {
                case GameModeKind.Brutal:
                    if (BrutalActive == enabled) {
                        return;
                    }
                    BrutalActive = enabled;
                    if (!enabled) {
                        AsuraActive = false;
                    }
                    break;
                case GameModeKind.Asura:
                    if (AsuraActive == enabled) {
                        return;
                    }
                    AsuraActive = enabled;
                    break;
                default:
                    if (GodSmithActive == enabled) {
                        return;
                    }
                    GodSmithActive = enabled;
                    break;
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
                //服务端权威校验：Boss 在场、修罗的残酷依赖、重复请求一律拒绝；拒因留档便于诊断
                if (!CanToggleNow()) {
                    CWRMod.Instance.Logger.Info($"[GameMode] 拒绝切换 {kind}->{enabled}（来自 {whoAmI}）：Boss 在场锁定");
                    return;
                }
                if (kind == GameModeKind.Asura && enabled && !BrutalActive) {
                    CWRMod.Instance.Logger.Info($"[GameMode] 拒绝切换 {kind}->{enabled}（来自 {whoAmI}）：依赖残酷模式未开启");
                    return;
                }
                if (FlagOf(kind) == enabled) {
                    CWRMod.Instance.Logger.Info($"[GameMode] 拒绝切换 {kind}->{enabled}（来自 {whoAmI}）：与当前状态重复");
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
                //回执只许客户端落地：服务端拒收 OpApply，堵死伪造回执绕过权威校验的通道
                if (VaultUtils.isServer) {
                    return;
                }
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

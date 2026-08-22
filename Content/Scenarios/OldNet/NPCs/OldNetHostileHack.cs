using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.HackTimes.PvP;
using CalamityOverhaul.Content.HackTimes.PvP.Protocols;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// PvP 骇入协议武器化桥（单人限定）。<br/>
    /// 正规管线是"攻击方请求 → 服务端授账 → DefenderApply → 防守方本机落地"，
    /// NPC 施放没有攻击方客户端与授账环节，这里绕开请求/授账/网络三层，
    /// 直接构造 effect 走落地面 <see cref="PlayerHackLedger.TryApplyLocal"/>；
    /// 效果时钟框架只在 MP 客户端驱动（PlayerHackNet.PostUpdateEverything），
    /// 单人由 <see cref="DriveClock"/> 代驱动。<br/>
    /// TODO(MP)：联机化需要走服务端授账 + DefenderApply 包路径，
    /// 且 ActivationId 高位段要与服务器分配段做冲突治理，M1 严格单人门控，不实装
    /// </summary>
    internal static class OldNetHostileHack
    {
        //独立高位自增段：避开将来服务器分配段；long 高位起步保证 > 0 且不与玩家侧冲突
        private static long nextActivationId = 1L << 40;

        /// <summary>
        /// 对目标玩家施加一条协议（防守方本机落地）。单人限定；
        /// CasterName 填 ICE 显示名（死讯/HUD 文案反查施法者名的协议靠它绕开玩家假设），
        /// CasterIndex = -1（表现层 ResolveActive 越界即判 null，降级为"信号丢失"结点）
        /// </summary>
        internal static bool TryCast(Player target, PlayerHackDef def, string casterName) {
            if (Main.netMode != NetmodeID.SinglePlayer || def == null
                || target == null || !target.active || target.dead) {
                return false;
            }
            PlayerHackEffect effect = new() {
                ActivationId = nextActivationId++,
                SlotIndex = def.SlotIndex,
                Hack = def,
                CasterIndex = -1,
                CasterName = casterName ?? string.Empty,
                Duration = def.GetDuration(),
            };
            return target.GetModPlayer<PlayerHackLedger>().TryApplyLocal(effect);
        }

        /// <summary>
        /// 单人代驱动帐本时钟。挂 OldNetPlayer.PostUpdate 且不做旧网门控
        /// 带着在册效果登出回主世界，效果也要走完而不是冻结；
        /// 严格 SinglePlayer 门控防将来 MP 双驱（双驱 = 时长减半级 bug）。
        /// SendLedgerReport 在非 MP 客户端自行早退，帐本空转开销可忽略
        /// </summary>
        internal static void DriveClock(Player player) {
            if (Main.netMode != NetmodeID.SinglePlayer
                || player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            PlayerHackLedger ledger = player.GetModPlayer<PlayerHackLedger>();
            if (ledger.ActiveEffects.Count == 0) {
                return;
            }
            ledger.TickLocalTruth();
        }

        /// <summary>
        /// 按威胁档位抽一条协议。M1a 先打通 GaugePollution；
        /// M1c 扩池（MapBlackout/CooldownInject/StealthStrip/CyberwareOffline/MeltdownBrand）
        /// </summary>
        internal static PlayerHackDef PickForTier(int tier, bool elite) {
            //T4 精英才配熔断标记（等价处决倒计时，狠度只给终局）
            if (elite && tier >= 4 && Main.rand.NextBool(3)) {
                PlayerHackDef brand = QuickHackDef.Get<MeltdownBrand>();
                if (brand != null) {
                    return brand;
                }
            }
            //T3+ 掺入狠招
            if (tier >= 3 && Main.rand.NextBool(2)) {
                PlayerHackDef pick = Main.rand.Next(3) switch {
                    0 => QuickHackDef.Get<CooldownInject>(),
                    1 => QuickHackDef.Get<StealthStrip>(),
                    _ => QuickHackDef.Get<CyberwareOffline>(),
                };
                if (pick != null) {
                    return pick;
                }
            }
            //T2 基础池：读数污染 / 地图熄灭
            if (tier >= 2 && Main.rand.NextBool(3)) {
                PlayerHackDef blackout = QuickHackDef.Get<MapBlackout>();
                if (blackout != null) {
                    return blackout;
                }
            }
            return QuickHackDef.Get<GaugePollution>();
        }
    }
}

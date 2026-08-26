using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 三符位叠算出的战斗档。空绳恒为 <see cref="Identity"/>，不进任何特殊分支；
    /// 倍率字段由 <see cref="KikasaTalismanDefinition.ModifyProfile"/> 逐位叠乘
    /// </summary>
    public struct KikasaTalismanProfile
    {
        /// <summary>墨雨节拍间隔倍率（&lt;1 更密，霖 0.80、沛 1.08）</summary>
        public float RainTempoMul;
        /// <summary>单枚墨滴伤害倍率（霖 0.94、潦 0.95）</summary>
        public float DropDamageMul;
        /// <summary>墨洼寿命倍率（潦 1.50）</summary>
        public float PuddleLifeMul;
        /// <summary>墨洼半径倍率（潦 1.30）</summary>
        public float PuddleRadiusMul;
        /// <summary>倒撑蓄墨速率倍率（沛 1.35）</summary>
        public float ChargeRateMul;
        /// <summary>墨泉终幕伤害倍率（沛 1.20）</summary>
        public float GeyserDamageMul;
        /// <summary>撑伞上浮时长倍率（霎 0.25）</summary>
        public float RiseFramesMul;
        /// <summary>悬点高度倍率（霄 2.0）</summary>
        public float HoverHeightMul;
        /// <summary>墨洼本体伤害倍率（渍 0.75）</summary>
        public float PuddleDamageMul;
        /// <summary>墨瀑宽度倍率（虹 0.90）</summary>
        public float PourWidthMul;
        /// <summary>墨瀑冲刷时长倍率（泷 0.80）</summary>
        public float PourSustainMul;
        /// <summary>潦「积潦」：未至湖倾档也让大滴落地积洼（湖倾档本就积洼，不受影响）</summary>
        public bool PuddleUnlock;
        /// <summary>禁止墨洼合并续命（霜「不可叠寿」）</summary>
        public bool PuddleNoRefresh;

        /// <summary>严格基准档：所有倍率恒等，开关全关</summary>
        public static KikasaTalismanProfile Identity => new() {
            RainTempoMul = 1f,
            DropDamageMul = 1f,
            PuddleLifeMul = 1f,
            PuddleRadiusMul = 1f,
            ChargeRateMul = 1f,
            GeyserDamageMul = 1f,
            RiseFramesMul = 1f,
            HoverHeightMul = 1f,
            PuddleDamageMul = 1f,
            PourWidthMul = 1f,
            PourSustainMul = 1f,
        };
    }

    /// <summary>
    /// 唤雨符效果层统一入口：战斗侧一律按归属玩家的
    /// <see cref="KikasaTalismanPlayer.Talismans"/> 解析（持伞时生效）；
    /// 符位表经玩家快照同步到各端，解析结果一致
    /// </summary>
    internal static class KikasaTalismanCombat
    {
        /// <summary>按归属玩家解析三符位合成档；未持伞返回 Identity</summary>
        public static KikasaTalismanProfile Resolve(Player owner) {
            KikasaTalismanProfile profile = KikasaTalismanProfile.Identity;
            if (owner == null || owner.HeldItem?.type != ModContent.ItemType<KikasaItem>()
                || !owner.TryGetModPlayer(out KikasaTalismanPlayer ktp)) {
                return profile;
            }
            for (int slot = 0; slot < KikasaTalismanStore.SlotCount; slot++) {
                KikasaTalismanRegistry.GetHung(ktp.Talismans, slot)?.ModifyProfile(ref profile);
            }
            return profile;
        }
    }
}

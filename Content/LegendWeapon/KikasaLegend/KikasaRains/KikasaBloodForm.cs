using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 血湖形态普攻的统一门与口径。域外与鬼雨形态是水墨笔触,血湖满水稳态才是浓血:
    /// 滴换血珠材质、首次入水在湖面起血柱、倒撑蓄墨从湖里抽血、三泉换血柱材质。
    /// 谓词读 <see cref="KikasaDomainPlayer.LakeAbilityReady"/>(Open 稳态、满水、非收合/翻转、画面不在梦侧),
    /// 涨水/收域/翻转仪式/鬼梦/鬼雨一律退回墨形态;各端从同步快照自算同一答案。
    /// 蓝图:Doc/plans/KikasaBloodForm/DESIGN.md
    /// </summary>
    internal static class KikasaBloodForm
    {
        /// <summary>血柱高度区间(px):吃入水动能插值,只有全速俯冲砸进水里的珠才顶到上限</summary>
        public const float ColumnHeightMin = 140f;
        public const float ColumnHeightMax = 300f;

        /// <summary>血柱宽度区间(px)</summary>
        public const float ColumnWidthMin = 34f;
        public const float ColumnWidthMax = 52f;

        /// <summary>血柱伤害=珠伤害×此值(额外伤害,一柱对同一目标只结算一次);散射小柱再减半</summary>
        public const float ColumnDamageMul = 0.4f;
        public const float ScatterColumnDamageMul = 0.5f;

        /// <summary>自卫滴(自动索敌)与瀑缘散射滴的柱高倍率:火力让位于亲手指挥的雨</summary>
        public const float AutoColumnHeightMul = 0.6f;
        public const float ScatterColumnHeightMul = 0.4f;

        /// <summary>散射小柱的宽度倍率</summary>
        public const float ScatterColumnWidthMul = 0.7f;

        /// <summary>血形态下三泉的柱高/柱宽倍率:满蓄终幕要压过普攻血柱的 300 上限</summary>
        public const float GeyserBloodHeightMul = 1.5f;
        public const float GeyserBloodWidthMul = 1.2f;

        /// <summary>在场血柱上限,超限不再生成(不顶掉旧柱);齐掷波入湖一排柱也够用</summary>
        public const int MaxColumnsAlive = 16;

        /// <summary>抽血血索的最大长度(px),伞离湖更远只留涟漪不画索</summary>
        public const float SiphonMaxLenPx = 420f;

        /// <summary>该玩家此刻是否处于血湖形态(满水血湖稳态、非鬼雨)</summary>
        public static bool Active(Player owner)
            => owner?.active == true
            && owner.TryGetModPlayer(out KikasaDomainPlayer kdp)
            && kdp.LakeAbilityReady && !kdp.IsRainForm;

        /// <summary>按玩家下标判血形态</summary>
        public static bool Active(int ownerWho)
            => ownerWho >= 0 && ownerWho < Main.maxPlayers && Active(Main.player[ownerWho]);

        /// <summary>血形态在效时取出湖态,否则 null</summary>
        public static KikasaDomainPlayer LakeOf(Player owner)
            => Active(owner) ? owner.GetModPlayer<KikasaDomainPlayer>() : null;

        //==================== 音效帧预算 ====================

        //齐掷波入湖同帧七八根柱,重水花与闷鼓只放头几声(同 KikasaLakeNPC 的入水花纪律)
        private static uint soundStamp;
        private static int soundLeft;

        /// <summary>本帧还有没有血柱音效名额,有则消耗一个</summary>
        public static bool TakeSoundBudget(int perFrame = 3) {
            if (soundStamp != Main.GameUpdateCount) {
                soundStamp = Main.GameUpdateCount;
                soundLeft = perFrame;
            }
            if (soundLeft <= 0) {
                return false;
            }
            soundLeft--;
            return true;
        }
    }
}

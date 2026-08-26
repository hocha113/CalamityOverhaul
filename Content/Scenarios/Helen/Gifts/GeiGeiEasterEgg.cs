using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Helen.Gifts
{
    /// <summary>
    /// 给给彩蛋:神吞战中本人受击过多仍获胜,战后海伦提起一位更加恐怖的勇士
    /// </summary>
    internal sealed class GeiGeiEasterEgg : HelenBossGiftNarrative, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Helen";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }

        public override StyleId DefaultStyle => "Sea";

        public override int TargetBossId => CWRID.NPC_DevourerofGodsHead;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "这一战真是凶险");
            L1 = this.GetLocalization(nameof(L1), () => "但有记载，当年有位叫给给的勇士更加恐怖");
        }

        protected override void Build(NarrativeComposer n) {
            n
             .Say("Helen", "Solemn", L0.Value)
             .Say("Helen", "Naughty", L1.Value);
        }

        //死亡瞬间在各客户端各自求值:只有这一场挨打够多的玩家才登记彩蛋
        protected override bool CanSpawned()
            => GeiGeiHitLedgerPlayer.LocalHitsThisFight >= GeiGeiHitLedgerPlayer.HitThreshold;

        protected override bool IsGiftCompleted()
            => HalibutStorySync.ReadGift(d => d.GeiGeiEasterEgg, d => d.GeiGeiEasterEgg);

        protected override void MarkGiftCompleted()
            => HalibutStorySync.WriteGift(d => d.GeiGeiEasterEgg = true, d => d.GeiGeiEasterEgg = true);
    }

    /// <summary>
    /// 本场神吞战斗的受击账本:纯本地演出判定,不落存档、不发包
    /// </summary>
    internal sealed class GeiGeiHitLedgerPlayer : ModPlayer
    {
        /// <summary>受击多少次算「过多」</summary>
        public const int HitThreshold = 30;

        private int hitsThisFight;
        private bool dogWasPresent;

        /// <summary>本地玩家在当前这场神吞战斗中的受击次数</summary>
        public static int LocalHitsThisFight
            => Main.LocalPlayer.GetModPlayer<GeiGeiHitLedgerPlayer>().hitsThisFight;

        public override void OnHurt(Player.HurtInfo info) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (DoGPresent()) {
                hitsThisFight++;
            }
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            //神吞头「从无到有」视作新一场战斗,清掉上一场的账;逃逸/团灭后的下一次出现同样走这里重置
            bool present = DoGPresent();
            if (present && !dogWasPresent) {
                hitsThisFight = 0;
            }
            dogWasPresent = present;
        }

        //灾厄缺席时 CWRID 返回 0,不做无效扫描
        private static bool DoGPresent()
            => CWRID.NPC_DevourerofGodsHead > 0 && NPC.AnyNPCs(CWRID.NPC_DevourerofGodsHead);
    }
}

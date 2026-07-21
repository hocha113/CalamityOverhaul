using CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs
{
    //便携式量子通讯装置
    internal class PQCD : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/PQCD";

        /// <summary>需要的信号塔数量</summary>
        public const int RequiredSignalTowers = 10;

        public static LocalizedText NeedSignalTowersText { get; private set; }
        public static LocalizedText SignalTowerProgressText { get; private set; }

        public override void SetStaticDefaults() {
            NeedSignalTowersText = this.GetLocalization(nameof(NeedSignalTowersText), () => "量子纠缠链路未完成！需要部署全部 {0} 座信号塔才能使用");
            SignalTowerProgressText = this.GetLocalization(nameof(SignalTowerProgressText), () => "信号塔部署进度: {0}/{1}");
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            //UseSound交给商店UI,避双击
            Item.UseSound = null;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 50);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            int completedCount = GetCompletedTowerCount();

            var progressLine = new TooltipLine(
                Mod,
                "SignalTowerProgress",
                string.Format(SignalTowerProgressText.Value, completedCount, RequiredSignalTowers)
            );

            if (completedCount >= RequiredSignalTowers) {
                progressLine.OverrideColor = Color.Lime;
            }
            else if (completedCount > 0) {
                progressLine.OverrideColor = Color.Yellow;
            }
            else {
                progressLine.OverrideColor = Color.Gray;
            }

            tooltips.Add(progressLine);

            if (completedCount < RequiredSignalTowers) {
                var warningLine = new TooltipLine(
                    Mod,
                    "SignalTowerWarning",
                    string.Format(NeedSignalTowersText.Value, RequiredSignalTowers)
                );
                warningLine.OverrideColor = Color.OrangeRed;
                tooltips.Add(warningLine);
            }
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                int completedCount = GetCompletedTowerCount();

                if (completedCount < RequiredSignalTowers) {
                    string warningText = string.Format(
                        NeedSignalTowersText.Value,
                        RequiredSignalTowers
                    );
                    string progressText = string.Format(
                        SignalTowerProgressText.Value,
                        completedCount,
                        RequiredSignalTowers
                    );

                    CombatText.NewText(
                        player.getRect(),
                        Color.OrangeRed,
                        warningText,
                        dramatic: true
                    );

                    CombatText.NewText(
                        player.getRect(),
                        Color.Yellow,
                        progressText,
                        dramatic: false
                    );

                    SoundEngine.PlaySound(SoundID.MenuClose with {
                        Volume = 0.7f,
                        Pitch = -0.3f
                    }, player.Center);

                    return true;
                }

                DraedonShopUI.Instance.Toggle();
            }
            return true;
        }

        private static int GetCompletedTowerCount() {
            if (!SignalTowerTargetManager.IsGenerated) {
                return 0;
            }

            int count = 0;
            foreach (var targetPoint in SignalTowerTargetManager.TargetPoints) {
                if (targetPoint.IsCompleted) {
                    count++;
                }
            }

            return count;
        }
    }
}

using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs
{
    //便携式量子通讯装置
    internal class PQCD : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/PQCD";

        /// <summary>
        /// 需要的信号塔数量
        /// </summary>
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
            Item.UseSound = CWRSound.ButtonZero;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 50);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //计算已完成的信号塔数量
            int completedCount = GetCompletedTowerCount();

            //添加进度提示
            var progressLine = new TooltipLine(
                Mod,
                "SignalTowerProgress",
                string.Format(SignalTowerProgressText.Value, completedCount, RequiredSignalTowers)
            );

            //根据完成度设置颜色
            if (completedCount >= RequiredSignalTowers) {
                progressLine.OverrideColor = Color.Lime; //全部完成，绿色
            }
            else if (completedCount > 0) {
                progressLine.OverrideColor = Color.Yellow; //部分完成，黄色
            }
            else {
                progressLine.OverrideColor = Color.Gray; //未开始，灰色
            }

            tooltips.Add(progressLine);

            //如果未完成，添加警告提示
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
                //检查是否所有信号塔都已部署
                int completedCount = GetCompletedTowerCount();

                if (completedCount < RequiredSignalTowers) {
                    //未完成所有信号塔部署，显示警告并拒绝打开
                    string warningText = string.Format(
                        NeedSignalTowersText.Value,
                        RequiredSignalTowers
                    );
                    string progressText = string.Format(
                        SignalTowerProgressText.Value,
                        completedCount,
                        RequiredSignalTowers
                    );

                    //显示战斗文本提示
                    CombatText.NewText(
                        player.getRect(),
                        Color.OrangeRed,
                        warningText,
                        dramatic: true
                    );

                    //稍后显示进度提示
                    CombatText.NewText(
                        player.getRect(),
                        Color.Yellow,
                        progressText,
                        dramatic: false
                    );

                    //播放失败音效
                    SoundEngine.PlaySound(SoundID.MenuClose with {
                        Volume = 0.7f,
                        Pitch = -0.3f
                    }, player.Center);

                    return true;
                }

                //所有信号塔已部署，打开商店界面
                DraedonShopUI.Instance.Active = !DraedonShopUI.Instance.Active;
            }
            return true;
        }

        /// <summary>
        /// 获取已完成的信号塔数量
        /// </summary>
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

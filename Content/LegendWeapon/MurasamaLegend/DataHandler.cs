using Terraria;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.InWorldBossPhase;
using static CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaOverride;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    internal class DataHandler
    {
        private static void ModifyWallSelect(int index, ref int damage) {
            if (index != 4) {
                return;
            }
            //击败了史莱姆之神但是没有击败肉山
            if (Level4 && !Main.hardMode) {
                damage += 6;
            }
            else {
                damage += 0;
            }
        }

        private static void ModifyMechBossSelect(int index, ref int damage) {
            if (index != 5) {
                return;
            }//index是5，执行到这里说明刚进入困难模式

            if (!Level5) {
                return;
            }

            //如果已经击败了灾厄三王，下面枚举判断所有机械Boss的选择
            do {
                if (!NPC.downedMechBoss1) {//毁灭者
                    damage += 4;
                    break;
                }
                if (!NPC.downedMechBoss2) {//双子魔眼
                    damage += 8;
                    break;
                }
                if (!NPC.downedMechBoss3) {//机械统帅
                    damage += 10;
                    break;
                }
            } while (false);
        }

        public static void DamageModify(Item item, Player player, ref StatModifier damage) {
            int onDamage = GetOnDamage(item);
            ModifyWallSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyMechBossSelect(item.CWR().LegendData.Level, ref onDamage);
            CWRUtils.ModifyLegendWeaponDamageFunc(player, item, onDamage, GetStartDamage, ref damage);
        }
    }
}

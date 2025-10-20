using System;
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
            //完成试炼5但是没有进入困难模式
            if (Level4 && !Main.hardMode) {
                damage += 3;
            }
            else {
                return;
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
                    damage += 5;
                    break;
                }
                if (!NPC.downedMechBoss2) {//双子魔眼
                    damage += 10;
                    break;
                }
                if (!NPC.downedMechBoss3) {//机械统帅
                    damage += 15;
                    break;
                }
            } while (false);
        }

        private static void ModifyGolemSelect(int index, ref int damage) {
            if (index != 7) {
                return;
            }

            if (Level7) {//石巨人
                damage += 20;
            }
            if (Downed14.Invoke()) {
                damage += 5;
            }
            if (Downed15.Invoke()) {
                damage += 5;
            }
            if (Downed16.Invoke()) {
                damage += 10;
            }
            else {
                return;
            }
            
        }
        public static void DamageModify(Item item, Player player, ref StatModifier damage) {
            int onDamage = GetOnDamage(item);
            ModifyWallSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyMechBossSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyGolemSelect(item.CWR().LegendData.Level, ref onDamage);
            CWRUtils.ModifyLegendWeaponDamageFunc(item, onDamage, GetStartDamage, ref damage);
            float meleeSpeedRoad = player.GetWeaponAttackSpeed(item);
            float SpeedToMelee = 1f + (float)Math.Log(meleeSpeedRoad) * 0.48f;
            damage *= SpeedToMelee;
        }
    }
}

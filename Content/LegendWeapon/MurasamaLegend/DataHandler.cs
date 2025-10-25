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
            if (Level4 && !Main.hardMode) {
                damage += 7;
            }
        }

        private static void ModifyMechBossSelect(int index, ref int damage) {
            if (index != 5) {
                return;
            }
            if (!Level5) {
                return;
            }
            
            if (NPC.downedMechBoss1) {//毁灭者
                damage += 7;
            }
            if (NPC.downedMechBoss2) {//双子魔眼
                damage += 8;
            }
            if (NPC.downedMechBoss3) {//机械统帅
                damage += 10;
            }
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
        }
        private static void ModifyAfterMoonSelect(int index, ref int damage) {
            if (index != 9) {
                return;
            }

            if (Level9) {
                damage += 50;
            }
            if (Downed23.Invoke()) {
                damage += 70;
            }
        }
        private static void ModifyAfterMechSelect(int index, ref int damage) {
            if (index != 6) {
                return;
            }

            if (Level6) {
                damage += 20;
            }
            if (VDownedV7.Invoke()) {
                damage += 15;
            }
        }
        public static void DamageModify(Item item, Player player, ref StatModifier damage) {
            int onDamage = GetOnDamage(item);
            ModifyWallSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyMechBossSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyGolemSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyAfterMoonSelect(item.CWR().LegendData.Level, ref onDamage);
            ModifyAfterMechSelect(item.CWR().LegendData.Level, ref onDamage);
            CWRUtils.ModifyLegendWeaponDamageFunc(item, onDamage, GetStartDamage, ref damage);
            float meleeSpeedRoad = player.GetWeaponAttackSpeed(item);
            float SpeedToMelee = 1f + (float)Math.Log(meleeSpeedRoad) * 0.48f;
            damage *= SpeedToMelee;
        }
    }
}

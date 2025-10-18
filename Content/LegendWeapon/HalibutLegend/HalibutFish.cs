using CalamityOverhaul.Common;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutFish : ModPlayer
    {
        /// <summary>
        /// 玩家是否曾经通过钓鱼获得过大比目鱼（首次保底逻辑用）
        /// </summary>
        public bool HasCaughtHalibut;
        public override void SaveData(TagCompound tag) {
            //保存是否曾钓到大比目鱼
            tag["HasCaughtHalibut"] = HasCaughtHalibut;
        }
        public override void LoadData(TagCompound tag) {
            //读取是否曾钓到大比目鱼
            HasCaughtHalibut = tag.GetBool("HasCaughtHalibut");
        }
        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (!CWRServerConfig.Instance.WeaponOverhaul || attempt.inHoney || attempt.inLava) {
                return;
            }

            if (HasCaughtHalibut) {
                if (Main.rand.NextBool(500)) {
                    itemDrop = HalibutOverride.ID;
                }
            }
            else {
                if (Main.rand.NextBool(10)) {
                    itemDrop = HalibutOverride.ID;
                    HasCaughtHalibut = true;
                }
            }
        }
    }
}

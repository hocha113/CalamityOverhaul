using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RangedModify
{
    internal class GlobalGun : GlobalRanged
    {
        public override void PreInOwnerByFeederGun(BaseFeederGun gun) {
            if (CWRServerConfig.Instance.MagazineSystem && gun.Owner.AdrenalineMode()) {
                gun.Owner.AddBuff(ModContent.BuffType<FrenziedMachineSoul>(), 10086);//For The Emperor!!!
            }
        }

        public override bool? CanUpdateMagazine(BaseFeederGun gun) {
            if (CWRServerConfig.Instance.MagazineSystem
                && gun.Owner.AdrenalineMode()) {//在肾上腺素下不会消耗弹匣子弹
                return false;
            }
            return base.CanUpdateMagazine(gun);
        }

        /// <summary>
        /// 关于肾上腺素时的枪械伤害缩放调整
        /// </summary>
        /// <param name="player"></param>
        /// <param name="modifiers"></param>
        public static void AdrenalineByGunDamageAC(Player player, ref NPC.HitModifiers modifiers) {
            if (!CWRServerConfig.Instance.MagazineSystem) {
                return;
            }

            CWRPlayer cwrPlayer = player.CWR();
            if (!cwrPlayer.TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                return;
            }

            if (gun.Item == null || gun.Item.type == ItemID.None) {
                return;
            }

            //由这个计算一个枪械在一个发射周期内多久可以打完弹匣
            float value = gun.FireTime * gun.Item.CWR().AmmoCapacity + gun.KreloadMaxTime;
            float value2 = 220;//先假设一个肾上腺素的逼近值，一个周期下来大概4秒
            float value3 = value2 / value;//最后除出来的值就是这个枪械的伤害溢出系数
            //伤害溢出小于或等于1的枪械说明不能在这个机制下吃到什么便宜，所以不管，
            //而大于1的枪械就或多或少的会因为火力强但持续力差等原因在这个机制上吃到便宜，
            //因为这个机制会弥补他们持续力差的缺点，导致超模
            if (value3 > 1) {//这里将伤害除以溢出的系数，以达到平衡的目的
                modifiers.FinalDamage /= value3;
            }
        }
    }
}

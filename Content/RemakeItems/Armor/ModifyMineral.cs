using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Others;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Armor
{
    //秘银
    internal class ModifyMythrilHood : CWRItemOverride
    {
        public override int TargetID => ItemID.MythrilHat;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization("UpdateArmorText",
            () => "During the reloading process, taking damage will release a large number of Mythril Sparks.");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.MythrilChainmail || legs.type != ItemID.MythrilGreaves) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "10%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.1f;

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                    for (int i = 0; i < 10; i++) {
                        int proj = Projectile.NewProjectile(player.FromObjectGetParent()
                    , player.Center + CWRUtils.randVr(124), player.velocity / 2, ModContent.ProjectileType<MythrilFlare>(), 30, 2, player.whoAmI);
                        Main.projectile[proj].DamageType = DamageClass.Ranged;
                    }
                }
            }
        }
    }
    //山铜
    internal class ModifyOrichalcumHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.OrichalcumHelmet;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization("UpdateArmorText",
            () => "During the reloading process, taking damage will cause a large number of petals to scatter and splatter.");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.OrichalcumBreastplate || legs.type != ItemID.OrichalcumLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "10%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.1f;

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[ModContent.ProjectileType<Hit>()] > 0) {
                    for (int i = 0; i < 16; i++) {
                        int proj = Projectile.NewProjectile(player.FromObjectGetParent()
                        , player.Center + CWRUtils.randVr(124), CWRUtils.randVr(24, 32), ProjectileID.FlowerPetal, 30, 2, player.whoAmI);
                        Main.projectile[proj].DamageType = DamageClass.Ranged;
                    }
                }
            }
        }
    }
    //钯金
    internal class ModifyPalladiumHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.PalladiumHelmet;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization("UpdateArmorText",
            () => "During the reloading process, health will be restored more quickly.");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.PalladiumBreastplate || legs.type != ItemID.PalladiumLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "10%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.1f;

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                player.lifeRegen += 12;
            }
        }
    }
    //钴蓝
    internal class ModifyCobaltMask : CWRItemOverride
    {
        public override int TargetID => ItemID.CobaltMask;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization("UpdateArmorText",
            () => "During the reloading process, 15% increased movement speed.");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.CobaltBreastplate || legs.type != ItemID.CobaltLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "10%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.1f;

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                player.moveSpeed += 0.15f;
            }
        }
    }
    //钛金
    internal class ModifyTitaniumHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.TitaniumHelmet;
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.TitaniumBreastplate || legs.type != ItemID.TitaniumLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "20%";
            player.CWR().KreloadTimeIncrease -= 0.2f;
        }
    }
    //精金
    internal class ModifyAdamantiteMask : CWRItemOverride
    {
        public override int TargetID => ItemID.AdamantiteMask;
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.AdamantiteBreastplate || legs.type != ItemID.AdamantiteLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "20%";
            player.CWR().KreloadTimeIncrease -= 0.2f;
        }
    }
    //精金
    internal class ModifyHallowedHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.HallowedHelmet;
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.HallowedPlateMail || legs.type != ItemID.HallowedGreaves) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "20%";
            player.CWR().KreloadTimeIncrease -= 0.2f;
        }
    }
    //叶绿
    internal class ModifyChlorophyteHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.ChlorophyteHelmet;
        public static int SpawnTime;
        public static LocalizedText UpdateArmorText { get; private set; }
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void SetStaticDefaults() => UpdateArmorText = this.GetLocalization("UpdateArmorText",
            () => "During the reloading process, a lingering spore cloud will be released.");
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.ChlorophytePlateMail || legs.type != ItemID.ChlorophyteGreaves) {
                return;
            }

            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "20%";
            player.setBonus += "\n" + UpdateArmorText.Value;
            player.CWR().KreloadTimeIncrease -= 0.2f;

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                if (player.whoAmI == Main.myPlayer && ++SpawnTime > 3) {
                    int proj = Projectile.NewProjectile(player.FromObjectGetParent()
                    , player.Center + CWRUtils.randVr(124), CWRUtils.randVr(4, 10), ProjectileID.SporeCloud, 60, 2, player.whoAmI);
                    Main.projectile[proj].DamageType = DamageClass.Ranged;
                    SpawnTime = 0;
                }
            }
            else {
                SpawnTime = 0;
            }
        }
    }
    //星璇
    internal class ModifyVortexHelmet : CWRItemOverride
    {
        public override int TargetID => ItemID.VortexHelmet;
        public override bool CanLoadLocalization => false;
        public override bool DrawingInfo => false;
        public override void UpdateArmorByHead(Player player, Item body, Item legs) => UpdateArmor(player, body, legs);
        public static void UpdateArmor(Player player, Item body, Item legs) {
            if (body.type != ItemID.VortexBreastplate || legs.type != ItemID.VortexLeggings) {
                return;
            }
            player.setBonus += "\n" + CWRLocText.Instance.KreloadTimeLessenText + "30%";
            player.CWR().KreloadTimeIncrease -= 0.3f;
        }
    }
}

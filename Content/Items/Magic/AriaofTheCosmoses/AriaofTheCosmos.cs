using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 寰宇咏叹调
    internal class AriaofTheCosmos : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";
        public readonly static string[] FullItems = ["0", "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0", "0",
            "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0",
            "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0",
            "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0",
            "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/Rock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock",
            "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0",
            "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0",
            "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "CalamityMod/MiracleMatter", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0",
            "0", "0", "0", "0", "CalamityOverhaul/StarflowPlatedBlock", "0", "0", "0", "0",
            "CalamityOverhaul/AriaofTheCosmos"
        ];

        public override void SetStaticDefaults() {
            SupertableUI.ModCall_OtherRpsData_StringList.Add(FullItems);
        }

        /// <summary>Q技能冷却(帧) 2秒</summary>
        public int QSkillCooldown;
        /// <summary>R技能冷却(帧) 3秒</summary>
        public int RSkillCooldown;
        private const int QSkillMaxCooldown = 120;
        private const int RSkillMaxCooldown = 180;

        public override void SetDefaults() {
            Item.damage = 285;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 20;
            Item.width = 52;
            Item.height = 52;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 5f;
            Item.value = Item.buyPrice(gold: 50);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AccretionDisk>();
            Item.shootSpeed = 0f;
            Item.channel = true;
            Item.CWR().OmigaSnyContent = FullItems;
        }

        //右键：蓄力压扁吸积盘
        public override bool AltFunctionUse(Player player) => true;

        //蓄力武器魔力在释放与技能时手动扣除
        public override void ModifyManaCost(Player player, ref float reduce, ref float mult) => mult = 0f;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<AriaofTheCosmosHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<AriaofTheCosmosHeld>(player, source);

        public override void HoldItem(Player player) {
            //Q/R挂在物品持有上 不依赖手持弹幕
            if (QSkillCooldown > 0) {
                QSkillCooldown--;
            }
            if (RSkillCooldown > 0) {
                RSkillCooldown--;
            }

            if (Main.myPlayer != player.whoAmI) {
                return;
            }

            ShootState state = player.GetShootState();
            EntitySource_ItemUse_WithAmmo source = new(player, Item, ItemID.None, "CWRGunShoot");

            if (CWRKeySystem.WeponSkill_Q.JustPressed && QSkillCooldown <= 0
                && player.CountProjectilesOfID<AriaQSkill>() == 0) {
                Projectile.NewProjectile(source, player.Center, Vector2.Zero
                    , ModContent.ProjectileType<AriaQSkill>(), state.WeaponDamage, state.WeaponKnockback, player.whoAmI);
                QSkillCooldown = QSkillMaxCooldown;
                player.statMana = Math.Max(player.statMana - Item.mana * 2, 0);
                SoundEngine.PlaySound(SoundID.Item109 with { Volume = 0.8f, Pitch = 0.3f }, player.Center);
            }

            if (CWRKeySystem.WeponSkill_R.JustPressed && RSkillCooldown <= 0
                && player.CountProjectilesOfID<AriaRSkill>() == 0) {
                Projectile.NewProjectile(source, player.Center, Vector2.Zero
                    , ModContent.ProjectileType<AriaRSkill>(), (int)(state.WeaponDamage * 1.5f), state.WeaponKnockback * 1.5f, player.whoAmI);
                RSkillCooldown = RSkillMaxCooldown;
                player.statMana = Math.Max(player.statMana - Item.mana * 3, 0);
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 0.9f, Pitch = -0.3f }, player.Center);
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_Q, "AriaofTheCosmosQSkill", CWRKeySystem.Notbound.Value);
            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_R, "AriaofTheCosmosRSkill", CWRKeySystem.Notbound.Value);
        }
    }
}

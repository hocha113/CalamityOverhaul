using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTerrorBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.TerrorBlade>();
        public override int ProtogenesisID => ModContent.ItemType<TerrorBlade>();
        private bool InCharge;
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.width = 88;
            item.damage = 560;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 20;
            item.useTime = 20;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 8.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 80;
            item.shoot = ModContent.ProjectileType<RTerrorBeam>();
            item.shootSpeed = 20f;
            item.value = CalamityGlobalItem.Rarity13BuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= InCharge ? 1.25f : 1;
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            knockback *= InCharge ? 1.25f : 1;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (CWRUtils.RemakeByItem<CalamityMod.Items.Weapons.Melee.TerrorBlade>(item)) {
                CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "TerrorBlade");
            }
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            if (item.CWR().HoldOwner != null && item.CWR().MeleeCharge > 0) {
                DrawRageEnergyChargeBar(item.CWR().HoldOwner, item);
            }
        }

        public override void UpdateInventory(Item item, Player player) {
            UpdateBar(item);
        }

        public override void HoldItem(Item item, Player player) {
            if (item.CWR().HoldOwner == null) {
                item.CWR().HoldOwner = player;
            }

            UpdateBar(item);
            if (item.CWR().MeleeCharge > 0) {
                item.shootSpeed = 20f;
                item.useAnimation = 15;
                item.useTime = 15;
                InCharge = true;
            }
            else {
                item.shootSpeed = 15f;
                item.useAnimation = 20;
                item.useTime = 20;
                InCharge = false;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool shootBool = false;
            if (!item.CWR().closeCombat) {
                bool olduseup = item.CWR().MeleeCharge > 0;//这里使用到了效差的流程思想，用于判断能量耗尽的那一刻            
                if (item.CWR().MeleeCharge > 0) {
                    item.CWR().MeleeCharge -= damage / 10;
                    Projectile.NewProjectileDirect(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                    shootBool = false;
                }
                else {
                    shootBool = true;
                }
                bool useup = item.CWR().MeleeCharge > 0;
                if (useup != olduseup) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge, player.Center);
                }
            }

            item.CWR().closeCombat = false;
            return shootBool;
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);

            bool oldcharge = item.CWR().MeleeCharge > 0;//与OnHitPvp一致，用于判断能量出现的那一刻
            item.CWR().MeleeCharge += hit.Damage / 5;
            bool charge = item.CWR().MeleeCharge > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge, player.Center);
            }
        }

        private static void UpdateBar(Item item) {
            if (item.CWR().MeleeCharge > TerrorBlade.TerrorBladeMaxRageEnergy)
                item.CWR().MeleeCharge = TerrorBlade.TerrorBladeMaxRageEnergy;
        }

        public static void DrawRageEnergyChargeBar(Player player, Item item) {
            if (player.HeldItem != item) return;
            Texture2D rageEnergyTop = CWRUtils.GetT2DValue(CWRConstant.UI + "FrightEnergyChargeTop");
            Texture2D rageEnergyBar = CWRUtils.GetT2DValue(CWRConstant.UI + "FrightEnergyChargeBar");
            Texture2D rageEnergyBack = CWRUtils.GetT2DValue(CWRConstant.UI + "FrightEnergyChargeBack");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (item.CWR().MeleeCharge / TerrorBlade.TerrorBladeMaxRageEnergy)), rageEnergyBar.Height);

            Main.spriteBatch.ResetBlendState();
            Main.EntitySpriteDraw(
                rageEnergyBack,
                drawPos,
                null,
                Color.White,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                rageEnergyBar,
                drawPos + new Vector2(offsetwid, 0) * slp,
                backRec,
                Color.White,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );

            Main.EntitySpriteDraw(
                rageEnergyTop,
                drawPos,
                null,
                Color.White,
                0,
                Vector2.Zero,
                slp,
                SpriteEffects.None,
                0
                );
            Main.spriteBatch.ResetUICanvasState();
        }
    }
}

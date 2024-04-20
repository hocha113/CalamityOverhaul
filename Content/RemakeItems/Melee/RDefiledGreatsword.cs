using CalamityMod.Buffs.StatBuffs;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDefiledGreatsword : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.DefiledGreatsword>();
        public override int ProtogenesisID => ModContent.ItemType<DefiledGreatswordEcType>();
        public override string TargetToolTipItemName => "DefiledGreatswordEcType";
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.width = 102;
            item.damage = 112;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 18;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 18;
            item.useTurn = true;
            item.knockBack = 7.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 102;
            item.value = CalamityGlobalItem.Rarity12BuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.shoot = ModContent.ProjectileType<BlazingPhantomBlade>();
            item.shootSpeed = 12f;
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

            if (item.CWR().MeleeCharge > 0) {
                item.useAnimation = 16;
                item.useTime = 16;
            }
            else {
                item.useAnimation = 26;
                item.useTime = 26;
            }

            UpdateBar(item);
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (item.CWR().MeleeCharge > DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy) {
                item.CWR().MeleeCharge = DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy;
            }
            if (!item.CWR().closeCombat) {
                item.CWR().MeleeCharge -= damage * 1.25f;
                if (item.CWR().MeleeCharge < 0) item.CWR().MeleeCharge = 0;

                if (item.CWR().MeleeCharge == 0) {
                    Projectile.NewProjectile(
                        source,
                        position,
                        velocity,
                        type,
                        damage,
                        knockback,
                        player.whoAmI,
                        1
                        );
                }
                else {

                    for (int i = 0; i < 2; i++) {
                        float rot = MathHelper.ToRadians(-10 + i * 20);
                        Projectile.NewProjectile(
                            source,
                            position,
                            velocity.RotatedBy(rot),
                            type,
                            damage,
                            knockback,
                            player.whoAmI,
                            1
                        );
                    }
                }
            }
            item.CWR().closeCombat = false;
            return false;
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax)
                addnum = 0;
            else addnum *= 2;
            item.CWR().MeleeCharge += addnum;

            player.AddBuff(ModContent.BuffType<BrutalCarnage>(), 300);
            target.AddBuff(70, 150);

            if (CWRIDs.WormBodys.Contains(target.type) && !Main.rand.NextBool(3)) {
                return;
            }
            int type = ModContent.ProjectileType<SunlightBlades>();
            int randomLengs = Main.rand.Next(150);
            for (int i = 0; i < 3; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-15, 15, 900 + randomLengs);
                Vector2 spanPos = target.Center + offsetvr;
                int proj1 = Projectile.NewProjectile(
                    CWRUtils.parent(player), spanPos,
                    CWRUtils.UnitVector(offsetvr) * -13,
                    type, item.damage - 50, 0, player.whoAmI);
                Vector2 offsetvr2 = CWRUtils.GetRandomVevtor(165, 195, 900 + randomLengs);
                Vector2 spanPos2 = target.Center + offsetvr2;
                int proj2 = Projectile.NewProjectile(
                    CWRUtils.parent(player), spanPos2,
                    CWRUtils.UnitVector(offsetvr2) * -13, type,
                    item.damage - 50, 0, player.whoAmI);
                Main.projectile[proj1].extraUpdates += 1;
                Main.projectile[proj2].extraUpdates += 1;
            }
        }

        private static void UpdateBar(Item item) {
            if (item.CWR().MeleeCharge > DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy)
                item.CWR().MeleeCharge = DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy;
        }

        public void DrawRageEnergyChargeBar(Player player, Item item) {
            if (player.HeldItem != item) return;
            Texture2D rageEnergyTop = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyTop");
            Texture2D rageEnergyBar = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyBar");
            Texture2D rageEnergyBack = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyBack");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (item.CWR().MeleeCharge / DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy)), rageEnergyBar.Height);

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

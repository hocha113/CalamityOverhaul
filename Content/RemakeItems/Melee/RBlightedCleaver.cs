using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
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
    internal class RBlightedCleaver : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.BlightedCleaver>();
        public override int ProtogenesisID => ModContent.ItemType<BlightedCleaver>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.width = 88;
            item.damage = 64;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 26;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 26;
            item.useTurn = true;
            item.knockBack = 5.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 88;
            item.value = CalamityGlobalItem.Rarity8BuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.shoot = ModContent.ProjectileType<RBlazingPhantomBlade>();
            item.shootSpeed = 12f;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "BlightedCleaver");
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
            if (!item.CWR().closeCombat) {
                if (item.CWR().MeleeCharge > 0) {
                    item.CWR().MeleeCharge -= damage / 2;
                    return true;
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
            else {
                addnum *= 1.5f;
            }

            item.CWR().MeleeCharge += addnum;

            if (CWRIDs.WormBodys.Contains(target.type) && !Main.rand.NextBool(5)) {
                return;
            }

            int type = ModContent.ProjectileType<HyperBlade>();
            for (int i = 0; i < 8; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-127.5f, -52.5f, 360);
                Vector2 spanPos = target.Center + offsetvr;
                int proj = Projectile.NewProjectile(
                    CWRUtils.parent(player),
                    spanPos,
                    CWRUtils.UnitVector(offsetvr) * -15,
                    type,
                    item.damage / 2,
                    0,
                    player.whoAmI
                    );
                Main.projectile[proj].timeLeft = 50;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = 15;
            }

            player.AddBuff(ModContent.BuffType<TyrantsFury>(), 180);
            target.AddBuff(70, 150);
        }

        private static void UpdateBar(Item item) {
            if (item.CWR().MeleeCharge > BlightedCleaver.BlightedCleaverMaxRageEnergy)
                item.CWR().MeleeCharge = BlightedCleaver.BlightedCleaverMaxRageEnergy;
        }

        public void DrawRageEnergyChargeBar(Player player, Item item) {
            if (player.HeldItem != item) return;
            Texture2D rageEnergyTop = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyTop");
            Texture2D rageEnergyBar = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyBar");
            Texture2D rageEnergyBack = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyBack");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (item.CWR().MeleeCharge / BlightedCleaver.BlightedCleaverMaxRageEnergy)), rageEnergyBar.Height);

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
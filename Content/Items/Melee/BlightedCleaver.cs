using CalamityMod.Buffs.StatBuffs;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 暴君之刃
    /// </summary>
    internal class BlightedCleaver : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BlightedCleaver";

        public const float BlightedCleaverMaxRageEnergy = 5000;
        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }

        public override void SetDefaults() {
            Item.width = 88;
            Item.damage = 64;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 26;
            Item.useTurn = true;
            Item.knockBack = 5.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 88;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<RBlazingPhantomBlade>();
            Item.shootSpeed = 12f;
            
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            base.PostDrawInInventory(spriteBatch, position, frame, drawColor, itemColor, origin, scale);

            if (Item.CWR().HoldOwner != null && Item.CWR().MeleeCharge > 0) {
                DrawRageEnergyChargeBar(Item.CWR().HoldOwner);
            }
        }

        public override void UpdateInventory(Player player) {
            UpdateBar();
            base.UpdateInventory(player);
        }

        public override void HoldItem(Player player) {
            if (Item.CWR().HoldOwner == null) {
                Item.CWR().HoldOwner = player;
            }

            if (rageEnergy > 0) {
                Item.useAnimation = 16;
                Item.useTime = 16;
            }
            else {
                Item.useAnimation = 26;
                Item.useTime = 26;
            }

            UpdateBar();
        }

        private void UpdateBar() {
            if (rageEnergy > BlightedCleaverMaxRageEnergy)
                rageEnergy = BlightedCleaverMaxRageEnergy;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Item.CWR().closeCombat) {
                if (rageEnergy > 0) {
                    rageEnergy -= damage / 2;
                    return true;
                }
            }
            
            Item.CWR().closeCombat = false;
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, 106);
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax)
                addnum = 0;
            else {
                addnum *= 1.5f;
            }

            rageEnergy += addnum;

            player.AddBuff(ModContent.BuffType<TyrantsFury>(), 180);
            target.AddBuff(70, 150);

            if (CWRIDs.WormBodys.Contains(target.type)) {
                return;
            }

            int type = ModContent.ProjectileType<HyperBlade>();
            for (int i = 0; i < 8; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-127.5f, -52.5f, 360);
                Vector2 spanPos = target.Center + offsetvr;
                int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos,
                    CWRUtils.UnitVector(offsetvr) * -12, type, Item.damage / 2, 0, player.whoAmI);
                Main.projectile[proj].timeLeft = 50;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = 15;
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            player.AddBuff(ModContent.BuffType<TyrantsFury>(), 180);
            target.AddBuff(70, 150);
        }

        public void DrawRageEnergyChargeBar(Player player) {
            if (player.HeldItem != Item) return;
            Texture2D rageEnergyTop = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyTop");
            Texture2D rageEnergyBar = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyBar");
            Texture2D rageEnergyBack = CWRUtils.GetT2DValue(CWRConstant.UI + "RageEnergyBack");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (rageEnergy / BlightedCleaverMaxRageEnergy)), rageEnergyBar.Height);

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

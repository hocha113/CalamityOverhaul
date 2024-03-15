using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 惊惧魂刃
    /// </summary>
    internal class TerrorBlade : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TerrorBlade";

        public const float TerrorBladeMaxRageEnergy = 5000;
        private bool InCharge;

        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }

        public override void SetDefaults() {
            Item.width = 88;
            Item.damage = 560;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 80;
            Item.shoot = ModContent.ProjectileType<RTerrorBeam>();
            Item.shootSpeed = 20f;
            Item.value = CalamityGlobalItem.Rarity13BuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>(CWRConstant.Cay_Wap_Melee + "TerrorBladeGlow", (AssetRequestMode)2).Value);
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            base.PostDrawInInventory(spriteBatch, position, frame, drawColor, itemColor, origin, scale);

            if (Item.CWR().HoldOwner != null && rageEnergy > 0) {
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

            UpdateBar();

            if (rageEnergy > 0) {
                Item.shootSpeed = 20f;
                Item.useAnimation = 15;
                Item.useTime = 15;
                InCharge = true;
            }
            else {
                Item.shootSpeed = 15f;
                Item.useAnimation = 20;
                Item.useTime = 20;
                InCharge = false;
            }
        }

        private void UpdateBar() {
            if (rageEnergy > TerrorBladeMaxRageEnergy)
                rageEnergy = TerrorBladeMaxRageEnergy;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            damage *= InCharge ? 1.25f : 1;
        }

        public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback) {
            knockback *= InCharge ? 1.25f : 1;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool shootBool = false;
            if (!Item.CWR().closeCombat) {
                bool olduseup = rageEnergy > 0;//这里使用到了效差的流程思想，用于判断能量耗尽的那一刻            
                if (rageEnergy > 0) {
                    rageEnergy -= damage / 10;
                    Projectile.NewProjectileDirect(
                        source,
                        position,
                        velocity,
                        type,
                        damage,
                        knockback,
                        player.whoAmI,
                        1
                        );
                    shootBool = false;
                }
                else {
                    shootBool = true;
                }
                bool useup = rageEnergy > 0;
                if (useup != olduseup) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge, player.Center);
                }
            }

            Item.CWR().closeCombat = false;
            return shootBool;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);

            bool oldcharge = rageEnergy > 0;//与OnHitPvp一致，用于判断能量出现的那一刻
            rageEnergy += hit.Damage / 5;
            bool charge = rageEnergy > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge, player.Center);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            Item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 160);

            bool oldcharge = rageEnergy > 0;
            rageEnergy += hurtInfo.Damage * 2;
            bool charge = rageEnergy > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge, player.Center);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.RedTorch);
            }
        }

        public void DrawRageEnergyChargeBar(Player player) {
            if (player.HeldItem != Item) return;
            Texture2D rageEnergyTop = CWRUtils.GetT2DValue(CWRConstant.UI + "FrightEnergyChargeTop");
            Texture2D rageEnergyBar = CWRUtils.GetT2DValue(CWRConstant.UI + "FrightEnergyChargeBar");
            Texture2D rageEnergyBack = CWRUtils.GetT2DValue(CWRConstant.UI + "FrightEnergyChargeBack");
            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (rageEnergy / TerrorBladeMaxRageEnergy)), rageEnergyBar.Height);

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

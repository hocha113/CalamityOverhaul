using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
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
    internal class TerrorBladeEcType : EctypeItem, ILoader
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TerrorBlade";

        public const float TerrorBladeMaxRageEnergy = 5000;
        private bool InCharge {
            get {
                Item.initialize();
                return Item.CWR().ai[0] == 1;
            }
        }

        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }

        private static Asset<Texture2D> rageEnergyTopAsset;
        private static Asset<Texture2D> rageEnergyBarAsset;
        private static Asset<Texture2D> rageEnergyBackAsset;
        void ILoader.SetupData() {
            if (!Main.dedServ) {
                rageEnergyTopAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeTop");
                rageEnergyBarAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeBar");
                rageEnergyBackAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeBack");
            }
        }
        void ILoader.UnLoadData() {
            rageEnergyTopAsset = null;
            rageEnergyBarAsset = null;
            rageEnergyBackAsset = null;
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
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.CWR().heldProjType = ModContent.ProjectileType<TerrorBladeHeld>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>(CWRConstant.Cay_Wap_Melee + "TerrorBladeGlow", (AssetRequestMode)2).Value);
        }

        public static void UpdateBar(Item item) {
            if (item.CWR().MeleeCharge > TerrorBladeMaxRageEnergy)
                item.CWR().MeleeCharge = TerrorBladeMaxRageEnergy;
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
                    Projectile.NewProjectileDirect(source, player.GetPlayerStabilityCenter(),
                        velocity, type, damage, knockback, player.whoAmI, 1);
                    shootBool = false;
                }
                else {
                    shootBool = true;
                }
                bool useup = rageEnergy > 0;
                if (useup != olduseup) {
                    SoundEngine.PlaySound(CWRSound.Peuncharge with { Volume = 0.4f }, player.Center);
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
                SoundEngine.PlaySound(CWRSound.Pecharge with { Volume = 0.4f }, player.Center);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            Item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 160);

            bool oldcharge = rageEnergy > 0;
            rageEnergy += hurtInfo.Damage * 2;
            bool charge = rageEnergy > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Volume = 0.4f }, player.Center);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.RedTorch);
            }
        }

        public static void DrawRageEnergyChargeBar(Player player, float alp) {
            Item item = player.ActiveItem();
            if (item.IsAir) {
                return;
            }

            Texture2D rageEnergyTop = rageEnergyTopAsset.Value;
            Texture2D rageEnergyBar = rageEnergyBarAsset.Value;
            Texture2D rageEnergyBack = rageEnergyBackAsset.Value;

            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (item.CWR().MeleeCharge / TerrorBladeMaxRageEnergy)), rageEnergyBar.Height);

            Main.EntitySpriteDraw(rageEnergyBack, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(rageEnergyBar, drawPos + new Vector2(offsetwid, 0) * slp, backRec, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(rageEnergyTop, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
        }
    }
}

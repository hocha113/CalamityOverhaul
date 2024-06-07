using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
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
    internal class BlightedCleaverEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BlightedCleaver";

        public const float BlightedCleaverMaxRageEnergy = 5000;
        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }

        public override void SetDefaults() {
            Item.width = 78;
            Item.damage = 60;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 26;
            Item.useTurn = true;
            Item.knockBack = 5.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 88;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<BlazingPhantomBlade>();
            Item.shootSpeed = 12f;
            Item.CWR().heldProjType = ModContent.ProjectileType<DefiledGreatswordHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Item.CWR().closeCombat) {
                if (rageEnergy > 0) {
                    rageEnergy -= damage;
                    if (rageEnergy < 0) {
                        rageEnergy = 0;
                    }
                    return true;
                }
            }

            Item.CWR().closeCombat = false;
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.RuneWizard);
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
            target.AddBuff(70, 150);

            if (CWRLoad.WormBodys.Contains(target.type)) {
                return;
            }

            int type = ModContent.ProjectileType<HyperBlade>();
            for (int i = 0; i < 4; i++) {
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
            target.AddBuff(70, 150);
        }
    }
}

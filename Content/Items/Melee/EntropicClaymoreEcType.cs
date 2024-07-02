using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 熵之舞
    /// </summary>
    internal class EntropicClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EntropicClaymore";

        public static readonly Color EntropicColor1 = new Color(25, 5, 9);

        public static readonly Color EntropicColor2 = new Color(25, 5, 9);

        public static readonly SoundStyle SwingSound = SoundID.Item1;

        public override void SetDefaults() {
            Item.damage = 152;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 78;
            Item.useTime = 78;
            Item.knockBack = 5.25f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<EntropicClaymoreHoldoutProj>();
            Item.shootSpeed = 12f;

        }

        public override void UseItemHitbox(Player player, ref Rectangle hitbox, ref bool noHitbox) {
            hitbox = CalamityUtils.FixSwingHitbox(118f, 118f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Item.initialize();
            Item.CWR().ai[0]++;
            if (Item.CWR().ai[0] > 2)
                Item.CWR().ai[0] = 0;
            Projectile proj = Projectile.NewProjectileDirect(
                source,
                position,
                velocity,
                type,
                damage,
                knockback,
                player.whoAmI,
                ai2: Item.useTime
                );
            proj.timeLeft = Item.useTime;
            proj.localAI[0] = Item.CWR().ai[0];
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, 27);
            }
        }
    }
}

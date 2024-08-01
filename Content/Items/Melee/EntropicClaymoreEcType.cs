using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
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
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 92;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 38;
            Item.useTime = 28;
            Item.knockBack = 5.25f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<EntropicClaymoreHeld>();
        }
    }

    internal class EntropicClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<EntropicClaymore>();
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 96;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 80;
            drawTrailTopWidth = 60;
            drawTrailCount = 46;
            Length = 122;
            unitOffsetDrawZkMode = -16;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            overOffsetCachesRoting = MathHelper.ToRadians(16);
        }

        public override void Shoot() {
            Vector2 toMou = ToMouse;
            Vector2 pos;
            int type0 = 10;
            float damageOffset = 0.5f;
            Item.initialize();
            switch (Item.CWR().ai[0]) {
                case 0:
                    type0 = ModContent.ProjectileType<EntropicFlechetteSmall>();
                    damageOffset = 0.3f;
                    break;
                case 1:
                    type0 = ModContent.ProjectileType<EntropicFlechette>();
                    damageOffset = 0.45f;
                    break;
                case 2:
                    type0 = ModContent.ProjectileType<EntropicFlechetteLarge>();
                    damageOffset = 0.6f;
                    break;
            }
            if (++Item.CWR().ai[0] > 2) {
                Item.CWR().ai[0] = 0;
            }
            for (int i = 0; i < 9; i++) {
                float rot = toMou.ToRotation() + MathHelper.ToRadians(-70 + 140 / 9f * i);
                pos = ShootSpanPos + rot.ToRotationVector2() * 130;
                Projectile.NewProjectile(Source, pos, toMou.UnitVector() * 13
                    , type0, (int)(Projectile.damage * damageOffset), Projectile.knockBack, Projectile.owner);
            }
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame);
            }
            return base.PreInOwnerUpdate();
        }
    }
}

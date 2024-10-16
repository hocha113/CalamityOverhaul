﻿using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class RedSunEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "RedSun";
        public override void SetDefaults() {
            Item.SetItemCopySD<RedSun>();
            Item.useTime = Item.useAnimation = 40;
            Item.SetKnifeHeld<RedSunHeld>();
        }
    }

    internal class R_RedSun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<RedSun>();
        public override int ProtogenesisID => ModContent.ItemType<RedSunEcType>();
        public override void SetDefaults(Item item) {
            item.useTime = item.useAnimation = 40;
            item.SetKnifeHeld<RedSunHeld>();
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class RedSunHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<RedSun>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "RedSun_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            canDrawSlashTrail = true;
            SwingData.starArg = 74;
            SwingData.baseSwingSpeed = 2.56f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 18;
            drawTrailTopWidth = 30;
            Length = 50;
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.YellowTorch);
            }
            if (Time % (10 * updateCount) == 0) {
                canShoot = true;
            }
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<RSSolarFlare>();
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = new Vector2(InMousePos.X, ShootSpanPos.Y);
                spwanPos.X += Main.rand.Next(-160, 160);
                spwanPos.Y -= 660;
                Vector2 ver = new Vector2(0, 16);
                ver = ver.RotatedByRandom(0.6f);
                ver *= Main.rand.NextFloat(0.3f, 2.33f);
                Projectile.NewProjectile(Source, spwanPos, ver, type, Projectile.damage, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HolyFlames>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HolyFlames>(), 300);
        }
    }
}

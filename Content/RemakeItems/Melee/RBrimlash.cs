using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBrimlash : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Brimlash>();
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.useTime = item.useAnimation = 30;
            if (player.altFunctionUse == 2) {
                item.useTime = item.useAnimation = 22;
            }
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(item, player, source, position, velocity, type, damage, knockback);

        public static void SetDefaultsFunc(Item Item) {
            Item.width = Item.height = 72;
            Item.damage = 70;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityLimeBuyPrice;
            Item.rare = ItemRarityID.Lime;
            Item.shoot = ModContent.ProjectileType<BrimlashProj>();
            Item.shootSpeed = 10f;
            Item.SetKnifeHeld<BrimlashHeld>();
        }

        public static bool ShootFunc(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, 0);
            return false;
        }
    }

    internal class BrimlashHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Brimlash>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override string GlowTexturePath => CWRConstant.Item_Melee + "BrimlashGlow";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            unitOffsetDrawZkMode = -4;
            SwingData.starArg = 74;
            SwingData.baseSwingSpeed = 4f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 20;
            drawTrailTopWidth = 40;
            drawTrailCount = 8;
            ShootSpeed = 10f;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, (int)CalamityDusts.Brimstone);
            }
        }

        public override bool PreInOwner() {
            if (Projectile.ai[0] == 1) {
                SwingData.baseSwingSpeed = 11.8f;
            }
            return base.PreInOwner();
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                SoundEngine.PlaySound(SoundID.Item84, Projectile.Center);
                Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());

                if (Main.rand.NextBool(6)) {
                    Owner.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 20);
                }

                float randRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 5; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 5f * i + randRot).ToRotationVector2() * 15;
                    Projectile.NewProjectile(Source, Owner.Center, vr, ModContent.ProjectileType<Brimlash2>()
                        , Projectile.damage / 3, Projectile.knockBack / 2, Owner.whoAmI);
                }
                return;
            }
            Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                , ModContent.ProjectileType<BrimlashProj>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
        }
    }
}

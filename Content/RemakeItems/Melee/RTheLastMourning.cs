using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheLastMourning : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheLastMourning>();
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 94;
            Item.height = 94;
            Item.DamageType = DamageClass.Melee;
            Item.damage = 320;
            Item.knockBack = 8.5f;
            Item.useAnimation = 18;
            Item.useTime = 18;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.Calamity().donorItem = true;
            Item.shoot = ModContent.ProjectileType<SoulSeekerSkull>();
            Item.shootSpeed = 15;
            Item.SetKnifeHeld<TheLastMourningHeld>(false);
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, type, (int)(damage * 1.25f), knockback, player.whoAmI, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class TheLastMourningHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TheLastMourning>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "TheLastMourning_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = -40;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 80;
            drawTrailCount = 16;
            Length = 110;
            unitOffsetDrawZkMode = 0;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 60;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 120;
            SwingData.maxClampLength = 130;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 12;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ModContent.ProjectileType<SoulSeekerSkull>()
                , Projectile.damage, Projectile.knockBack, Main.myPlayer, 0f, Main.rand.Next(3));
        }

        public override bool PreInOwner() {
            if (Main.rand.NextBool(5)) {
                int dustType = 5;
                switch (Main.rand.Next(3)) {
                    case 0:
                        dustType = 5;
                        break;
                    case 1:
                        dustType = 6;
                        break;
                    case 2:
                        dustType = 174;
                        break;
                    default:
                        break;
                }
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, Owner.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[dust].velocity *= 0.2f;
            }
            if (Projectile.ai[0] == 1) {
                SwingData.starArg = 60;
                distanceToOwner = -20;
                drawTrailBtommWidth = 60;
                SwingData.ler1_UpLengthSengs = 0.16f;
                SwingData.minClampLength = 150;
                SwingData.maxClampLength = 160;
                SwingData.ler1_UpSizeSengs = 0.1f;
            }
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if ((CWRLoad.WormBodys.Contains(target.type) || target.type == NPCID.Probe) && !Main.rand.NextBool(4)) {
                return;
            }
            if (Projectile.ai[0] == 1) {
                SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
                Vector2 spanPos = ShootSpanPos + CWRUtils.randVr(600, 700);
                Vector2 ver = spanPos.To(target.Center).UnitVector() * ShootSpeed;
                Projectile.NewProjectile(Source, spanPos, ver, ModContent.ProjectileType<SoulSeekerSkull>()
                , Projectile.damage, Projectile.knockBack, Main.myPlayer, 0f, Main.rand.Next(3));
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.ai[0] == 1) {
                SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
                Vector2 spanPos = ShootSpanPos + CWRUtils.randVr(600, 700);
                Vector2 ver = spanPos.To(target.Center).UnitVector() * ShootSpeed;
                Projectile.NewProjectile(Source, spanPos, ver, ModContent.ProjectileType<SoulSeekerSkull>()
                , Projectile.damage, Projectile.knockBack, Main.myPlayer, 0f, Main.rand.Next(3));
            }
        }
    }
}

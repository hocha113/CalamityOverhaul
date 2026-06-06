using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石猎刀：快速连击，每第三击为更宽的终结斩，向前迸射大理石碎片
    /// </summary>
    internal class MarbleHuntingKnife : ModItem
    {
        private static int swingCounter;

        public override void SetDefaults() {
            Item.width = Item.height = 40;
            Item.damage = 13;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 3f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.shoot = ModContent.ProjectileType<MarbleHuntingKnifeHeld>();
            Item.shootSpeed = 8f;
            Item.value = Item.sellPrice(0, 0, 50, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<MarbleHuntingKnifeHeld>()] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            MarbleSwingPlayer mp = player.GetModPlayer<MarbleSwingPlayer>();
            bool finisher = mp.ComboStep >= 2;
            mp.ComboStep = finisher ? 0 : mp.ComboStep + 1;
            mp.ComboTimer = 45;

            swingCounter++;
            float dir = swingCounter % 2 == 0 ? 1f : -1f;
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, dir, finisher ? 1f : 0f);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.MarbleBlock, 14)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 6)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleHuntingKnifeHeld : BaseSwingHeld
    {
        private bool isFinisher;

        protected override string TexturePath => GraniteMarbleVFX.MarbleTex + "MarbleHuntingKnife";
        protected override float SwingArc => isFinisher ? 3.1f : 2.3f;
        protected override float HoldDistance => 26f;
        protected override float BladeLength => isFinisher ? 66f : 52f;
        protected override float BladeWidth => 30f;
        protected override float TrailWidthMax => isFinisher ? 52f : 36f;
        protected override int TrailLength => 18;
        protected override Color TrailColor => isFinisher ? GraniteMarbleVFX.MarbleGold : GraniteMarbleVFX.MarbleCore;

        //利落的 ease-out，读作一记快速挥斩
        protected override float SwingEase(float p) => p * (2f - p);

        protected override void OnSwingStart() {
            isFinisher = Projectile.ai[1] >= 0.5f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(isFinisher
                    ? SoundID.Item71 with { Pitch = 0.15f }
                    : SoundID.Item1 with { Pitch = 0.35f, Volume = 0.7f }, Projectile.Center);
            }
        }

        protected override void OnSwingUpdate(float p, float ang) {
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleCore.ToVector3() * 0.35f);
            if (isFinisher && p > 0.25f && p < 0.8f && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 along = Owner.GetPlayerStabilityCenter()
                    + ang.ToRotationVector2() * Main.rand.NextFloat(BladeLength * 0.5f, BladeLength);
                PRTLoader.NewParticle<PRT_Sparkle>(along, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.5f)
                    .Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, 0.5f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!isFinisher || Projectile.numHits != 0) {
                return;
            }

            Vector2 tip = Owner.GetPlayerStabilityCenter() + CurrentAngle.ToRotationVector2() * BladeLength;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f }, tip);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(tip, Main.rand.NextVector2Circular(3f, 3f)
                        , GraniteMarbleVFX.MarbleGold, 0.6f).Configure(GraniteMarbleVFX.MarbleGold, 16, 0.2f, Main.rand.NextFloat(0.5f, 0.8f));
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 baseDir = CurrentAngle.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    Vector2 v = baseDir.RotatedBy(MathHelper.Lerp(-0.5f, 0.5f, i / 2f)) * Main.rand.NextFloat(7f, 10f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), tip, v
                        , ModContent.ProjectileType<MarbleShard>(), (int)(Projectile.damage * 0.5f)
                        , Projectile.knockBack * 0.5f, Projectile.owner);
                }
            }
        }
    }

    /// <summary>
    /// 大理石近战连击状态：用于猎刀的三连终结判定
    /// </summary>
    internal class MarbleSwingPlayer : ModPlayer
    {
        public int ComboStep;
        public int ComboTimer;

        public override void ResetEffects() {
            if (ComboTimer > 0) {
                ComboTimer--;
            }
            else {
                ComboStep = 0;
            }
        }
    }
}

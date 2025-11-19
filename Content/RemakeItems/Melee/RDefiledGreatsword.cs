using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDefiledGreatsword : CWRItemOverride, ICWRLoader
    {
        public const float DefiledGreatswordMaxRageEnergy = 15000;
        private static Asset<Texture2D> rageEnergyBarAsset;
        private static Asset<Texture2D> rageEnergyBackAsset;
        void ICWRLoader.SetupData() {
            if (!Main.dedServ) {
                rageEnergyBarAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "RageEnergyBar");
                rageEnergyBackAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "RageEnergyBack");
            }
        }
        void ICWRLoader.UnLoadData() {
            rageEnergyBarAsset = null;
            rageEnergyBackAsset = null;
        }
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);

        public static void SetDefaultsFunc(Item Item) {
            Item.width = 102;
            Item.damage = 112;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.useTurn = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 102;
            Item.shootSpeed = 12f;
            Item.CWR().heldProjType = ModContent.ProjectileType<Projectiles.Weapons.Melee.HeldProjectiles.DefiledGreatswordHeld>();
            Item.SetKnifeHeld<DefiledGreatswordHeld>();
        }

        public static void DrawRageEnergyChargeBar(Player player, float alp, float charge) {
            Item item = player.GetItem();
            if (item.IsAir) {
                return;
            }

            Texture2D rageEnergyBar = rageEnergyBarAsset.Value;
            Texture2D rageEnergyBack = rageEnergyBackAsset.Value;

            float slp = 1;
            Vector2 drawPos = player.GetPlayerStabilityCenter() + new Vector2(rageEnergyBack.Width / -2, 120) - Main.screenPosition;
            int width = (int)(rageEnergyBar.Width * charge);
            if (width > rageEnergyBar.Width) {
                width = rageEnergyBar.Width;
            }
            Rectangle backRec = new Rectangle(0, 0, width, rageEnergyBar.Height);

            Main.EntitySpriteDraw(rageEnergyBack, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(rageEnergyBar, drawPos + new Vector2(10, 12) * slp, backRec, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
        }
    }

    internal class RageKillerImpact : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "RageKillerImpact";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 4;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3() * 1.75f * Main.essScale);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;

            Projectile.ai[1]++;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawOrigin = texture.Size() / 2;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(Color.White, Color.Gold, 1f / Projectile.oldPos.Length * k) * (1f - 1f / Projectile.oldPos.Length * k));
                if (Projectile.ai[1] > 160) {
                    color.A = 0;
                }
                float slp = (0.6f + 0.4f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale * slp, SpriteEffects.None, 0);
            }

            Texture2D glow = CWRUtils.GetT2DValue(Texture + "Glow");
            Vector2 drawOrigin2 = glow.Size() / 2;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(Color.White, Color.Gold, 1f / Projectile.oldPos.Length * k) * (1f - 1f / Projectile.oldPos.Length * k));
                if (Projectile.ai[1] > 160) {
                    color.A = 0;
                }
                float slp = (0.6f + 0.4f * (Projectile.oldPos.Length - k) / Projectile.oldPos.Length);
                Main.EntitySpriteDraw(glow, drawPos, null, color, Projectile.rotation, drawOrigin2, Projectile.scale * slp, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    internal class DefiledGreatswordHeld : BaseKnife
    {
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail4";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BlightedCleaver_Bar";
        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 70;
            drawTrailCount = 16;
            Length = 130;
            unitOffsetDrawZkMode = -6;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 4.2f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 130;
            SwingData.maxClampLength = 140;
            SwingData.ler1_UpSizeSengs = 0.026f;
            ShootSpeed = 12;
            IgnoreImpactBoxSize = false;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position, Projectile.width
                    , Projectile.height, DustID.RuneWizard);
            }
        }

        public override bool PreInOwner() {
            if (rageEnergy > 0) {
                SwingData.baseSwingSpeed = 12.45f;
            }
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 5f);
            return base.PreInOwner();
        }

        public override void Shoot() {
            int damage = Projectile.damage;
            Player player = Owner;

            if (!Item.CWR().closeCombat) {
                rageEnergy -= damage * 1.25f;
                if (rageEnergy < 0) {
                    rageEnergy = 0;
                }

                if (rageEnergy == 0) {
                    Projectile.NewProjectile(Source, player.GetPlayerStabilityCenter(), ShootVelocity
                        , ModContent.ProjectileType<RageKillerImpact>(), (int)(damage * 0.75)
                        , Projectile.knockBack * 0.5f, player.whoAmI);
                }
                else {
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(Source, player.GetPlayerStabilityCenter(), ShootVelocity.RotatedBy((-1 + i) * 0.2f)
                        , ModContent.ProjectileType<RageKillerImpact>(), damage, Projectile.knockBack, player.whoAmI);
                    }
                }
            }
            Item.CWR().closeCombat = false;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Owner;
            Item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax) {
                addnum = 0;
            }
            else {
                addnum *= 2;
            }

            rageEnergy += addnum;
            if (rageEnergy > RDefiledGreatsword.DefiledGreatswordMaxRageEnergy) {
                rageEnergy = RDefiledGreatsword.DefiledGreatswordMaxRageEnergy;
            }

            player.AddBuff(CWRID.Buff_BrutalCarnage, 300);
            target.AddBuff(BuffID.Venom, 150);

            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(3)) {
                return;
            }
            int type = ModContent.ProjectileType<SunlightBlades>();
            int randomLengs = Main.rand.Next(150);
            for (int i = 0; i < 3; i++) {
                Vector2 offsetvr = VaultUtils.RandVrInAngleRange(-15, 15, 900 + randomLengs);
                Vector2 spanPos = target.Center + offsetvr;
                int proj1 = Projectile.NewProjectile(
                    player.FromObjectGetParent(), spanPos,
                    offsetvr.UnitVector() * -13,
                    type, Item.damage - 50, 0, player.whoAmI);
                Vector2 offsetvr2 = VaultUtils.RandVrInAngleRange(165, 195, 900 + randomLengs);
                Vector2 spanPos2 = target.Center + offsetvr2;
                int proj2 = Projectile.NewProjectile(
                    player.FromObjectGetParent(), spanPos2,
                    offsetvr2.UnitVector() * -13, type,
                    Item.damage - 50, 0, player.whoAmI);
                Main.projectile[proj1].extraUpdates += 1;
                Main.projectile[proj2].extraUpdates += 1;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Player player = Owner;
            Item.CWR().closeCombat = true;
            int type = ModContent.ProjectileType<SunlightBlades>();
            int offsety = 180;
            for (int i = 0; i < 16; i++) {
                Vector2 offsetvr = new(-600, offsety);
                Vector2 spanPos = offsetvr + player.Center;
                _ = Projectile.NewProjectile(
                    player.FromObjectGetParent(),
                    spanPos,
                    offsetvr.UnitVector() * -13,
                    type,
                    Item.damage / 3,
                    0,
                    player.whoAmI
                    );
                Vector2 offsetvr2 = new(600, offsety);
                Vector2 spanPos2 = offsetvr + player.Center;
                _ = Projectile.NewProjectile(
                    player.FromObjectGetParent(),
                    spanPos2,
                    offsetvr2.UnitVector() * -13,
                    type,
                    Item.damage / 3,
                    0,
                    player.whoAmI
                    );

                offsety -= 20;
            }
            player.AddBuff(CWRID.Buff_BrutalCarnage, 300);
            target.AddBuff(BuffID.Venom, 150);
        }
    }
}

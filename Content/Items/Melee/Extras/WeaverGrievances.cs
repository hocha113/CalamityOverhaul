using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    /// <summary>
    /// 怨念编织者
    /// </summary>
    internal class WeaverGrievances : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "WeaverGrievances";
        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 22;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<WeaverBeam>();
            Item.shootSpeed = 18f;
            Item.SetKnifeHeld<WeaverGrievancesHeld>();
        }
    }

    internal class WeaverGrievancesHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<WeaverGrievances>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "WeaverGrievances_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = -20;
            drawTrailBtommWidth = 80;
            drawTrailTopWidth = 80;
            drawTrailCount = 6;
            Length = 200;
            unitOffsetDrawZkMode = 0;
            Projectile.width = Projectile.height = 186;
            distanceToOwner = -60;
            SwingData.starArg = 30;
            SwingData.ler1_UpLengthSengs = 0.05f;
            SwingData.minClampLength = 200;
            SwingData.maxClampLength = 210;
            SwingData.ler1_UpSizeSengs = 0.016f;
            SwingData.baseSwingSpeed = 4.2f;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            autoSetShoot = true;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(
            phase0SwingSpeed: -0.2f,
            phase1SwingSpeed: 18.2f,
            phase2SwingSpeed: 2f,
            swingSound: SoundID.Item1 with { Pitch = -0.6f });
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), ShootSpanPos, ShootVelocity.UnitVector() * 18,
                ModContent.ProjectileType<WeaverBeam>(), Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner);
        }

        public override void MeleeEffect() {

        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }

    internal class WeaverExplode : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 420;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Projectile.Center + CWRUtils.randVr(Projectile.width / 2);
                PRTLoader.NewParticle<PRT_HellFire>(pos, new Vector2(0, -Main.rand.Next(2, 4)), default, Main.rand.NextFloat(0.3f, 1));
            }
        }
    }

    internal class WeaverBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Wave_highest";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 32;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.width = 462;
            Projectile.height = 462;
            Projectile.timeLeft = 160;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 4;
            Projectile.penetrate = -1;
        }
        public override void AI() {
            if (Projectile.ai[0] == 0 && Projectile.ai[1] >= 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot, Projectile.position);
                Projectile.spriteDirection = Projectile.direction;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
                Projectile.ai[0] = 1;
            }

            if (Projectile.ai[1] >= 60) {
                Projectile.scale -= 0.004f;
            }
            else {
                Projectile.scale += 0.006f;
            }

            if (Projectile.alpha <= 255) {
                Projectile.alpha += 2;
            }

            Projectile.spriteDirection = Projectile.direction;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Projectile.velocity *= 0.98f;
            if (Projectile.velocity.Length() > 16) {
                Projectile.velocity *= 0.98f;
            }
            if (Projectile.timeLeft == 2) {
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<WeaverExplode>(), Projectile.damage, 0, Projectile.owner);
            }
            Projectile.ai[1]++;
        }


        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2;
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Lerp(Color.DarkRed, Color.Red, 1f / Projectile.oldPos.Length * k) * (1f - 1f / Projectile.oldPos.Length * k));
                float slp = (0.6f + 0.4f * (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) * 0.24f;
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale * slp, SpriteEffects.None, 0);
            }
            return false;
        }
        public override void PostDraw(Color lightColor) {
            Lighting.AddLight(Projectile.Center, Color.Orange.ToVector3() * 1.75f * Main.essScale);
        }
    }
}

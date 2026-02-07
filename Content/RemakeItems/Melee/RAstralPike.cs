using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAstralPike : CWRItemOverride
    {
        public const int InTargetProjToLang = 1220;
        public const int ShootPeriod = 2;
        internal static int index;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<AstralPikeHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(item, player, source, position, velocity, type, damage, knockback);
        }
        public static bool ShootFunc(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (++index > 3) {
                index = 0;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, index);
            return false;
        }
    }

    internal class AstralPikeHeld : BaseKnife
    {
        public override int TargetID => CWRItemOverride.GetCalItemID("AstralPike");
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Astral_Bar";
        public override Texture2D TextureValue => CWRUtils.GetT2DValue(CWRConstant.Cay_Proj_Melee + "Spears/AstralPikeProj");
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
            SwingData.baseSwingSpeed = 6;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            SwingDrawRotingOffset = MathHelper.PiOver2;
            ShootSpeed = 13;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 3) {
                if (Time == 0) {
                    OtherMeleeSize = 1.4f;
                }

                SwingData.baseSwingSpeed = 10;
                SwingAIType = SwingAITypeEnum.Down;

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.025f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }
                return true;
            }

            if (Projectile.ai[0] == 0) {
                StabBehavior(initialLength: 60, lifetime: 26, scaleFactorDenominator: 520f, minLength: 20, maxLength: 120, canDrawSlashTrail: true);
                return false;
            }

            return true;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 3) {
                return;
            }
            if (Projectile.ai[0] == 1 || Projectile.ai[0] == 2) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.2f }, Projectile.Center);
            const float sengs = 0.25f;
            Vector2 spanPos = Projectile.Center + AbsolutelyShootVelocity * 0.5f;
            Vector2 targetPos = AbsolutelyShootVelocity.UnitVector() * RAstralPike.InTargetProjToLang + Projectile.Center;
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(Source, spanPos, AbsolutelyShootVelocity.RotatedBy(sengs - sengs * i)
                    , ModContent.ProjectileType<AstralPikeBeam>(), Projectile.damage, Projectile.knockBack * 0.25f, Projectile.owner, targetPos.X, targetPos.Y);
            }
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , CWRID.Dust_AstralOrange, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_AstralInfectionDebuff, 300);
            if (Projectile.ai[0] != 3) {
                for (int i = 0; i < 3; i++) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile star = CWRRef.ProjectileBarrage(Source, Projectile.Center, target.Center, Main.rand.NextBool()
                            , 800f, 800f, 800f, 800f, 10f, CWRID.Proj_AstralStar, (int)(Projectile.damage * 0.4), 1f, Projectile.owner, true);
                        star.DamageType = DamageClass.Melee;
                        star.ai[0] = 3f;
                    }
                }
            }
        }
    }

    internal class AstralPikeBeam : ModProjectile
    {
        private Vector2 targetPos {
            get => new Vector2(Projectile.ai[0], Projectile.ai[1]);
            set {
                Projectile.ai[0] = value.X;
                Projectile.ai[1] = value.Y;
            }
        }
        public override string Texture => CWRConstant.Placeholder;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 122;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.alpha = 50;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 150;
            Projectile.scale = 0.3f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
        }

        public override void AI() {
            if (Projectile.timeLeft > 90) {
                Projectile.velocity *= 0.98f;
            }
            else {
                if (Projectile.timeLeft == 90) {
                    Projectile.velocity = Projectile.velocity.UnitVector() * 23;
                }
                Projectile.SmoothHomingBehavior(targetPos, 1, 0.05f);
            }
            PRT_Line spark2 = new PRT_Line(Projectile.Center, -Projectile.velocity * 0.05f, false, 17, 1.7f, Color.Goldenrod);
            PRTLoader.AddParticle(spark2);
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawStarTrail(Projectile, Color.Coral, Color.White);
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 2);
            return false;
        }
    }
}

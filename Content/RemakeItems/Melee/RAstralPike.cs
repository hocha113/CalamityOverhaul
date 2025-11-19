using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
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
                    , ModContent.DustType<AstralOrange>(), Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
            if (Projectile.ai[0] != 3) {
                for (int i = 0; i < 3; i++) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile star = CalamityUtils.ProjectileBarrage(Source, Projectile.Center, target.Center, Main.rand.NextBool()
                            , 800f, 800f, 800f, 800f, 10f, ModContent.ProjectileType<AstralStar>(), (int)(Projectile.damage * 0.4), 1f, Projectile.owner, true);
                        if (star.whoAmI.WithinBounds(Main.maxProjectiles)) {
                            star.DamageType = DamageClass.Melee;
                            star.ai[0] = 3f;
                        }
                    }
                }
            }
        }
    }
}

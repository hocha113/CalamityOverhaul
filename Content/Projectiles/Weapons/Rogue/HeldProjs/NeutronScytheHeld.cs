using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs
{
    internal class NeutronScytheHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Item + "Rogue/NeutronScythe";
        private Vector2 orig = Vector2.Zero;
        private int fireIndex;
        private int fireIndex2;
        public override void SetThrowable() {
            HandOnTwringMode = -30;
            OffsetRoting = 20;
            TotalLifetime = 1200;
            Projectile.timeLeft = TotalLifetime + ChargeUpTime;
        }

        public override void PostSetThrowable() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
            orig = Projectile.velocity.X > 0 ? new Vector2(12, 68) : new Vector2(12, 27);
        }

        public override bool PreThrowOut() {
            if (Projectile.ai[2] == 0) {
                Projectile.extraUpdates = 2;
                if (stealthStrike) {
                    SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = 0.1f, Volume = 0.8f, MaxInstances = 3 }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        for (int i = 0; i < 13; i++) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center
                            , (MathHelper.TwoPi / 13f * i).ToRotationVector2() * 6, Type
                            , Projectile.damage / 2, 2, Projectile.owner, 0, 0, 1);
                        }
                    }
                }
            }
            else {
                Projectile.extraUpdates = 1;
            }
            if (!VaultUtils.isServer) {
                orig = VaultUtils.GetOrig(TextureValue, 13);
            }

            return base.PreThrowOut();
        }

        public override void FlyToMovementAI() {
            Projectile.rotation += Projectile.velocity.X * 0.01f;
            if (++fireIndex2 > 180) {
                if (!Owner.Alives()) {
                    Projectile.Kill();
                }
                Projectile.ChasingBehavior(Owner.Center, 11 + Projectile.ai[2] * 10);
                if (Projectile.Distance(Owner.Center) < 32) {
                    Projectile.Kill();
                }

            }
            if (Projectile.Distance(Owner.Center) < 1200) {
                if (++fireIndex > 15) {
                    NPC target = Projectile.Center.FindClosestNPC(11200);
                    if (target != null) {
                        SoundEngine.PlaySound(CWRSound.Pecharge with {
                            Pitch = -0.6f + Main.rand.NextFloat(-0.1f, 0.2f),
                            Volume = 0.5f
                        }, Projectile.Center);
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                        , Projectile.Center.To(target.Center).UnitVector() * 22
                        , ModContent.ProjectileType<NeutronLaser>(), Projectile.damage / 3, 0);
                        }
                    }
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center
                        , Vector2.Zero, ModContent.ProjectileType<NeutronExplosionRogue>(), Projectile.damage / 2, 0);
                    }
                    fireIndex = 0;
                }
            }
        }

        public override void PostUpdate() => VaultUtils.ClockFrame(ref Projectile.frame, 5, 12);

        public override void DrawThrowable(Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition
                , TextureValue.GetRectangle(Projectile.frame, 13), lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , orig, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }
    }

    internal class NeutronExplosionRogue : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        public override void SetDefaults() {
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.width = Projectile.height = 100;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 6;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool CanDrawCustom() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 13; j++) {
                        BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                            , vr * (0.24f), false, 30, 1.2f, Color.CadetBlue);
                        PRTLoader.AddParticle(spark);
                    }
                }
                Projectile.ai[2]++;
            }
            Projectile.ai[0] += 0.15f;
            if (Projectile.timeLeft > 10) {
                Projectile.localAI[0] += 0.06f;
                Projectile.ai[1] += 0.1f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition
                    , null, warpColor, 0, warpTex.Size() / 2, Projectile.localAI[0], SpriteEffects.None, 0f);
            }
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}

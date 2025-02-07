using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class MuraExecutionCutOnSpan : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private List<Vector2> PosLists;
        private float orbNinmsWeith;
        private float maxOrbNinmsWeith;
        private bool onSpan = true;
        private bool onOrb = true;
        private Trail Trail;
        public override void SetStaticDefaults() => CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        public override void SetDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 7000;
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 100 * Projectile.MaxUpdates;
            maxOrbNinmsWeith = Main.rand.NextFloat(2, 6.3f);
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (onSpan) {
                Projectile.ai[0] = Projectile.Center.X;
                Projectile.ai[1] = Projectile.Center.Y;
                Projectile.rotation = Projectile.velocity.ToRotation();
                PosLists = [];
                Vector2 rot = Projectile.velocity.UnitVector();
                for (int i = -50; i < 50; i++) {
                    PosLists.Add(Projectile.Center + rot * 150 * i);
                }
                Vector2 toOwner = Projectile.Center.To(Main.player[Projectile.owner].Center).UnitVector();
                Projectile.position += toOwner * 1000;
                onSpan = false;
            }

            if (Projectile.timeLeft > 25) {
                //Projectile.ai[0] += 0.001f;
                Projectile.localAI[0] += 0.001f;
                orbNinmsWeith += 0.02f;
                if (orbNinmsWeith > maxOrbNinmsWeith) {
                    orbNinmsWeith = maxOrbNinmsWeith;
                }
            }
            else {
                //Projectile.ai[0] -= 0.001f;
                Projectile.localAI[0] -= 0.001f;
                orbNinmsWeith -= 0.05f;
                if (orbNinmsWeith < 0) {
                    orbNinmsWeith = 0;
                }
            }

            if (Projectile.timeLeft == 50 && onOrb) {
                //SoundEngine.PlaySound(ModSound.EndSilkOrbSpanSound with { Volume = 0.1f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), new Vector2(Projectile.ai[0], Projectile.ai[1])
                        , Projectile.velocity, ModContent.ProjectileType<MuraExecutionCut>()
                        , Projectile.damage, 0, Projectile.owner, 0, Main.rand.Next(30));
                }

                onOrb = false;
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (PosLists == null) {
                return;
            }

            Trail ??= new Trail(PosLists.ToArray(), (float _) => orbNinmsWeith, (Vector2 _) => Color.White * orbNinmsWeith);

            Effect effect = Filters.Scene["CWRMod:gradientTrail"].GetShader().Shader;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRAsset.Placeholder_White.Value);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Placeholder_White.Value);
            effect.Parameters["uGradient"].SetValue(CWRAsset.Placeholder_White.Value);
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}

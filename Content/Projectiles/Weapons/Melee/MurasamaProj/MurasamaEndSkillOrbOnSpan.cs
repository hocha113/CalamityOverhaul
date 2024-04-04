using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaEndSkillOrbOnSpan : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        internal PrimitiveTrail LightningDrawer;
        private List<Vector2> PosLists;
        private float orbNinmsWeith;
        private float maxOrbNinmsWeith;
        private bool onSpan = true;
        private bool onOrb = true;

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
            maxOrbNinmsWeith = Main.rand.NextFloat(3, 53.3f);
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (onSpan) {
                Projectile.ai[0] = Projectile.Center.X;
                Projectile.ai[1] = Projectile.Center.Y;
                Projectile.rotation = Projectile.velocity.ToRotation();
                PosLists = new List<Vector2>();
                Vector2 rot = Projectile.velocity.UnitVector();
                for (int i = 0; i < 100; i++) {
                    PosLists.Add(Projectile.Center + (rot * 50 * i));
                }
                Vector2 toOwner = Projectile.Center.To(Main.player[Projectile.owner].Center).UnitVector();
                Projectile.position += toOwner * 1000;
                onSpan = false;
            }

            if (Projectile.timeLeft > 25) {
                //Projectile.ai[0] += 0.001f;
                Projectile.localAI[0] += 0.001f;
                orbNinmsWeith += 0.05f;
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
                    Projectile.NewProjectile(Projectile.parent(), new Vector2(Projectile.ai[0], Projectile.ai[1]), Projectile.velocity, ModContent.ProjectileType<MurasamaEndSkillOrb>()
                , Projectile.damage, 0, Projectile.owner, Projectile.velocity.ToRotation(), Main.rand.Next(100));
                }
                
                onOrb = false;
            }
        }

        public Color PrimitiveColorFunction(float completionRatio) => Color.White;

        public float PrimitiveWidthFunction(float completionRatio) => orbNinmsWeith;

        public override bool PreDraw(ref Color lightColor) {
            if (PosLists == null) 
                return false;

            if (LightningDrawer is null)
                LightningDrawer = new PrimitiveTrail(PrimitiveWidthFunction, PrimitiveColorFunction, PrimitiveTrail.RigidPointRetreivalFunction, GameShaders.Misc["CalamityMod:TrailStreak"]);
            GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            LightningDrawer.Draw(PosLists, Projectile.Size * 0.5f - Main.screenPosition, 18);
            return false;
        }
    }
}

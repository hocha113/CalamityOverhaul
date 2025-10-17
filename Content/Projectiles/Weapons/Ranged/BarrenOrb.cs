﻿using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class BarrenOrb : StormArc
    {
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 9;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Electrified, 120);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < Main.rand.Next(3, 7); i++) {
                    Vector2 pos = target.Center + Main.rand.NextVector2Unit() * Main.rand.Next(target.width);
                    Vector2 particleSpeed = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(15.5f, 37.7f);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , 0.3f, Color.Yellow, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.75f, Pitch = -0.2f }, target.position);
            }
        }

        public override Color PrimitiveColorFunction(float completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = VaultUtils.MultiStepColorLerp(colorInterpolant, Color.Yellow, Color.Gold, Color.White);
            return color;
        }
    }
}

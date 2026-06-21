using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Scenarios.SupCal.End.EternalBlazingNow
{
    internal class EbnPlayer : ModPlayer
    {
        public bool IsEbn => EbnState.OnEbn(Player);

        public static bool OnEbn(Player player) => EbnState.OnEbn(player);

        public static bool IsConquered(Player player) => EbnState.IsConquered(player);

        #region 数据字段
        private readonly List<AuraParticleData> auraParticles = [];
        private float auraPhase;
        private float pulsePhase;
        private float wingFlamePhase;
        private bool _syncIsEbn;

        private class AuraParticleData
        {
            public float Angle;
            public float Distance;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float RotationSpeed;
            public Color Color;
        }
        #endregion

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            EbnState.SendEbnSync(Player, toWho, fromWho);
        }

        public override void SendClientChanges(ModPlayer clientPlayer) {
            if (((EbnPlayer)clientPlayer)._syncIsEbn != IsEbn) {
                EbnState.SendEbnSync(Player);
            }
        }

        public override void CopyClientState(ModPlayer targetCopy) {
            ((EbnPlayer)targetCopy)._syncIsEbn = IsEbn;
        }

        public override void ResetEffects() {
            if (!IsEbn) {
                auraParticles.Clear();
            }
        }

        public override void PostUpdateMiscEffects() {
            if (!IsEbn) {
                return;
            }

            auraPhase += 0.04f;
            pulsePhase += 0.06f;
            wingFlamePhase += 0.08f;

            if (auraPhase > MathHelper.TwoPi) auraPhase -= MathHelper.TwoPi;
            if (pulsePhase > MathHelper.TwoPi) pulsePhase -= MathHelper.TwoPi;
            if (wingFlamePhase > MathHelper.TwoPi) wingFlamePhase -= MathHelper.TwoPi;

            UpdateAuraParticles();
            UpdateLighting();
        }

        public override void ModifyMaxStats(out StatModifier health, out StatModifier mana) {
            health = StatModifier.Default;
            mana = StatModifier.Default;

            if (IsEbn) {
                health.Base = 2200;
                mana.Base = 2400;
            }
        }

        public override void PostUpdateEquips() {
            if (!IsEbn) {
                return;
            }

            Player.statDefense += 50;
            Player.GetDamage(DamageClass.Generic) += 0.5f;
            Player.GetCritChance(DamageClass.Generic) += 50f;
            Player.moveSpeed += 0.2f;
            Player.maxRunSpeed += 2f;
            Player.maxFallSpeed += 1f;
            Player.jumpSpeedBoost += 1f;
            Player.wingTimeMax = (int)(Player.wingTimeMax * 3f);

            Player.noFallDmg = true;
            Player.fireWalk = true;
            Player.buffImmune[BuffID.OnFire] = true;
            Player.buffImmune[BuffID.OnFire3] = true;
            Player.buffImmune[BuffID.CursedInferno] = true;
            Player.buffImmune[BuffID.Burning] = true;

            Player.manaRegen += 50;
            Player.manaCost *= 0.5f;
        }

        private void UpdateAuraParticles() {
            for (int i = auraParticles.Count - 1; i >= 0; i--) {
                var particle = auraParticles[i];
                particle.Life++;
                particle.Angle += particle.RotationSpeed;
                particle.Distance += (float)Math.Sin(particle.Life * 0.1f) * 0.5f;

                if (particle.Life >= particle.MaxLife) {
                    auraParticles.RemoveAt(i);
                }
            }

            if (Main.rand.NextBool(5)) {
                SpawnAuraParticle();
            }
        }

        private void SpawnAuraParticle() {
            auraParticles.Add(new AuraParticleData {
                Angle = Main.rand.NextFloat(MathHelper.TwoPi),
                Distance = Main.rand.NextFloat(40f, 80f),
                Life = 0,
                MaxLife = Main.rand.NextFloat(80f, 120f),
                Scale = Main.rand.NextFloat(0.8f, 1.5f),
                RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f),
                Color = Main.rand.Next([
                    new Color(255, 120, 60),
                    new Color(255, 80, 40),
                    new Color(200, 50, 30)
                ])
            });
        }

        private void UpdateLighting() {
            float pulse = (float)Math.Sin(pulsePhase * 2f) * 0.3f + 0.7f;
            float lightIntensity = 2.5f * pulse;

            Lighting.AddLight(Player.Center,
                2.0f * lightIntensity,
                0.6f * lightIntensity,
                0.3f * lightIntensity);

            if (Player.wingTime > 0) {
                Lighting.AddLight(Player.Center + new Vector2(-25f, -10f),
                    1.5f * pulse, 0.5f * pulse, 0.2f * pulse);
                Lighting.AddLight(Player.Center + new Vector2(25f, -10f),
                    1.5f * pulse, 0.5f * pulse, 0.2f * pulse);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!IsEbn) {
                return;
            }

            if (modifiers.CritDamage.Multiplicative > 0) {
                modifiers.CritDamage *= 2.5f;
            }

            if (target.boss) {
                modifiers.FinalDamage *= 1.5f;
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!IsEbn) {
                return;
            }

            modifiers.FinalDamage *= 0.3f;

            if (Main.rand.NextBool(5)) {
                modifiers.FinalDamage *= 0f;
                for (int i = 0; i < 15; i++) {
                    Dust d = Dust.NewDustPerfect(Player.Center, CWRID.Dust_Brimstone,
                        Main.rand.NextVector2Circular(6f, 6f), 100, default, 2f);
                    d.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.5f }, Player.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsEbn) {
                return;
            }

            if (Main.rand.NextBool(3)) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust d = Dust.NewDustPerfect(target.Center, CWRID.Dust_Brimstone, vel,
                        100, default, 1.5f);
                    d.noGravity = true;
                }
            }

            int healAmount = Math.Max(1, damageDone / 20);
            if (Player.statLife < Player.statLifeMax2) {
                Player.statLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
            }
        }
    }
}

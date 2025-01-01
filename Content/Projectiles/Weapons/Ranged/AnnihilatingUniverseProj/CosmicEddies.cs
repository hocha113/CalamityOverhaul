using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class CosmicEddies : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private float rgs => Projectile.width * Projectile.ai[1] / 40;
        private int time;
        private SlotId soundSlot;
        private SoundStyle modSoundtyle;
        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 560;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Player player = CWRUtils.GetPlayerInstance(Projectile.owner);
            Projectile ownerProj = CWRUtils.GetProjectileInstance((int)Projectile.ai[0]);
            if (!player.Alives()) {
                Projectile.Kill();
                return;
            }

            if (player.PressKey(false) && ownerProj.Alives() && Projectile.ai[2] == 0) {
                if (Projectile.ai[1] == 0) {
                    Projectile.rotation = ownerProj.rotation;
                }

                time++;
                Projectile.ai[1]++;
                if (Projectile.ai[1] > 600) {
                    Projectile.ai[1] = 600;
                }
                Projectile.timeLeft = (int)Projectile.ai[1] + 60;
                Vector2 targetPos = player.Center + Projectile.rotation.ToRotationVector2() * 156;
                Projectile.velocity = Projectile.Center.To(targetPos);
                Projectile.EntityToRot(ownerProj.rotation, 0.1f);

                if (time % 100 == 0 && time > 0 && Projectile.IsOwnedByLocalPlayer()) {
                    float pwer = time / 20;
                    if (pwer > 40) {
                        pwer = 40;
                    }
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.Center.To(Main.MouseWorld).UnitVector() * 23
                    , ModContent.ProjectileType<DivineDevourerIllusionHead>(), Projectile.damage / 2, 3, Projectile.owner, ai1: pwer);
                }
            }
            else {
                Projectile.ai[2]++;
                Projectile.damage = Projectile.originalDamage + (int)Projectile.ai[1] * 5;

                if (Projectile.timeLeft <= Projectile.ai[1] + 30) {
                    NPC target = Projectile.Center.FindClosestNPC(1900);
                    if (target != null) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.1f);
                    }
                }
                else {
                    Projectile.velocity = Projectile.rotation.ToRotationVector2() * 22;
                }

            }
            if (Main.netMode != NetmodeID.Server) {
                int maxdustnum = (int)(Projectile.ai[1] / 40f);
                for (int i = 0; i < maxdustnum; i++) {

                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next((int)rgs);
                    Vector2 particleSpeed = pos.To(Projectile.Center + Projectile.velocity).UnitVector() * Main.rand.NextFloat(5.5f, 7.7f);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.3f + Projectile.ai[1] / 1000f), Color.Purple, 30, 1, 1.5f, hueShift: 0.0f, _entity: Projectile);
                    PRTLoader.AddParticle(energyLeak);
                }

                modSoundtyle = CWRSound.BlackHole;
                if (!SoundEngine.TryGetActiveSound(soundSlot, out var activeSoundTwister)) {
                    soundSlot = SoundEngine.PlaySound(modSoundtyle, Projectile.Center);
                }
                else {
                    // 如果声音正在播放，则更新声音的位置以匹配弹丸的当前位置。
                    activeSoundTwister.Position = Projectile.position;
                }
            }
            if (Projectile.timeLeft < 60) {
                Projectile.scale = Projectile.timeLeft / 60f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, rgs, targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.spriteBatch.EnterShaderRegion();

            Texture2D noiseTexture = ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/VoronoiShapes").Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = noiseTexture.Size() * 0.5f;
            GameShaders.Misc["CalamityMod:DoGPortal"].UseOpacity(Projectile.scale);
            GameShaders.Misc["CalamityMod:DoGPortal"].UseColor(Color.DarkBlue);
            GameShaders.Misc["CalamityMod:DoGPortal"].UseSecondaryColor(Color.BlueViolet);
            GameShaders.Misc["CalamityMod:DoGPortal"].Apply();

            float slp = Projectile.ai[1] / 300f + MathF.Sin(Main.GameUpdateCount * CWRUtils.atoR * 2) * 0.1f;
            Main.EntitySpriteDraw(noiseTexture, drawPosition, null, Color.White, 0f, origin, slp, SpriteEffects.None, 0);
            Main.spriteBatch.ExitShaderRegion();

            return false;
        }
    }
}

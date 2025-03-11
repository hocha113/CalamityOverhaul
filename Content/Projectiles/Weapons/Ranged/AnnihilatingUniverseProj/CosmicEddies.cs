using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class CosmicEddies : BaseHeldProj, ICWRLoader
    {
        public override string Texture => CWRConstant.Placeholder;
        private float Rots => Projectile.width * Projectile.ai[1] / 40;
        private int Time { get; set; }
        private SlotId SoundSlot { get; set; }
        private static Asset<Texture2D> VoronoiShapes { get; set; }
        void ICWRLoader.LoadAsset() => VoronoiShapes = CWRUtils.GetT2DAsset("CalamityMod/ExtraTextures/GreyscaleGradients/VoronoiShapes");
        void ICWRLoader.UnLoadData() => VoronoiShapes = null;
        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = 6;
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

                Time++;
                Projectile.ai[1]++;
                if (Projectile.ai[1] > 600) {
                    Projectile.ai[1] = 600;
                }
                Projectile.timeLeft = (int)Projectile.ai[1] + 60;
                Vector2 targetPos = player.Center + Projectile.rotation.ToRotationVector2() * 156;
                Projectile.velocity = Projectile.Center.To(targetPos);
                Projectile.EntityToRot(ownerProj.rotation, 0.1f);

                if (Time % 100 == 0 && Time > 0) {
                    SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 0.4f });
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        float pwer = Time / 20;
                        if (pwer > 40) {
                            pwer = 40;
                        }
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, UnitToMouseV * 23
                        , ModContent.ProjectileType<DivineDevourerIllusionHead>(), Projectile.damage / 2, 3, Projectile.owner, pwer);
                    }
                }
            }
            else {
                Projectile.ai[2]++;
                Projectile.damage = Projectile.originalDamage + (int)Projectile.ai[1] * 5;

                if (Projectile.timeLeft <= Projectile.ai[1] + 30) {
                    NPC target = Projectile.Center.FindClosestNPC(1900);
                    if (target != null) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.3f);
                        if (Projectile.Distance(target.Center) < 120) {
                            Projectile.Kill();
                        }
                    }
                }
                else {
                    Projectile.velocity = Projectile.rotation.ToRotationVector2() * 22;
                }

            }

            if (!VaultUtils.isServer) {
                int maxdustnum = (int)(Projectile.ai[1] / 40f);
                for (int i = 0; i < maxdustnum; i++) {

                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next((int)Rots);
                    Vector2 particleSpeed = pos.To(Projectile.Center + Projectile.velocity).UnitVector() * Main.rand.NextFloat(5.5f, 7.7f);
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.3f + Projectile.ai[1] / 1000f), Color.Purple, 30, 1, 1.5f, hueShift: 0.0f, _entity: Projectile);
                    PRTLoader.AddParticle(energyLeak);
                }

                if (!SoundEngine.TryGetActiveSound(SoundSlot, out var activeSoundTwister)) {
                    SoundSlot = SoundEngine.PlaySound(CWRSound.BlackHole with { MaxInstances = 5 }, Projectile.Center);
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

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, Rots, targetHitbox);

        public override void OnKill(int timeLeft) {
            Projectile.Explode(800);
            if (!VaultUtils.isServer) {//生成这种粒子不是好主意
                for (int i = 0; i < Rots; i++) {
                    Vector2 particleSpeed = CWRUtils.GetRandomVevtor(0, 360, Main.rand.Next(16, 49));
                    Vector2 pos = Projectile.Center;
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.4f, 1.2f), Color.Purple, 60, 1, 1.5f, hueShift: 0.0f);
                    energyLeak.ShouldKillWhenOffScreen = false;
                    PRTLoader.AddParticle(energyLeak);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.spriteBatch.EnterShaderRegion();
            Texture2D noiseTexture = VoronoiShapes.Value;
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

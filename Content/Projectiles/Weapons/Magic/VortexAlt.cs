using CalamityMod;
using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Magic;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    //变化就是好事，不是吗？
    internal class VortexAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder2;
        public const int ExplosionDelay = 45;
        public const float InitialScale = 0.0004f;
        public const float MaxScale = 2.7f;
        public static int ID { get; private set; }
        [VaultLoaden("@CalamityMod/ExtraTextures/GreyscaleGradients/BlobbyNoise")]
        internal static Asset<Texture2D> WorleyNoise { get; private set; }
        public float Time {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        public bool HasBeenReleased {
            get => Projectile.ai[1] == 1f;
            set => Projectile.ai[1] = value.ToInt();
        }
        //自定义本地化键
        public override LocalizedText DisplayName => ProjectileLoader.GetProjectile(ModContent.ProjectileType<EnormousConsumingVortex>()).DisplayName;
        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() => Projectile.CloneDefaults(ModContent.ProjectileType<EnormousConsumingVortex>());
        public override void NetHeldSend(BinaryWriter writer) => writer.Write(Projectile.scale);
        public override void NetHeldReceive(BinaryReader reader) => Projectile.scale = reader.ReadSingle();
        public override void AI() {
            Lighting.AddLight(Projectile.Center, Vector3.One * 1.3f);

            if ((!DownRight || Owner.CCed) && !HasBeenReleased) {
                HandleEarlyTermination();
                return;
            }

            if (ShouldDestroyProjectile()) {
                Projectile.Kill();
                return;
            }

            GenerateEffects();
            ManageProjectileBehavior();
            UpdateProjectileState();
            HandleFinalPhase();
            Time++;
        }

        private void HandleEarlyTermination() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (Time >= SubsumingVortex.LargeVortexChargeupTime || Time >= SubsumingVortex.VortexShootDelay) {
                    float speedFactor = Time >= SubsumingVortex.LargeVortexChargeupTime ? SubsumingVortex.ReleaseDamageFactor : (1f + Time * 0.0152f);
                    FireProjectile(InMousePos, speedFactor);
                }
                else {
                    Projectile.Kill();
                }
            }
        }

        private bool ShouldDestroyProjectile() => HasBeenReleased && Projectile.timeLeft > ExplosionDelay && !Projectile.WithinRange(Owner.Center, 2000f);

        private void FireProjectile(Vector2 target, float damageMultiplier) {
            Projectile.velocity = Projectile.SafeDirectionTo(target) * SubsumingVortex.ReleaseSpeed;
            Projectile.damage = (int)(Projectile.damage * damageMultiplier);
            HasBeenReleased = true;
            Projectile.netUpdate = true;
        }

        private void GenerateEffects() {
            if (Time < SubsumingVortex.VortexShootDelay) return;

            Vector2 position = Owner.GetPlayerStabilityCenter() + new Vector2(Owner.direction * 22f, 0f);
            if (Main.rand.NextBool()) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Color effectColor = CalamityUtils.MulticolorLerp(Main.rand.NextFloat(), CalamityUtils.ExoPalette);
                GeneralParticleHandler.SpawnParticle(new SquishyLightParticle(position, velocity, 0.55f, effectColor, 40, 1f, 1.5f));
            }
        }

        private void ManageProjectileBehavior() {
            NPC target = Projectile.Center.ClosestNPCAt(SubsumingVortex.SmallVortexTargetRange - 100f);
            if (target == null || HasBeenReleased || Time >= SubsumingVortex.LargeVortexChargeupTime
                || Time % SubsumingVortex.VortexReleaseRate != SubsumingVortex.VortexReleaseRate - 1) {
                return;
            }
            if (DownRight && Time >= SubsumingVortex.VortexShootDelay) {
                SoundEngine.PlaySound(SoundID.Item84, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Vector2 direction = Projectile.SafeDirectionTo(target.Center) * 8f;
                    float hue = (Time - SubsumingVortex.VortexShootDelay) / 125f;
                    hue = MathHelper.Clamp(hue, 0, 1f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, direction
                        , ModContent.ProjectileType<ExoVortex>(), Projectile.damage, Projectile.knockBack, Projectile.owner, hue);
                }
                Projectile.netUpdate = true;
            }
        }

        private void UpdateProjectileState() {
            if (Projectile.IsOwnedByLocalPlayer() && !HasBeenReleased) {
                Vector2 targetPos = Owner.GetPlayerStabilityCenter() + new Vector2(Owner.direction * Projectile.scale * 30f
                    , Utils.Remap(Time, 0f, 90f, -30f, (float)Math.Cos(Projectile.timeLeft / 32f) * 30f));
                targetPos += (InMousePos - targetPos) * SubsumingVortex.GiantVortexMouseDriftFactor;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (targetPos - Projectile.Center).UnitVector() * 32f, 0.04f);
                Projectile.netSpam = 0;
                Projectile.netUpdate = true;
            }
        }

        private void HandleFinalPhase() {
            Projectile.Opacity = Utils.GetLerpValue(0f, 20f, Time, true) * Utils.GetLerpValue(0f, ExplosionDelay, Projectile.timeLeft, true);
            Projectile.scale = Utils.Remap(Time, 0f, SubsumingVortex.LargeVortexChargeupTime
                , InitialScale, MaxScale) * Utils.Remap(Projectile.timeLeft, ExplosionDelay, 1f, 1f, 5.4f);
            Projectile.ExpandHitboxBy((int)(Projectile.scale * 62f));
            if (Projectile.timeLeft < ExplosionDelay) Projectile.velocity *= 0.8f;
            Projectile.spriteDirection = Projectile.direction = Owner.direction;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!HasBeenReleased || Projectile.timeLeft < ExplosionDelay) {
                return;
            }

            SoundEngine.PlaySound(SubsumingVortex.ExplosionSound with { Volume = 1.3f }, Projectile.Center);
            Projectile.timeLeft = ExplosionDelay;
            Projectile.netUpdate = true;
        }

        public static void DoDraw() {
            Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
            Texture2D worleyNoise = WorleyNoise.Value;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != ID) {
                    continue;
                }

                Vector2 drawPosition = proj.Center - Main.screenPosition;
                Vector2 scale = proj.Size / worleyNoise.Size() * 2f;
                float spinRotation = Main.GlobalTimeWrappedHourly * 2.4f;

                GameShaders.Misc["CalamityMod:ExoVortex"].Apply();

                for (int i = 0; i < CalamityUtils.ExoPalette.Length; i++) {
                    float spinDirection = (i % 2f == 0f).ToDirectionInt();
                    Vector2 drawOffset = (MathHelper.TwoPi * i / CalamityUtils.ExoPalette.Length +
                        Main.GlobalTimeWrappedHourly * spinDirection * 4f).ToRotationVector2() * proj.scale * 15f;
                    Main.spriteBatch.Draw(worleyNoise, drawPosition + drawOffset, null
                        , CalamityUtils.ExoPalette[i] * proj.Opacity, spinDirection * spinRotation, worleyNoise.Size() * 0.5f, scale, 0, 0f);
                }
            }
            Main.spriteBatch.ExitShaderRegion();
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}

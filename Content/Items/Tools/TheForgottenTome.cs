using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class TheForgottenTome : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/TheForgottenTome";
        public static LocalizedText CompletionText;
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
            CompletionText = this.GetLocalization(nameof(CompletionText), () => "你已经遗忘所有");
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useAnimation = 60;
            Item.useTime = 60;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            Item.maxStack = 1;
            Item.rare = ModContent.RarityType<Violet>();
            Item.value = Item.sellPrice(platinum: 5);
            Item.UseSound = SoundID.Item122;
        }

        public override bool CanUseItem(Player player) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }
            ADVSave save = halibutPlayer.ADCSave;
            if (save == null) {
                return false;
            }
            return IsAnySaveDataActive(save);
        }

        private static bool IsAnySaveDataActive(ADVSave save) {
            FieldInfo[] fields = typeof(ADVSave).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (field.FieldType == typeof(bool)) {
                    bool value = (bool)field.GetValue(save);
                    if (value) {
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool? UseItem(Player player) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }
            if (Main.myPlayer == player.whoAmI) {
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<ForgottenTomeEffect>(),
                    0,
                    0,
                    player.whoAmI
                );
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Book)
                .AddIngredient(ItemID.FallenStar, 20)
                .AddIngredient<CosmiliteBar>(5)
                .AddIngredient(ItemID.FragmentNebula, 10)
                .AddIngredient(ItemID.FragmentVortex, 10)
                .AddIngredient(ItemID.LunarBar, 10)
                .AddTile(TileID.Bookcases)
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            foreach (TooltipLine line in tooltips) {
                if (line.Name == "ItemName") {
                    continue;
                }
                line.OverrideColor = Color.IndianRed;
                line.Text = line.Text.Replace("[NAME]", Main.LocalPlayer.name);
            }
        }
    }

    internal class ForgottenTomeEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private enum EffectPhase
        {
            Gather,
            Rewind,
            Reset,
            Complete
        }

        private EffectPhase Phase {
            get => (EffectPhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        private const int GatherDuration = 60;
        private const int RewindDuration = 90;
        private const int ResetDuration = 30;
        private const int CompleteDuration = 45;

        private readonly List<TimeParticle> particles = new List<TimeParticle>();
        private readonly List<ClockHand> clockHands = new List<ClockHand>();
        private float auraRadius = 0f;
        private float auraIntensity = 0f;
        private int resetFieldCount = 0;

        public override void SetDefaults() {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = GatherDuration + RewindDuration + ResetDuration + CompleteDuration;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            Timer++;

            switch (Phase) {
                case EffectPhase.Gather:
                    GatherPhaseAI(owner);
                    break;
                case EffectPhase.Rewind:
                    RewindPhaseAI(owner);
                    break;
                case EffectPhase.Reset:
                    ResetPhaseAI(owner);
                    break;
                case EffectPhase.Complete:
                    CompletePhaseAI(owner);
                    break;
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].ShouldRemove) {
                    particles.RemoveAt(i);
                }
            }

            for (int i = clockHands.Count - 1; i >= 0; i--) {
                clockHands[i].Update(owner.Center);
                if (clockHands[i].ShouldRemove) {
                    clockHands.RemoveAt(i);
                }
            }
        }

        private void GatherPhaseAI(Player owner) {
            if (Timer == 1) {
                InitializeClockHands(owner);
                PlayGatherSound(owner);
            }

            float progress = Timer / GatherDuration;
            auraRadius = MathHelper.Lerp(0f, 300f, EaseOutCubic(progress));
            auraIntensity = MathHelper.Lerp(0f, 1f, progress);

            if (Timer % 3 == 0) {
                SpawnGatherParticles(owner.Center);
            }

            if (Timer % 20 == 0) {
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.3f,
                    Pitch = -0.5f + progress * 0.5f
                }, owner.Center);
            }

            if (Timer >= GatherDuration) {
                Phase = EffectPhase.Rewind;
                Timer = 0;
                PlayRewindSound(owner);
            }
        }

        private void RewindPhaseAI(Player owner) {
            float progress = Timer / RewindDuration;

            auraRadius = MathHelper.Lerp(300f, 150f, progress);
            auraIntensity = MathHelper.Lerp(1f, 1.2f, (float)Math.Sin(progress * MathHelper.Pi));

            if (Timer % 2 == 0) {
                SpawnRewindParticles(owner.Center, progress);
            }

            if (Timer % 15 == 0) {
                SoundEngine.PlaySound(SoundID.Item9 with {
                    Volume = 0.4f,
                    Pitch = 0.5f - progress * 0.8f
                }, owner.Center);
            }

            if (Timer >= RewindDuration) {
                Phase = EffectPhase.Reset;
                Timer = 0;
                ResetAllADVData(owner);
                PlayResetSound(owner);
            }
        }

        private void ResetPhaseAI(Player owner) {
            float progress = Timer / ResetDuration;

            auraRadius = MathHelper.Lerp(150f, 0f, EaseInCubic(progress));
            auraIntensity = MathHelper.Lerp(1.2f, 0.3f, progress);

            if (Timer % 1 == 0) {
                SpawnResetBurst(owner.Center);
            }

            if (Timer % 5 == 0) {
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.3f,
                    Pitch = progress * 0.8f
                }, owner.Center);
            }

            if (Timer >= ResetDuration) {
                Phase = EffectPhase.Complete;
                Timer = 0;
                PlayCompleteSound(owner);
                ShowCompletionMessage(owner);
            }
        }

        private void CompletePhaseAI(Player owner) {
            float progress = Timer / CompleteDuration;
            auraIntensity = MathHelper.Lerp(0.3f, 0f, progress);

            if (Timer >= CompleteDuration) {
                Projectile.Kill();
            }
        }

        private void InitializeClockHands(Player owner) {
            for (int i = 0; i < 12; i++) {
                float angle = (i / 12f) * MathHelper.TwoPi;
                clockHands.Add(new ClockHand(angle, 150f + i * 10f));
            }
        }

        private void ResetAllADVData(Player owner) {
            if (!owner.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            ADVSave save = halibutPlayer.ADCSave;
            if (save == null) {
                return;
            }

            FieldInfo[] fields = typeof(ADVSave).GetFields(BindingFlags.Public | BindingFlags.Instance);
            resetFieldCount = 0;

            foreach (FieldInfo field in fields) {
                if (field.FieldType == typeof(bool)) {
                    bool value = (bool)field.GetValue(save);
                    if (value) {
                        field.SetValue(save, false);
                        resetFieldCount++;
                    }
                }
            }

            ScenarioManager.ResetAll();

            for (int i = 0; i < 30; i++) {
                SpawnResetExplosion(owner.Center);
            }
        }

        private void SpawnGatherParticles(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(50f, auraRadius);
            Vector2 pos = center + angle.ToRotationVector2() * radius;
            Vector2 velocity = (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);

            particles.Add(new TimeParticle(pos, velocity, TimeParticle.ParticleType.Gather));
        }

        private void SpawnRewindParticles(Vector2 center, float intensity) {
            for (int i = 0; i < 2; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(100f, 250f);
                Vector2 pos = center + angle.ToRotationVector2() * radius;

                float orbitSpeed = Main.rand.NextFloat(0.05f, 0.1f);
                Vector2 velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * orbitSpeed * 100f;
                velocity += (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);

                particles.Add(new TimeParticle(pos, velocity, TimeParticle.ParticleType.Rewind));
            }
        }

        private void SpawnResetBurst(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
            particles.Add(new TimeParticle(center, velocity, TimeParticle.ParticleType.Reset));
        }

        private void SpawnResetExplosion(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);
            particles.Add(new TimeParticle(center, velocity, TimeParticle.ParticleType.Burst));
        }

        private static void PlayGatherSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.6f,
                Pitch = -0.5f
            }, owner.Center);
        }

        private static void PlayRewindSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.8f,
                Pitch = 0.5f
            }, owner.Center);
        }

        private static void PlayResetSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item62 with {
                Volume = 0.8f,
                Pitch = -0.3f
            }, owner.Center);
        }

        private static void PlayCompleteSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.7f,
                Pitch = 0.8f
            }, owner.Center);
            SoundEngine.PlaySound(SoundID.MaxMana, owner.Center);
        }

        private static void ShowCompletionMessage(Player owner) {
            if (Main.netMode != NetmodeID.Server) {
                string text = TheForgottenTome.CompletionText.Value;
                CombatText.NewText(owner.Hitbox, new Color(180, 180, 255), text, true);
            }
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Player owner = Main.player[Projectile.owner];

            DrawAura(sb, owner.Center);
            DrawClockHands(sb, owner.Center);

            foreach (var particle in particles) {
                particle.Draw(sb);
            }

            if (Phase == EffectPhase.Reset || Phase == EffectPhase.Complete) {
                DrawCenterGlow(sb, owner.Center);
            }

            return false;
        }

        private void DrawAura(SpriteBatch sb, Vector2 center) {
            if (auraIntensity <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color auraColor = new Color(180, 180, 255) * (auraIntensity * 0.3f);

            for (int i = 0; i < 3; i++) {
                float ringRadius = auraRadius * (0.8f + i * 0.1f);
                float ringThickness = 1.5f + i;
                int segments = 64;

                for (int j = 0; j < segments; j++) {
                    float angle1 = (j / (float)segments) * MathHelper.TwoPi;
                    float angle2 = ((j + 1) / (float)segments) * MathHelper.TwoPi;

                    Vector2 pos1 = center + angle1.ToRotationVector2() * ringRadius;
                    Vector2 pos2 = center + angle2.ToRotationVector2() * ringRadius;

                    Vector2 diff = pos2 - pos1;
                    float length = diff.Length();
                    float rotation = diff.ToRotation();

                    sb.Draw(pixel, pos1 - Main.screenPosition, null, auraColor,
                        rotation, Vector2.Zero, new Vector2(length, ringThickness), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawClockHands(SpriteBatch sb, Vector2 center) {
            foreach (var hand in clockHands) {
                hand.Draw(sb, center);
            }
        }

        private void DrawCenterGlow(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(Timer * 0.3f) * 0.3f + 0.7f;
            Color glowColor = new Color(180, 180, 255) * (auraIntensity * pulse);

            for (int i = 0; i < 5; i++) {
                float scale = 20f * (1f + i * 0.3f);
                float alpha = (1f - i / 5f) * auraIntensity;
                sb.Draw(pixel, center - Main.screenPosition, null, glowColor * alpha,
                    0f, new Vector2(0.5f), new Vector2(scale), SpriteEffects.None, 0f);
            }
        }
    }

    internal class TimeParticle
    {
        public enum ParticleType
        {
            Gather,
            Rewind,
            Reset,
            Burst
        }

        public Vector2 Position;
        public Vector2 Velocity;
        public ParticleType Type;
        public float Scale;
        public float Rotation;
        public float Alpha;
        public int Life;
        public int MaxLife;

        public bool ShouldRemove => Life >= MaxLife;

        public TimeParticle(Vector2 pos, Vector2 vel, ParticleType type) {
            Position = pos;
            Velocity = vel;
            Type = type;
            Scale = Main.rand.NextFloat(0.8f, 1.5f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Alpha = 1f;
            Life = 0;
            MaxLife = type switch {
                ParticleType.Gather => Main.rand.Next(40, 70),
                ParticleType.Rewind => Main.rand.Next(50, 90),
                ParticleType.Reset => Main.rand.Next(30, 60),
                ParticleType.Burst => Main.rand.Next(40, 80),
                _ => 60
            };
        }

        public void Update() {
            Life++;
            Position += Velocity;

            if (Type == ParticleType.Gather || Type == ParticleType.Rewind) {
                Velocity *= 0.98f;
            } else {
                Velocity *= 0.96f;
            }

            Rotation += 0.08f;
            Alpha = 1f - (Life / (float)MaxLife);
        }

        public void Draw(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color color = Type switch {
                ParticleType.Gather => new Color(180, 180, 255),
                ParticleType.Rewind => new Color(200, 200, 255),
                ParticleType.Reset => new Color(255, 255, 255),
                ParticleType.Burst => new Color(220, 220, 255),
                _ => Color.White
            } * Alpha;

            sb.Draw(pixel, Position - Main.screenPosition, null, color,
                Rotation, new Vector2(0.5f), new Vector2(Scale * 3f), SpriteEffects.None, 0f);
        }
    }

    internal class ClockHand
    {
        public float Angle;
        public float Radius;
        public float RotationSpeed;
        public float Alpha;
        public int Life;
        public int MaxLife;

        public bool ShouldRemove => Life >= MaxLife;

        public ClockHand(float angle, float radius) {
            Angle = angle;
            Radius = radius;
            RotationSpeed = Main.rand.NextFloat(0.02f, 0.05f) * (Main.rand.NextBool() ? 1 : -1);
            Alpha = 1f;
            Life = 0;
            MaxLife = 300;
        }

        public void Update(Vector2 center) {
            Life++;
            Angle += RotationSpeed;
            Alpha = 1f - (Life / (float)MaxLife);
        }

        public void Draw(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 endPos = center + Angle.ToRotationVector2() * Radius;

            Vector2 diff = endPos - center;
            float length = diff.Length();
            float rotation = diff.ToRotation();

            Color color = new Color(180, 180, 255) * (Alpha * 0.6f);

            sb.Draw(pixel, center - Main.screenPosition, null, color,
                rotation, Vector2.Zero, new Vector2(length, 2f), SpriteEffects.None, 0f);

            for (int i = 0; i < 3; i++) {
                float glowAlpha = (1f - i / 3f) * Alpha * 0.3f;
                sb.Draw(pixel, endPos - Main.screenPosition, null, color * glowAlpha,
                    0f, new Vector2(0.5f), new Vector2(6f + i * 2f), SpriteEffects.None, 0f);
            }
        }
    }
}

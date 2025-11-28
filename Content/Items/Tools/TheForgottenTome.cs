using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.ADV.DialogueBoxs;
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
            if (player.CountProjectilesOfID<ForgottenTomeEffect>() > 0) {
                return false;
            }
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }
            ADVSave save = halibutPlayer.ADVSave;
            if (save == null) {
                return false;
            }
            return IsAnySaveDataActive(save);
        }

        internal static bool IsAnySaveDataActive(ADVSave save) {
            FieldInfo[] fields = typeof(ADVSave).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (field.FieldType == typeof(bool)) {
                    bool value = (bool)field.GetValue(save);
                    if (value) {
                        return true;
                    }
                }
                if (field.FieldType == typeof(int)) {
                    int value = (int)field.GetValue(save);
                    if (value != 0) {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static int ResetAllADVData(Player owner) {
            int resetFieldCount = 0;
            if (!owner.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return resetFieldCount;
            }

            ADVSave save = halibutPlayer.ADVSave;
            if (save == null) {
                return resetFieldCount;
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
                if (field.FieldType == typeof(int)) {
                    int value = (int)field.GetValue(save);
                    if (value != 0) {
                        field.SetValue(save, 0);
                        resetFieldCount++;
                    }
                }
            }

            DialogueUIRegistry.ForceCloseBox(DialogueUIRegistry.Current);
            ScenarioManager.ResetAll();
            return resetFieldCount;
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
            auraRadius = MathHelper.Lerp(0f, 300f, CWRUtils.EaseOutCubic(progress));
            auraIntensity = MathHelper.Lerp(0f, 1f, progress);

            if (Timer % 2 == 0) {
                SpawnGatherParticles(owner.Center);
            }

            if (Timer % 20 == 0) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                    Volume = 0.3f,
                    Pitch = -0.5f + progress * 0.3f
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
            auraIntensity = MathHelper.Lerp(1f, 1.5f, (float)Math.Sin(progress * MathHelper.Pi));

            if (Timer % 2 == 0) {
                SpawnRewindParticles(owner.Center, progress);
            }

            if (Timer % 12 == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.4f,
                    Pitch = 0.3f - progress * 0.6f
                }, owner.Center);
            }

            if (Timer >= RewindDuration) {
                Phase = EffectPhase.Reset;
                Timer = 0;
                TheForgottenTome.ResetAllADVData(owner);
                for (int i = 0; i < 40; i++) {
                    SpawnResetExplosion(owner.Center);
                }
                PlayResetSound(owner);
            }
        }

        private void ResetPhaseAI(Player owner) {
            float progress = Timer / ResetDuration;

            auraRadius = MathHelper.Lerp(150f, 0f, CWRUtils.EaseInCubic(progress));
            auraIntensity = MathHelper.Lerp(1.5f, 0.4f, progress);

            if (Timer % 1 == 0) {
                SpawnResetBurst(owner.Center);
            }

            if (Timer % 5 == 0) {
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.35f,
                    Pitch = progress * 0.5f
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
            auraIntensity = MathHelper.Lerp(0.4f, 0f, progress);

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

        private void SpawnGatherParticles(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(50f, auraRadius);
            Vector2 pos = center + angle.ToRotationVector2() * radius;
            Vector2 velocity = (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.5f, 3.5f);

            particles.Add(new TimeParticle(pos, velocity, TimeParticle.ParticleType.Gather));
        }

        private void SpawnRewindParticles(Vector2 center, float intensity) {
            for (int i = 0; i < 3; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(100f, 250f);
                Vector2 pos = center + angle.ToRotationVector2() * radius;

                float orbitSpeed = Main.rand.NextFloat(0.06f, 0.12f);
                Vector2 velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * orbitSpeed * 100f;
                velocity += (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2.5f, 5f);

                particles.Add(new TimeParticle(pos, velocity, TimeParticle.ParticleType.Rewind));
            }
        }

        private void SpawnResetBurst(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 7f);
            particles.Add(new TimeParticle(center, velocity, TimeParticle.ParticleType.Reset));
        }

        private void SpawnResetExplosion(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 14f);
            particles.Add(new TimeParticle(center, velocity, TimeParticle.ParticleType.Burst));
        }

        private static void PlayGatherSound(Player owner) {
            SoundEngine.PlaySound(SoundID.NPCDeath13 with {
                Volume = 0.7f,
                Pitch = -0.6f
            }, owner.Center);
        }

        private static void PlayRewindSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Zombie53 with {
                Volume = 0.8f,
                Pitch = -0.3f
            }, owner.Center);
        }

        private static void PlayResetSound(Player owner) {
            SoundEngine.PlaySound(SoundID.NPCDeath19 with {
                Volume = 0.9f,
                Pitch = -0.5f
            }, owner.Center);
        }

        private static void PlayCompleteSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item103 with {
                Volume = 0.8f,
                Pitch = -0.4f
            }, owner.Center);
        }

        private static void ShowCompletionMessage(Player owner) {
            if (Main.netMode != NetmodeID.Server) {
                string text = TheForgottenTome.CompletionText.Value;
                CombatText.NewText(owner.Hitbox, new Color(200, 50, 50), text, true);
            }
        }

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

            //血红色光晕，带有脉动效果
            float pulse = (float)Math.Sin(Timer * 0.15f) * 0.2f + 0.8f;
            Color auraColor = new Color(200, 30, 30) * (auraIntensity * 0.4f * pulse);
            Color innerColor = new Color(140, 0, 0) * (auraIntensity * 0.5f * pulse);

            for (int i = 0; i < 4; i++) {
                float ringRadius = auraRadius * (0.7f + i * 0.15f);
                float ringThickness = 2f + i * 0.8f;
                int segments = 64;

                Color ringColor = Color.Lerp(innerColor, auraColor, i / 4f);

                for (int j = 0; j < segments; j++) {
                    float angle1 = (j / (float)segments) * MathHelper.TwoPi;
                    float angle2 = ((j + 1) / (float)segments) * MathHelper.TwoPi;

                    Vector2 pos1 = center + angle1.ToRotationVector2() * ringRadius;
                    Vector2 pos2 = center + angle2.ToRotationVector2() * ringRadius;

                    Vector2 diff = pos2 - pos1;
                    float length = diff.Length();
                    float rotation = diff.ToRotation();

                    sb.Draw(pixel, pos1 - Main.screenPosition, null, ringColor,
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
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = (float)Math.Sin(Timer * 0.4f) * 0.4f + 0.6f;
            Color glowColor = new Color(220, 20, 20, 0) * (auraIntensity * pulse);
            Color darkGlow = new Color(100, 0, 0, 0) * (auraIntensity * pulse * 0.8f);

            for (int i = 0; i < 6; i++) {
                float scale = 5f * (1f + i * 0.4f);
                float alpha = (1f - i / 6f) * auraIntensity;
                Color color = Color.Lerp(glowColor, darkGlow, i / 6f);
                sb.Draw(glow, center - Main.screenPosition, null, color * alpha,
                    0f, glow.Size() / 2, scale, SpriteEffects.None, 0f);
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
        public Color BaseColor;

        public bool ShouldRemove => Life >= MaxLife;

        public TimeParticle(Vector2 pos, Vector2 vel, ParticleType type) {
            Position = pos;
            Velocity = vel;
            Type = type;
            Scale = Main.rand.NextFloat(0.9f, 1.8f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Alpha = 1f;
            Life = 0;
            MaxLife = type switch {
                ParticleType.Gather => Main.rand.Next(35, 65),
                ParticleType.Rewind => Main.rand.Next(45, 85),
                ParticleType.Reset => Main.rand.Next(25, 55),
                ParticleType.Burst => Main.rand.Next(35, 75),
                _ => 60
            };

            //设置不同类型粒子的血红色调
            BaseColor = type switch {
                ParticleType.Gather => new Color(180, 40, 40),
                ParticleType.Rewind => new Color(200, 20, 20),
                ParticleType.Reset => new Color(220, 50, 50),
                ParticleType.Burst => new Color(160, 10, 10),
                _ => new Color(200, 30, 30)
            };
        }

        public void Update() {
            Life++;
            Position += Velocity;

            if (Type == ParticleType.Gather || Type == ParticleType.Rewind) {
                Velocity *= 0.97f;
            }
            else {
                Velocity *= 0.94f;
            }

            Rotation += 0.1f;
            Alpha = 1f - (Life / (float)MaxLife);

            //粒子在消散时变暗
            float fadeProgress = Life / (float)MaxLife;
            if (fadeProgress > 0.7f) {
                BaseColor = Color.Lerp(BaseColor, new Color(80, 0, 0), (fadeProgress - 0.7f) / 0.3f);
            }
        }

        public void Draw(SpriteBatch sb) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color color = BaseColor * Alpha;

            float finalScale = Scale * (1f + (float)Math.Sin(Life * 0.3f) * 0.2f);

            sb.Draw(pixel, Position - Main.screenPosition, null, color,
                Rotation, new Vector2(0.5f), new Vector2(finalScale * 3.5f), SpriteEffects.None, 0f);
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
            RotationSpeed = Main.rand.NextFloat(0.025f, 0.06f) * (Main.rand.NextBool() ? 1 : -1);
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

            //血红色时针，带有暗红渐变
            Color handColor = new Color(160, 20, 20) * (Alpha * 0.7f);
            Color tipColor = new Color(200, 40, 40) * (Alpha * 0.8f);

            sb.Draw(pixel, center - Main.screenPosition, null, handColor,
                rotation, Vector2.Zero, new Vector2(length, 2.5f), SpriteEffects.None, 0f);

            //时针末端的血珠效果
            for (int i = 0; i < 4; i++) {
                float glowAlpha = (1f - i / 4f) * Alpha * 0.4f;
                Color glowColor = Color.Lerp(handColor, tipColor, i / 4f);
                sb.Draw(pixel, endPos - Main.screenPosition, null, glowColor * glowAlpha,
                    0f, new Vector2(0.5f), new Vector2(7f + i * 2.5f), SpriteEffects.None, 0f);
            }
        }
    }
}

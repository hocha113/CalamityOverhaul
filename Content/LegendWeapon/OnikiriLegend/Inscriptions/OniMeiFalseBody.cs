using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>Frozen player-pose decoy left at the start of a Flash Step</summary>
    internal class OniMeiFalseBody : ModProjectile
    {
        private const int DefaultLifetime = 90;
        private const int ShatterVisualTicks = 12;
        private const float BladeScale = 0.9f;

        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float LifeMax => ref Projectile.ai[0];

        private bool initialized;
        private bool poseCaptured;
        private bool shattered;
        private bool shatterVfxPlayed;
        private byte shatterAge;
        private int timer;
        private float seed;

        private Vector2 snapshotDrawOffset;
        private int snapshotDirection = 1;
        private float snapshotGravDir = 1f;
        private Rectangle snapshotBodyFrame;
        private Rectangle snapshotLegFrame;
        private float snapshotFullRotation;
        private Vector2 snapshotFullRotationOrigin;
        private float snapshotBladeRotation;
        private int snapshotBladeFacing = 1;

        internal bool IsAvailable => Projectile.active && !shattered;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 56;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = DefaultLifetime;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(poseCaptured);
            if (poseCaptured) {
                WriteVector2(writer, snapshotDrawOffset);
                writer.Write((sbyte)snapshotDirection);
                writer.Write(snapshotGravDir);
                WriteRectangle(writer, snapshotBodyFrame);
                WriteRectangle(writer, snapshotLegFrame);
                writer.Write(snapshotFullRotation);
                WriteVector2(writer, snapshotFullRotationOrigin);
                writer.Write(snapshotBladeRotation);
                writer.Write((sbyte)snapshotBladeFacing);
            }
            writer.Write(shattered);
            writer.Write(shatterAge);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            poseCaptured = reader.ReadBoolean();
            if (poseCaptured) {
                snapshotDrawOffset = ReadVector2(reader);
                snapshotDirection = reader.ReadSByte() >= 0 ? 1 : -1;
                snapshotGravDir = reader.ReadSingle() >= 0f ? 1f : -1f;
                snapshotBodyFrame = ReadRectangle(reader);
                snapshotLegFrame = ReadRectangle(reader);
                snapshotFullRotation = reader.ReadSingle();
                snapshotFullRotationOrigin = ReadVector2(reader);
                snapshotBladeRotation = reader.ReadSingle();
                snapshotBladeFacing = reader.ReadSByte() >= 0 ? 1 : -1;
            }

            bool wasShattered = shattered;
            shattered = reader.ReadBoolean();
            shatterAge = reader.ReadByte();
            if (shattered && !wasShattered) {
                Projectile.timeLeft = Math.Max(2, ShatterVisualTicks + 2 - shatterAge);
                shatterVfxPlayed = false;
                SpawnShatterVfxOnce();
            }
        }

        public static void Fire(Player player, Vector2 pos) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }

            float bladeRotation;
            int bladeFacing;
            if (!OniBladeHandoff.TryPeek(player, out bladeRotation, out bladeFacing)) {
                bladeFacing = player.direction >= 0 ? 1 : -1;
                bladeRotation = bladeFacing > 0
                    ? -0.72f * player.gravDir
                    : MathHelper.Pi + 0.72f * player.gravDir;
            }
            Fire(player, pos, bladeRotation, bladeFacing);
        }

        /// <summary>Spawns a decoy with an explicit world-space blade pose</summary>
        public static void Fire(Player player, Vector2 pos, float bladeRotation, int bladeFacing) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }

            int type = ModContent.ProjectileType<OniMeiFalseBody>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile old = Main.projectile[i];
                if (old.active && old.owner == player.whoAmI && old.type == type) {
                    old.Kill();
                }
            }

            Projectile spawned = Projectile.NewProjectileDirect(
                player.GetSource_Misc("CWR_OniMeiFalseBody"), pos, Vector2.Zero,
                type, 0, 0f, player.whoAmI, ai0: OniMeiCombat.FalseBodyLifeTicks);
            if (spawned.ModProjectile is OniMeiFalseBody body) {
                body.CapturePose(player, pos, bladeRotation, bladeFacing);
                spawned.netUpdate = true;
            }
        }

        public static bool AnyOwned(Player player) => TryGetOwned(player) != null;

        /// <summary>Removing the inscription dismisses the live shadow without creating vacuum debt.</summary>
        public static void DismissOwned(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            int type = ModContent.ProjectileType<OniMeiFalseBody>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && projectile.owner == player.whoAmI
                    && projectile.type == type) {
                    projectile.Kill();
                }
            }
        }

        public static OniMeiFalseBody TryGetOwned(Player player) {
            if (player == null) {
                return null;
            }

            int type = ModContent.ProjectileType<OniMeiFalseBody>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is OniMeiFalseBody body && body.IsAvailable) {
                    return body;
                }
            }
            return null;
        }

        /// <summary>Consumes the owner's live decoy and starts its synchronized shatter state</summary>
        public static bool TryConsumeOwned(Player player) {
            OniMeiFalseBody body = TryGetOwned(player);
            return body != null && body.TryShatter();
        }

        /// <summary>Compatibility entry point for callers that already resolved the decoy</summary>
        public void Shatter() => TryShatter();

        public bool TryShatter() {
            if (!IsAvailable) {
                return false;
            }

            shattered = true;
            shatterAge = 0;
            Projectile.timeLeft = ShatterVisualTicks + 2;
            Projectile.netUpdate = true;
            SpawnShatterVfxOnce();

            if (Projectile.owner == Main.myPlayer
                && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers) {
                Main.player[Projectile.owner].GetModPlayer<OnikiriPlayer>().OnFalseBodyShattered();
            }
            return true;
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                if (LifeMax <= 0f) {
                    LifeMax = DefaultLifetime;
                }
                Projectile.timeLeft = Math.Max(1, (int)LifeMax);
                seed = Projectile.identity * 0.6180339887f % 1f;
            }

            timer++;
            if (shattered) {
                SpawnShatterVfxOnce();
                if (++shatterAge >= ShatterVisualTicks) {
                    Projectile.Kill();
                }
                return;
            }

            if (Main.dedServ) {
                return;
            }
            if (timer % 8 == 0) {
                Vector2 offset = new((seed - 0.5f) * 10f, Main.rand.NextFloat(-6f, 6f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center + offset,
                    -Vector2.UnitY * snapshotGravDir * Main.rand.NextFloat(0.3f, 0.8f)
                        + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    Color.White, Main.rand.NextFloat(0.04f, 0.07f))
                    ?.Configure(Main.rand.Next(12, 20), new Color(90, 22, 32), new Color(22, 10, 16));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.08f, 0.11f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ || !poseCaptured
                || Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) {
                return false;
            }

            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active) {
                return false;
            }

            float life = MathHelper.Clamp(Projectile.timeLeft / Math.Max(LifeMax, 1f), 0f, 1f);
            float shatterProgress = shattered
                ? MathHelper.Clamp(shatterAge / (float)ShatterVisualTicks, 0f, 1f)
                : 0f;
            float opacity = (0.32f + MathF.Sqrt(life) * 0.30f) * (1f - shatterProgress);
            if (opacity <= 0.01f) {
                return false;
            }

            Vector2 splitAxis = (snapshotBladeRotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 shatterShift = splitAxis * (shatterProgress * 5f);
            Vector2 drawPosition = Projectile.Center + snapshotDrawOffset + shatterShift;

            PlayerCloneRenderer.Prepare(owner);
            Color outline = new Color(112, 12, 25, 185) * opacity;
            Color ink = new Color(8, 2, 6, 225) * opacity;
            const float outlineWidth = 2f;
            DrawClone(drawPosition + new Vector2(outlineWidth, 0f), outline);
            DrawClone(drawPosition - new Vector2(outlineWidth, 0f), outline);
            DrawClone(drawPosition + new Vector2(0f, outlineWidth), outline);
            DrawClone(drawPosition - new Vector2(0f, outlineWidth), outline);
            DrawClone(drawPosition, ink);
            DrawBlade(owner, drawPosition, opacity, shatterProgress);
            return false;
        }

        private void CapturePose(Player player, Vector2 spawnCenter, float bladeRotation, int bladeFacing) {
            poseCaptured = true;
            snapshotDrawOffset = player.position - spawnCenter;
            snapshotDirection = player.direction >= 0 ? 1 : -1;
            snapshotGravDir = player.gravDir >= 0f ? 1f : -1f;
            snapshotBodyFrame = player.bodyFrame;
            snapshotLegFrame = player.legFrame;
            snapshotFullRotation = player.fullRotation;
            snapshotFullRotationOrigin = player.fullRotationOrigin;
            snapshotBladeRotation = bladeRotation;
            snapshotBladeFacing = bladeFacing >= 0 ? 1 : -1;
        }

        private void DrawClone(Vector2 position, Color color) {
            PlayerCloneRenderer.DrawPrepared(position, color, snapshotDirection,
                snapshotBodyFrame, snapshotLegFrame, snapshotFullRotation,
                snapshotFullRotationOrigin, snapshotGravDir);
        }

        private void DrawBlade(Player owner, Vector2 drawPosition, float opacity, float shatterProgress) {
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 textureSize = blade.Size();
            Vector2 origin = textureSize * OniBladePose.HiltUV;
            Vector2 textureTip = textureSize * OniBladePose.TipUV;
            SpriteEffects effects = SpriteEffects.None;
            if (snapshotBladeFacing < 0) {
                effects = SpriteEffects.FlipVertically;
                origin.Y = textureSize.Y - origin.Y;
                textureTip.Y = textureSize.Y - textureTip.Y;
            }

            Vector2 hand = drawPosition + owner.Size * 0.5f
                + new Vector2(snapshotDirection * 3f, -5f * snapshotGravDir);
            if (MathF.Abs(snapshotFullRotation) > 0.001f) {
                Vector2 pivot = drawPosition + snapshotFullRotationOrigin;
                hand = pivot + (hand - pivot).RotatedBy(snapshotFullRotation);
            }

            float textureAxis = (textureTip - origin).ToRotation();
            float rotation = snapshotBladeRotation - textureAxis;
            Vector2 normal = (snapshotBladeRotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 screen = hand - Main.screenPosition + normal * (shatterProgress * 3f);
            Main.spriteBatch.Draw(blade, screen + normal * 1.5f, null,
                new Color(125, 13, 26, 190) * opacity,
                rotation, origin, BladeScale * 1.02f, effects, 0f);
            Main.spriteBatch.Draw(blade, screen, null,
                new Color(12, 2, 6, 235) * opacity,
                rotation, origin, BladeScale, effects, 0f);
        }

        private void SpawnShatterVfxOnce() {
            if (Main.dedServ || shatterVfxPlayed) {
                return;
            }
            shatterVfxPlayed = true;

            int randomSeed = Projectile.identity * 397 ^ Projectile.owner * 7919;
            UnifiedRandom random = new(randomSeed);
            for (int i = 0; i < 10; i++) {
                float angle = random.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * random.NextFloat(1.6f, 4.2f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center, velocity,
                    new Color(235, 92, 105), random.NextFloat(0.20f, 0.38f))
                    ?.Configure(random.Next(10, 17), affectedByGravity: false);
            }
            for (int i = 0; i < 6; i++) {
                float angle = random.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * random.NextFloat(3f, 14f);
                Vector2 velocity = angle.ToRotationVector2() * random.NextFloat(0.3f, 1.5f);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center + offset, velocity,
                    Color.White, random.NextFloat(0.06f, 0.10f))
                    ?.Configure(random.Next(14, 23), new Color(100, 24, 34), new Color(20, 10, 14));
            }
        }

        private static void WriteVector2(BinaryWriter writer, Vector2 value) {
            writer.Write(value.X);
            writer.Write(value.Y);
        }

        private static Vector2 ReadVector2(BinaryReader reader)
            => new(reader.ReadSingle(), reader.ReadSingle());

        private static void WriteRectangle(BinaryWriter writer, Rectangle value) {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Width);
            writer.Write(value.Height);
        }

        private static Rectangle ReadRectangle(BinaryReader reader)
            => new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }
}

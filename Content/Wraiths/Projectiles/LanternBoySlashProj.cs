using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer;
using SlashDef = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer.SlashDef;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>鬼灯同步墨斩，客户端只上报候选</summary>
    internal sealed class LanternBoySlashProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 42;
        private const int VisualLifetime = 18;
        private const int CandidateFrames = 3;
        private const int CollisionSamples = 13;

        internal int ParentIdentity => (int)Projectile.ai[0];
        internal ushort RoundSerial
            => (ushort)Math.Clamp((int)Projectile.ai[1], 0, ushort.MaxValue);
        internal byte LanternSlot
            => (byte)Math.Clamp((int)Projectile.ai[2], 0, byte.MaxValue);

        internal int Beat { get; private set; }
        internal float Aim { get; private set; }
        internal int Facing { get; private set; }
        internal int BaseWeaponDamage { get; private set; }
        internal float Knockback { get; private set; }
        internal float BladeScale { get; private set; } = 1f;
        internal int OriginalDamageStart { get; private set; }
        internal uint ActionSerial { get; private set; }
        internal ulong AuthoritySpawnTick { get; private set; }

        private readonly HashSet<long> submittedTargets = [];
        private bool initialized;
        private bool invalidated;
        private bool burstPlayed;
        private int age;

        private int BurstFrame => OriginalDamageStart + LanternSlot * 2;
        private int VisualStart => Math.Max(BurstFrame - 2, 0);
        private Vector2 SlashCenter
            => Projectile.Center + Aim.ToRotationVector2() * (20f * EffectiveScale);
        private float EffectiveScale => MathHelper.Clamp(BladeScale, 0.72f, 1.35f);

        internal bool IsAuthorityReady => initialized && !invalidated && IsPayloadValid()
            && age + 2 >= BurstFrame && age < Lifetime;

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 220;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
            => AuthoritySpawnTick = Main.GameUpdateCount;

        internal void Initialize(in WraithComboBeatEvent beat) {
            Beat = beat.Beat;
            Aim = beat.Aim;
            Facing = beat.Facing;
            BaseWeaponDamage = beat.BaseWeaponDamage;
            Knockback = beat.Knockback;
            BladeScale = beat.BladeScale;
            OriginalDamageStart = beat.DamageStart;
            ActionSerial = beat.ActionSerial;
            initialized = IsPayloadValid();
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)Beat);
            writer.Write(Aim);
            writer.Write((sbyte)Facing);
            writer.Write(BaseWeaponDamage);
            writer.Write(Knockback);
            writer.Write(BladeScale);
            writer.Write((byte)OriginalDamageStart);
            writer.Write(ActionSerial);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            Beat = reader.ReadByte();
            Aim = reader.ReadSingle();
            Facing = reader.ReadSByte();
            BaseWeaponDamage = reader.ReadInt32();
            Knockback = reader.ReadSingle();
            BladeScale = reader.ReadSingle();
            OriginalDamageStart = reader.ReadByte();
            ActionSerial = reader.ReadUInt32();
            initialized = IsPayloadValid();
            invalidated |= !initialized;
        }

        private bool IsPayloadValid()
            => ParentIdentity >= 0 && RoundSerial != 0 && LanternSlot < 3
                && Beat >= 0 && Beat < 5 && Facing is -1 or 1
                && BaseWeaponDamage > 0 && BaseWeaponDamage <= 100_000_000
                && float.IsFinite(Aim) && float.IsFinite(Knockback)
                && float.IsFinite(BladeScale) && BladeScale is >= 0.5f and <= 1.8f
                && OriginalDamageStart is >= 0 and <= 16;

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void AI() {
            age++;
            Projectile.velocity = Vector2.Zero;

            LanternBoyProj parent = ResolveParent();
            if (parent == null) {
                invalidated = true;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 6);
            }

            if (Projectile.owner == Main.myPlayer && (!initialized || invalidated
                || parent?.HasAttackChannel != true)) {
                invalidated = true;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 6);
            }

            if (!burstPlayed && initialized && age >= BurstFrame) {
                burstPlayed = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item71 with {
                        Volume = 0.24f,
                        Pitch = 0.22f + LanternSlot * 0.08f,
                        MaxInstances = 6,
                    }, Projectile.Center);
                }
            }

            if (Projectile.owner == Main.myPlayer && initialized && !invalidated
                && age >= BurstFrame && age < BurstFrame + CandidateFrames) {
                PublishCandidates(parent);
            }
        }

        private LanternBoyProj ResolveParent() {
            int parentType = ModContent.ProjectileType<LanternBoyProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && projectile.owner == Projectile.owner
                    && projectile.identity == ParentIdentity && projectile.type == parentType
                    && projectile.ModProjectile is LanternBoyProj parent) {
                    return parent;
                }
            }
            return null;
        }

        private void PublishCandidates(LanternBoyProj parent) {
            if (parent == null) {
                return;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC target = Main.npc[i];
                if (!target.active || !target.CanBeChasedBy() || !CollidesWith(target.Hitbox)) {
                    continue;
                }
                long key = ((long)target.type << 32) ^ (uint)target.whoAmI;
                if (!submittedTargets.Add(key)) {
                    continue;
                }
                WraithNet.RequestLanternImpact(parent.Projectile, Projectile,
                    RoundSerial, LanternSlot, target.whoAmI, target.type);
            }
        }

        internal bool CollidesWith(Rectangle targetHitbox) {
            if (!initialized) {
                return false;
            }
            Rectangle greedy = targetHitbox;
            greedy.Inflate(8, 8);
            SlashDef definition = BuildDefinition();
            const int collisionAge = 3;
            CSR.SlashBandSample previous = CSR.SampleBand(
                in definition, SlashCenter, 0.06f, collisionAge);
            float collisionPoint = 0f;
            for (int i = 1; i < CollisionSamples; i++) {
                float u = 0.06f + 0.88f * (i / (float)(CollisionSamples - 1));
                CSR.SlashBandSample current = CSR.SampleBand(
                    in definition, SlashCenter, u, collisionAge);
                float width = MathF.Max(10f, (previous.Width + current.Width) * 0.5f);
                if (Collision.CheckAABBvLineCollision(greedy.TopLeft(), greedy.Size(),
                    previous.Center, current.Center, width, ref collisionPoint)) {
                    return true;
                }
                previous = current;
            }
            return false;
        }

        private SlashDef BuildDefinition() {
            float flip = Beat is 1 or 4 ? -Facing : Facing;
            float rotationOffset = Beat switch {
                0 => Facing * 0.15f,
                1 => -Facing * 0.10f,
                3 => -Facing * 0.20f,
                4 => Facing * 0.14f,
                _ => 0f,
            };
            return new SlashDef {
                SweepFrames = 3,
                GatherFrames = 1,
                Life = VisualLifetime,
                ErodeStart = 6,
                ErodeFrames = 12,
                ColorShiftDelay = 4f,
                ColorShiftFrames = 10f,
                DamageStart = 2,
                DamageEnd = 5,
                Mode = 0f,
                Rot = Aim + rotationOffset,
                Span = 2.75f,
                Thick = 0.31f,
                HalfX = 75f * EffectiveScale,
                HalfY = 50f * EffectiveScale,
                Flip = flip,
                Opacity = 0.88f,
                FrontGlow = 1.25f,
                Seed = (RoundSerial * 0.173f + LanternSlot * 0.271f) % 1f,
                TailErode = 0.52f,
                FlashPower = 0.34f,
                RazorTailWiden = 0.38f,
                Ink = 0.78f,
                FeiBai = 0.48f,
                Bleed = 0.28f,
                SplitTail = 0.55f,
            };
        }

        internal void GetLanternMotion(out float pulse, out float recoil) {
            int untilBurst = BurstFrame - age;
            pulse = untilBurst is >= 0 and <= 4 ? 1f - untilBurst / 4f : 0f;
            int sinceBurst = age - BurstFrame;
            recoil = sinceBurst is >= 0 and <= 7
                ? MathF.Sin(sinceBurst / 7f * MathHelper.Pi) * (1f - sinceBurst / 9f)
                : 0f;
            if (sinceBurst is >= 0 and <= 5) {
                pulse = MathF.Max(pulse, (1f - sinceBurst / 5f) * 0.72f);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            int localAge = age - VisualStart;
            if (localAge < 0 || localAge >= VisualLifetime) {
                return;
            }

            SlashDef definition = BuildDefinition();
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!CSR.BeginDraw(device, out Effect effect, out BlendState previousBlend,
                out RasterizerState previousRaster, out DepthStencilState previousDepth)) {
                return;
            }

            effect.Parameters["uColHot"]?.SetValue(new Vector3(1.08f, 0.30f, 0.055f));
            effect.Parameters["uColBright"]?.SetValue(new Vector3(0.86f, 0.075f, 0.025f));
            effect.Parameters["uColDeep"]?.SetValue(new Vector3(0.36f, 0.018f, 0.012f));
            effect.Parameters["uColDark"]?.SetValue(new Vector3(0.035f, 0.003f, 0.002f));
            CSR.DrawThreeLayers(device, effect, in definition, SlashCenter, localAge, 0f);
            CSR.EndDraw(device, previousBlend, previousRaster, previousDepth);
        }
    }
}

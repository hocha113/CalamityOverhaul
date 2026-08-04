using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>提灯童子三灯控制器与权威轮次</summary>
    internal sealed class LanternBoyProj : ModProjectile, IPrimitiveDrawable, ICrimsonFarDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LanternCount = 3;
        private const int AppearFrames = 24;
        private const int DismissFrames = 18;
        private const int MinimumRoundInterval = 4;
        private const int AuthorityRoundLifetime = 90;
        private const float OrbitSpeed = 0.012f;
        private const float MaxTargetRange = 480f;

        private enum LanternState : byte
        {
            Appearing,
            Active,
            Dismissing,
        }

        private sealed class AuthorityRound
        {
            internal ushort Serial;
            internal uint ActionSerial;
            internal int Beat;
            internal float Aim;
            internal int Facing;
            internal float BladeScale;
            internal int DamageStart;
            internal int Damage;
            internal float Knockback;
            internal int CritChance;
            internal ulong StartedAt;
            internal ulong CreatedAt;
            internal bool Paid;
            internal readonly int[] SlashIdentities = [-1, -1, -1];
            internal readonly HashSet<(byte Slot, int TargetId, int TargetType)> Hits = [];
        }

        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float Mastery => ref Projectile.ai[2];

        private LanternState State {
            get => (LanternState)StateRaw;
            set => StateRaw = (float)value;
        }

        private Player Owner => Main.player[Projectile.owner];
        private readonly Dictionary<ushort, AuthorityRound> authorityRounds = [];
        private readonly List<ushort> staleRounds = [];
        private ushort nextRoundSerial;
        private ushort lastAuthorityRoundSerial;
        private ulong lastLocalRoundTick;
        private ulong lastAuthorityRoundTick;
        private int visualAge;
        private int dismissVisualAge = -1;
        private bool channelInvalidated;
        private long vesselInstanceId;

        internal bool HasAttackChannel => !channelInvalidated && HasBoundVessel()
            && WraithAbilityService.HasAbilityChannel(Owner, LanternBoyAbility.Key);

        internal static LanternBoyProj Find(int owner) {
            int type = ModContent.ProjectileType<LanternBoyProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && projectile.owner == owner && projectile.type == type
                    && projectile.ModProjectile is LanternBoyProj lantern) {
                    return lantern;
                }
            }
            return null;
        }

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 260;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        internal void BindVessel(Item vesselItem) {
            long instanceId = OnikiriData.TryGet(vesselItem)?.InstanceId ?? 0;
            if (instanceId == 0 || vesselInstanceId == instanceId) {
                return;
            }
            vesselInstanceId = instanceId;
            Projectile.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(vesselInstanceId);
            writer.Write((short)Math.Clamp(dismissVisualAge, -1, short.MaxValue));
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            long incoming = reader.ReadInt64();
            dismissVisualAge = reader.ReadInt16();
            if (Main.netMode != NetmodeID.Server) {
                vesselInstanceId = incoming;
                return;
            }
            if (vesselInstanceId == 0) {
                long heldInstanceId = OnikiriData.TryGet(Owner.HeldItem)?.InstanceId ?? 0;
                if (incoming == 0 || incoming != heldInstanceId) {
                    channelInvalidated = true;
                    return;
                }
                vesselInstanceId = incoming;
            }
            else if (incoming != vesselInstanceId) {
                channelInvalidated = true;
            }
        }

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = Owner.MountedCenter;
            visualAge++;
            StateTimer++;

            bool resolved = WraithAbilityService.TryResolve(Owner, LanternBoyAbility.Key,
                out WraithAbilityContext context);
            bool controlsState = Main.netMode == NetmodeID.Server
                || Projectile.owner == Main.myPlayer;
            bool hasChannel = WraithAbilityService.HasAbilityChannel(
                Owner, LanternBoyAbility.Key) && HasBoundVessel();
            resolved &= hasChannel;
            if (controlsState && !hasChannel) {
                channelInvalidated = true;
            }
            if (State is LanternState.Appearing or LanternState.Active) {
                if (resolved) {
                    Mastery = context.Mastery;
                    if (State == LanternState.Appearing && StateTimer >= AppearFrames) {
                        Transition(LanternState.Active);
                    }
                }
                else if (controlsState) {
                    Transition(LanternState.Dismissing);
                }
            }
            else if (StateTimer >= DismissFrames) {
                Projectile.Kill();
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                PruneAuthorityRounds();
            }
            if (!Main.dedServ) {
                AddLanternLights();
            }
        }

        private void Transition(LanternState next) {
            if (State == next) {
                return;
            }
            if (next == LanternState.Dismissing) {
                dismissVisualAge = visualAge;
            }
            State = next;
            StateTimer = 0f;
            if (Main.netMode == NetmodeID.Server || Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        internal void PublishComboBeat(in WraithComboBeatEvent beat) {
            if (Projectile.owner != Main.myPlayer || State == LanternState.Dismissing
                || beat.Beat < 0 || beat.Beat >= 5 || !float.IsFinite(beat.Aim)
                || !float.IsFinite(beat.Knockback) || !float.IsFinite(beat.BladeScale)
                || beat.Facing is not (-1 or 1) || beat.BaseWeaponDamage <= 0
                || beat.DamageStart < 0 || beat.DamageStart > 16) {
                return;
            }

            ulong now = Main.GameUpdateCount;
            if (lastLocalRoundTick != 0 && now - lastLocalRoundTick < MinimumRoundInterval) {
                return;
            }
            lastLocalRoundTick = now;
            nextRoundSerial++;
            if (nextRoundSerial == 0) {
                nextRoundSerial++;
            }

            int slashType = ModContent.ProjectileType<LanternBoySlashProj>();
            for (byte slot = 0; slot < LanternCount; slot++) {
                Vector2 origin = GetLanternPosition(slot, out _, out _);
                int whoAmI = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    origin,
                    Vector2.Zero,
                    slashType,
                    0,
                    0f,
                    Projectile.owner,
                    Projectile.identity,
                    nextRoundSerial,
                    slot);
                if (whoAmI >= 0 && whoAmI < Main.maxProjectiles
                    && Main.projectile[whoAmI].ModProjectile is LanternBoySlashProj slash) {
                    slash.Initialize(in beat);
                    Main.projectile[whoAmI].netUpdate = true;
                }
            }
        }

        internal bool TryApplyAuthorityImpact(LanternBoySlashProj slash, ushort serial,
            byte slot, int targetId, int targetType) {
            if (Main.netMode == NetmodeID.MultiplayerClient || !Projectile.active
                || slash?.Projectile?.active != true || channelInvalidated
                || !ReferenceEquals(Find(Projectile.owner), this)
                || slash.Projectile.owner != Projectile.owner
                || slash.ParentIdentity != Projectile.identity
                || slash.RoundSerial != serial || slash.LanternSlot != slot
                || serial == 0 || slot >= LanternCount || !slash.IsAuthorityReady
                || !WraithAbilityService.HasAbilityChannel(Owner, LanternBoyAbility.Key)
                || !HasBoundVessel()
                || targetId < 0 || targetId >= Main.maxNPCs) {
                return false;
            }

            NPC target = Main.npc[targetId];
            Vector2 slashOrigin = slash.Projectile.Center;
            if (!float.IsFinite(slashOrigin.X) || !float.IsFinite(slashOrigin.Y)
                || !target.active || target.type != targetType || !target.CanBeChasedBy()
                || Vector2.DistanceSquared(Owner.Center, target.Center)
                    > MaxTargetRange * MaxTargetRange
                || Vector2.DistanceSquared(Owner.MountedCenter, slashOrigin)
                    > 240f * 240f
                || !slash.CollidesWith(target.Hitbox)) {
                return false;
            }

            if (!authorityRounds.TryGetValue(serial, out AuthorityRound round)) {
                ulong roundStart = slash.AuthoritySpawnTick;
                if (!IsNewerSerial(serial, lastAuthorityRoundSerial)
                    || lastAuthorityRoundSerial != 0
                        && (roundStart < lastAuthorityRoundTick
                            || roundStart - lastAuthorityRoundTick < MinimumRoundInterval)
                    || !WraithAbilityService.TryResolve(Owner, LanternBoyAbility.Key,
                        out WraithAbilityContext context)) {
                    return false;
                }

                int weaponDamage = Math.Max(Owner.GetWeaponDamage(context.VesselItem), 1);
                round = new AuthorityRound {
                    Serial = serial,
                    ActionSerial = slash.ActionSerial,
                    Beat = slash.Beat,
                    Aim = slash.Aim,
                    Facing = slash.Facing,
                    BladeScale = slash.BladeScale,
                    DamageStart = slash.OriginalDamageStart,
                    Damage = Math.Max((int)(weaponDamage * 0.20f), 1),
                    Knockback = Owner.GetWeaponKnockback(context.VesselItem) * 0.25f,
                    CritChance = Math.Max(Owner.GetWeaponCrit(context.VesselItem), 0),
                    StartedAt = roundStart,
                    CreatedAt = Main.GameUpdateCount,
                };
                authorityRounds[serial] = round;
                lastAuthorityRoundSerial = serial;
                lastAuthorityRoundTick = roundStart;
            }
            else if (!MatchesRound(round, slash)) {
                return false;
            }

            int slashIdentity = slash.Projectile.identity;
            if (round.SlashIdentities[slot] < 0) {
                round.SlashIdentities[slot] = slashIdentity;
            }
            else if (round.SlashIdentities[slot] != slashIdentity) {
                return false;
            }

            var hitKey = (slot, targetId, targetType);
            if (!round.Hits.Add(hitKey)) {
                return false;
            }

            WraithAbilityContext unpaidContext = default;
            if (!round.Paid && !WraithAbilityService.TryResolve(Owner, LanternBoyAbility.Key,
                out unpaidContext)) {
                return false;
            }

            bool crit = round.CritChance > 0 && Main.rand.Next(100) < round.CritChance;
            int lifeBefore = target.life;
            Owner.ApplyDamageToNPC(target, round.Damage, round.Knockback,
                round.Facing, crit, Projectile.DamageType);
            if (target.life >= lifeBefore) {
                return false;
            }

            if (!round.Paid) {
                if (!WraithAbilityService.TryCommitUse(in unpaidContext)) {
                    return false;
                }
                round.Paid = true;
            }
            return true;
        }

        private static bool MatchesRound(AuthorityRound round, LanternBoySlashProj slash)
            => round.ActionSerial == slash.ActionSerial && round.Beat == slash.Beat
                && round.Facing == slash.Facing
                && round.DamageStart == slash.OriginalDamageStart
                && Math.Abs((long)round.StartedAt - (long)slash.AuthoritySpawnTick) <= 3L
                && MathF.Abs(MathHelper.WrapAngle(round.Aim - slash.Aim)) < 0.002f
                && MathF.Abs(round.BladeScale - slash.BladeScale) < 0.002f;

        private bool HasBoundVessel() {
            if (vesselInstanceId == 0) {
                return true;
            }
            return OnikiriData.TryGet(Owner.HeldItem)?.InstanceId == vesselInstanceId
                && !OnikiriNet.HasDuplicateInstanceId(Owner, vesselInstanceId);
        }

        private static bool IsNewerSerial(ushort incoming, ushort current)
            => current == 0 || incoming != current && (ushort)(incoming - current) < 0x8000;

        private void PruneAuthorityRounds() {
            if (authorityRounds.Count == 0) {
                return;
            }
            ulong now = Main.GameUpdateCount;
            staleRounds.Clear();
            foreach (KeyValuePair<ushort, AuthorityRound> pair in authorityRounds) {
                if (now - pair.Value.CreatedAt > AuthorityRoundLifetime) {
                    staleRounds.Add(pair.Key);
                }
            }
            foreach (ushort serial in staleRounds) {
                authorityRounds.Remove(serial);
            }
        }

        private float GetIgnition(byte slot) {
            int age = State == LanternState.Dismissing && dismissVisualAge >= 0
                ? dismissVisualAge
                : visualAge;
            float progress = (age - slot * 4f) / 16f;
            return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(progress, 0f, 1f));
        }

        private float GetExtinguish(byte slot) {
            if (State != LanternState.Dismissing) {
                return 0f;
            }
            float progress = (StateTimer - slot * 3f) / 12f;
            return MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(progress, 0f, 1f));
        }

        internal Vector2 GetLanternPosition(byte slot, out float depth, out float scale) {
            float phase = visualAge * OrbitSpeed + Projectile.identity * 0.173f
                + slot * MathHelper.TwoPi / LanternCount;
            depth = MathF.Sin(phase);
            scale = MathHelper.Lerp(0.82f, 1.08f, (depth + 1f) * 0.5f);
            Vector2 position = Owner.MountedCenter + new Vector2(MathF.Cos(phase) * 96f,
                depth * 52f - 20f);

            GetAttackMotion(slot, out float pulse, out float recoil, out Vector2 recoilDirection);
            position -= recoilDirection * (recoil * 10f);
            return position;
        }

        private void GetAttackMotion(byte slot, out float pulse, out float recoil,
            out Vector2 recoilDirection) {
            pulse = 0f;
            recoil = 0f;
            recoilDirection = Vector2.UnitX * Owner.direction;
            int slashType = ModContent.ProjectileType<LanternBoySlashProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.owner != Projectile.owner
                    || projectile.type != slashType
                    || projectile.ModProjectile is not LanternBoySlashProj slash
                    || slash.ParentIdentity != Projectile.identity || slash.LanternSlot != slot) {
                    continue;
                }
                slash.GetLanternMotion(out float candidatePulse, out float candidateRecoil);
                if (candidatePulse + candidateRecoil > pulse + recoil) {
                    pulse = candidatePulse;
                    recoil = candidateRecoil;
                    recoilDirection = slash.Aim.ToRotationVector2();
                }
            }
        }

        private void AddLanternLights() {
            for (byte slot = 0; slot < LanternCount; slot++) {
                float visibility = GetIgnition(slot) * (1f - GetExtinguish(slot));
                if (visibility <= 0.01f) {
                    continue;
                }
                Vector2 position = GetLanternPosition(slot, out _, out _);
                float flicker = 0.88f + 0.12f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 7f + slot * 2.1f);
                Lighting.AddLight(position,
                    new Vector3(0.42f, 0.055f, 0.018f) * visibility * flicker);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void ICrimsonFarDrawable.DrawFarSlashes() => DrawLanternLayer(front: false);

        void IPrimitiveDrawable.DrawPrimitives() => DrawLanternLayer(front: true);

        private void DrawLanternLayer(bool front) {
            if (Main.dedServ) {
                return;
            }

            SpriteBatch spriteBatch = Main.spriteBatch;
            DrawSoftGlow(spriteBatch, front);
            Texture2D white = VaultAsset.placeholder2?.Value ?? TextureAssets.MagicPixel.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            Effect effect = EffectLoader.WraithLantern?.Value;
            bool custom = effect != null && noise != null;
            bool domainFallback = !custom && EffectLoader.OniDomainDeco?.Value != null;

            if (custom || domainFallback) {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    effect: custom ? effect : EffectLoader.OniDomainDeco.Value,
                    transformMatrix: Main.GameViewMatrix.TransformationMatrix);
                if (domainFallback) {
                    EffectLoader.OniDomainDeco.Value.Parameters["uTime"]
                        ?.SetValue((float)Main.timeForVisualEffects * 0.05f);
                    EffectLoader.OniDomainDeco.Value.CurrentTechnique =
                        EffectLoader.OniDomainDeco.Value.Techniques["TechLantern"];
                }

                for (byte slot = 0; slot < LanternCount; slot++) {
                    Vector2 position = GetLanternPosition(slot, out float depth, out float scale);
                    if ((depth >= 0f) != front) {
                        continue;
                    }
                    float ignition = GetIgnition(slot);
                    float extinguish = GetExtinguish(slot);
                    float opacity = ignition * (1f - extinguish);
                    if (opacity <= 0.01f) {
                        continue;
                    }
                    GetAttackMotion(slot, out float pulse, out float recoil, out _);
                    float rotation = MathF.Sin(visualAge * 0.045f + slot * 1.7f) * 0.045f
                        + recoil * (slot % 2 == 0 ? -0.08f : 0.08f);
                    if (custom) {
                        effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                        effect.Parameters["uOpacity"]?.SetValue(opacity);
                        effect.Parameters["uIgnition"]?.SetValue(ignition);
                        effect.Parameters["uExtinguish"]?.SetValue(extinguish);
                        effect.Parameters["uPulse"]?.SetValue(MathHelper.Clamp(pulse + recoil, 0f, 1f));
                        effect.Parameters["uSeed"]?.SetValue((Projectile.identity * 0.173f
                            + slot * 0.311f) % 1f);
                        effect.Parameters["uNoiseTex"]?.SetValue(noise);
                    }
                    effect?.CurrentTechnique.Passes[0].Apply();
                    Vector2 drawScale = new(46f * scale / white.Width, 62f * scale / white.Height);
                    Color tint = custom
                        ? new Color(185, 39, 24)
                        : new Color(185, 39, 24) * opacity;
                    spriteBatch.Draw(white, position - Main.screenPosition, null,
                        tint, rotation, white.Size() * 0.5f,
                        drawScale, SpriteEffects.None, 0f);
                }
                spriteBatch.End();
            }
            else {
                DrawVanillaFallback(spriteBatch, front);
            }
        }

        private void DrawVanillaFallback(SpriteBatch spriteBatch, bool front) {
            Texture2D texture = TextureAssets.Item[ItemID.ChineseLantern].Value;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (byte slot = 0; slot < LanternCount; slot++) {
                Vector2 position = GetLanternPosition(slot, out float depth, out float scale);
                if ((depth >= 0f) != front) {
                    continue;
                }
                float opacity = GetIgnition(slot) * (1f - GetExtinguish(slot));
                if (opacity <= 0.01f) {
                    continue;
                }
                float targetHeight = 54f * scale;
                spriteBatch.Draw(texture, position - Main.screenPosition, null,
                    new Color(196, 50, 30) * opacity, 0f, texture.Size() * 0.5f,
                    targetHeight / texture.Height, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        private void DrawSoftGlow(SpriteBatch spriteBatch, bool front) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            for (byte slot = 0; slot < LanternCount; slot++) {
                Vector2 position = GetLanternPosition(slot, out float depth, out float scale);
                if ((depth >= 0f) != front) {
                    continue;
                }
                float opacity = GetIgnition(slot) * (1f - GetExtinguish(slot));
                if (opacity <= 0.01f) {
                    continue;
                }
                Color color = new Color(0.75f, 0.10f, 0.025f, 0f) * (opacity * 0.24f);
                spriteBatch.Draw(glow, position - Main.screenPosition, null, color, 0f,
                    glow.Size() * 0.5f, 72f * scale / glow.Width,
                    SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}

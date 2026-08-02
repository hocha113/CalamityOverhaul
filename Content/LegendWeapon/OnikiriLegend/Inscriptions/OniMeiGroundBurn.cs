using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    internal enum OniMeiBurnStyle : byte
    {
        Scorch,
        Ember,
    }

    /// <summary>Ground-anchored fire shared by the Scorch and Ember inscriptions</summary>
    internal class OniMeiGroundBurn : ModProjectile
    {
        private const int HitboxWidth = 40;
        private const int HitboxHeight = 100;
        private const int LeadNoDamageTicks = 4;
        private const int TailNoDamageTicks = 6;
        internal const int SharedHitCooldown = 30;
        internal const int EmberHitCooldown = 45;
        private const int MinimumLifetime = LeadNoDamageTicks + TailNoDamageTicks + 2;
        private const float RefreshRadius = 32f;
        private const float GroundProbeRise = 72f;
        private const float GroundProbeReach = 224f;
        private const float BaseVisualHeight = 26f;
        private const float BaseVisualWidth = 38f;

        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Lifetime => ref Projectile.ai[0];
        private ref float VisualScale => ref Projectile.ai[1];
        private ref float Age => ref Projectile.ai[2];

        private OniMeiBurnStyle style;
        private Vector2 groundPosition;
        private float gravityDirection = 1f;
        private bool hasGroundAnchor;
        private bool initialized;
        private float swayPhase;
        private float visualHeight;
        private float visualWidth;

        public OniMeiBurnStyle Style => style;
        public Vector2 GroundPosition => groundPosition;

        public override void SetDefaults() {
            Projectile.width = HitboxWidth;
            Projectile.height = HitboxHeight;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = SharedHitCooldown;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)style);
            writer.Write(hasGroundAnchor);
            if (hasGroundAnchor) {
                writer.Write(groundPosition.X);
                writer.Write(groundPosition.Y);
                writer.Write((sbyte)(gravityDirection >= 0f ? 1 : -1));
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            byte rawStyle = reader.ReadByte();
            style = rawStyle <= (byte)OniMeiBurnStyle.Ember
                ? (OniMeiBurnStyle)rawStyle
                : OniMeiBurnStyle.Scorch;
            hasGroundAnchor = reader.ReadBoolean();
            if (hasGroundAnchor) {
                Vector2 received = new(reader.ReadSingle(), reader.ReadSingle());
                gravityDirection = reader.ReadSByte() >= 0 ? 1f : -1f;
                if (float.IsFinite(received.X) && float.IsFinite(received.Y)) {
                    groundPosition = received;
                    ApplyGroundAnchor();
                }
                else {
                    hasGroundAnchor = false;
                }
            }
        }

        /// <summary>Probes along the player's gravity and only spawns when a real surface is found</summary>
        public static bool TrySpawnOrRefresh(Player player, Vector2 worldPos, int damage, int life,
            float scale, OniMeiBurnStyle burnStyle, Projectile parent = null) {
            if (player == null || Main.myPlayer != player.whoAmI
                || !TryResolveGround(player, worldPos, out Vector2 ground)) {
                return false;
            }

            return TrySpawnOrRefreshAtGround(player, ground, player.gravDir,
                damage, life, scale, burnStyle, parent);
        }

        /// <summary>Terrain probe used by dash path sampling and execution fields</summary>
        public static bool TryResolveGround(Player player, Vector2 worldPos, out Vector2 ground) {
            ground = default;
            if (player == null) {
                return false;
            }
            return TryResolveGround(worldPos, player.gravDir, out ground);
        }

        public static bool TryResolveGround(Vector2 worldPos, float gravDir, out Vector2 ground) {
            ground = default;
            if (!float.IsFinite(worldPos.X) || !float.IsFinite(worldPos.Y)) {
                return false;
            }

            float gravity = gravDir >= 0f ? 1f : -1f;
            Vector2 gravityAxis = Vector2.UnitY * gravity;
            const int probeSize = 4;
            Vector2 probeTopLeft = worldPos - new Vector2(probeSize * 0.5f);
            Point initialTile = probeTopLeft.ToTileCoordinates();
            if (!WorldGen.InWorld(initialTile.X, initialTile.Y, 2)) {
                return false;
            }
            float lifted = 0f;
            while (lifted < GroundProbeRise
                && Collision.SolidCollision(probeTopLeft, probeSize, probeSize)) {
                probeTopLeft -= gravityAxis * 8f;
                lifted += 8f;
            }
            if (Collision.SolidCollision(probeTopLeft, probeSize, probeSize)) {
                return false;
            }

            Vector2 desired = gravityAxis * (GroundProbeReach + lifted);

            Point startTile = probeTopLeft.ToTileCoordinates();
            Point endTile = (probeTopLeft + desired).ToTileCoordinates();
            if (!WorldGen.InWorld(startTile.X, startTile.Y, 2)
                || !WorldGen.InWorld(endTile.X, endTile.Y, 2)) {
                return false;
            }

            Vector2 allowed = Collision.TileCollision(probeTopLeft, desired,
                probeSize, probeSize, fallThrough: false, fall2: false, gravDir: (int)gravity);
            if (MathF.Abs(allowed.Y) >= MathF.Abs(desired.Y) - 0.5f) {
                return false;
            }

            Vector2 stoppedTopLeft = probeTopLeft + allowed;
            ground = new Vector2(stoppedTopLeft.X + probeSize * 0.5f,
                gravity > 0f ? stoppedTopLeft.Y + probeSize : stoppedTopLeft.Y);
            return float.IsFinite(ground.X) && float.IsFinite(ground.Y);
        }

        /// <summary>Spawns from an already verified surface point</summary>
        public static bool TrySpawnOrRefreshAtGround(Player player, Vector2 ground, float gravDir,
            int damage, int life, float scale, OniMeiBurnStyle burnStyle, Projectile parent = null) {
            if (player == null || Main.myPlayer != player.whoAmI
                || !float.IsFinite(ground.X) || !float.IsFinite(ground.Y)) {
                return false;
            }

            Point tile = ground.ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 2)) {
                return false;
            }

            life = Math.Max(life, MinimumLifetime);
            scale = Math.Max(scale, 0.1f);
            damage = Math.Max(damage, 1);
            float gravity = gravDir >= 0f ? 1f : -1f;
            int type = ModContent.ProjectileType<OniMeiGroundBurn>();

            OniMeiGroundBurn refresh = null;
            float nearestSq = RefreshRadius * RefreshRadius;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != player.whoAmI || proj.type != type
                    || proj.ModProjectile is not OniMeiGroundBurn burn
                    || burn.style != burnStyle) {
                    continue;
                }

                if (burnStyle == OniMeiBurnStyle.Ember) {
                    if (refresh == null) {
                        refresh = burn;
                    }
                    else {
                        proj.Kill();
                    }
                    continue;
                }

                float distanceSq = Vector2.DistanceSquared(burn.groundPosition, ground);
                if (distanceSq <= nearestSq) {
                    nearestSq = distanceSq;
                    refresh = burn;
                }
            }

            if (refresh != null) {
                refresh.Configure(ground, gravity, damage, life, scale, burnStyle);
                OniMeiActionContext.Inherit(parent, refresh.Projectile, secondary: true);
                return true;
            }

            Vector2 center = ground - Vector2.UnitY * gravity * (HitboxHeight * 0.5f);
            Projectile spawned = Projectile.NewProjectileDirect(
                parent?.GetSource_FromAI() ?? player.GetSource_Misc("CWR_OniMeiGroundBurn"), center, Vector2.Zero,
                type, damage, 0f, player.whoAmI, ai0: life, ai1: scale);
            if (spawned.ModProjectile is not OniMeiGroundBurn created) {
                spawned.Kill();
                return false;
            }

            created.Configure(ground, gravity, damage, life, scale, burnStyle);
            OniMeiActionContext.Inherit(parent, spawned, secondary: true);
            return true;
        }

        public static bool AnyOwnedStyle(Player player, OniMeiBurnStyle burnStyle) {
            if (player == null) {
                return false;
            }

            int type = ModContent.ProjectileType<OniMeiGroundBurn>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.type == type
                    && proj.ModProjectile is OniMeiGroundBurn burn
                    && burn.style == burnStyle && burn.Age < burn.Lifetime) {
                    return true;
                }
            }
            return false;
        }

        public override bool? CanDamage() {
            if (!initialized || Age <= LeadNoDamageTicks
                || Lifetime - Age <= TailNoDamageTicks) {
                return false;
            }
            return null;
        }

        public override bool? CanHitNPC(NPC target) {
            NPC root = OniMeiCombat.ResolveEffectRoot(target);
            if (root == null) {
                return false;
            }
            return root.GetGlobalNPC<OniMeiGroundBurnHitGate>().CanHit(Projectile.owner, style)
                ? null
                : false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            NPC root = OniMeiCombat.ResolveEffectRoot(target);
            root?.GetGlobalNPC<OniMeiGroundBurnHitGate>().Commit(Projectile.owner, style);
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                Lifetime = Math.Max(Lifetime, MinimumLifetime);
                if (VisualScale <= 0.01f) {
                    VisualScale = 1f;
                }
                if (!hasGroundAnchor) {
                    hasGroundAnchor = true;
                    groundPosition = Projectile.Center
                        + Vector2.UnitY * gravityDirection * (HitboxHeight * 0.5f);
                }
                swayPhase = Projectile.identity * 0.6180339887f % MathHelper.TwoPi;
                ApplyGroundAnchor();
            }

            Age++;
            if (Age >= Lifetime) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            ApplyGroundAnchor();
            swayPhase += 0.18f;

            float remainingRatio = MathHelper.Clamp((Lifetime - Age) / Math.Max(Lifetime, 1f), 0f, 1f);
            float baseHeight = BaseVisualHeight * VisualScale;
            visualWidth = BaseVisualWidth * VisualScale;
            if (Age < 8f) {
                visualHeight = MathHelper.Lerp(0f, baseHeight, Age / 8f);
            }
            else if (remainingRatio < 0.18f) {
                visualHeight = baseHeight * (remainingRatio / 0.18f);
            }
            else {
                visualHeight = baseHeight * (0.9f + MathF.Sin(swayPhase) * 0.1f);
            }

            if (!Main.dedServ && remainingRatio > 0.04f) {
                SpawnCrimsonFlame();
            }

            float lightFactor = visualHeight / Math.Max(baseHeight, 1f);
            Vector3 tint = style == OniMeiBurnStyle.Ember
                ? new Vector3(1.05f, 0.48f, 0.16f)
                : new Vector3(0.82f, 0.12f, 0.12f);
            Vector2 lightPosition = groundPosition
                - Vector2.UnitY * gravityDirection * visualHeight * 0.5f;
            Lighting.AddLight(lightPosition, tint * lightFactor);
        }

        private void Configure(Vector2 ground, float gravDir, int damage, int life,
            float scale, OniMeiBurnStyle burnStyle) {
            style = burnStyle;
            groundPosition = ground;
            gravityDirection = gravDir >= 0f ? 1f : -1f;
            hasGroundAnchor = true;
            initialized = true;
            Lifetime = Math.Max(life, MinimumLifetime);
            VisualScale = Math.Max(scale, 0.1f);
            Age = 0f;
            visualHeight = 0f;
            Projectile.damage = Math.Max(damage, 1);
            Projectile.originalDamage = Projectile.damage;
            Projectile.timeLeft = 2;
            swayPhase = Projectile.identity * 0.6180339887f % MathHelper.TwoPi;
            ApplyGroundAnchor();
            Projectile.netUpdate = true;
        }

        private void ApplyGroundAnchor() {
            if (!hasGroundAnchor) {
                return;
            }
            Projectile.Center = groundPosition
                - Vector2.UnitY * gravityDirection * (HitboxHeight * 0.5f);
        }

        private void SpawnCrimsonFlame() {
            if (visualHeight < 4f) {
                return;
            }

            float spreadX = visualWidth * 0.45f;
            Vector2 upward = -Vector2.UnitY * gravityDirection;
            Color smokeDeep = style == OniMeiBurnStyle.Ember
                ? new Color(140, 40, 20)
                : new Color(100, 24, 30);
            Color smokeCore = style == OniMeiBurnStyle.Ember
                ? new Color(22, 11, 10)
                : new Color(24, 12, 16);
            Color spark = style == OniMeiBurnStyle.Ember
                ? new Color(235, 150, 80)
                : new Color(255, 92, 82);

            if ((int)Age % 3 == 0) {
                Vector2 pos = groundPosition + Vector2.UnitX * Main.rand.NextFloat(-spreadX, spreadX);
                Vector2 velocity = upward * Main.rand.NextFloat(0.5f, 1.3f)
                    + Vector2.UnitX * Main.rand.NextFloat(-0.4f, 0.4f);
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, velocity, Color.White,
                    Main.rand.NextFloat(0.05f, 0.09f) * VisualScale)
                    ?.Configure(Main.rand.Next(14, 22), smokeDeep, smokeCore);
            }
            if ((int)Age % 4 == 0) {
                Vector2 pos = groundPosition
                    + Vector2.UnitX * Main.rand.NextFloat(-spreadX * 0.7f, spreadX * 0.7f)
                    + upward * Main.rand.NextFloat(0f, visualHeight * 0.45f);
                Vector2 velocity = upward * Main.rand.NextFloat(1.0f, 2.4f)
                    + Vector2.UnitX * Main.rand.NextFloat(-0.6f, 0.6f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, velocity, spark,
                    Main.rand.NextFloat(0.2f, 0.38f) * VisualScale)
                    ?.Configure(Main.rand.Next(10, 16), affectedByGravity: false);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>Per-root, per-player latch shared by every active ground-burn projectile</summary>
    internal sealed class OniMeiGroundBurnHitGate : GlobalNPC
    {
        private ulong[] nextSharedTick;
        private ulong[] nextEmberTick;

        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC entity) {
            nextSharedTick = null;
            nextEmberTick = null;
        }

        internal bool CanHit(int owner, OniMeiBurnStyle style) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return true;
            }
            ulong now = Main.GameUpdateCount;
            if (nextSharedTick != null && now < nextSharedTick[owner]) {
                return false;
            }
            return style != OniMeiBurnStyle.Ember
                || nextEmberTick == null
                || now >= nextEmberTick[owner];
        }

        internal void Commit(int owner, OniMeiBurnStyle style) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            nextSharedTick ??= new ulong[Main.maxPlayers];
            nextSharedTick[owner] = Main.GameUpdateCount
                + (ulong)OniMeiGroundBurn.SharedHitCooldown;
            if (style == OniMeiBurnStyle.Ember) {
                nextEmberTick ??= new ulong[Main.maxPlayers];
                nextEmberTick[owner] = Main.GameUpdateCount
                    + (ulong)OniMeiGroundBurn.EmberHitCooldown;
            }
        }
    }
}

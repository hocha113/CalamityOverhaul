using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 鵺切「落鵺」：空中第五拍不再横甩巨弧，改成整个人扑下去砸地。<br/>
    /// 俯冲期间接管玩家纵向速度（横向仍留一点操舵余地），落地炸开墨压环并把周围拽向落点。<br/>
    /// 收势 <see cref="OniMeiCombat.NueDiveRecoverTicks"/> 帧不能疾走——砸完要爬起来。<br/>
    /// ai[0]=基础武器伤害 ai[1]=尺寸倍率
    /// </summary>
    internal class OniMeiNueDive : ModProjectile, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>起手：空中一顿，读作"收翅"</summary>
        private const int PoiseFrames = 6;
        /// <summary>落地后的定格帧数（砸完的那一停）</summary>
        private const int SettleFrames = 10;
        /// <summary>横向操舵每帧加速度</summary>
        private const float SteerAccel = 0.9f;
        private const float SteerMax = 6f;

        private static readonly Color PaperEdge = new(255, 240, 226);
        private static readonly Color InkDeep = new(46, 12, 18);

        private enum Phase : byte
        {
            Poise,
            Dive,
            Settle,
        }

        private Phase phase = Phase.Poise;
        private int phaseTimer;
        private bool initialized;
        private bool landed;

        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[0]);
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private Player Owner => Main.player[Projectile.owner];

        bool IOniBladeOccupant.HardOccupiesBlade => phase != Phase.Settle;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = PoiseFrames + OniMeiCombat.NueDiveMaxFrames + SettleFrames + 10;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端起手；须确认离地够高（判高在调用方）</summary>
        internal static Projectile Fire(Player player, int baseWeaponDamage, float sizeMul,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return null;
            }
            if (player.ownedProjectileCounts[ModContent.ProjectileType<OniMeiNueDive>()] > 0) {
                return null;
            }
            return Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiNueDive"),
                player.Center, Vector2.Zero, ModContent.ProjectileType<OniMeiNueDive>(),
                0, 0f, player.whoAmI, ai0: Math.Max(1, baseWeaponDamage), ai1: sizeMul);
        }

        /// <summary>离地高度够不够扑一次（owner 端在第五拍前问一句）</summary>
        internal static bool HasDiveRoom(Player player) {
            if (player == null || player.mount?.Active == true || player.gravDir < 0f) {
                return false;
            }
            Vector2 foot = player.Bottom;
            Point tile = foot.ToTileCoordinates();
            int steps = (int)(OniMeiCombat.NueDiveMinHeight / 16f) + 1;
            for (int i = 0; i <= steps; i++) {
                int y = tile.Y + i;
                if (!WorldGen.InWorld(tile.X, y, 1)) {
                    return false;
                }
                Tile probe = Framing.GetTileSafely(tile.X, y);
                if (probe.HasTile && Main.tileSolid[probe.TileType]) {
                    //脚下就是地，没得扑
                    return y * 16f - foot.Y >= OniMeiCombat.NueDiveMinHeight;
                }
            }
            return true;
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                PlayPoiseCue();
            }
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = Owner.Center;
            phaseTimer++;

            switch (phase) {
                case Phase.Poise:
                    TickPoise();
                    break;
                case Phase.Dive:
                    TickDive();
                    break;
                default:
                    if (phaseTimer >= SettleFrames) {
                        Projectile.Kill();
                        return;
                    }
                    break;
            }
        }

        /// <summary>收翅：空中一顿，纵向速度归零，给俯冲一个预备动作</summary>
        private void TickPoise() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity.Y *= 0.35f;
                Owner.velocity.X *= 0.7f;
            }
            if (!Main.dedServ && phaseTimer % 2 == 0) {
                //收翅：身周墨羽向内收拢
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 at = Owner.Center + ang.ToRotationVector2() * Main.rand.NextFloat(30f, 54f);
                PRTLoader.NewParticle<PRT_OniInkDrop>(at, (Owner.Center - at) * 0.12f,
                    InkDeep, Main.rand.NextFloat(0.16f, 0.28f))
                    ?.Configure(Main.rand.Next(10, 16));
            }
            if (phaseTimer >= PoiseFrames) {
                phase = Phase.Dive;
                phaseTimer = 0;
                Projectile.netUpdate = true;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = -0.35f, Volume = 0.75f },
                        Owner.Center);
                }
            }
        }

        /// <summary>俯冲：纵向被接管，横向留一点操舵；触地或超时即砸</summary>
        private void TickDive() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity.Y = OniMeiCombat.NueDiveSpeed;
                //留一点横向操舵，让"往哪砸"仍是玩家的决定
                float steer = Owner.controlLeft ? -1f : Owner.controlRight ? 1f : 0f;
                Owner.velocity.X = MathHelper.Clamp(Owner.velocity.X + steer * SteerAccel,
                    -SteerMax, SteerMax);
                Owner.fallStart = (int)(Owner.position.Y / 16f);
            }

            if (!Main.dedServ) {
                SpawnDiveWake();
            }

            bool grounded = Owner.velocity.Y == 0f || Collision.SolidCollision(
                Owner.position + Vector2.UnitY * 4f, Owner.width, Owner.height);
            if (grounded || phaseTimer >= OniMeiCombat.NueDiveMaxFrames) {
                Land();
            }
        }

        private void Land() {
            if (landed) {
                return;
            }
            landed = true;
            phase = Phase.Settle;
            phaseTimer = 0;
            Projectile.netUpdate = true;
            Vector2 impact = Owner.Bottom;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.velocity.Y = -3.2f;
                Owner.velocity.X *= 0.25f;
                //砸完要爬起来：收势期禁疾走，代价看得见也感觉得到
                if (Owner.TryGetModPlayer(out OnikiriPlayer okp)) {
                    okp.LockDashForNueDive(OniMeiCombat.NueDiveRecoverTicks);
                }
                int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.NueDiveDamageMul));
                //落点冲击：左右各一道贴地断斩，合起来读作"砸开的地面"
                CrimsonRendCleave.FireCross(Owner, impact - Vector2.UnitY * 12f, 0f, 0.30f,
                    damage, 6f, SizeMul * 1.05f, Projectile.GetSource_FromAI(), CleaveStyle.LionJaw);
                PullNearby(impact);
            }

            PlayLandCue(impact);
        }

        /// <summary>把落点周围的敌人短暂拽向砸点：给"落鵺"一个聚拢的读法</summary>
        private void PullNearby(Vector2 impact) {
            float radiusSq = OniMeiCombat.NueDiveRadius * OniMeiCombat.NueDiveRadius;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy() || npc.boss || npc.knockBackResist <= 0f) {
                    continue;
                }
                if (npc.DistanceSQ(impact) > radiusSq) {
                    continue;
                }
                Vector2 pull = (impact - npc.Center).SafeNormalize(Vector2.Zero)
                    * OniMeiCombat.NueDivePullStrength * npc.knockBackResist;
                npc.velocity += pull;
            }
        }

        private void PlayPoiseCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.55f, Volume = 0.42f }, Owner.Center);
        }

        /// <summary>俯冲尾迹：沿速度拉长的墨条，禁各向同性喷雾</summary>
        private void SpawnDiveWake() {
            for (int i = 0; i < 2; i++) {
                Vector2 at = Owner.Center + Main.rand.NextVector2Circular(14f, 20f)
                    - Vector2.UnitY * Main.rand.NextFloat(0f, 26f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(at,
                    -Vector2.UnitY * Main.rand.NextFloat(6f, 14f)
                        + Vector2.UnitX * Main.rand.NextFloat(-1f, 1f),
                    PaperEdge * 0.8f, Main.rand.NextFloat(0.16f, 0.30f))
                    ?.Configure(Main.rand.Next(8, 14), affectedByGravity: false);
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(Owner.Center - Vector2.UnitY * 20f,
                    -Vector2.UnitY * Main.rand.NextFloat(1f, 2.4f), Color.White,
                    Main.rand.NextFloat(0.06f, 0.10f))
                    ?.Configure(Main.rand.Next(14, 22), new Color(110, 28, 34), new Color(22, 10, 16));
            }
        }

        private void PlayLandCue(Vector2 impact) {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.55f, Volume = 0.55f }, impact);
            SoundEngine.PlaySound(CWRSound.KatanaHit with { Pitch = -0.40f, Volume = 0.95f }, impact);
            if (Main.dedServ) {
                return;
            }
            Owner.CWR()?.GetScreenShake(6.5f);
            //贴地外扩的一圈尘墨：横向铺开而不是向上喷，砸地才有重量
            for (int i = 0; i < 18; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(impact + Vector2.UnitX * side * Main.rand.NextFloat(0f, 40f),
                    new Vector2(side * Main.rand.NextFloat(3.5f, 9f), -Main.rand.NextFloat(0.2f, 1.4f)),
                    Color.White, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(18, 30), new Color(118, 30, 36), new Color(24, 12, 18));
            }
            for (int i = 0; i < 10; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(impact,
                    new Vector2(side * Main.rand.NextFloat(4f, 11f), -Main.rand.NextFloat(1f, 4f)),
                    PaperEdge, Main.rand.NextFloat(0.22f, 0.40f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write((byte)phase);
            writer.Write((short)phaseTimer);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            byte rawPhase = reader.ReadByte();
            phase = rawPhase <= (byte)Phase.Settle ? (Phase)rawPhase : Phase.Poise;
            phaseTimer = reader.ReadInt16();
            initialized = true;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}

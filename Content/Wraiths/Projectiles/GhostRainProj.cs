using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Abilities;
using CalamityOverhaul.Content.Wraiths.Abilities.GhostRains;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Projectiles
{
    /// <summary>
    /// 鬼雨常驻雨幕控制器：提刀役使期间阴叠→入雨→稳态雨幕；收刀/换役鬼则散场。<br/>
    /// 入雨帧权威结算一次代价，其后雨蚀与拽入沿用该快照，直至本弹体退场。
    /// </summary>
    internal sealed class GhostRainProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float AgeRaw => ref Projectile.ai[0];
        private ref float PaidRaw => ref Projectile.ai[1];
        private ref float Mastery => ref Projectile.ai[2];

        private int Age {
            get => (int)AgeRaw;
            set => AgeRaw = value;
        }

        internal bool Paid {
            get => PaidRaw > 0.5f;
            private set => PaidRaw = value ? 1f : 0f;
        }

        private Player Owner => Main.player[Projectile.owner];

        private int fadeAge = -1;
        private byte stormSeed;
        private float masterySnapshot;
        private int weaponDamageSnapshot;
        private bool seedRolled;

        internal bool Fading => fadeAge >= 0;
        internal byte StormSeed => stormSeed;
        internal int StormAge => Age;
        /// <summary>表现簿记身份：弹体身份 + 入雨/散场跃迁，不含逐帧年龄。</summary>
        internal uint PresenceRevision => (uint)(Projectile.identity * 397
            ^ (Paid ? 1 : 0) ^ (Fading ? 2 : 0));

        /// <summary>驱动天幕/滤镜的在场强度 0~1</summary>
        internal float Presence {
            get {
                if (Fading) {
                    float from = GhostRainStorm.Envelope(Math.Max(Age, GhostRainStorm.GloomEnd));
                    return from * (1f - fadeAge / (float)GhostRainStorm.FadeFrames);
                }
                return GhostRainStorm.Envelope(Age);
            }
        }

        internal static GhostRainProj Find(int owner) {
            int type = ModContent.ProjectileType<GhostRainProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && projectile.owner == owner && projectile.type == type
                    && projectile.ModProjectile is GhostRainProj rain) {
                    return rain;
                }
            }
            return null;
        }

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

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

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(stormSeed);
            writer.Write((short)Math.Clamp(fadeAge, -1, short.MaxValue));
            writer.Write(masterySnapshot);
            writer.Write(weaponDamageSnapshot);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            stormSeed = reader.ReadByte();
            fadeAge = reader.ReadInt16();
            masterySnapshot = reader.ReadSingle();
            weaponDamageSnapshot = reader.ReadInt32();
            seedRolled = true;
        }

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = Owner.MountedCenter;
            EnsureSeed();

            bool controlsState = Main.netMode == NetmodeID.Server
                || Projectile.owner == Main.myPlayer;
            bool hasChannel = WraithAbilityService.HasAbilityChannel(Owner, GhostRainAbility.Key);

            if (Fading) {
                fadeAge++;
                if (!Main.dedServ) {
                    GhostRainFx.OnControllerTick(Owner, this);
                }
                if (fadeAge >= GhostRainStorm.FadeFrames) {
                    Projectile.Kill();
                }
                return;
            }

            if (controlsState && !hasChannel) {
                BeginFade(broadcast: true);
                return;
            }

            Age++;

            //入雨帧：仅权威端结算；失格则散场且不扣费
            if (!Paid && Age == GhostRainStorm.CommitFrame
                && Main.netMode != NetmodeID.MultiplayerClient) {
                if (WraithAbilityService.TryResolve(Owner, GhostRainAbility.Key,
                        out WraithAbilityContext context)
                    && WraithAbilityService.TryCommitUse(in context)) {
                    Paid = true;
                    masterySnapshot = MathHelper.Clamp(context.Mastery, 0f, 1f);
                    Mastery = masterySnapshot;
                    weaponDamageSnapshot = Math.Max(
                        Owner.GetWeaponDamage(context.VesselItem), 1);
                    GhostRainStorm.ShowRainText(Owner);
                    Projectile.netUpdate = true;
                }
                else {
                    BeginFade(broadcast: true);
                    return;
                }
            }

            if (Paid && Main.netMode != NetmodeID.MultiplayerClient) {
                UpdateAuthorityCombat();
            }

            if (!Main.dedServ) {
                GhostRainFx.OnControllerTick(Owner, this);
            }
        }

        private void EnsureSeed() {
            if (seedRolled) {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            stormSeed = (byte)Main.rand.Next(256);
            seedRolled = true;
            Projectile.netUpdate = true;
        }

        private void BeginFade(bool broadcast) {
            if (Fading) {
                return;
            }
            fadeAge = 0;
            if (broadcast) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>雨蚀与拽入：权威端按节拍结算，全程使用入雨帧快照。</summary>
        private void UpdateAuthorityCombat() {
            int t = Age;
            if (t <= GhostRainStorm.CommitFrame) {
                return;
            }

            if (t % GhostRainStorm.ErodeInterval == 0) {
                int damage = Math.Max(1, (int)(weaponDamageSnapshot
                    * MathHelper.Lerp(0.10f, 0.18f, masterySnapshot)));
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!IsErodable(npc)) {
                        continue;
                    }
                    int direction = npc.Center.X >= Owner.Center.X ? 1 : -1;
                    Owner.ApplyDamageToNPC(npc, damage, 0f, direction, false,
                        CWRRef.GetTrueMeleeDamageClass());
                }
            }

            //稳态雨幕持续可拽；节奏与旧雨峰相同
            if (t > GhostRainStorm.RainfallEnd
                && t % GhostRainStorm.YankInterval == 0) {
                YankOne();
            }
        }

        private void YankOne() {
            int count = 0;
            NPC picked = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!IsErodable(npc)) {
                    continue;
                }
                count++;
                if (Main.rand.Next(count) == 0) {
                    picked = npc;
                }
            }
            if (picked == null) {
                return;
            }

            int bonus = Math.Max(1, (int)(weaponDamageSnapshot
                * MathHelper.Lerp(0.10f, 0.18f, masterySnapshot)));
            if (picked.boss || picked.knockBackResist <= 0f) {
                int direction = picked.Center.X >= Owner.Center.X ? 1 : -1;
                Owner.ApplyDamageToNPC(picked, bonus * 2, 0f, direction, false,
                    CWRRef.GetTrueMeleeDamageClass());
            }
            else {
                float toCenterX = MathHelper.Clamp(
                    (Owner.Center.X - picked.Center.X) * 0.02f, -3f, 3f);
                picked.velocity = new Vector2(toCenterX, -8.5f * picked.knockBackResist - 2f);
                picked.netUpdate = true;
            }

            Vector2 throat = picked.Center - new Vector2(0f, 180f);
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendGhostRainYankFx(picked.whoAmI, throat);
            }
            else {
                GhostRainFx.TriggerYank(picked.Center, throat);
            }
        }

        private bool IsErodable(NPC npc)
            => npc.CanBeChasedBy()
                && Vector2.DistanceSquared(npc.Center, Owner.Center)
                    <= GhostRainStorm.Radius * GhostRainStorm.Radius;
    }
}

using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>SHPC 命中附加效果，数据侵蚀/时相减速等</summary>
    internal partial class SHPCNPCEffects : GlobalNPC
    {
        private const int MaxChronalSlowTime = 120;

        public override bool InstancePerEntity => true;

        /// <summary>数据侵蚀剩余帧数</summary>
        public int DataErosionTime;
        /// <summary>每次 tick 伤害量</summary>
        public int DataErosionTickDmg;
        /// <summary>时相减速剩余帧数</summary>
        public int ChronalSlowTime;
        /// <summary>黑曜石裂纹剩余帧数与层数</summary>
        public int ObsidianCrackTime;
        public int ObsidianCrackStacks;
        public int ObsidianCrackOwner = Main.maxPlayers;
        public int ObsidianCrackDamage;
        /// <summary>生命芽寄生剩余帧数与 tick 伤害</summary>
        public int LifebloomTime;
        public int LifebloomTickDmg;
        public int LifebloomOwner = Main.maxPlayers;
        /// <summary>湿苔缠绕剩余帧数与层数</summary>
        public int MossTime;
        public int MossStacks;
        /// <summary>蜂巢信息素标记</summary>
        public int PheromoneTime;
        public int PheromoneOwner = Main.maxPlayers;

        private static bool _shaderActive;
        private static int _shaderNpcIndex = -1;
        private ulong _lastChronalTick = ulong.MaxValue;
        private bool _chronalScaleApplied;

        /// <summary>施加数据侵蚀；多人下经服务端权威写入</summary>
        public void ApplyDataErosion(NPC npc, int duration, int tickDmg) {
            Player owner = ResolveLocalOwner();
            if (owner == null) {
                return;
            }
            RequestApply(owner, npc, EffectKind.DataErosion, duration, tickDmg);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ApplyDataErosionAuthority(duration, tickDmg);
            }
        }

        /// <summary>施加时相减速；多人下经服务端权威写入</summary>
        public void ApplyChronalSlow(NPC npc, int duration) {
            Player owner = ResolveLocalOwner();
            if (owner == null) {
                return;
            }
            RequestApply(owner, npc, EffectKind.ChronalSlow, duration);
            if (Main.netMode == NetmodeID.MultiplayerClient
                && npc?.active == true && !npc.boss && duration > 0) {
                ChronalSlowTime = Math.Max(ChronalSlowTime,
                    Math.Clamp(duration, 1, MaxChronalSlowTime));
                ApplyChronalScale(npc);
            }
        }

        public void ApplyObsidianCrack(NPC npc, int duration, int owner, int damage) {
            Player player = ResolveOwnerPlayer(owner);
            if (player == null) {
                return;
            }
            RequestApply(player, npc, EffectKind.ObsidianCrack, duration, damage);
            // 叠层爆发只在权威端执行，客户端只预览层数/计时
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ObsidianCrackTime = Math.Max(ObsidianCrackTime, duration);
                ObsidianCrackOwner = owner;
                ObsidianCrackDamage = Math.Max(ObsidianCrackDamage, damage);
                ObsidianCrackStacks = Math.Min(ObsidianCrackStacks + 1, 3);
            }
        }

        public void ApplyLifebloom(NPC npc, int duration, int tickDmg, int owner) {
            Player player = ResolveOwnerPlayer(owner);
            if (player == null) {
                return;
            }
            RequestApply(player, npc, EffectKind.Lifebloom, duration, tickDmg);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ApplyLifebloomAuthority(duration, tickDmg, owner);
            }
        }

        /// <summary>
        /// 请求引爆目标身上的黑曜石裂纹。OnOrbDetonation 只在 owner 客户端触发，
        /// 而 <see cref="BurstObsidian"/> 在客户端是 no-op：必须经服务端执行
        /// </summary>
        public void RequestObsidianBurst(NPC npc, int owner, int damage) {
            Player player = ResolveOwnerPlayer(owner);
            if (player == null) {
                return;
            }
            RequestApply(player, npc, EffectKind.ObsidianBurst, 1, damage);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                //本地预览清层；真正的爆发由服务端生成弹幕并 SyncProjectile 铺回
                ObsidianCrackTime = 0;
                ObsidianCrackStacks = 0;
                ObsidianCrackDamage = 0;
            }
        }

        //苔藓无请求通道：湿苔斑弹幕本身已同步到服务端，由其 AI 在权威端直写
        // ApplyMossAuthority，客户端只消费 ExtraAI 镜像

        public void ApplyPheromone(NPC npc, int duration, int owner) {
            Player player = ResolveOwnerPlayer(owner);
            if (player == null) {
                return;
            }
            RequestApply(player, npc, EffectKind.Pheromone, duration);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                ApplyPheromoneAuthority(duration, owner);
            }
        }

        internal void ApplyDataErosionAuthority(int duration, int tickDmg) {
            DataErosionTime = Math.Max(DataErosionTime, duration);
            DataErosionTickDmg = Math.Max(DataErosionTickDmg, tickDmg);
        }

        internal void ApplyChronalSlowAuthority(NPC npc, int duration) {
            if (Main.netMode == NetmodeID.MultiplayerClient || npc?.active != true
                || npc.boss || duration <= 0) {
                return;
            }

            int newTime = Math.Max(ChronalSlowTime,
                Math.Clamp(duration, 1, MaxChronalSlowTime));
            if (newTime == ChronalSlowTime) {
                return;
            }

            //生效沿才标 netUpdate，续时靠 PreAI 载波，避免逐次命中放大成同步洪流
            bool fresh = ChronalSlowTime <= 0;
            ChronalSlowTime = newTime;
            ApplyChronalScale(npc);
            if (fresh && Main.netMode == NetmodeID.Server) {
                npc.netUpdate = true;
            }
        }

        internal void ApplyObsidianCrackAuthority(NPC npc, int duration, int owner,
            int damage) {
            ObsidianCrackTime = Math.Max(ObsidianCrackTime, duration);
            ObsidianCrackOwner = owner;
            ObsidianCrackDamage = Math.Max(ObsidianCrackDamage, damage);
            ObsidianCrackStacks++;
            if (ObsidianCrackStacks >= 3) {
                BurstObsidian(npc, ObsidianCrackOwner, ObsidianCrackDamage);
                ObsidianCrackStacks = 0;
                ObsidianCrackTime = 0;
                ObsidianCrackDamage = 0;
            }
        }

        internal void ApplyLifebloomAuthority(int duration, int tickDmg, int owner) {
            LifebloomTime = Math.Max(LifebloomTime, duration);
            LifebloomTickDmg = Math.Max(LifebloomTickDmg, tickDmg);
            LifebloomOwner = owner;
        }

        internal void ApplyMossAuthority(int duration, int stacks) {
            MossTime = Math.Max(MossTime, duration);
            MossStacks = Math.Min(MossStacks + stacks, 5);
        }

        internal void ApplyPheromoneAuthority(int duration, int owner) {
            PheromoneTime = Math.Max(PheromoneTime, duration);
            PheromoneOwner = owner;
        }

        public override void SetDefaults(NPC npc) {
            ChronalSlowTime = 0;
            _lastChronalTick = ulong.MaxValue;
            _chronalScaleApplied = false;
        }

        public override void ResetEffects(NPC npc) {
            ulong update = Main.GameUpdateCount;
            if (_lastChronalTick == update) {
                return;
            }
            _lastChronalTick = update;

            if (ChronalSlowTime <= 0 || npc.boss) {
                bool notify = ChronalSlowTime > 0 && npc.boss;
                ClearChronalSlow(npc, notify);
                return;
            }

            if (!_chronalScaleApplied) {
                ApplyChronalScale(npc);
            }
            //客户端镜像也倒数：服务端拒绝请求时预览能自然过期，ExtraAI 到达则覆写纠偏
            ChronalSlowTime--;
            if (ChronalSlowTime == 0 && Main.netMode == NetmodeID.Server) {
                npc.netUpdate = true;
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 pos = npc.Center
                    + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, vel,
                    new Color(120, 80, 255), Main.rand.NextFloat(0.5f, 1.2f))
                    .Configure(new Color(60, 30, 180), Main.rand.Next(10, 20));
            }
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter,
            BinaryWriter binaryWriter) {
            bool chronal = ChronalSlowTime > 0 && !npc.boss;
            bool erosion = DataErosionTime > 0;
            bool crack = ObsidianCrackTime > 0;
            bool bloom = LifebloomTime > 0;
            bool moss = MossTime > 0;
            bool pheromone = PheromoneTime > 0;
            bitWriter.WriteBit(chronal);
            bitWriter.WriteBit(erosion);
            bitWriter.WriteBit(crack);
            bitWriter.WriteBit(bloom);
            bitWriter.WriteBit(moss);
            bitWriter.WriteBit(pheromone);
            if (chronal) {
                binaryWriter.Write((byte)Math.Clamp(ChronalSlowTime, 1,
                    MaxChronalSlowTime));
            }
            if (erosion) {
                binaryWriter.Write((ushort)Math.Clamp(DataErosionTime, 1, 600));
                binaryWriter.Write(Math.Clamp(DataErosionTickDmg, 1, 100_000));
            }
            if (crack) {
                binaryWriter.Write((ushort)Math.Clamp(ObsidianCrackTime, 1, 600));
                binaryWriter.Write((byte)Math.Clamp(ObsidianCrackStacks, 0, 3));
                binaryWriter.Write((byte)Math.Clamp(ObsidianCrackOwner, 0,
                    Main.maxPlayers));
                binaryWriter.Write(Math.Clamp(ObsidianCrackDamage, 0, 100_000));
            }
            if (bloom) {
                binaryWriter.Write((ushort)Math.Clamp(LifebloomTime, 1, 600));
                binaryWriter.Write(Math.Clamp(LifebloomTickDmg, 1, 100_000));
                binaryWriter.Write((byte)Math.Clamp(LifebloomOwner, 0,
                    Main.maxPlayers));
            }
            if (moss) {
                binaryWriter.Write((ushort)Math.Clamp(MossTime, 1, 600));
                binaryWriter.Write((byte)Math.Clamp(MossStacks, 1, 5));
            }
            if (pheromone) {
                binaryWriter.Write((ushort)Math.Clamp(PheromoneTime, 1, 600));
                binaryWriter.Write((byte)Math.Clamp(PheromoneOwner, 0,
                    Main.maxPlayers));
            }
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader,
            BinaryReader binaryReader) {
            bool chronal = bitReader.ReadBit();
            bool erosion = bitReader.ReadBit();
            bool crack = bitReader.ReadBit();
            bool bloom = bitReader.ReadBit();
            bool moss = bitReader.ReadBit();
            bool pheromone = bitReader.ReadBit();

            ChronalSlowTime = chronal
                ? Math.Clamp((int)binaryReader.ReadByte(), 1, MaxChronalSlowTime)
                : 0;
            _lastChronalTick = Main.GameUpdateCount;
            if (ChronalSlowTime > 0 && !npc.boss) {
                ApplyChronalScale(npc);
            }
            else {
                ClearChronalSlow(npc, false);
            }

            if (erosion) {
                DataErosionTime = Math.Clamp((int)binaryReader.ReadUInt16(), 1, 600);
                DataErosionTickDmg = Math.Clamp(binaryReader.ReadInt32(), 1, 100_000);
            }
            else {
                DataErosionTime = 0;
                DataErosionTickDmg = 0;
            }

            if (crack) {
                ObsidianCrackTime = Math.Clamp((int)binaryReader.ReadUInt16(), 1, 600);
                ObsidianCrackStacks = Math.Clamp((int)binaryReader.ReadByte(), 0, 3);
                ObsidianCrackOwner = Math.Clamp((int)binaryReader.ReadByte(), 0,
                    Main.maxPlayers);
                ObsidianCrackDamage = Math.Clamp(binaryReader.ReadInt32(), 0, 100_000);
            }
            else {
                ObsidianCrackTime = 0;
                ObsidianCrackStacks = 0;
                ObsidianCrackDamage = 0;
            }

            if (bloom) {
                LifebloomTime = Math.Clamp((int)binaryReader.ReadUInt16(), 1, 600);
                LifebloomTickDmg = Math.Clamp(binaryReader.ReadInt32(), 1, 100_000);
                LifebloomOwner = Math.Clamp((int)binaryReader.ReadByte(), 0,
                    Main.maxPlayers);
            }
            else {
                LifebloomTime = 0;
                LifebloomTickDmg = 0;
            }

            if (moss) {
                MossTime = Math.Clamp((int)binaryReader.ReadUInt16(), 1, 600);
                MossStacks = Math.Clamp((int)binaryReader.ReadByte(), 1, 5);
            }
            else {
                MossTime = 0;
                MossStacks = 0;
            }

            if (pheromone) {
                PheromoneTime = Math.Clamp((int)binaryReader.ReadUInt16(), 1, 600);
                PheromoneOwner = Math.Clamp((int)binaryReader.ReadByte(), 0,
                    Main.maxPlayers);
            }
            else {
                PheromoneTime = 0;
            }
        }

        public static void BurstObsidian(NPC npc, int owner, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (owner < 0 || owner >= Main.maxPlayers) {
                return;
            }
            int shardDamage = Math.Max(damage, 1);
            for (int i = 0; i < 4; i++) {
                NPC target = npc.Center.FindClosestNPC(620f, false, true,
                    new List<NPC> { npc });
                float angle = MathHelper.TwoPi * i / 4f
                    + Main.rand.NextFloat(-0.25f, 0.25f);
                Vector2 dir = target != null
                    ? (target.Center - npc.Center).SafeNormalize(
                        angle.ToRotationVector2())
                    : angle.ToRotationVector2();
                int shard = Projectile.NewProjectile(npc.GetSource_FromThis(),
                    npc.Center, dir * Main.rand.NextFloat(9f, 13f),
                    ModContent.ProjectileType<SHPCObsidianShardProj>(),
                    shardDamage, 0f, owner);
                if (shard >= 0 && shard < Main.maxProjectiles) {
                    SyncProjectileFromServer(Main.projectile[shard]);
                }
            }
            //中央冲击，Detonation 半径110 走 ai[2] 进生成包
            int centerDmg = Math.Max(damage * 2, 1);
            int idx = Projectile.NewProjectile(npc.GetSource_FromThis(),
                npc.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                centerDmg, 0f, owner, ai0: 0.4f, ai1: 0f, ai2: 110f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                SyncProjectileFromServer(Main.projectile[idx]);
            }
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(npc.Center, vel,
                        new Color(60, 35, 95), Main.rand.NextFloat(0.7f, 1.5f))
                        .Configure(new Color(255, 80, 35), Main.rand.Next(16, 30));
                }
                //PRT 加色批 A=0 整层不显示,A 须满值
                PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                    new Color(150, 80, 220), 0.05f).Configure(0.05f, 0.55f, 22);
                PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                    new Color(255, 110, 50), 0.05f).Configure(0.05f, 0.4f, 28);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50 with {
                    Volume = 0.55f,
                    Pitch = 0.2f,
                }, npc.Center);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                    Volume = 0.5f,
                    Pitch = -0.3f,
                }, npc.Center);
                CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
                    .SHPCNaturalFx.Shake(5f);
            }
        }

        public static List<NPC> CollectPheromoneTargets(int owner, Vector2 center,
            float range, int maxCount) {
            List<NPC> targets = [];
            float rangeSq = range * range;
            for (int i = 0; i < Main.maxNPCs && targets.Count < maxCount; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, center) > rangeSq) {
                    continue;
                }
                if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                    continue;
                }
                if (eff.PheromoneTime <= 0 || eff.PheromoneOwner != owner) {
                    continue;
                }
                targets.Add(npc);
            }
            return targets;
        }

        public override bool PreAI(NPC npc) {
            // 伤害/治疗/爆发只在权威端结算；计时器各端同步倒数，
            // 这样服务端拒绝请求时客户端预览也能自然过期，ExtraAI 到达则覆写纠偏
            bool authority = Main.netMode != NetmodeID.MultiplayerClient;

            if (DataErosionTime > 0) {
                DataErosionTime--;
                if (authority) {
                    int elapsed = (int)Main.GameUpdateCount;
                    if (elapsed % 30 == 0 && DataErosionTickDmg > 0) {
                        npc.SimpleStrikeNPC(DataErosionTickDmg, 0, false, 0f,
                            null, false, 0f, true);
                    }
                    if (DataErosionTime == 0 && Main.netMode == NetmodeID.Server) {
                        npc.netUpdate = true;
                    }
                }
            }
            else {
                DataErosionTickDmg = 0;
            }

            if (ObsidianCrackTime > 0) {
                ObsidianCrackTime--;
                if (authority && ObsidianCrackTime == 0
                    && Main.netMode == NetmodeID.Server) {
                    npc.netUpdate = true;
                }
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                        npc.width * 0.45f, npc.height * 0.45f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos,
                        Main.rand.NextVector2Circular(1.2f, 1.2f),
                        new Color(70, 45, 110), Main.rand.NextFloat(0.35f, 0.9f))
                        .Configure(new Color(255, 90, 40), Main.rand.Next(8, 18));
                }
            }
            else {
                ObsidianCrackStacks = 0;
                ObsidianCrackDamage = 0;
            }

            if (LifebloomTime > 0) {
                LifebloomTime--;
                if (authority) {
                    if ((int)Main.GameUpdateCount % 45 == 0 && LifebloomTickDmg > 0) {
                        npc.SimpleStrikeNPC(LifebloomTickDmg, 0, false, 0f,
                            null, false, 0f, true);
                        TryHealLifebloomOwner(npc, Math.Max(1, LifebloomTickDmg / 4));
                    }
                    if (LifebloomTime == 0 && Main.netMode == NetmodeID.Server) {
                        npc.netUpdate = true;
                    }
                }
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(
                        npc.Center + Main.rand.NextVector2Circular(
                            npc.width * 0.5f, npc.height * 0.5f),
                        new Vector2(0f, Main.rand.NextFloat(-1.8f, -0.4f)),
                        new Color(90, 255, 130), Main.rand.NextFloat(0.4f, 0.9f))
                        .Configure(new Color(30, 140, 55), Main.rand.Next(12, 24));
                }
            }
            else {
                LifebloomTickDmg = 0;
            }

            if (MossTime > 0) {
                MossTime--;
                if (authority && MossTime == 0
                    && Main.netMode == NetmodeID.Server) {
                    npc.netUpdate = true;
                }
                if (!npc.boss) {
                    npc.velocity *= MossStacks >= 4 ? 0.82f : 0.94f;
                }
            }
            else {
                MossStacks = 0;
            }

            if (PheromoneTime > 0) {
                PheromoneTime--;
                if (PheromoneTime == 0 && Main.netMode == NetmodeID.Server) {
                    npc.netUpdate = true;
                }
            }

            //存续载波：施加只在生效沿同步一次，之后每 ~10 帧补一发全量镜像，
            //兼做丢包自愈（错题本 3.2）；whoAmI 错相避免同帧集中广播
            if (Main.netMode == NetmodeID.Server
                && (DataErosionTime > 0 || ObsidianCrackTime > 0
                    || LifebloomTime > 0 || MossTime > 0
                    || PheromoneTime > 0 || ChronalSlowTime > 0)
                && ((int)Main.GameUpdateCount + npc.whoAmI) % 10 == 0) {
                npc.netUpdate = true;
            }
            return true;
        }

        public override void OnKill(NPC npc) {
            ClearChronalSlow(npc, false);
            if (LifebloomTime <= 0 || LifebloomOwner < 0
                || LifebloomOwner >= Main.maxPlayers) {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            NPC target = npc.Center.FindClosestNPC(520f, false, true,
                new List<NPC> { npc });
            if (target == null || !target.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                return;
            }
            eff.ApplyLifebloomAuthority(Math.Max(LifebloomTime / 2, 90),
                Math.Max(LifebloomTickDmg, 1), LifebloomOwner);
            if (Main.netMode == NetmodeID.Server) {
                target.netUpdate = true;
            }
        }

        private void ApplyChronalScale(NPC npc) {
            TimeFreezeSystem.SetNPCTimeScale<ChronalGripModule>(npc, 0.5f);
            _chronalScaleApplied = true;
        }

        private void ClearChronalSlow(NPC npc, bool notify) {
            ChronalSlowTime = 0;
            if (_chronalScaleApplied) {
                TimeFreezeSystem.ClearNPCTimeScale<ChronalGripModule>(npc);
                _chronalScaleApplied = false;
            }
            if (notify && Main.netMode == NetmodeID.Server) {
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 生命芽吸血：服务端算额度，owner 本机加血。非 SSC 下服务端写 statLife 会被覆盖
        /// </summary>
        private void TryHealLifebloomOwner(NPC npc, int amount) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (LifebloomOwner < 0 || LifebloomOwner >= Main.maxPlayers
                || amount <= 0) {
                return;
            }
            Player player = Main.player[LifebloomOwner];
            if (player == null || !player.active || player.dead) {
                return;
            }
            if (Vector2.DistanceSquared(player.Center, npc.Center)
                > 900f * 900f) {
                return;
            }
            if (Main.netMode == NetmodeID.Server) {
                // SpiritHeal(66) 收包端一律加算回血，owner 也生效；
                // PlayerHeal(35) 只播数字不加血，别用错。服务端镜像也同步加上
                player.statLife = Math.Min(player.statLife + amount,
                    player.statLifeMax2);
                NetMessage.SendData(MessageID.SpiritHeal, -1, -1, null,
                    LifebloomOwner, amount);
                return;
            }
            if (player.statLife >= player.statLifeMax2) {
                return;
            }
            player.statLife = Math.Min(player.statLife + amount, player.statLifeMax2);
            player.HealEffect(amount);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch,
            Vector2 screenPos, Color drawColor) {
            RestoreLeakedBatch(spriteBatch);

            if (DataErosionTime <= 0) {
                return true;
            }

            Effect shader = HackEffectAssets.HackContagion;
            if (shader == null) {
                return true;
            }

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            float totalTime = 240f;
            float progress = Math.Clamp(1f - DataErosionTime / totalTime, 0f, 1f);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(1f);
            shader.Parameters["texelSize"]?.SetValue(
                new Vector2(1f / tex.Width, 1f / tex.Height));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            _shaderNpcIndex = npc.whoAmI;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch,
            Vector2 screenPos, Color drawColor) {
            if (_shaderActive && _shaderNpcIndex == npc.whoAmI) {
                EndShaderBatch(spriteBatch);
            }
        }

        private static void RestoreLeakedBatch(SpriteBatch spriteBatch) {
            if (_shaderActive) {
                EndShaderBatch(spriteBatch);
            }
        }

        private static void EndShaderBatch(SpriteBatch spriteBatch) {
            if (!_shaderActive) {
                return;
            }
            _shaderActive = false;
            _shaderNpcIndex = -1;
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static Player ResolveLocalOwner() {
            if (Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[Main.myPlayer];
            return player?.active == true ? player : null;
        }

        private static Player ResolveOwnerPlayer(int owner) {
            if (owner < 0 || owner >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[owner];
            return player?.active == true ? player : null;
        }
    }
}

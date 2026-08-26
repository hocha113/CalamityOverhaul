using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenBee
{
    /// <summary>
    /// 蜂涡信标玩家侧：蜂标记账(仅owner端，命中解算本就跑在owner)与蜜蜡甲充放。<br/>
    /// 蜡甲模拟各端同跑(输入全是同步观测量：装备位/速度)，
    /// 甲存在性以 <see cref="WaxWardBuff"/> 为跨端真相(原版buff同步)；
    /// 吸收结算仅受击者本机(镜像 ShieldGeneratorPlayer 的 owner-local 契约)，远端只做表现近似
    /// </summary>
    internal class SwarmVortexPlayer : ModPlayer
    {
        /// <summary>蜂黄</summary>
        internal static readonly Color BeeGold = new(255, 210, 74);
        /// <summary>琥珀</summary>
        internal static readonly Color Amber = new(212, 136, 32);
        /// <summary>蜜蜡浅色</summary>
        internal static readonly Color WaxPale = new(242, 224, 158);

        private struct MarkEntry
        {
            /// <summary>当前层数</summary>
            public int Stacks;
            /// <summary>目标类型(槽位复用校验)</summary>
            public int NpcType;
            /// <summary>距上次命中帧数</summary>
            public int Age;
        }

        /// <summary>本帧装备生效，物品钩子逐帧点亮</summary>
        internal bool Equipped;
        private bool equippedLast;

        //==================== 蜂标(owner端私有记账) ====================
        private readonly Dictionary<int, MarkEntry> marks = [];
        private readonly List<int> markScratch = [];

        //==================== 蜜蜡甲 ====================
        /// <summary>当前蜡池 0~WaxMax(远端为本地镜像近似)</summary>
        internal float WaxCharge;
        /// <summary>连续静立帧数</summary>
        private int stillTicks;
        /// <summary>移动后的保留窗倒计时</summary>
        private int retainTimer;
        /// <summary>碎甲重结晶锁</summary>
        private int rebuildLock;
        /// <summary>吸收/碎裂闪光包络 0~1，渲染消费</summary>
        internal float CrackFlash;
        /// <summary>结晶完成脉冲 0~1，渲染消费</summary>
        internal float FormFlash;
        //上帧是否挂着蜡甲buff(远端碎甲边沿检测)
        private bool hadWaxBuff;
        //上帧充能是否已满(结晶完成边沿)
        private bool waxWasFull;

        public override void ResetEffects() => Equipped = false;

        //==================== 命中入口(仅owner端触发，见netcode §2.2) ====================

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => HandleHit(target);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            //蜂涡自己的蜂噬不回喂续时，否则自续永动
            if (proj.type == ModContent.ProjectileType<SwarmVortexProj>()) {
                return;
            }
            HandleHit(target);
        }

        private void HandleHit(NPC target) {
            if (!Equipped || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (target == null || !target.active || !target.CanBeChasedBy()) {
                return;
            }

            //已在蜂涡里：续时而不叠标
            int vortexType = ModContent.ProjectileType<SwarmVortexProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != Player.whoAmI || proj.type != vortexType) {
                    continue;
                }
                if ((int)proj.ai[0] != target.whoAmI || proj.ai[2] != 0f) {
                    continue;
                }
                proj.timeLeft = Math.Min(proj.timeLeft + SwarmVortexBeacon.VortexExtendPerHit,
                    SwarmVortexBeacon.VortexMaxTicks);
                proj.netUpdate = true;
                SpawnExtendFX(target);
                return;
            }

            //叠标
            if (!marks.TryGetValue(target.whoAmI, out MarkEntry entry) || entry.NpcType != target.type) {
                entry = new MarkEntry { Stacks = 0, NpcType = target.type };
            }
            entry.Stacks++;
            entry.Age = 0;

            if (entry.Stacks >= SwarmVortexBeacon.MarkMax) {
                marks.Remove(target.whoAmI);
                LaunchVortex(target);
                return;
            }
            marks[target.whoAmI] = entry;
            SpawnMarkTickFX(target, entry.Stacks);
        }

        /// <summary>叠满放涡：ai0=目标槽位 ai1=目标类型，全部随生成包出发(netcode §2.7)</summary>
        private void LaunchVortex(NPC target) {
            int damage = ComputeVortexDamage(Player);
            Item beacon = FindEquippedBeacon();
            var source = beacon != null ? Player.GetSource_Accessory(beacon)
                : Player.GetSource_Misc("SwarmVortexBeacon");
            Projectile.NewProjectile(source,
                target.Center, Vector2.Zero, ModContent.ProjectileType<SwarmVortexProj>(),
                damage, 0f, Player.whoAmI, target.whoAmI, target.type);

            //成形演出由弹幕自身首帧在各端播，这里只给owner一记近距离手感
            if (!VaultUtils.isServer && Player.Distance(target.Center) < 1100f) {
                Main.LocalPlayer?.CWR()?.GetScreenShake(3f);
            }
        }

        /// <summary>蜂噬单跳伤害：基础值吃全伤害加成，蜂巢背包再乘协同</summary>
        internal static int ComputeVortexDamage(Player player) {
            float dmg = player.GetTotalDamage(DamageClass.Generic)
                .ApplyTo(SwarmVortexBeacon.VortexHitDamage);
            if (player.strongBees) {
                dmg *= SwarmVortexBeacon.HivePackMult;
            }
            return Math.Max(1, (int)dmg);
        }

        private Item FindEquippedBeacon() {
            int beaconType = ModContent.ItemType<SwarmVortexBeacon>();
            foreach (Item item in Player.armor) {
                if (item != null && !item.IsAir && item.type == beaconType) {
                    return item;
                }
            }
            return null;
        }

        //==================== 逐帧：蜂标老化 + 蜡甲充放 ====================

        public override void PostUpdate() {
            //卸下清场
            if (!Equipped) {
                if (equippedLast) {
                    marks.Clear();
                    WaxCharge = 0f;
                    retainTimer = 0;
                    rebuildLock = 0;
                    if (Player.whoAmI == Main.myPlayer && Player.HasBuff<WaxWardBuff>()) {
                        Player.ClearBuff(ModContent.BuffType<WaxWardBuff>());
                    }
                }
                equippedLast = false;
                DecayFlashes();
                DetectRemoteBreak();
                return;
            }
            equippedLast = true;

            UpdateMarks();
            UpdateWax();
            DecayFlashes();
            DetectRemoteBreak();
        }

        private void DecayFlashes() {
            if (CrackFlash > 0f) {
                CrackFlash = Math.Max(0f, CrackFlash - 0.06f);
            }
            if (FormFlash > 0f) {
                FormFlash = Math.Max(0f, FormFlash - 0.045f);
            }
        }

        private void UpdateMarks() {
            if (Player.whoAmI != Main.myPlayer || marks.Count == 0) {
                return;
            }
            markScratch.Clear();
            foreach (KeyValuePair<int, MarkEntry> kv in marks) {
                NPC npc = kv.Key >= 0 && kv.Key < Main.maxNPCs ? Main.npc[kv.Key] : null;
                MarkEntry entry = kv.Value;
                //槽位失效/类型换人/超时 → 撤标
                if (npc == null || !npc.active || npc.type != entry.NpcType
                    || ++entry.Age > SwarmVortexBeacon.MarkFadeTicks) {
                    markScratch.Add(kv.Key);
                    continue;
                }
                marks[kv.Key] = entry;
                SpawnMarkIdleFX(npc, entry.Stacks);
            }
            foreach (int key in markScratch) {
                marks.Remove(key);
            }
        }

        private void UpdateWax() {
            bool still = !Player.dead && Player.velocity.LengthSquared() < 0.04f;

            if (rebuildLock > 0) {
                rebuildLock--;
            }
            else if (still) {
                stillTicks++;
                //落定一小拍后蜂群才开始回巢
                if (stillTicks > 10 && WaxCharge < SwarmVortexBeacon.WaxMax) {
                    WaxCharge = Math.Min(WaxCharge + SwarmVortexBeacon.WaxChargePerTick,
                        SwarmVortexBeacon.WaxMax);
                    retainTimer = SwarmVortexBeacon.WaxRetainTicks;
                    SpawnWaxChargeFX();
                }
            }
            else {
                stillTicks = 0;
                if (retainTimer > 0) {
                    retainTimer--;
                }
                else if (WaxCharge > 0f) {
                    //保留窗过后融蜡，滴落收尾
                    WaxCharge = Math.Max(0f, WaxCharge - SwarmVortexBeacon.WaxMeltPerTick);
                    SpawnWaxMeltFX();
                }
            }

            //结晶完成脉冲(边沿)
            bool full = WaxCharge >= SwarmVortexBeacon.WaxMax;
            if (full && !waxWasFull) {
                FormFlash = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.55f }, Player.Center);
                    SpawnWaxFormedFX();
                }
            }
            waxWasFull = full;

            //buff存在性：owner权威，跨端靠原版buff同步
            if (Player.whoAmI == Main.myPlayer) {
                if (WaxCharge >= 1f) {
                    Player.AddBuff(ModContent.BuffType<WaxWardBuff>(), 5);
                }
            }
        }

        /// <summary>远端碎甲边沿：buff消失且镜像池还厚 → 补一记碎裂演出并清镜像</summary>
        private void DetectRemoteBreak() {
            bool hasBuff = Player.HasBuff<WaxWardBuff>();
            if (Player.whoAmI != Main.myPlayer && hadWaxBuff && !hasBuff && WaxCharge > 8f) {
                WaxCharge = 0f;
                rebuildLock = SwarmVortexBeacon.WaxRebuildLock;
                CrackFlash = 1f;
                PlayShatterFX();
            }
            hadWaxBuff = hasBuff;
        }

        //==================== 受击吸收(owner-local，ShieldGenerator契约) ====================

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (Player.whoAmI != Main.myPlayer || !Equipped || WaxCharge < 1f) {
                return;
            }
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => {
                //整额吸收，至少放行1点保留受击反馈
                int absorb = (int)Math.Min(WaxCharge, info.Damage - 1);
                if (absorb <= 0) {
                    return;
                }
                info.Damage -= absorb;
                WaxCharge -= absorb;
                CrackFlash = 1f;
                retainTimer = Math.Max(retainTimer, 60);

                if (WaxCharge < 1f) {
                    //碎甲：清池入锁，撤buff让各端同见
                    WaxCharge = 0f;
                    rebuildLock = SwarmVortexBeacon.WaxRebuildLock;
                    Player.ClearBuff(ModContent.BuffType<WaxWardBuff>());
                    PlayShatterFX();
                }
                else if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit30 with { Volume = 0.5f, Pitch = 0.35f }, Player.Center);
                    SpawnWaxCrackFX(absorb);
                }
            };
        }

        public override void OnHurt(Player.HurtInfo info) {
            //远端吸收近似：owner已把到手伤害压过，这里只补表现(池扣多少无从得知，扣一口意思到位)
            if (Player.whoAmI == Main.myPlayer || VaultUtils.isServer) {
                return;
            }
            if (!Player.HasBuff<WaxWardBuff>() || WaxCharge < 1f) {
                return;
            }
            WaxCharge = Math.Max(0f, WaxCharge - Math.Max(10f, info.Damage));
            CrackFlash = 1f;
            SpawnWaxCrackFX(10);
        }

        public override void UpdateDead() {
            //死亡清甲，复活不带残盾；蜂标一并散伙
            WaxCharge = 0f;
            retainTimer = 0;
            stillTicks = 0;
            rebuildLock = 0;
            marks.Clear();
        }

        //==================== 表现(全部client-only) ====================

        /// <summary>叠标一跳：目标身上琥珀闪点，层数越高越密</summary>
        private void SpawnMarkTickFX(NPC target, int stacks) {
            if (VaultUtils.isServer) {
                return;
            }
            int count = 2 + stacks / 2;
            for (int i = 0; i < count; i++) {
                Vector2 pos = target.position + new Vector2(
                    Main.rand.NextFloat(target.width), Main.rand.NextFloat(target.height));
                PRTLoader.NewParticle<PRT_BeeGlint>(pos, Main.rand.NextVector2Circular(1.4f, 1.4f),
                    Color.Lerp(BeeGold, Amber, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f));
            }
            //临满一层：警示升调
            if (stacks == SwarmVortexBeacon.MarkMax - 1) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = 0.7f, MaxInstances = 2 }, target.Center);
            }
        }

        /// <summary>持标常态：零星蜂尘，读作"被蜂群盯上了"</summary>
        private void SpawnMarkIdleFX(NPC npc, int stacks) {
            if (VaultUtils.isServer || Main.GameUpdateCount % 9 != 0) {
                return;
            }
            if (!Main.rand.NextBool(3 - Math.Min(2, stacks / 3))) {
                return;
            }
            Vector2 pos = npc.position + new Vector2(
                Main.rand.NextFloat(npc.width), Main.rand.NextFloat(npc.height));
            PRTLoader.NewParticle<PRT_BeeGlint>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.5f),
                Amber * (0.5f + 0.5f * stacks / SwarmVortexBeacon.MarkMax), 0.8f);
        }

        /// <summary>续时反馈：几粒亮闪</summary>
        private void SpawnExtendFX(NPC target) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_BeeGlint>(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f),
                    Main.rand.NextVector2Circular(2f, 2f), BeeGold, 1.2f);
            }
        }

        /// <summary>充蜡中：蜂群回巢，单蜂螺旋归投</summary>
        private void SpawnWaxChargeFX() {
            if (VaultUtils.isServer || Main.GameUpdateCount % 3 != 0) {
                return;
            }
            float chargeT = WaxCharge / SwarmVortexBeacon.WaxMax;
            Vector2 start = Player.Center + Main.rand.NextVector2CircularEdge(150f, 120f);
            PRTLoader.NewParticle<PRT_VortexBee>(start, Main.rand.NextVector2Circular(2f, 2f),
                Color.Lerp(BeeGold, WaxPale, chargeT), Main.rand.NextFloat(0.85f, 1.1f))
                ?.Configure(Player, 16f, Main.rand.NextBool() ? 1f : -1f,
                    Main.rand.Next(26, 40), PRT_VortexBee.ModeConverge);
        }

        /// <summary>结晶完成：蜡屑定格一圈+亮环脉冲</summary>
        private void SpawnWaxFormedFX() {
            for (int i = 0; i < 14; i++) {
                float angle = MathHelper.TwoPi * i / 14f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * new Vector2(30f, 36f);
                PRTLoader.NewParticle<PRT_BeeGlint>(pos, angle.ToRotationVector2() * 0.6f,
                    WaxPale, 1.1f);
            }
        }

        /// <summary>吸收未碎：蜡屑迸出</summary>
        private void SpawnWaxCrackFX(int absorb) {
            int count = Math.Min(4 + absorb / 6, 12);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_WaxChip>(
                    Player.Center + Main.rand.NextVector2Circular(22f, 26f), vel,
                    Color.Lerp(WaxPale, Amber, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f));
            }
        }

        /// <summary>碎甲：蜡片四溅+蜜滴坠落+闷响，owner与远端边沿共用</summary>
        internal void PlayShatterFX() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.55f, Pitch = 0.4f }, Player.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.3f, Pitch = 0.6f }, Player.Center);
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_WaxChip>(
                    Player.Center + Main.rand.NextVector2Circular(20f, 26f), vel,
                    Color.Lerp(WaxPale, Amber, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.6f));
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -1f));
                PRTLoader.NewParticle<PRT_HoneyDrop>(Player.Center, vel,
                    Amber * 0.9f, Main.rand.NextFloat(0.9f, 1.3f));
            }
            if (Player.whoAmI == Main.myPlayer) {
                Main.LocalPlayer?.CWR()?.GetScreenShake(2.5f);
            }
        }

        /// <summary>融蜡收尾：偶发蜜滴</summary>
        private void SpawnWaxMeltFX() {
            if (VaultUtils.isServer || !Main.rand.NextBool(5)) {
                return;
            }
            Vector2 pos = Player.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-20f, 20f));
            PRTLoader.NewParticle<PRT_HoneyDrop>(pos, new Vector2(0f, 0.5f),
                Amber * 0.8f, Main.rand.NextFloat(0.7f, 1f));
        }
    }
}

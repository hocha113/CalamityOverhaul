using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenSlime
{
    /// <summary>
    /// 折光华尔兹：史莱姆皇后残酷遗物。
    /// 空中显著机动(跳跃顶点/二段跳/折返/冲刺)在身后凝结折射水晶(FIFO上限)，
    /// 水晶按布晶顺序连成棱镜光束链(满编首尾闭环)；同时大幅强化空中加速与转向。
    /// 水晶与连线由 owner 端权威管理，弹幕走原版同步链
    /// </summary>
    internal class RefractionWaltz : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //框架 §9 统一梯度：T3 段 40~60 金，取中值
            Item.value = Item.buyPrice(0, 45, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            RefractionWaltzPlayer mp = player.GetModPlayer<RefractionWaltzPlayer>();
            mp.Equipped = true;
            mp.HideVisual = hideVisual;
            mp.SourceItem = Item;
        }
    }

    /// <summary>
    /// 折光 debuff：被棱镜光束/碎晶/水晶命中的敌人染上，
    /// 期间移动被位移回滚式减速拖慢(载体 <see cref="RefractionTagNPC"/>)，
    /// 且折光华尔兹佩戴者对其暴击率提高。owner 端命中钩 AddBuff 骑原版 buff 同步
    /// </summary>
    internal class RefractionTag : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "RefractionWaltzDebuff";

        /// <summary>标记时长(帧)</summary>
        public const int TagFrames = 240;
        /// <summary>普通敌位移回滚系数(框架轻档)</summary>
        public const float SlowK = 0.20f;
        /// <summary>Boss级位移回滚系数</summary>
        public const float SlowBossK = 0.08f;
        /// <summary>佩戴者对折光目标的附加暴击率(%)</summary>
        public const int CritBonusPercent = 8;

        //Buffs.hjson 两语言均有对应条目(zh 正典)，代码默认值兜底
        public override LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), () => "折光");
        public override LocalizedText Description => this.GetLocalization(nameof(Description), () => "移动迟缓，折光华尔兹佩戴者对其暴击率提高8%");

        public override void SetStaticDefaults() => Main.debuff[Type] = true;

        public override void Update(NPC npc, ref int buffIndex) {
            npc.GetGlobalNPC<RefractionTagNPC>().Refracted = true;
            if (VaultUtils.isServer) {
                return;
            }
            float hue = npc.whoAmI * 0.161f % 1f + Main.GlobalTimeWrappedHourly * 0.1f;
            Lighting.AddLight(npc.Center, QueenMotion.PrismHue(hue).ToVector3() * 0.22f);
            if (Main.rand.NextBool(7)) {
                Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    DustID.TintableDust, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f), 140,
                    QueenMotion.PrismHue(hue + Main.rand.NextFloat(0.3f)), 0.9f);
                d.noGravity = true;
            }
            //暴击提示：体表细碎棱光闪点，低频点缀(约1~2粒/秒)
            if (Main.rand.NextBool(40)) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f),
                    Vector2.Zero, QueenMotion.PrismHue(hue + Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.4f, 2.2f))?.Configure(Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>
    /// 折光的逐实体载体：位移回滚式减速(不碰velocity，AI节奏不乱)，
    /// 各端按同步的 buff 状态一致回滚
    /// </summary>
    internal class RefractionTagNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>本帧处于折光(buff逐帧点亮)</summary>
        public bool Refracted;

        /// <summary>Boss级：本体/计名Boss/蠕虫链(整链同档，避免分段回滚拉伸链体)</summary>
        internal static bool IsBossLike(NPC npc)
            => npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.aiStyle == NPCAIStyleID.Worm;

        public override void ResetEffects(NPC npc) => Refracted = false;

        public override void PostAI(NPC npc) {
            if (!Refracted) {
                return;
            }
            //普通敌20%、Boss级8%(框架轻档)
            float hold = IsBossLike(npc) ? RefractionTag.SlowBossK : RefractionTag.SlowK;
            npc.position -= npc.velocity * hold;
        }
    }

    /// <summary>
    /// 折光华尔兹逐玩家状态：空中机动检测、水晶 FIFO、连线管理全在实例字段。
    /// 布晶/连线决策只在 owner 端做(弹幕经原版同步链到各端)，
    /// 空中机动增益与拖尾视觉各端按同步的装备状态自行生效
    /// </summary>
    internal class RefractionWaltzPlayer : ModPlayer
    {
        /// <summary>场上水晶上限，先进先出</summary>
        public const int MaxCrystals = 6;
        /// <summary>两次布晶最小间隔(帧)</summary>
        private const int SpawnCooldownFrames = 9;
        /// <summary>连线核对周期(帧)，分帧摊销</summary>
        private const int LinkScanPeriod = 15;

        //空中机动增益(舞蹈操作的地基，档位冻结)
        private const float AirAccelMult = 1.9f;
        private const float AirTurnMult = 2.4f;
        private const float AirSpeedMult = 1.18f;

        /// <summary>本帧生效装备，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>可见性开关(只压拖尾等纯装饰)</summary>
        public bool HideVisual;
        /// <summary>生成源物品引用，逐帧由 UpdateAccessory 刷新</summary>
        internal Item SourceItem;

        private int spawnCooldown;
        private int crystalSeq;
        private int linkScanTimer;
        private Vector2 prevVelocity;
        private bool prevAirborne;

        public override void ResetEffects() {
            Equipped = false;
            HideVisual = false;
        }

        /// <summary>空中加速度/转向/速度上限强化，原版算完后叠乘</summary>
        public override void PostUpdateRunSpeeds() {
            if (!Equipped || Player.velocity.Y == 0f) {
                return;
            }
            Player.runAcceleration *= AirAccelMult;
            Player.runSlowdown *= AirTurnMult;
            Player.maxRunSpeed *= AirSpeedMult;
            Player.accRunSpeed *= AirSpeedMult;
        }

        public override void PostUpdate() {
            if (!Equipped) {
                prevVelocity = Player.velocity;
                prevAirborne = false;
                return;
            }

            bool airborne = Player.velocity.Y != 0f;
            if (spawnCooldown > 0) {
                spawnCooldown--;
            }

            //布晶与连线：owner 端权威
            if (Player.whoAmI == Main.myPlayer && !Player.dead) {
                DetectWaltzStep(airborne);
                ReconcileLinks();
            }

            AerialTrailFX(airborne);

            prevVelocity = Player.velocity;
            prevAirborne = airborne;
        }

        /// <summary>折光目标弱点暴露：佩戴者对其附加8%暴击率(命中裁决端独立掷骰)</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Equipped || !target.HasBuff(ModContent.BuffType<RefractionTag>())) {
                return;
            }
            if (Main.rand.Next(100) < RefractionTag.CritBonusPercent) {
                modifiers.SetCrit();
            }
        }

        #region 布晶
        /// <summary>空中显著机动检测：跳跃顶点/骤升(二段跳)/横向折返/冲刺</summary>
        private void DetectWaltzStep(bool airborne) {
            if (spawnCooldown > 0 || !airborne || !prevAirborne) {
                return;
            }

            Vector2 v = Player.velocity;
            //跳跃顶点：上升转下落的过零帧
            bool apex = prevVelocity.Y < -1.2f && v.Y >= -0.05f;
            //骤升：一帧内获得强向上冲量(二段跳/云跳/翅膀起振)
            bool boost = v.Y <= prevVelocity.Y - 5f;
            //折返：高速横移中反向
            bool flip = Math.Sign(v.X) != Math.Sign(prevVelocity.X) && Math.Sign(v.X) != 0
                && Math.Abs(prevVelocity.X) > 3.5f;
            //冲刺：一帧内获得强横向冲量
            bool dash = Math.Abs(v.X) - Math.Abs(prevVelocity.X) > 5f;

            if (!apex && !boost && !flip && !dash) {
                return;
            }

            SpawnCrystal();
            spawnCooldown = SpawnCooldownFrames;
        }

        /// <summary>在身后凝结一颗折射水晶，超编先碎最老的一颗</summary>
        private void SpawnCrystal() {
            //FIFO：数满时点名最老者快速碎裂让位
            int count = 0;
            Projectile oldest = null;
            float oldestSeq = float.MaxValue;
            int crystalType = ModContent.ProjectileType<RefractionWaltzCrystalProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != crystalType || proj.owner != Player.whoAmI) {
                    continue;
                }
                count++;
                if (proj.ai[0] < oldestSeq) {
                    oldestSeq = proj.ai[0];
                    oldest = proj;
                }
            }
            if (count >= MaxCrystals && oldest?.ModProjectile is RefractionWaltzCrystalProj old) {
                old.BeginEvict();
            }

            crystalSeq++;
            //身后：逆运动方向退一段
            Vector2 back = -Player.velocity.SafeNormalize(Vector2.UnitY) * 30f;
            Vector2 pos = Player.Center + back;
            float hueSeed = crystalSeq * 0.147f % 1f;
            //基伤挂通用加成，生成时折算(长寿命者另有 owner 端逐帧刷新)
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic)
                .ApplyTo(RefractionWaltzCrystalProj.ContactDamage);
            Projectile.NewProjectile(GetCrystalSource(), pos, Vector2.Zero,
                crystalType, damage, 6f, Player.whoAmI,
                crystalSeq, hueSeed);
        }

        private Terraria.DataStructures.IEntitySource GetCrystalSource() {
            return SourceItem != null ? Player.GetSource_Accessory(SourceItem) : Player.GetSource_Misc("RefractionWaltz");
        }
        #endregion

        #region 连线
        /// <summary>
        /// 连线核对(owner 端，每15帧一轮摊销成本)：
        /// 按布晶顺序把成形水晶连成链(晶i↔晶i+1)，满编时首尾闭环；
        /// 拓扑外的旧光束点名收线。光束自身仍负责断线自查(端点消亡/视线被断)
        /// </summary>
        private void ReconcileLinks() {
            if (++linkScanTimer < LinkScanPeriod) {
                return;
            }
            linkScanTimer = 0;

            int crystalType = ModContent.ProjectileType<RefractionWaltzCrystalProj>();
            int beamType = ModContent.ProjectileType<RefractionWaltzBeamProj>();

            //收集成形水晶
            List<Projectile> crystals = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != crystalType || proj.owner != Player.whoAmI) {
                    continue;
                }
                if (proj.ModProjectile is not RefractionWaltzCrystalProj crystal || !crystal.LinkReady) {
                    continue;
                }
                (crystals ??= new List<Projectile>(MaxCrystals)).Add(proj);
            }

            //目标拓扑：布晶序(ai[0])相邻配对，满6颗加首尾闭环
            HashSet<int> desired = null;
            int chainCount = 0;
            if (crystals != null && crystals.Count >= 2) {
                crystals.Sort((a, b) => a.ai[0].CompareTo(b.ai[0]));
                chainCount = crystals.Count;
                desired = new HashSet<int>();
                for (int i = 0; i < chainCount - 1; i++) {
                    desired.Add(PackPair(crystals[i].identity, crystals[i + 1].identity));
                }
                if (chainCount >= MaxCrystals) {
                    desired.Add(PackPair(crystals[chainCount - 1].identity, crystals[0].identity));
                }
            }

            //既有光束盘点：链上的记账，链外的(端点仍活但拓扑已变)收线
            HashSet<int> linked = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != beamType || proj.owner != Player.whoAmI) {
                    continue;
                }
                int key = PackPair((int)proj.ai[0], (int)proj.ai[1]);
                if (desired != null && desired.Contains(key)) {
                    (linked ??= new HashSet<int>()).Add(key);
                }
                else if (proj.ModProjectile is RefractionWaltzBeamProj beam) {
                    beam.StartCollapse();
                }
            }

            if (desired == null) {
                return;
            }

            //补拉缺失链段
            for (int i = 0; i < chainCount; i++) {
                Projectile a = crystals[i];
                Projectile b;
                if (i < chainCount - 1) {
                    b = crystals[i + 1];
                }
                else if (chainCount >= MaxCrystals) {
                    b = crystals[0];
                }
                else {
                    break;
                }
                if (linked != null && linked.Contains(PackPair(a.identity, b.identity))) {
                    continue;
                }
                if (!Collision.CanHitLine(a.Center, 1, 1, b.Center, 1, 1)) {
                    continue;
                }
                float huePhase = ((a.ai[1] + b.ai[1]) * 0.5f + 0.13f) % 1f;
                int damage = (int)Player.GetTotalDamage(DamageClass.Generic)
                    .ApplyTo(RefractionWaltzBeamProj.BeamDamage);
                Projectile.NewProjectile(GetCrystalSource(), (a.Center + b.Center) * 0.5f, Vector2.Zero,
                    beamType, damage, 0.5f, Player.whoAmI,
                    a.identity, b.identity, huePhase);
            }
        }

        /// <summary>端点对打包：小 identity 在高位，保证无序对唯一</summary>
        private static int PackPair(int idA, int idB) {
            int lo = Math.Min(idA, idB);
            int hi = Math.Max(idA, idB);
            return lo << 16 | hi & 0xFFFF;
        }
        #endregion

        /// <summary>空中疾舞拖尾：纯装饰，尊重可见性开关</summary>
        private void AerialTrailFX(bool airborne) {
            if (VaultUtils.isServer || HideVisual || !airborne || Player.dead) {
                return;
            }
            if (Player.velocity.Length() < 7f || !Main.rand.NextBool(2)) {
                return;
            }
            Dust d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(10f, 16f),
                DustID.TintableDust, -Player.velocity * 0.12f, 150, QueenMotion.GetQueenDustColor(), 1.05f);
            d.noGravity = true;
        }
    }
}

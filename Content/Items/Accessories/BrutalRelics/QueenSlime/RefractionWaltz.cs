using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
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
    /// 视线可达的水晶两两拉起棱镜光束杀阵；同时大幅强化空中加速与转向。
    /// 水晶与连线由 owner 端权威管理，弹幕走原版同步链
    /// </summary>
    internal class RefractionWaltz : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期(史莱姆皇后档)饰品约4~6金购价，按系列基准放大到约4倍
            Item.value = Item.buyPrice(0, 20, 0, 0);
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
    /// 期间受本遗物一切伤害提高(结算在各弹幕 ModifyHitNPC)。
    /// owner 端命中钩 AddBuff 骑原版 buff 同步
    /// </summary>
    internal class RefractionTag : ModBuff
    {
        public override string Texture => CWRConstant.Item_BrutalRelic + "RefractionWaltzDebuff";

        /// <summary>标记时长(帧)</summary>
        public const int TagFrames = 240;
        /// <summary>受本遗物伤害倍率</summary>
        public const float DamageTakenMult = 1.3f;

        //hjson 骨架未含 Buffs 条目(所有权限制)，用代码默认值兜底，zh 正典
        public override LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), () => "折光");
        public override LocalizedText Description => this.GetLocalization(nameof(Description), () => "受到折光华尔兹的伤害提高30%");

        public override void SetStaticDefaults() => Main.debuff[Type] = true;

        public override void Update(NPC npc, ref int buffIndex) {
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

        //空中机动增益(超模档，用户签字基调)
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
            Projectile.NewProjectile(GetCrystalSource(), pos, Vector2.Zero,
                crystalType, RefractionWaltzCrystalProj.ContactDamage, 6f, Player.whoAmI,
                crystalSeq, hueSeed);
        }

        private Terraria.DataStructures.IEntitySource GetCrystalSource() {
            return SourceItem != null ? Player.GetSource_Accessory(SourceItem) : Player.GetSource_Misc("RefractionWaltz");
        }
        #endregion

        #region 连线
        /// <summary>
        /// 连线核对(owner 端，每15帧一轮摊销成本)：
        /// 对全部成形水晶两两配对，视线可达且尚无光束的补拉光束。
        /// 光束自身负责断线(端点消亡/视线被断由光束分帧自查)
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
            if (crystals == null || crystals.Count < 2) {
                return;
            }

            //已有光束的端点对(identity 打包成键)
            HashSet<int> linked = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != beamType || proj.owner != Player.whoAmI) {
                    continue;
                }
                (linked ??= new HashSet<int>()).Add(PackPair((int)proj.ai[0], (int)proj.ai[1]));
            }

            for (int i = 0; i < crystals.Count; i++) {
                for (int j = i + 1; j < crystals.Count; j++) {
                    Projectile a = crystals[i];
                    Projectile b = crystals[j];
                    if (linked != null && linked.Contains(PackPair(a.identity, b.identity))) {
                        continue;
                    }
                    if (!Collision.CanHitLine(a.Center, 1, 1, b.Center, 1, 1)) {
                        continue;
                    }
                    float huePhase = ((a.ai[1] + b.ai[1]) * 0.5f + 0.13f) % 1f;
                    Projectile.NewProjectile(GetCrystalSource(), (a.Center + b.Center) * 0.5f, Vector2.Zero,
                        beamType, RefractionWaltzBeamProj.BeamDamage, 0.5f, Player.whoAmI,
                        a.identity, b.identity, huePhase);
                }
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

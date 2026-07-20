using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 高效握把：零损循环——命中按本次射击蓝耗凝聚回收晶胞，
    /// 攒满五胞后下一次左键成为零损射击（蓝耗全额回收 + 晶胞灌注强化）
    /// </summary>
    internal sealed class EfficientGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //高效翠绿
        public override Color TintColor => new(60, 220, 120);

        //═════ 可调参数（平衡位） ═════
        /// <summary>满环晶胞数，攒满进入零损待发</summary>
        internal const int MaxCells = 5;
        /// <summary>凝成一枚晶胞所需回收能量</summary>
        private const float CellEnergyCost = 16f;
        /// <summary>光束命中回收比例：回收能量 = 本次射击蓝耗 × 此值（每束仅首次命中结算）</summary>
        private const float BeamRecoverRatio = 0.35f;
        /// <summary>激光命中回收比例（受内置冷却节流）</summary>
        private const float LaserRecoverRatio = 0.25f;
        /// <summary>激光回收内置冷却（帧），人群与单体回收节奏一致</summary>
        private const float LaserRecoverICD = 12f;
        /// <summary>零损射击每枚晶胞的伤害加成（满 5 胞 = ×2.0）</summary>
        private const float EmpowerDamagePerCell = 0.20f;
        /// <summary>激光形态零损引爆：基础伤害比例 + 每胞加成</summary>
        private const float LaserBlastBase = 0.6f;
        private const float LaserBlastPerCell = 0.25f;
        /// <summary>激光形态零损引爆半径（像素）与最大引爆距离</summary>
        private const float LaserBlastRadius = 190f;
        private const float LaserBlastMaxRange = 1600f;
        /// <summary>强化光束标记的强制过期帧数（光束寿命 180 帧 + 余量）</summary>
        private const int EmpowerTrackFrames = 720;
        /// <summary>射击蓝耗兜底值（拿不到 HeldItem 时用武器基础蓝耗）</summary>
        private const int FallbackShotCost = 8;

        //回收金绿配色：青柠主色 + 鎏金强化
        internal static readonly Color CellMain = new(170, 255, 80);
        internal static readonly Color CellDim = new(60, 130, 30);
        internal static readonly Color CellGold = new(255, 215, 110);
        internal static readonly Color GoldEdge = new(200, 140, 30);

        /// <summary>已凝成晶胞数（仅 myPlayer 端有意义，经星环 ai[0] 同步展示）</summary>
        private int cells;
        /// <summary>当前胞的凝聚能量 0~CellEnergyCost</summary>
        private float cellEnergy;
        /// <summary>进度是否待同步给星环弹幕</summary>
        private bool ringDirty;
        /// <summary>上一帧 itemAnimation，跳升 = 新一轮使用开始</summary>
        private int prevItemAnim;
        /// <summary>缓存的左键单发蓝耗；右键蓄力期间 GetManaCost 被清零，需用此值回收</summary>
        private int cachedShotCost = FallbackShotCost;
        /// <summary>零损强化光束标记：whoAmI → 过期帧（仅 owner 端填充，消亡/超时清理）</summary>
        private readonly Dictionary<int, int> empoweredBeams = new();
        /// <summary>激光回收内置冷却计时</summary>
        private float laserIcd;

        public override void Apply(ref ShootContext ctx) {
            //能量效率身份基线
            ctx.ManaCostMul += -0.12f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer || beam.IsDerived) return;
            //每束光束仅首次命中结算：一发射击的蓝耗只回收一次，穿透/人群不加速循环
            if (beam.Projectile.numHits != 0) return;
            AddRecycleEnergy(Main.player[beam.Projectile.owner], BeamRecoverRatio, target.Center);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer || laserIcd > 0f) return;
            laserIcd = LaserRecoverICD;
            AddRecycleEnergy(Main.player[laser.Projectile.owner], LaserRecoverRatio, target.Center);
        }

        /// <summary>命中回收：按本次射击蓝耗的比例累积凝聚能量，凝满成胞；零损待发期间不再回收；免蓝射击无从回收</summary>
        private void AddRecycleEnergy(Player owner, float ratio, Vector2 hitPos) {
            if (owner == null || !owner.active || cells >= MaxCells || cachedShotCost <= 0) return;

            cellEnergy += cachedShotCost * ratio;
            while (cellEnergy >= CellEnergyCost && cells < MaxCells) {
                cellEnergy -= CellEnergyCost;
                cells++;
            }
            if (cells >= MaxCells) {
                cellEnergy = 0f; //满环封存，多余能量不溢出存储
            }
            ringDirty = true;

            //命中点凝取微粒：能量碎屑朝玩家回流，体现"从命中处回收"（屏外不烧粒子）
            if (!VaultUtils.IsPointOnScreen(hitPos - Main.screenPosition, 150)) return;
            Vector2 back = (owner.Center - hitPos).SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(hitPos + Main.rand.NextVector2Circular(8f, 8f),
                    back * Main.rand.NextFloat(2f, 4.5f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    CellMain, Main.rand.NextFloat(0.35f, 0.7f)).Configure(CellDim, Main.rand.Next(12, 20));
            }
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            //零损强化光束的金色回流轨迹（仅 owner 客户端记录了标记）
            if (Main.netMode == NetmodeID.Server || empoweredBeams.Count == 0) return;
            if (!empoweredBeams.ContainsKey(beam.Projectile.whoAmI)) return;

            Projectile p = beam.Projectile;
            Lighting.AddLight(p.Center, CellGold.ToVector3() * 0.35f);
            if (!Main.rand.NextBool(3)
                || !VaultUtils.IsPointOnScreen(p.Center - Main.screenPosition, 150)) return;
            Player owner = Main.player[p.owner];
            Vector2 back = (owner.Center - p.Center).SafeNormalize(Vector2.Zero);
            PRTLoader.NewParticle<PRT_CyberSquare>(p.Center + Main.rand.NextVector2Circular(7f, 7f),
                back * Main.rand.NextFloat(1.5f, 3.5f) - p.velocity * 0.05f,
                CellGold, Main.rand.NextFloat(0.5f, 1.0f)).Configure(GoldEdge, Main.rand.Next(14, 26));
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (empoweredBeams.Count == 0) return;
            empoweredBeams.Remove(beam.Projectile.whoAmI);
        }

        public override void OnPlayerUpdate(Player player) {
            if (player.whoAmI != Main.myPlayer) return;

            laserIcd = MathF.Max(laserIcd - TimeGear.TimeScale, 0f);
            //标记字典兜底清理：光束正常消亡已移除，这里防极端情况泄漏
            if (empoweredBeams.Count > 0 && Main.GameUpdateCount % 60 == 0) {
                PruneExpiredMarks();
            }

            bool holding = player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID;
            //在非右键帧刷新左键蓝耗缓存（右键期间 ModifyManaCost 把 mult 清零，采样会失真）；
            //允许缓存为 0：免蓝窗口（速射喷射期）没花蓝就没得回收，循环让位给免蓝招牌
            if (holding && player.altFunctionUse != 2) {
                cachedShotCost = Math.Max(player.GetManaCost(player.HeldItem), 0);
            }
            int ringType = ModContent.ProjectileType<SHPCRecycleCellRingProj>();
            if (holding && player.ownedProjectileCounts[ringType] < 1) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Center, Vector2.Zero, ringType, 0, 0f, player.whoAmI);
                ringDirty = true; //新环需要一次进度回灌
            }
            SyncRing(player, ringType);

            //零损射击消费检测：itemAnimation 跳升 = 新一轮使用开始；
            //本发蓝耗为 0（如速射喷射期强制免蓝）时按住不放——免蓝窗口里返还无意义，攒着晶胞等窗口结束；
            //仅蓄力中的球封锁触发（防蓄力使用误判），球发射后飞行期左键齐射照常可吃零损
            bool newUse = player.itemAnimation > prevItemAnim;
            prevItemAnim = player.itemAnimation;
            if (newUse && holding && cells >= MaxCells
                && player.altFunctionUse != 2
                && player.GetManaCost(player.HeldItem) > 0
                && !OwnerHasChargingOrb(player)) {
                ConsumeZeroLossShot(player, ringType);
            }
        }

        /// <summary>拥有者是否有仍在蓄力的能量球（飞行中的球不算）</summary>
        private static bool OwnerHasChargingOrb(Player player) {
            int orbType = ModContent.ProjectileType<CyberChargeOrbProj>();
            if (player.ownedProjectileCounts[orbType] <= 0) return false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.type == orbType
                    && p.ModProjectile is CyberChargeOrbProj orb && orb.IsCharging) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>把凝聚进度写入星环 ai[0]；汇聚动画播放期间不打断</summary>
        private void SyncRing(Player player, int ringType) {
            if (!ringDirty) return;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != ringType) continue;
                if (p.ai[1] > 0f) return; //汇聚中，等动画结束再同步
                float progress = cells + MathHelper.Clamp(cellEnergy / CellEnergyCost, 0f, 0.999f);
                if (Math.Abs(p.ai[0] - progress) > 0.001f) {
                    p.ai[0] = progress;
                    p.netUpdate = true;
                }
                ringDirty = false;
                return;
            }
        }

        /// <summary>
        /// 零损射击结算：本次射击蓝耗全额回收（同帧内已扣除，等额返还即净零消耗），
        /// 光束模式强化本帧刚射出的原生光束，激光模式在瞄准点引爆全部回收能量
        /// </summary>
        private void ConsumeZeroLossShot(Player player, int ringType) {
            int spentCells = cells;
            cells = 0;
            cellEnergy = 0f;
            ringDirty = true;

            //免蓝：返还本次射击的实际蓝耗
            int refund = player.GetManaCost(player.HeldItem);
            if (refund > 0) {
                player.statMana = Math.Min(player.statMana + refund, player.statManaMax2);
                player.ManaEffect(refund);
            }

            float dmgMul = 1f + EmpowerDamagePerCell * spentCells;
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            if (ctx.LaserMode) {
                //激光形态：在瞄准点（限程）引爆全部回收能量
                Vector2 aim = Main.MouseWorld;
                Vector2 toAim = aim - player.Center;
                if (toAim.LengthSquared() > LaserBlastMaxRange * LaserBlastMaxRange) {
                    aim = player.Center + toAim.SafeNormalize(Vector2.UnitX) * LaserBlastMaxRange;
                }
                int dmg = Math.Max((int)(player.GetWeaponDamage(player.HeldItem)
                    * (LaserBlastBase + LaserBlastPerCell * spentCells)), 1);
                int idx = Projectile.NewProjectile(player.GetSource_FromThis(),
                    aim, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, player.whoAmI, ai0: 1f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].localAI[2] = LaserBlastRadius;
                }
                //爆点叠一层鎏金放电，把共享爆炸染上零损循环的识别色
                for (int i = 0; i < 14; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f) * Main.rand.NextFloat(0.5f, 1f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(aim, vel,
                        CellGold, Main.rand.NextFloat(0.7f, 1.5f)).Configure(GoldEdge, Main.rand.Next(18, 34));
                }
            }
            else {
                //光束形态：本帧 ItemCheck 刚生成、尚未跑首帧 AI（localAI[0]==0）的原生光束即这次零损齐射
                int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (!p.active || p.owner != player.whoAmI || p.type != beamType) continue;
                    if (p.localAI[0] != 0f) continue;
                    if (p.ModProjectile is not CyberTraceBeamProj beam || beam.IsDerived) continue;
                    p.damage = Math.Max((int)(p.damage * dmgMul), p.damage + 1);
                    p.netUpdate = true;
                    empoweredBeams[p.whoAmI] = (int)Main.GameUpdateCount + EmpowerTrackFrames;
                }
            }

            //星环进入汇聚动画（音效与粒子由星环按状态变化播放）
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != ringType) continue;
                if (p.ModProjectile is SHPCRecycleCellRingProj ring) {
                    ring.StartGather();
                }
                break;
            }

            //枪口灌注爆发 + 屏幕微震（myPlayer 必为客户端）
            if (player.TryGetModPlayer(out CWRPlayer cp)) {
                cp.GetScreenShake(3f);
            }
            Vector2 muzzleDir = player.itemRotation.ToRotationVector2();
            if (player.direction == -1) muzzleDir = -muzzleDir;
            Vector2 muzzle = player.Center + muzzleDir * 52f;
            PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero,
                CellGold with { A = 0 }, 0.05f).Configure(0.05f, 0.5f, 18);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = muzzleDir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_CyberSquare>(muzzle, vel,
                    CellGold, Main.rand.NextFloat(0.6f, 1.3f)).Configure(GoldEdge, Main.rand.Next(16, 30));
            }
        }

        private void PruneExpiredMarks() {
            int now = (int)Main.GameUpdateCount;
            List<int> expired = null;
            foreach (var kv in empoweredBeams) {
                if (now > kv.Value) {
                    (expired ??= new List<int>()).Add(kv.Key);
                }
            }
            if (expired == null) return;
            foreach (int key in expired) {
                empoweredBeams.Remove(key);
            }
        }
    }

    /// <summary>
    /// 回收晶胞星环：五枚菱形晶胞环绕玩家缓慢公转（SHPCModRecycleCell.fx 逐胞绘制）；
    /// ai[0]=凝聚进度 0~5 含小数液面（同步），ai[1]=汇聚倒计时；改件卸下或换武器自毁
    /// </summary>
    internal sealed class SHPCRecycleCellRingProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int CellCount = EfficientGripModule.MaxCells;
        private const float RingRadius = 54f;
        //单胞画布边长（px），菱形绘制在此 quad 内
        private const float CellQuadSize = 40f;
        /// <summary>汇聚动画帧数：晶胞从环位冲向枪口</summary>
        internal const float GatherFrames = 12f;

        /// <summary>凝聚进度 0~5（含小数=当前胞液面），经 ai[0] 同步</summary>
        private float Progress {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        /// <summary>汇聚倒计时，>0 播放收束动画；各端本地倒数</summary>
        private float GatherTimer {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        //───── 本地视觉状态（各端由同步的 Progress/GatherTimer 独立推演）─────
        private readonly float[] cellFill = new float[CellCount];
        private readonly float[] cellFlash = new float[CellCount];
        private float primedPulse;
        private float ringRotation;
        private float ringSpin;
        private float spawnFade;
        private int prevWholeCells;
        private float prevGatherTimer;
        private bool baselineInit;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        /// <summary>模块调用：进入汇聚动画（拥有者端），随同步包扩散到各端</summary>
        public void StartGather() {
            if (Projectile.owner != Main.myPlayer) return;
            GatherTimer = GatherFrames;
            Projectile.netUpdate = true;
        }

        /// <summary>枪口汇聚点：随玩家当前持械朝向，各端可算</summary>
        private Vector2 GatherTargetWorld() {
            Player owner = Main.player[Projectile.owner];
            Vector2 dir = owner.itemRotation.ToRotationVector2();
            if (owner.direction == -1) dir = -dir;
            return owner.Center + dir * 52f;
        }

        /// <summary>阵位 i 的世界坐标：公转环位，汇聚期间加速冲向枪口</summary>
        private Vector2 CellWorldPos(int index) {
            Vector2 orbit = Projectile.Center
                + (ringRotation + MathHelper.TwoPi * index / CellCount).ToRotationVector2() * RingRadius;
            if (GatherTimer > 0f) {
                float t = 1f - MathHelper.Clamp(GatherTimer / GatherFrames, 0f, 1f);
                return Vector2.Lerp(orbit, GatherTargetWorld(), t * t); //ease-in 冲刺
            }
            return orbit;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead
                || owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID) {
                Projectile.Kill();
                return;
            }
            //改件自检只在拥有者端裁决：模块数据不联机同步，远端 HasModule 恒 false，
            //若远端也 Kill 会陷入"owner netUpdate 重建→远端自杀"抖动循环
            if (Projectile.owner == Main.myPlayer
                && !SHPCModificationSystem.HasModule<EfficientGripModule>(owner)) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 30;

            float timeScale = TimeGear.TimeScale;
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.3f);

            float progressNow = MathHelper.Clamp(Progress, 0f, CellCount);
            int wholeCells = (int)progressNow;
            bool gathering = GatherTimer > 0f;
            bool primed = !gathering && wholeCells >= CellCount;

            //首帧以同步值为基线，避免重新召唤时把"状态恢复"误播成逐胞凝成音画
            if (!baselineInit) {
                prevWholeCells = wholeCells;
                prevGatherTimer = GatherTimer;
                baselineInit = true;
            }

            //公转：进度越高越快，待发提速，汇聚急旋
            float targetSpin = 0.007f + progressNow * 0.0022f + (primed ? 0.02f : 0f) + (gathering ? 0.07f : 0f);
            ringSpin = MathHelper.Lerp(ringSpin, targetSpin, 0.1f);
            ringRotation += ringSpin * timeScale;

            spawnFade = MathF.Min(spawnFade + 0.06f, 1f);
            primedPulse = MathHelper.Lerp(primedPulse, primed ? 1f : 0f, 0.12f);
            for (int i = 0; i < CellCount; i++) {
                cellFlash[i] = MathF.Max(cellFlash[i] - 0.07f, 0f);
                //液面目标：凝聚进度逐胞涨起；汇聚期间保持满载飞行
                float fillTarget = gathering ? 1f : MathHelper.Clamp(progressNow - i, 0f, 1f);
                cellFill[i] = MathHelper.Lerp(cellFill[i], fillTarget, 0.18f);
            }

            //成胞检测：各端由同步的 Progress 驱动音画
            if (wholeCells != prevWholeCells) {
                if (wholeCells > prevWholeCells && !gathering) {
                    OnCellFormed(wholeCells);
                }
                prevWholeCells = wholeCells;
            }

            //汇聚倒计时：各端本地倒数保证动画流畅；结束瞬间枪口放电
            if (gathering) {
                GatherTimer = MathF.Max(GatherTimer - timeScale, 0f);
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                    //汇聚途中的能量逸散
                    int idx = Main.rand.Next(CellCount);
                    PRTLoader.NewParticle<PRT_CyberSquare>(CellWorldPos(idx), Main.rand.NextVector2Circular(1.5f, 1.5f),
                        EfficientGripModule.CellGold, Main.rand.NextFloat(0.4f, 0.8f))
                        .Configure(EfficientGripModule.GoldEdge, Main.rand.Next(8, 16));
                }
            }
            if (prevGatherTimer <= 0f && GatherTimer > 0f) {
                OnGatherStart();
            }
            if (prevGatherTimer > 0f && GatherTimer <= 0f) {
                OnGatherFinish();
                if (Projectile.owner == Main.myPlayer) {
                    Progress = 0f;
                    Projectile.netUpdate = true;
                }
            }
            prevGatherTimer = GatherTimer;

            //待发态金辉逸散，提示零损射击已就绪
            if (primed && Main.netMode != NetmodeID.Server && Main.rand.NextBool(8)) {
                int idx = Main.rand.Next(CellCount);
                PRTLoader.NewParticle<PRT_CyberSquare>(CellWorldPos(idx) + Main.rand.NextVector2Circular(5f, 5f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.0f),
                    EfficientGripModule.CellGold, Main.rand.NextFloat(0.35f, 0.65f))
                    .Configure(EfficientGripModule.GoldEdge, Main.rand.Next(12, 20));
            }

            Lighting.AddLight(Projectile.Center,
                (primed ? EfficientGripModule.CellGold : EfficientGripModule.CellMain).ToVector3()
                * (0.08f + progressNow * 0.04f) * spawnFade);
        }

        /// <summary>第 n 枚晶胞凝聚成型：逐胞升调音 + 白闪；满环附加零损就绪提示</summary>
        private void OnCellFormed(int wholeCells) {
            int idx = Math.Clamp(wholeCells - 1, 0, CellCount - 1);
            cellFlash[idx] = 1f;
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 pos = CellWorldPos(idx);
            SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.24f, Pitch = 0.1f + wholeCells * 0.12f }, pos);
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2CircularEdge(2.2f, 2.2f),
                    EfficientGripModule.CellMain, Main.rand.NextFloat(0.4f, 0.85f))
                    .Configure(EfficientGripModule.CellDim, Main.rand.Next(10, 18));
            }
            if (wholeCells >= CellCount) {
                //满环：零损射击就绪
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                    EfficientGripModule.CellGold with { A = 0 }, 0.05f).Configure(0.05f, 0.42f, 16);
            }
        }

        /// <summary>汇聚开始（零损射击瞬间）：全胞金闪 + 强化射击音，各端本地播放</summary>
        private void OnGatherStart() {
            for (int i = 0; i < CellCount; i++) {
                cellFlash[i] = 1f;
            }
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.45f, Pitch = 0.3f }, Projectile.Center);
        }

        /// <summary>汇聚结束：晶胞在枪口放电解体</summary>
        private void OnGatherFinish() {
            if (Main.netMode == NetmodeID.Server) return;
            Vector2 muzzle = GatherTargetWorld();
            SoundEngine.PlaySound(SoundID.Item112 with { Volume = 0.35f, Pitch = 0.5f }, muzzle);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(muzzle, vel,
                    EfficientGripModule.CellGold, Main.rand.NextFloat(0.5f, 1.1f))
                    .Configure(EfficientGripModule.GoldEdge, Main.rand.Next(14, 24));
            }
        }

        public override void OnKill(int timeLeft) {
            //回收解体：胞位碎屑，避免凭空消失
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < CellCount; i++) {
                if (cellFill[i] < 0.05f) continue;
                PRTLoader.NewParticle<PRT_CyberSquare>(CellWorldPos(i), Main.rand.NextVector2Circular(1.8f, 1.8f),
                    EfficientGripModule.CellMain, Main.rand.NextFloat(0.5f, 0.8f))
                    .Configure(EfficientGripModule.CellDim, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (spawnFade < 0.02f) return;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null || glow == null) return;

            //胞位背光（当前批次为 Deferred+Additive，直接绘制）
            for (int i = 0; i < CellCount; i++) {
                if (cellFill[i] < 0.03f && cellFlash[i] < 0.03f) continue;
                Vector2 cellScreen = CellWorldPos(i) - Main.screenPosition;
                Color glowCol = Color.Lerp(EfficientGripModule.CellMain, EfficientGripModule.CellGold,
                    MathF.Max(primedPulse, GatherTimer > 0f ? 1f : 0f));
                spriteBatch.Draw(glow, cellScreen, null,
                    glowCol * ((0.20f * cellFill[i] + cellFlash[i] * 0.35f + primedPulse * 0.12f) * spawnFade),
                    0f, glow.Size() * 0.5f, 0.38f + cellFlash[i] * 0.15f, SpriteEffects.None, 0f);
            }

            //切 Immediate 批次，用专属晶胞着色器逐胞绘制
            Effect shader = EffectLoader.SHPCModRecycleCell?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader != null && noise != null) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.graphics.GraphicsDevice.Textures[1] = noise;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
                shader.Parameters["fadeAlpha"]?.SetValue(spawnFade);
                shader.Parameters["primedPulse"]?.SetValue(primedPulse);
                shader.Parameters["mainColor"]?.SetValue(EfficientGripModule.CellMain.ToVector3());
                shader.Parameters["coreColor"]?.SetValue(EfficientGripModule.CellGold.ToVector3());

                //汇聚时晶胞收拢缩小，制造"灌注进枪口"的体积感
                float gatherShrink = GatherTimer > 0f
                    ? MathHelper.Lerp(1f, 0.45f, 1f - MathHelper.Clamp(GatherTimer / GatherFrames, 0f, 1f))
                    : 1f;

                for (int i = 0; i < CellCount; i++) {
                    shader.Parameters["fill"]?.SetValue(cellFill[i]);
                    shader.Parameters["flash"]?.SetValue(cellFlash[i]);
                    shader.Parameters["cellRot"]?.SetValue(ringRotation * 0.7f + i * 0.5f);
                    shader.CurrentTechnique.Passes[0].Apply();
                    Vector2 cellScreen = CellWorldPos(i) - Main.screenPosition;
                    spriteBatch.Draw(white, cellScreen, null, Color.White, 0f,
                        new Vector2(0.5f, 0.5f), CellQuadSize * gatherShrink, SpriteEffects.None, 0f);
                }

                //恢复调用方期望的批次状态（Deferred + Additive + PointWrap）
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }
    }
}

using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EmpressOfLight
{
    /// <summary>
    /// 昼夜干涉之翼：光之女皇残酷遗物。
    /// 授予光女翼档位飞行与悬浮；昼形态自己的弹幕拉出干涉光径、光径交点引爆棱镜爆裂，
    /// 夜形态机动与自动闪避强化、逼近的敌方弹幕拖出极光残迹；
    /// 破晓与入夜的瞬间引发一次全屏干涉爆发（范围伤害+清除敌方弹幕）
    /// </summary>
    internal class WingsOfInterference : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //平衡框架 §9：T4 遗物统一 75 金
            Item.value = Item.buyPrice(0, 75, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            WingsOfInterferencePlayer mp = player.GetModPlayer<WingsOfInterferencePlayer>();
            mp.Equipped = true;
            mp.HideVisual = hideVisual;
            mp.SourceItem = Item;
            mp.ApplyFlightStats();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            base.ModifyTooltips(tooltips);
            //当前形态动态行（hjson块所有权只覆盖Tooltip，此行走代码默认值兜底，zh正典）
            string formLine = Main.dayTime
                ? this.GetLocalization("FormDay", () => "此刻是白昼：干涉织网已展开").Value
                : this.GetLocalization("FormNight", () => "此刻是黑夜：极光疾翔已展开").Value;
            tooltips.Add(new TooltipLine(Mod, "InterferenceForm", formLine) {
                OverrideColor = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.08f) % 1f, 0.75f, 0.72f)
            });
        }
    }

    /// <summary>
    /// 单条干涉光径：跟踪一枚己方弹幕的飞行轨迹（等距采样）。
    /// 视觉各端本地自采（弹幕位置本身经原版同步，形态各端近似一致）；
    /// 交点判定只在 owner 端做，爆裂经弹幕同步链广播
    /// </summary>
    internal sealed class InterferenceTrail
    {
        public int ProjWhoAmI;
        public int ProjIdentity;
        public int ProjType;
        /// <summary>本径色相（identity 黄金比散列，各端一致）</summary>
        public float Hue;
        /// <summary>宿主弹幕仍在飞（false 进入渐隐余像）</summary>
        public bool Alive = true;
        /// <summary>渐隐计时（Alive=false 后递增）</summary>
        public int LingerTimer;
        /// <summary>本帧新增点数（交点检测只测新增段）</summary>
        public int FreshPoints;
        /// <summary>旧点在前新点在后</summary>
        public readonly List<Vector2> Points = new(48);
        //包围盒（粗剔除与AABB预筛）
        public Vector2 Min;
        public Vector2 Max;

        public void RecalcBounds() {
            Min = new Vector2(float.MaxValue);
            Max = new Vector2(float.MinValue);
            for (int i = 0; i < Points.Count; i++) {
                Min = Vector2.Min(Min, Points[i]);
                Max = Vector2.Max(Max, Points[i]);
            }
        }
    }

    /// <summary>
    /// 昼夜干涉之翼逐玩家状态：飞行授予、光径采样、交点引爆、夜闪避、切换爆发。
    /// 状态全在实例字段；采样与演出各端本地，伤害决策只在 owner 端
    /// </summary>
    internal class WingsOfInterferencePlayer : ModPlayer
    {
        #region 常量
        /// <summary>光女翼档位（LongTrailRainbowWings：飞速8/加速4.5/按下悬浮16）</summary>
        private const int WingSlotEmpress = 45;
        /// <summary>昼形态飞行时间（原版最强翼180帧，遗物越级档）</summary>
        private const int WingTimeDay = 210;
        /// <summary>夜形态飞行时间</summary>
        private const int WingTimeNight = 270;
        /// <summary>夜形态移动速度加成</summary>
        private const float NightMoveSpeed = 0.20f;
        /// <summary>夜形态跑速/翼平飞上限乘数</summary>
        private const float NightRunMult = 1.10f;

        /// <summary>夜闪避冷却（帧）：48s 周期，2026-08-29 用户终审定值</summary>
        public const int DodgeCooldownFrames = 2880;
        /// <summary>闪避成功后的无敌帧</summary>
        private const int DodgeImmuneFrames = 30;

        /// <summary>同时跟踪的光径上限</summary>
        private const int MaxTrails = 12;
        /// <summary>轨迹采样步长(px)</summary>
        private const float SampleStep = 14f;
        /// <summary>单径最大点数（约640px光径）</summary>
        private const int MaxTrailPoints = 46;
        /// <summary>弹幕死亡后光径渐隐帧数</summary>
        public const int TrailLingerFrames = 30;
        /// <summary>单帧位移超过该值视为瞬移，断线重录</summary>
        private const float TeleportBreak = 200f;

        /// <summary>棱镜爆裂基伤（Generic 全加成）</summary>
        private const float BurstBaseDamage = 200f;
        /// <summary>爆裂判定半径(px)</summary>
        private const float BurstRadius = 150f;
        /// <summary>爆点位置冷却（帧）：同点近旁不复爆</summary>
        private const int BurstLockFrames = 45;
        private const float BurstLockRadius = 160f;
        /// <summary>每帧引爆上限（节流）</summary>
        private const int MaxBurstsPerFrame = 2;

        /// <summary>昼夜切换全屏爆发基伤</summary>
        private const float DawnBurstBaseDamage = 1100f;
        /// <summary>爆发判定半径(px)</summary>
        public const float DawnBurstRadius = 1250f;
        #endregion

        #region 状态字段（全实例，禁static）
        /// <summary>本帧生效装备，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>可见性开关（只压翼/光径等纯装饰）</summary>
        public bool HideVisual;
        /// <summary>生成源物品引用，逐帧由 UpdateAccessory 刷新</summary>
        internal Item SourceItem;

        /// <summary>翼展开度 0~1（视觉，各端本地缓动）</summary>
        public float WingSpread;
        /// <summary>翼扑动相位累计</summary>
        public float WingFlap;
        /// <summary>昼夜视觉混合 0夜→1昼</summary>
        public float DayBlend;

        /// <summary>夜闪避冷却剩余帧</summary>
        public int DodgeCooldown;

        //昼夜切换沿检测
        private bool lastDayTime;
        private bool dayInitialized;

        /// <summary>活跃光径（渲染句柄读取）</summary>
        internal readonly List<InterferenceTrail> Trails = new(MaxTrails);
        //爆点位置冷却表（owner 端）
        private readonly List<Vector2> burstLockPos = new();
        private readonly List<int> burstLockTimer = new();

        //---- P1 采样重构状态 ----
        /// <summary>事件驱动的光径候选（OnSpawn 推入，owner 端 PostUpdate 排空）</summary>
        private readonly List<int> pendingTrailProj = new(8);
        /// <summary>注册扫描计时：owner 每4帧补扫兜漏，旁观副本每8帧低频采样</summary>
        private int trailSampleTimer;
        /// <summary>敌弹计数门刷新倒计时（每8帧刷新）</summary>
        private int hostileGateTimer;
        /// <summary>缓存的敌对弹幕数，空场时夜读弹层整段跳过</summary>
        private int hostileProjCached;

        /// <summary>在场帧戳：翼或光径可见的实例每帧盖戳，渲染层据此跳过空场全玩家表扫描</summary>
        internal static ActivityStamp PresenceStamp;
        #endregion

        public override void ResetEffects() {
            //冷却纪律：冻结制。冷却只在装备时递减，卸装冻结不清零不递减（原「卸装清零」
            //可摘戴一次白嫖重置 48s 必闪，D 阶段回溯修正）。Equipped 此刻仍是上一帧值，先读再清旗
            if (Equipped && DodgeCooldown > 0) {
                DodgeCooldown--;
            }
            Equipped = false;
            HideVisual = false;
        }

        /// <summary>飞行授予：光女翼档位+悬浮+昼夜双档续航（UpdateAccessory 逐帧调用）</summary>
        internal void ApplyFlightStats() {
            //不覆盖玩家已有的更高档翅膀，只保底光女翼手感
            Player.wingsLogic = Math.Max(Player.wingsLogic, WingSlotEmpress);
            Player.wingTimeMax = Math.Max(Player.wingTimeMax, Main.dayTime ? WingTimeDay : WingTimeNight);
            Player.noFallDmg = true;
            if (!Main.dayTime) {
                Player.moveSpeed += NightMoveSpeed;
            }
        }

        /// <summary>夜形态跑速上限强化，原版算完后叠乘</summary>
        public override void PostUpdateRunSpeeds() {
            if (!Equipped || Main.dayTime) {
                return;
            }
            Player.maxRunSpeed *= NightRunMult;
            Player.accRunSpeed *= NightRunMult;
        }

        /// <summary>夜形态自动闪避：冷却好即必闪，棱彩碎散演出（FreeDodge 仅 owner 本机跑）</summary>
        public override bool FreeDodge(Player.HurtInfo info) {
            if (!Equipped || Main.dayTime || DodgeCooldown > 0 || info.SourceDamage <= 0) {
                return false;
            }
            DodgeCooldown = DodgeCooldownFrames;
            Player.GivePlayerImmuneState(DodgeImmuneFrames, true);

            SoundEngine.PlaySound(SoundID.Item162 with { Volume = 0.6f, Pitch = 0.45f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item177 with { Volume = 0.4f, Pitch = 0.2f }, Player.Center);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 16; i++) {
                    float hue = 0.55f + i / 16f * 0.35f;
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Player.Center,
                        VaultUtils.RandVr(3f, 9f), EmpressMotion.Prism(hue, 0.7f),
                        Main.rand.NextFloat(0.8f, 1.3f))?.Configure(22, hue);
                }
                PRTLoader.NewParticle<PRT_EmpressRipple>(Player.Center, Vector2.Zero,
                    Color.White, 0.9f)?.Configure(20, 0.68f);
            }
            return true;
        }

        /// <summary>昼夜切换沿检测（死亡期间此钩不跑，由 UpdateDead 兜住 lastDayTime）</summary>
        public override void PostUpdateEquips() {
            if (!dayInitialized) {
                dayInitialized = true;
                lastDayTime = Main.dayTime;
                return;
            }
            if (lastDayTime == Main.dayTime) {
                return;
            }
            lastDayTime = Main.dayTime;
            if (Equipped) {
                TriggerInterferenceBurst();
            }
        }

        /// <summary>
        /// 死亡期间跟踪昼夜位，防复活瞬间误爆；光径随攻击中断散去。
        /// 死亡期间 ResetEffects 不跑（原版 dead 分支早退），冷却在这里显式递减照常流逝，
        /// 对齐血雾之瞳纪律；冻结制只针对卸装，死亡不冻结
        /// </summary>
        public override void UpdateDead() {
            lastDayTime = Main.dayTime;
            if (DodgeCooldown > 0) {
                DodgeCooldown--;
            }
            for (int i = 0; i < Trails.Count; i++) {
                Trails[i].Alive = false;
            }
            AdvanceTrailFade();
            WingSpread = MathHelper.Lerp(WingSpread, 0f, 0.1f);
            if (WingSpread > 0.02f || Trails.Count > 0) {
                PresenceStamp.Stamp();
            }
        }

        /// <summary>
        /// 昼夜切换全屏干涉爆发：伤害弹幕只在 owner 端生成一次（防跨端重复），
        /// 弹幕清除由该弹幕的服务端实例执行，演出各端本地自播
        /// </summary>
        private void TriggerInterferenceBurst() {
            bool toDay = Main.dayTime;

            //owner 端：范围伤害弹幕（服务端实例负责清弹）
            if (Player.whoAmI == Main.myPlayer) {
                int dmg = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(DawnBurstBaseDamage);
                Projectile.NewProjectile(BurstSource(), Player.Center, Vector2.Zero,
                    ModContent.ProjectileType<InterferenceDawnBurst>(), dmg, 9f, Player.whoAmI,
                    toDay ? 0.12f : 0.68f);
            }

            //各端演出：屏幕棱彩折射一闪（复用光女屏幕后效）+蝶群+震屏
            if (VaultUtils.isServer) {
                return;
            }
            EmpressScreenFX.PushPrismPulse(Player.Center, 0.9f, 44);
            SoundEngine.PlaySound(SoundID.Item161 with { Volume = 0.85f, Pitch = toDay ? 0.3f : -0.15f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item165 with { Volume = 0.6f, Pitch = toDay ? 0.42f : -0.3f }, Player.Center);
            EmpressMotion.Shake(Player.Center, 5f, 24);
            for (int i = 0; i < 18; i++) {
                float hue = (toDay ? 0.08f : 0.58f) + i / 18f * 0.3f;
                PRTLoader.NewParticle<PRT_EmpressButterfly>(Player.Center + Main.rand.NextVector2Circular(40f, 40f),
                    VaultUtils.RandVr(2f, 7f), EmpressMotion.Prism(hue, 0.68f),
                    Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(50, 90), hue);
            }
            for (int i = 0; i < 26; i++) {
                float hue = i / 26f;
                PRTLoader.NewParticle<PRT_EmpressSpark>(Player.Center,
                    VaultUtils.RandVr(5f, 14f), EmpressMotion.FormPrism(hue, toDay ? 1f : 0f),
                    Main.rand.NextFloat(0.9f, 1.5f))?.Configure(28, hue);
            }
        }

        private Terraria.DataStructures.IEntitySource BurstSource()
            => SourceItem != null ? Player.GetSource_Accessory(SourceItem) : Player.GetSource_Misc("WingsOfInterference");

        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                //服务端不养视觉轨迹，仅保证渐隐表清空
                if (Trails.Count > 0) {
                    Trails.Clear();
                }
                return;
            }

            UpdateWingVisual();
            AdvanceBurstLocks();

            bool visualOn = Equipped && !HideVisual;
            bool isOwner = Player.whoAmI == Main.myPlayer;

            //光径：昼形态采样（owner 逐帧续线+事件注册+4帧补扫；旁观副本8帧低频，不做交点检测），
            //夜形态与卸装只余像渐隐
            if (visualOn && Main.dayTime && !Player.dead) {
                if (isOwner) {
                    ExtendTrails(1);
                    DrainTrailCandidates();
                    if (++trailSampleTimer >= 4) {
                        trailSampleTimer = 0;
                        ScanForNewTrails();
                    }
                }
                else if (++trailSampleTimer >= 8) {
                    ExtendTrails(trailSampleTimer);
                    ScanForNewTrails();
                    trailSampleTimer = 0;
                }
            }
            else {
                for (int i = 0; i < Trails.Count; i++) {
                    Trails[i].Alive = false;
                }
                pendingTrailProj.Clear();
                trailSampleTimer = 0;
            }
            AdvanceTrailFade();

            //在场帧戳：渲染层空场早退的依据
            if (WingSpread > 0.02f || Trails.Count > 0) {
                PresenceStamp.Stamp();
            }

            //交点引爆：伤害决策只在 owner 端
            if (Equipped && Main.dayTime && !Player.dead && isOwner) {
                DetectIntersections();
            }

            //夜形态读弹辅助：极光残迹（owner 本机的提示层，敌弹计数门空场跳过）
            if (visualOn && !Main.dayTime && !Player.dead && isOwner && AnyHostileProjectiles()) {
                EmitAuroraTraces();
            }
        }

        /// <summary>翼展开/扑动/昼夜混合推进（纯视觉，各端本地）</summary>
        private void UpdateWingVisual() {
            float spreadTarget = 0f;
            bool flying = false;
            if (Equipped && !HideVisual && !Player.dead) {
                float speed = Player.velocity.Length();
                flying = Player.velocity.Y != 0f;
                spreadTarget = MathHelper.Clamp(0.35f + speed * 0.055f + (flying ? 0.25f : 0f), 0.35f, 1f);
            }
            WingSpread = MathHelper.Lerp(WingSpread, spreadTarget, 0.085f);
            WingFlap += 0.055f + (flying ? 0.15f : 0f) + WingSpread * 0.03f;
            DayBlend = MathHelper.Lerp(DayBlend, Main.dayTime ? 1f : 0f, 0.025f);

            if (WingSpread > 0.05f) {
                Color glow = EmpressMotion.FormPrism((WingFlap * 0.02f) % 1f, DayBlend, 0.6f);
                Lighting.AddLight(Player.Center, glow.ToVector3() * WingSpread * 0.42f);
            }
        }

        /// <summary>
        /// 已注册光径的等距续线（whoAmI+identity+type 三元验证，防槽位复用串线）。
        /// frames=距上次采样的帧数：旁观副本低频路径传 &gt;1，瞬移断线阈值按帧数同倍放宽
        /// </summary>
        private void ExtendTrails(int frames) {
            float breakDist = TeleportBreak * Math.Max(frames, 1);
            foreach (InterferenceTrail trail in Trails) {
                trail.FreshPoints = 0;
                if (!trail.Alive) {
                    continue;
                }
                Projectile proj = Main.projectile[trail.ProjWhoAmI];
                if (!proj.active || proj.identity != trail.ProjIdentity || proj.type != trail.ProjType
                    || proj.owner != Player.whoAmI) {
                    trail.Alive = false;
                    continue;
                }

                //等距补插采样（弹幕单帧位移跨多个步长时保持点距恒定）
                Vector2 anchor = proj.Center;
                Vector2 last = trail.Points[^1];
                float move = Vector2.Distance(last, anchor);
                if (move > breakDist) {
                    trail.Alive = false;
                    continue;
                }
                while (move >= SampleStep) {
                    Vector2 dir = (anchor - last) / move;
                    last += dir * SampleStep;
                    trail.Points.Add(last);
                    trail.FreshPoints++;
                    move = Vector2.Distance(last, anchor);
                }
                if (trail.Points.Count > MaxTrailPoints) {
                    trail.Points.RemoveRange(0, trail.Points.Count - MaxTrailPoints);
                    trail.RecalcBounds();
                }
                else if (trail.FreshPoints > 0) {
                    for (int i = trail.Points.Count - trail.FreshPoints; i < trail.Points.Count; i++) {
                        trail.Min = Vector2.Min(trail.Min, trail.Points[i]);
                        trail.Max = Vector2.Max(trail.Max, trail.Points[i]);
                    }
                }
            }
        }

        /// <summary>OnSpawn 事件入口：本机出生的攻击弹幕推入候选队列（容量兜底防高射速爆表）</summary>
        internal void QueueTrailCandidate(int projIndex) {
            if (pendingTrailProj.Count < 16) {
                pendingTrailProj.Add(projIndex);
            }
        }

        /// <summary>排空候选队列并注册（owner 端逐帧，注册延迟为零）</summary>
        private void DrainTrailCandidates() {
            for (int i = 0; i < pendingTrailProj.Count; i++) {
                TryRegisterTrail(pendingTrailProj[i]);
            }
            pendingTrailProj.Clear();
        }

        /// <summary>低频全表补扫：兜住不经本机 NewProjectile 出生的漏网弹（服务端代生成等）</summary>
        private void ScanForNewTrails() {
            if (Trails.Count >= MaxTrails) {
                return;
            }
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != Player.whoAmI) {
                    continue;
                }
                TryRegisterTrail(proj.whoAmI);
                if (Trails.Count >= MaxTrails) {
                    break;
                }
            }
        }

        /// <summary>候选校验+去重+建径（事件注册与补扫共用同一套过滤）</summary>
        private void TryRegisterTrail(int index) {
            if (index < 0 || index >= Main.maxProjectiles || Trails.Count >= MaxTrails) {
                return;
            }
            Projectile proj = Main.projectile[index];
            if (!proj.active || proj.owner != Player.whoAmI || !proj.friendly || proj.hostile
                || proj.damage <= 0 || proj.minion || proj.sentry
                || proj.velocity.LengthSquared() < 7f) {
                return;
            }
            //自产爆裂不再拉线，防递归织网
            if (proj.type == ModContent.ProjectileType<InterferencePrismBurst>()
                || proj.type == ModContent.ProjectileType<InterferenceDawnBurst>()) {
                return;
            }
            foreach (InterferenceTrail trail in Trails) {
                if (trail.Alive && trail.ProjWhoAmI == index && trail.ProjIdentity == proj.identity) {
                    return;
                }
            }

            InterferenceTrail fresh = new() {
                ProjWhoAmI = index,
                ProjIdentity = proj.identity,
                ProjType = proj.type,
                //identity 黄金比散列：各端色相一致
                Hue = proj.identity * 0.61803399f % 1f
            };
            fresh.Points.Add(proj.Center);
            fresh.Min = fresh.Max = proj.Center;
            Trails.Add(fresh);
        }

        /// <summary>渐隐推进：失活光径余像倒计时后移除</summary>
        private void AdvanceTrailFade() {
            for (int i = Trails.Count - 1; i >= 0; i--) {
                InterferenceTrail trail = Trails[i];
                if (trail.Alive) {
                    continue;
                }
                trail.LingerTimer++;
                if (trail.LingerTimer >= TrailLingerFrames || trail.Points.Count < 2) {
                    Trails.RemoveAt(i);
                }
            }
        }

        private void AdvanceBurstLocks() {
            for (int i = burstLockTimer.Count - 1; i >= 0; i--) {
                if (--burstLockTimer[i] <= 0) {
                    burstLockTimer.RemoveAt(i);
                    burstLockPos.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 交点检测（owner 端）：只测本帧新增段×其他活跃径，AABB 预筛+
        /// 位置冷却+每帧引爆上限三重节流
        /// </summary>
        private void DetectIntersections() {
            int burstsThisFrame = 0;
            for (int a = 0; a < Trails.Count && burstsThisFrame < MaxBurstsPerFrame; a++) {
                InterferenceTrail ta = Trails[a];
                if (!ta.Alive || ta.FreshPoints <= 0 || ta.Points.Count < 2) {
                    continue;
                }
                int freshStart = Math.Max(ta.Points.Count - ta.FreshPoints - 1, 0);

                for (int b = 0; b < Trails.Count && burstsThisFrame < MaxBurstsPerFrame; b++) {
                    if (b == a) {
                        continue;
                    }
                    InterferenceTrail tb = Trails[b];
                    if (!tb.Alive || tb.Points.Count < 2) {
                        continue;
                    }
                    //AABB 预筛（新增段范围 vs 整径包围盒）
                    if (ta.Max.X < tb.Min.X || ta.Min.X > tb.Max.X
                        || ta.Max.Y < tb.Min.Y || ta.Min.Y > tb.Max.Y) {
                        continue;
                    }

                    for (int i = freshStart; i < ta.Points.Count - 1 && burstsThisFrame < MaxBurstsPerFrame; i++) {
                        for (int j = 0; j < tb.Points.Count - 1; j++) {
                            if (!SegmentsIntersect(ta.Points[i], ta.Points[i + 1],
                                tb.Points[j], tb.Points[j + 1], out Vector2 hit)) {
                                continue;
                            }
                            if (IsBurstLocked(hit)) {
                                continue;
                            }
                            FirePrismBurst(hit, ta.Hue);
                            burstsThisFrame++;
                            break;
                        }
                    }
                }
            }
        }

        private bool IsBurstLocked(Vector2 pos) {
            float r2 = BurstLockRadius * BurstLockRadius;
            for (int i = 0; i < burstLockPos.Count; i++) {
                if (Vector2.DistanceSquared(burstLockPos[i], pos) < r2) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>交点引爆：owner 端生成棱镜爆裂弹幕（经原版同步链广播）</summary>
        private void FirePrismBurst(Vector2 pos, float hue) {
            burstLockPos.Add(pos);
            burstLockTimer.Add(BurstLockFrames);
            int dmg = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(BurstBaseDamage);
            Projectile.NewProjectile(BurstSource(), pos, Vector2.Zero,
                ModContent.ProjectileType<InterferencePrismBurst>(), dmg, 5f, Player.whoAmI,
                hue, BurstRadius);
        }

        /// <summary>2D 线段相交（叉积法），交点经参数插值返回</summary>
        private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out Vector2 hit) {
            hit = default;
            Vector2 r = p2 - p1;
            Vector2 s = q2 - q1;
            float rxs = r.X * s.Y - r.Y * s.X;
            if (Math.Abs(rxs) < 0.0001f) {
                return false;
            }
            Vector2 qp = q1 - p1;
            float t = (qp.X * s.Y - qp.Y * s.X) / rxs;
            float u = (qp.X * r.Y - qp.Y * r.X) / rxs;
            if (t < 0f || t > 1f || u < 0f || u > 1f) {
                return false;
            }
            hit = p1 + r * t;
            return true;
        }

        /// <summary>敌弹计数门：每8帧刷新一次敌对弹幕计数，空场时夜读弹层整段跳过</summary>
        private bool AnyHostileProjectiles() {
            if (--hostileGateTimer <= 0) {
                hostileGateTimer = 8;
                hostileProjCached = 0;
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.hostile && proj.damage > 0) {
                        hostileProjCached++;
                    }
                }
            }
            return hostileProjCached > 0;
        }

        /// <summary>夜形态读弹辅助：逼近的敌方弹幕沿途留下极光残迹（owner 本机提示层）</summary>
        private void EmitAuroraTraces() {
            const float WarnRange = 360f;
            float r2 = WarnRange * WarnRange;
            int emitted = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.hostile || proj.damage <= 0) {
                    continue;
                }
                if (Vector2.DistanceSquared(proj.Center, Player.Center) > r2) {
                    continue;
                }
                //逐弹错相节流：每3帧一粒
                if ((proj.whoAmI * 7 + (int)Main.GameUpdateCount) % 3 != 0) {
                    continue;
                }
                float hue = 0.52f + proj.whoAmI * 0.618034f % 1f * 0.3f;
                PRTLoader.NewParticle<PRT_EmpressSpark>(proj.Center, proj.velocity * 0.06f,
                    EmpressMotion.Prism(hue, 0.62f), Main.rand.NextFloat(0.45f, 0.7f))?.Configure(14, hue);
                if (++emitted >= 8) {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 光径注册的事件驱动入口：本机玩家昼形态下出生的攻击弹幕即时推入候选队列，
    /// 免去逐帧全表扫描（漏网的由低频补扫兜底）。OnSpawn 只在生成端触发，
    /// 远端旁观副本不走此路径，其光径由低频采样自行维护
    /// </summary>
    internal sealed class InterferenceTrailSpawnWatcher : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (VaultUtils.isServer || !Main.dayTime || projectile.owner != Main.myPlayer
                || !projectile.friendly || projectile.hostile || projectile.damage <= 0
                || projectile.minion || projectile.sentry) {
                return;
            }
            Player player = Main.player[projectile.owner];
            if (player?.active != true || player.dead
                || !player.TryGetModPlayer(out WingsOfInterferencePlayer mp)
                || !mp.Equipped || mp.HideVisual) {
                return;
            }
            mp.QueueTrailCandidate(projectile.whoAmI);
        }
    }
}

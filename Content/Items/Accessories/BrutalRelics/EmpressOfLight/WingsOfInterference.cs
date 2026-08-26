using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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
            //光女档掉落物约20~25金购价，按系列基准放大到约4~5倍
            Item.value = Item.buyPrice(1, 0, 0, 0);
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
        /// <summary>昼形态飞行时间（原版最强翼180帧，超模档）</summary>
        private const int WingTimeDay = 240;
        /// <summary>夜形态飞行时间</summary>
        private const int WingTimeNight = 320;
        /// <summary>夜形态移动速度加成</summary>
        private const float NightMoveSpeed = 0.32f;
        /// <summary>夜形态跑速/翼平飞上限乘数</summary>
        private const float NightRunMult = 1.18f;

        /// <summary>夜闪避冷却（帧）</summary>
        public const int DodgeCooldownFrames = 480;
        /// <summary>闪避成功后的无敌帧</summary>
        private const int DodgeImmuneFrames = 45;

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
        private const float BurstBaseDamage = 240f;
        /// <summary>爆裂判定半径(px)</summary>
        private const float BurstRadius = 150f;
        /// <summary>爆点位置冷却（帧）：同点近旁不复爆</summary>
        private const int BurstLockFrames = 30;
        private const float BurstLockRadius = 130f;
        /// <summary>每帧引爆上限（节流）</summary>
        private const int MaxBurstsPerFrame = 2;

        /// <summary>昼夜切换全屏爆发基伤</summary>
        private const float DawnBurstBaseDamage = 1350f;
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
        #endregion

        public override void ResetEffects() {
            Equipped = false;
            HideVisual = false;
            if (DodgeCooldown > 0) {
                DodgeCooldown--;
            }
        }

        /// <summary>飞行授予：光女翼档位+悬浮+超模续航（UpdateAccessory 逐帧调用）</summary>
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

        /// <summary>死亡期间跟踪昼夜位，防复活瞬间误爆；光径随攻击中断散去</summary>
        public override void UpdateDead() {
            lastDayTime = Main.dayTime;
            for (int i = 0; i < Trails.Count; i++) {
                Trails[i].Alive = false;
            }
            AdvanceTrailFade();
            WingSpread = MathHelper.Lerp(WingSpread, 0f, 0.1f);
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

            //光径：昼形态采样，夜形态与卸装只余像渐隐
            if (visualOn && Main.dayTime && !Player.dead) {
                SampleTrails();
            }
            else {
                for (int i = 0; i < Trails.Count; i++) {
                    Trails[i].Alive = false;
                }
            }
            AdvanceTrailFade();

            //交点引爆：伤害决策只在 owner 端
            if (Equipped && Main.dayTime && !Player.dead && Player.whoAmI == Main.myPlayer) {
                DetectIntersections();
            }

            //夜形态读弹辅助：极光残迹（owner 本机的提示层）
            if (visualOn && !Main.dayTime && !Player.dead && Player.whoAmI == Main.myPlayer) {
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

        /// <summary>光径采样：注册己方攻击弹幕并等距记录轨迹（各端本地自采）</summary>
        private void SampleTrails() {
            //1) 校验现存径的宿主弹幕（whoAmI+identity+type 三元验证，防槽位复用串线）
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
                if (move > TeleportBreak) {
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

            //2) 注册新弹幕
            if (Trails.Count >= MaxTrails) {
                return;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != Player.whoAmI || !proj.friendly || proj.hostile
                    || proj.damage <= 0 || proj.minion || proj.sentry
                    || proj.velocity.LengthSquared() < 7f) {
                    continue;
                }
                //自产爆裂不再拉线，防递归织网
                if (proj.type == ModContent.ProjectileType<InterferencePrismBurst>()
                    || proj.type == ModContent.ProjectileType<InterferenceDawnBurst>()) {
                    continue;
                }
                bool tracked = false;
                foreach (InterferenceTrail trail in Trails) {
                    if (trail.Alive && trail.ProjWhoAmI == i && trail.ProjIdentity == proj.identity) {
                        tracked = true;
                        break;
                    }
                }
                if (tracked) {
                    continue;
                }

                InterferenceTrail fresh = new() {
                    ProjWhoAmI = i,
                    ProjIdentity = proj.identity,
                    ProjType = proj.type,
                    //identity 黄金比散列：各端色相一致
                    Hue = proj.identity * 0.61803399f % 1f
                };
                fresh.Points.Add(proj.Center);
                fresh.Min = fresh.Max = proj.Center;
                Trails.Add(fresh);
                if (Trails.Count >= MaxTrails) {
                    break;
                }
            }
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

        /// <summary>夜形态读弹辅助：逼近的敌方弹幕沿途留下极光残迹（owner 本机提示层）</summary>
        private void EmitAuroraTraces() {
            const float WarnRange = 360f;
            float r2 = WarnRange * WarnRange;
            int emitted = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.hostile || proj.damage <= 0) {
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
}

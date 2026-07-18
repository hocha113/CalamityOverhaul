using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>速射枪管「过热红线」：持续开火积热，热量抬升攻速并把光束烧至红热白炽；
    /// 顶到红线进入过热喷射（攻速峰值+零蓝耗），随后强制冷却，循环往复</summary>
    internal sealed class RapidBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //过热红橙
        public override Color TintColor => new(255, 96, 42);

        /// <summary>热循环三段：积热→过热喷射→强制冷却</summary>
        internal enum HeatPhase : byte
        {
            Building,
            Venting,
            Cooling,
        }

        //═════ 可调参数 ═════
        private const float BaseAttackSpeedAdd = 0.15f;  //基础攻速（高射速身份底盘）
        private const float BaseDamageAdd = -0.18f;      //常驻伤害代价
        private const float BaseSpreadAdd = 0.25f;       //高射速散布代价
        private const float HeatAttackSpeedMax = 0.45f;  //积热攻速上限（heat×此值，满热+45%）
        private const float HeatPerShot = 0.06f;         //每次击发积热（约17发顶到红线）
        private const int HeatGraceFrames = 32;          //停火后热量开始散失的缓冲帧
        private const float HeatDecayPerFrame = 0.005f;  //停火散热速率（满槽约3.3s放空）
        private const int VentFrames = 230;              //过热喷射时长（约3.8s）
        private const float VentAttackSpeedAdd = 0.60f;  //喷射攻速峰值（合计+75%）
        private const float VentManaCostAdd = -1f;       //喷射期魔力消耗-100%
        private const int CoolFrames = 160;              //强制冷却时长（约2.7s）
        private const float CoolAttackSpeedAdd = -0.40f; //冷却攻速惩罚（合计-25%）
        private const float AlarmThreshold = 0.85f;      //红线警报起始热量
        private const int AlarmBeepInterval = 30;        //警报蜂鸣间隔帧
        internal const float SheathThreshold = 0.22f;    //光束热鞘/火渣的最低热量快照

        //═════ 热量状态（per-玩家：每个玩家槽位持有独立实例，参照守望/支架惯例） ═════
        private HeatPhase phase = HeatPhase.Building;
        private float heat;
        private int phaseTimer;
        private float tickCarry;
        private int sinceShot;
        private int alarmSoundTimer;
        private int ventSoundTimer;
        private int prevItemAnimation;
        private uint lastTick;

        //光束发射瞬间的热量快照（whoAmI→heat），仅客户端写入；消亡移除+定期兜底清理
        private readonly Dictionary<int, float> beamHeat = new();
        private readonly List<int> pruneScratch = new();
        private int pruneTimer;

        internal HeatPhase Phase => phase;
        internal float Heat01 => heat;
        /// <summary>冷却复位进度 0→1，非冷却期恒 0</summary>
        internal float CoolProgress => phase == HeatPhase.Cooling
            ? 1f - MathHelper.Clamp(phaseTimer / (float)CoolFrames, 0f, 1f) : 0f;
        internal bool AlarmActive => phase == HeatPhase.Venting
            || (phase == HeatPhase.Building && heat >= AlarmThreshold);

        /// <summary>黑体色温近似：暗红→炽橙→白炽，与 SHPCModRedline.fx 的 heatRamp 对齐</summary>
        internal static Color HeatColor(float t) {
            Color c = Color.Lerp(new Color(115, 20, 8), new Color(255, 115, 25), MathHelper.Clamp(t * 2f, 0f, 1f));
            return Color.Lerp(c, new Color(255, 238, 205), MathHelper.Clamp(t * 2f - 1f, 0f, 1f));
        }

        /// <summary>枪口世界坐标与瞄准方向（itemRotation 会被网络同步，远端可用）</summary>
        internal static Vector2 GetMuzzle(Player player, out Vector2 aimDir) {
            float aim = player.direction == 1 ? player.itemRotation : player.itemRotation + MathHelper.Pi;
            aimDir = aim.ToRotationVector2();
            return player.RotatedRelativePoint(player.MountedCenter, true) + aimDir * 46f;
        }

        /// <summary>当前装备的本改件实例，未装备 null</summary>
        internal static RapidBarrelModule GetOn(Player player) {
            if (player == null) {
                return null;
            }
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp == null) {
                return null;
            }
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                if (sp.GetModule(i)?.ModItem is RapidBarrelModule m) {
                    return m;
                }
            }
            return null;
        }

        /// <summary>光束发射瞬间的热量快照，未登记返回 -1</summary>
        internal float GetBeamHeat(int whoAmI) => beamHeat.TryGetValue(whoAmI, out float v) ? v : -1f;

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += BaseAttackSpeedAdd;
            ctx.DamageMul += BaseDamageAdd;
            ctx.SpreadMul += BaseSpreadAdd;
            //热循环动态注入
            switch (phase) {
                case HeatPhase.Building:
                    ctx.AttackSpeedMul += heat * HeatAttackSpeedMax;
                    break;
                case HeatPhase.Venting:
                    ctx.AttackSpeedMul += VentAttackSpeedAdd;
                    ctx.ManaCostMul += VentManaCostAdd;
                    break;
                case HeatPhase.Cooling:
                    ctx.AttackSpeedMul += CoolAttackSpeedAdd;
                    break;
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) {
                return;
            }
            //改件曾被卸下/预设切换：喷射与冷却一律折算成足额冷却，防止拆装改件跳过惩罚
            if (Main.GameUpdateCount - lastTick > 4) {
                if (phase != HeatPhase.Building) {
                    phase = HeatPhase.Cooling;
                    phaseTimer = CoolFrames;
                }
                heat = 0f;
                prevItemAnimation = 0;
                tickCarry = 0f;
            }
            lastTick = Main.GameUpdateCount;

            if (player.dead) {
                phase = HeatPhase.Building;
                heat = 0f;
                phaseTimer = 0;
                prevItemAnimation = 0;
                return;
            }

            int tick = TickUp(ref tickCarry);
            bool holding = player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID;

            //击发侦测：动画计数回跳的那一帧即开火帧（右键蓄力不积热），兼容激光持续压枪
            if (holding && player.ItemAnimationActive && player.altFunctionUse != 2
                && player.itemAnimation > prevItemAnimation) {
                OnShotFired(player);
            }
            prevItemAnimation = holding && player.ItemAnimationActive ? player.itemAnimation : 0;

            switch (phase) {
                case HeatPhase.Building:
                    sinceShot += tick;
                    if (heat > 0f && sinceShot > HeatGraceFrames) {
                        heat = MathF.Max(heat - HeatDecayPerFrame * tick, 0f);
                    }
                    //红线警报蜂鸣
                    if (heat >= AlarmThreshold) {
                        alarmSoundTimer -= tick;
                        if (alarmSoundTimer <= 0) {
                            alarmSoundTimer = AlarmBeepInterval;
                            if (Main.netMode != NetmodeID.Server) {
                                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.34f, Pitch = 0.85f }, player.Center);
                            }
                        }
                    }
                    else {
                        alarmSoundTimer = 0;
                    }
                    break;
                case HeatPhase.Venting:
                    phaseTimer -= tick;
                    //喷射期持续蒸汽嘶鸣（低频低量）
                    ventSoundTimer -= tick;
                    if (ventSoundTimer <= 0) {
                        ventSoundTimer = 26;
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.16f, Pitch = 0.4f }, player.Center);
                        }
                    }
                    if (phaseTimer <= 0) {
                        EnterCooling(player);
                    }
                    break;
                case HeatPhase.Cooling:
                    phaseTimer -= tick;
                    if (phaseTimer <= 0) {
                        BecomeReady(player);
                    }
                    break;
            }

            PruneBeamSnapshots(player, tick);

            //仪表弹幕保障：热量存在或处于喷射/冷却时挂载，仅拥有者端生成
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (heat <= 0.02f && phase == HeatPhase.Building) {
                return;
            }
            int gaugeType = ModContent.ProjectileType<SHPCRedlineGaugeProj>();
            if (player.ownedProjectileCounts[gaugeType] < 1) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Center, Vector2.Zero, gaugeType, 0, 0f, player.whoAmI);
            }
        }

        private void OnShotFired(Player player) {
            sinceShot = 0;
            if (phase == HeatPhase.Building) {
                heat = MathF.Min(heat + HeatPerShot, 1f);
                if (heat >= 1f) {
                    EnterVenting(player);
                }
            }
            //枪口灼热焰渣：热度越高越浓
            float intensity = phase == HeatPhase.Venting ? 1f : heat;
            if (intensity > 0.15f && Main.netMode != NetmodeID.Server) {
                Vector2 muzzle = GetMuzzle(player, out Vector2 aimDir);
                int count = 2 + (int)(intensity * 3f);
                for (int i = 0; i < count; i++) {
                    Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(2.5f, 6.5f);
                    PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(muzzle, vel,
                        HeatColor(intensity), Main.rand.NextFloat(0.5f, 0.95f))
                        .Configure(new Color(120, 25, 10), Main.rand.Next(14, 26), buoyant: false);
                }
            }
        }

        private void EnterVenting(Player player) {
            phase = HeatPhase.Venting;
            phaseTimer = VentFrames;
            ventSoundTimer = 20;
            heat = 1f;
            SHPCNaturalFx.Shake(3f);
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), new Color(255, 120, 40), "// REDLINE", true, false);
            }
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.25f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.5f }, player.Center);
            //破线瞬间：白热冲环 + 蒸汽爆
            Vector2 muzzle = GetMuzzle(player, out Vector2 aimDir);
            PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero,
                new Color(255, 150, 60, 0), 0.05f).Configure(0.05f, 0.45f, 18);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle + Main.rand.NextVector2Circular(8f, 8f),
                    aimDir.RotatedBy(Main.rand.NextFloat(-1.2f, 1.2f)) * Main.rand.NextFloat(1.5f, 4.5f),
                    new Color(255, 240, 225), Main.rand.NextFloat(0.4f, 0.75f))
                    .Configure(Main.rand.Next(22, 38), 0.6f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(muzzle,
                    Main.rand.NextVector2CircularEdge(3.5f, 3.5f),
                    HeatColor(1f), Main.rand.NextFloat(0.6f, 1.1f))
                    .Configure(new Color(150, 35, 15), Main.rand.Next(20, 34));
            }
        }

        private void EnterCooling(Player player) {
            phase = HeatPhase.Cooling;
            phaseTimer = CoolFrames;
            heat = 0f;
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            //淬火收声：汽液骤冷嘶鸣 + 余烟
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.45f, Pitch = -0.3f }, player.Center);
            Vector2 muzzle = GetMuzzle(player, out _);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.6f, -0.6f)),
                    new Color(185, 205, 225), Main.rand.NextFloat(0.35f, 0.6f))
                    .Configure(Main.rand.Next(26, 44), 0.45f, Main.rand.NextFloat(-0.03f, 0.03f));
            }
        }

        private void BecomeReady(Player player) {
            phase = HeatPhase.Building;
            phaseTimer = 0;
            heat = 0f;
            sinceShot = 0;
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            //复位就绪：清脆上膛
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.4f }, player.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                new Color(120, 190, 235, 0), 0.05f).Configure(0.05f, 0.28f, 14);
        }

        /// <summary>兜底清理快照字典：改件卸下/漏掉消亡回调时防泄漏</summary>
        private void PruneBeamSnapshots(Player player, int tick) {
            pruneTimer += tick;
            if (pruneTimer < 90 || beamHeat.Count == 0) {
                return;
            }
            pruneTimer = 0;
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            pruneScratch.Clear();
            foreach (int id in beamHeat.Keys) {
                Projectile p = Main.projectile[id];
                if (!p.active || p.type != beamType || p.owner != player.whoAmI) {
                    pruneScratch.Add(id);
                }
            }
            foreach (int id in pruneScratch) {
                beamHeat.Remove(id);
            }
        }

        //═════════════ 光束钩子 ═════════════

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            //快照与火渣均为客户端视觉/绘制数据，服务端不登记（消亡回调也只在客户端派发）
            if (Main.netMode == NetmodeID.Server || beam.IsDerived) {
                return;
            }
            int id = beam.Projectile.whoAmI;
            if (!beamHeat.TryGetValue(id, out float snap)) {
                //发射瞬间锁定热度：冷枪射出的光束不会在飞行途中变红
                snap = phase == HeatPhase.Venting ? 1f : heat;
                beamHeat[id] = snap;
            }
            if (snap < SheathThreshold) {
                return;
            }
            Lighting.AddLight(beam.Projectile.Center, HeatColor(snap).ToVector3() * 0.35f * snap);
            //灼热火渣尾：热度越高越密，喷射态光束几乎连成焰线
            int chance = snap >= 0.999f ? 3 : (int)MathHelper.Lerp(9f, 4f, snap);
            if (Main.rand.NextBool(chance)) {
                Vector2 perp = beam.FlightDirection.RotatedBy(MathHelper.PiOver2);
                PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(
                    beam.Projectile.Center - beam.FlightDirection * 12f + perp * Main.rand.NextFloat(-7f, 7f),
                    -beam.FlightDirection * Main.rand.NextFloat(1f, 2.6f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.Lerp(HeatColor(snap), Color.White, snap * 0.3f), Main.rand.NextFloat(0.45f, 0.9f))
                    .Configure(new Color(120, 25, 10), Main.rand.Next(16, 30), buoyant: false);
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) {
                return;
            }
            float snap = GetBeamHeat(beam.Projectile.whoAmI);
            if (snap < SheathThreshold) {
                return;
            }
            //红热命中：火渣飞溅，白炽弹附带灼铁嘶声
            int count = 3 + (int)(snap * 5f);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f + snap * 3f, 3f + snap * 3f);
                PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(target.Center + vel * 2f, vel,
                    Color.Lerp(HeatColor(snap), Color.White, snap * 0.25f), Main.rand.NextFloat(0.5f, 1.0f))
                    .Configure(new Color(120, 25, 10), Main.rand.Next(16, 28));
            }
            if (snap >= 0.9f && Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.22f, Pitch = 0.55f }, target.Center);
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            beamHeat.Remove(beam.Projectile.whoAmI);
        }

        //═════════════ 激光钩子 ═════════════
        //本改件占据枪管槽，现版本激光模式（均为枪管改件提供）无法与其共存；
        //仍按规范成对接入：若未来非枪管来源开启激光，积热由击发侦测天然覆盖（持续压枪即持续积热），
        //此处补上色温接管与命中反馈，喷射期零蓝耗对激光同样生效

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            float q = phase == HeatPhase.Venting ? 1f : heat;
            if (q < SheathThreshold) {
                return;
            }
            Color hot = HeatColor(q);
            laser.ThemeCore = Color.Lerp(laser.ThemeCore, Color.Lerp(hot, Color.White, 0.35f), q);
            laser.ThemeGlow = Color.Lerp(laser.ThemeGlow, hot, q);
            laser.ThemeAura = Color.Lerp(laser.ThemeAura, new Color(140, 22, 8), q);
            laser.ThemeParticleMain = Color.Lerp(laser.ThemeParticleMain, hot, q);
            laser.ThemeParticleEdge = Color.Lerp(laser.ThemeParticleEdge, new Color(150, 35, 15), q);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            float q = phase == HeatPhase.Venting ? 1f : heat;
            if (q < SheathThreshold || !Main.rand.NextBool(4)) {
                return;
            }
            PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(target.Center,
                Main.rand.NextVector2CircularEdge(3f, 3f), HeatColor(q), Main.rand.NextFloat(0.4f, 0.8f))
                .Configure(new Color(120, 25, 10), Main.rand.Next(14, 24));
        }
    }

    /// <summary>过热红线仪表：枪口热量弧表+色温辉光+热浪羽流（SHPCModRedline.fx），
    /// 并为炽热光束叠加色温热鞘；开火时锚定枪口，停火悬停头顶如烟囱泄压</summary>
    internal sealed class SHPCRedlineGaugeProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxSheaths = 10;      //每帧最多绘制热鞘的光束数
        private const int SheathPoints = 12;    //热鞘 Trail 顶点数
        private const int SheathStride = 2;     //oldPos 取样步长

        private float smoothAim = -MathHelper.PiOver2;
        private float drawRotation = -MathHelper.PiOver2;
        private float anchorBlend;
        private float displayHeat;
        private float ventVis;
        private float alarmVis;
        private float coolVis;
        private float fade;
        private float steamTimer;
        private float shimmerTimer;

        //热鞘 Trail 池（Coral 礁线同款复用法）
        private readonly List<Trail> sheathTrails = new();
        private readonly List<Vector2[]> sheathSegments = new();
        private float sheathWidth;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 8;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            RapidBarrelModule module = owner != null && owner.active && !owner.dead
                ? RapidBarrelModule.GetOn(owner) : null;
            if (module == null) {
                Projectile.Kill();
                return;
            }
            //完全冷透且场上已无本主光束（热鞘绘制的宿主）才退场；计数各端一致，避免服务端提前裁决
            bool idle = module.Phase == RapidBarrelModule.HeatPhase.Building && module.Heat01 <= 0.01f;
            if (idle && fade < 0.04f
                && owner.ownedProjectileCounts[ModContent.ProjectileType<CyberTraceBeamProj>()] <= 0) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 8;

            float ts = TimeGear.TimeScale;
            bool holding = owner.HeldItem != null && owner.HeldItem.type == SHPCOverride.ID;
            bool firing = holding && owner.ItemAnimationActive && owner.altFunctionUse != 2;

            //锚点：开火锚枪口随瞄准，停火悬停头顶转为烟囱朝上
            if (firing) {
                float aim = owner.direction == 1 ? owner.itemRotation : owner.itemRotation + MathHelper.Pi;
                smoothAim = smoothAim.AngleLerp(aim, 0.45f);
            }
            anchorBlend = MathHelper.Lerp(anchorBlend, firing ? 1f : 0f, firing ? 0.3f : 0.1f);
            drawRotation = drawRotation.AngleLerp(firing ? smoothAim : -MathHelper.PiOver2, 0.22f);

            Vector2 muzzle = owner.RotatedRelativePoint(owner.MountedCenter, true)
                + smoothAim.ToRotationVector2() * 46f;
            Vector2 hover = owner.Center + new Vector2(0f, -56f + owner.gfxOffY);
            Projectile.Center = Vector2.Lerp(hover, muzzle, anchorBlend);

            //显示量平滑
            displayHeat = MathHelper.Lerp(displayHeat, module.Heat01, 0.2f);
            ventVis = MathHelper.Lerp(ventVis, module.Phase == RapidBarrelModule.HeatPhase.Venting ? 1f : 0f, 0.15f);
            coolVis = MathHelper.Lerp(coolVis, module.CoolProgress, 0.2f);
            alarmVis = MathHelper.Lerp(alarmVis, module.AlarmActive ? 1f : 0f, 0.2f);
            fade = MathHelper.Lerp(fade, idle ? 0f : 1f, idle ? 0.12f : 0.2f);

            SpawnStateParticles(module, ts);

            Color lightCol = RapidBarrelModule.HeatColor(MathF.Max(displayHeat, ventVis));
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * fade * (0.2f + displayHeat * 0.4f + ventVis * 0.4f));
        }

        /// <summary>喷射蒸汽刀/冷却凝雾/高热蒸腾的持续粒子</summary>
        private void SpawnStateParticles(RapidBarrelModule module, float ts) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            Vector2 dir = drawRotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            if (module.Phase == RapidBarrelModule.HeatPhase.Venting) {
                //垂直枪管的双侧泄压蒸汽 + 前向火渣
                steamTimer += ts;
                if (steamTimer >= 3f) {
                    steamTimer = 0f;
                    for (int side = -1; side <= 1; side += 2) {
                        PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + perp * side * 7f,
                            perp * side * Main.rand.NextFloat(2.4f, 4.6f) + dir * Main.rand.NextFloat(-0.4f, 0.9f),
                            new Color(255, 242, 228), Main.rand.NextFloat(0.32f, 0.6f))
                            .Configure(Main.rand.Next(18, 32), 0.55f, Main.rand.NextFloat(-0.05f, 0.05f));
                    }
                    if (Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(Projectile.Center + dir * 6f,
                            dir * Main.rand.NextFloat(2f, 5f) + perp * Main.rand.NextFloat(-1.2f, 1.2f),
                            RapidBarrelModule.HeatColor(1f), Main.rand.NextFloat(0.5f, 0.9f))
                            .Configure(new Color(150, 35, 15), Main.rand.Next(16, 28), buoyant: false);
                    }
                }
            }
            else if (module.Phase == RapidBarrelModule.HeatPhase.Cooling) {
                //冷却期淡蓝凝雾缓缓上飘
                steamTimer += ts;
                if (steamTimer >= 7f) {
                    steamTimer = 0f;
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.4f, -0.5f)),
                        new Color(175, 200, 225), Main.rand.NextFloat(0.28f, 0.5f))
                        .Configure(Main.rand.Next(24, 40), 0.4f, Main.rand.NextFloat(-0.03f, 0.03f));
                }
            }
            else if (displayHeat > 0.55f) {
                //高热蒸腾：偶发火渣自枪口热浮
                shimmerTimer += ts;
                if (shimmerTimer >= 9f) {
                    shimmerTimer = 0f;
                    PRTLoader.NewParticle<PRT_SHPCRedlineCinder>(
                        Projectile.Center + dir * Main.rand.NextFloat(-4f, 10f) + perp * Main.rand.NextFloat(-5f, 5f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.2f, -0.3f)),
                        RapidBarrelModule.HeatColor(displayHeat), Main.rand.NextFloat(0.35f, 0.7f))
                        .Configure(new Color(120, 25, 10), Main.rand.Next(18, 32));
                }
            }
        }

        //═════ 热鞘：为炽热光束叠加色温渐变外皮（复用 CyberTraceBeam.fx，Obsidian 同款用法） ═════

        private float SheathWidthFunction(float progress) {
            float taper = 1f - MathHelper.Clamp(progress, 0f, 1f);
            return sheathWidth * (0.35f + 0.65f * taper);
        }

        private Color SheathColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            Player owner = Main.player[Projectile.owner];
            RapidBarrelModule module = owner != null && owner.active
                ? RapidBarrelModule.GetOn(owner) : null;
            if (module == null) {
                return;
            }
            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || noise == null) {
                return;
            }

            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            bool begun = false;
            int drawn = 0;

            for (int i = 0; i < Main.maxProjectiles && drawn < MaxSheaths; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != Projectile.owner || proj.type != beamType) {
                    continue;
                }
                float snap = module.GetBeamHeat(proj.whoAmI);
                if (snap < RapidBarrelModule.SheathThreshold) {
                    continue;
                }
                if (proj.oldPos == null || proj.oldPos.Length < SheathPoints * SheathStride) {
                    continue;
                }

                //顶点池扩容与填充：头部为当前位置，oldPos 隔点取样拉出热鞘段
                while (sheathSegments.Count <= drawn) {
                    Vector2[] seg = new Vector2[SheathPoints];
                    sheathSegments.Add(seg);
                    sheathTrails.Add(new Trail(seg, SheathWidthFunction, SheathColorFunction));
                }
                Vector2[] pts = sheathSegments[drawn];
                Vector2 half = proj.Size * 0.5f;
                pts[0] = proj.Center;
                for (int s = 1; s < SheathPoints; s++) {
                    Vector2 raw = proj.oldPos[s * SheathStride];
                    pts[s] = raw == Vector2.Zero ? pts[s - 1] : raw + half;
                }
                sheathTrails[drawn].TrailPositions = pts;

                //色温注入：快照越高越白炽
                float glowT = MathF.Pow(snap, 1.2f);
                Color core = Color.Lerp(RapidBarrelModule.HeatColor(snap), Color.White, snap * 0.3f);
                Color glow = RapidBarrelModule.HeatColor(snap * 0.72f);
                sheathWidth = MathHelper.Lerp(12f, 26f, snap);

                shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.045f);
                shader.Parameters["fadeAlpha"]?.SetValue(0.62f * glowT);
                shader.Parameters["coreColor"]?.SetValue(core.ToVector3());
                shader.Parameters["glowColor"]?.SetValue(glow.ToVector3());
                shader.Parameters["auraColor"]?.SetValue(new Vector3(0.47f, 0.07f, 0.02f));
                shader.Parameters["uNoiseTex"]?.SetValue(noise);
                shader.Parameters["overdriveAmount"]?.SetValue(0f);
                shader.Parameters["glitchBurst"]?.SetValue(0f);
                shader.Parameters["odCoreColor"]?.SetValue(core.ToVector3());
                shader.Parameters["odGlowColor"]?.SetValue(glow.ToVector3());
                shader.Parameters["odAuraColor"]?.SetValue(new Vector3(0.47f, 0.07f, 0.02f));

                if (!begun) {
                    device.BlendState = BlendState.Additive;
                    begun = true;
                }
                sheathTrails[drawn].DrawTrail(shader);
                drawn++;
            }

            if (begun) {
                device.BlendState = BlendState.AlphaBlend;
            }
        }

        //═════ 仪表本体：SHPCModRedline.fx ═════

        public override bool PreDraw(ref Color lightColor) {
            if (fade < 0.02f) {
                return false;
            }
            Effect shader = EffectLoader.SHPCModRedline?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return false;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["heatRatio"]?.SetValue(MathHelper.Clamp(displayHeat, 0f, 1f));
            shader.Parameters["ventFlash"]?.SetValue(ventVis);
            shader.Parameters["coolProgress"]?.SetValue(coolVis);
            shader.Parameters["alarmBlink"]?.SetValue(alarmVis);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                drawRotation, canvas.Size() * 0.5f,
                new Vector2(168f, 168f), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fade < 0.02f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            //枪口余温光核：色温随热度，喷射时白炽鼓胀
            float intensity = MathF.Max(displayHeat, ventVis);
            if (intensity < 0.05f && coolVis < 0.05f) {
                return;
            }
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.14f);
            Color hot = RapidBarrelModule.HeatColor(intensity);
            Color inner = Color.Lerp(hot, new Color(150, 190, 225), coolVis * 0.85f) with { A = 0 };
            Color outer = Color.Lerp(new Color(140, 25, 8), new Color(60, 85, 115), coolVis * 0.85f) with { A = 0 };
            float scale = (0.32f + intensity * 0.3f + ventVis * 0.22f) * pulse;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screenPos,
                inner * (fade * (0.35f + intensity * 0.45f)),
                outer * (fade * 0.25f), scale, 0f, 3);
        }
    }
}

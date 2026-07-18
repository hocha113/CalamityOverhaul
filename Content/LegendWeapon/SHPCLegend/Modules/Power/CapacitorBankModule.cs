using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>储能阵列：蓄力逐格点亮身侧电容排，球发射后电容架驻留原地逐格放电，向飞行中的球射供能电弧泵伤害与爆炸半径</summary>
    internal sealed class CapacitorBankModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //储能黄绿
        public override Color TintColor => new(200, 255, 80);

        /// <summary>当前电容架弹幕索引，仅 owner 端使用</summary>
        private int bankIndex = -1;

        public override void Apply(ref ShootContext ctx) {
            ctx.ChargeTimeMul += -0.2f;
            ctx.OrbSpeedMul += -0.12f;
            ctx.ManaCostMul += 0.35f;
        }

        public override void OnOrbCharging(CyberChargeOrbProj orb, Player owner) {
            //生成结算只在拥有者端做，NewProjectile 自行同步到其他端
            if (orb.Projectile.owner != Main.myPlayer) return;
            int bankType = ModContent.ProjectileType<SHPCCapacitorBankProj>();

            //已有正在服务本球的电容架：O(1) 快路径
            if (bankIndex >= 0 && bankIndex < Main.maxProjectiles) {
                Projectile p = Main.projectile[bankIndex];
                if (p.active && p.type == bankType && p.owner == orb.Projectile.owner
                    && p.ModProjectile is SHPCCapacitorBankProj fast && fast.IsServing(orb)) {
                    return;
                }
            }
            //兜底扫描：模块实例重建后与在场电容架重新挂钩，避免重复生成
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == bankType && p.owner == orb.Projectile.owner
                    && p.ModProjectile is SHPCCapacitorBankProj bank && bank.IsServing(orb)) {
                    bankIndex = i;
                    return;
                }
            }
            bankIndex = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                owner.Center, Vector2.Zero, bankType, 0, 0f, orb.Projectile.owner,
                ai0: orb.Projectile.whoAmI);
        }
    }

    /// <summary>
    /// 电容架弹幕：蓄力期跟随玩家头顶逐格灌注点亮；球发射后驻留原地进入放电序列，
    /// 按固定间隔逐格打出供能电弧，电弧包头送达球体时追加伤害与爆炸半径；
    /// 球消亡后余格泄压散场。SHPCModCapacitorBank.fx（CapacitorCell + FeedArc 双 technique）
    /// </summary>
    internal sealed class SHPCCapacitorBankProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 常量

        /// <summary>电容格数</summary>
        private const int MaxCells = 6;
        /// <summary>放电间隔（帧）</summary>
        private const int PulseInterval = 12;
        /// <summary>电弧包头送达耗时（帧）</summary>
        private const int PulseTravelFrames = 8;
        /// <summary>电弧送达后残辉帧数</summary>
        private const int ArcFadeFrames = 10;
        /// <summary>每次送达为球追加的伤害倍率（基于发射瞬间基准伤害加算）</summary>
        private const float DamagePerPulse = 0.12f;
        /// <summary>每次送达追加的爆炸半径倍率</summary>
        private const float RadiusPerPulse = 0.06f;
        /// <summary>泄压节拍（帧/格）</summary>
        private const int VentInterval = 6;
        /// <summary>电容格绘制尺寸（像素）</summary>
        private const float CellW = 22f, CellH = 36f;
        /// <summary>格间距（像素）</summary>
        private const float CellSpacing = 27f;
        /// <summary>电弧折线顶点数</summary>
        private const int ArcPointCount = 14;

        #endregion

        #region 颜色

        private static readonly Color BankCore = new(240, 255, 200);
        private static readonly Color BankGlow = new(170, 240, 60);
        private static readonly Color BankAura = new(60, 110, 25);

        #endregion

        #region 状态

        private enum BankState
        {
            Charging = 0,   //跟随玩家，随球蓄力灌注
            Discharging = 1,//球已发射，驻留原地逐格放电
            Venting = 2,    //球已消亡/放电完毕，余格泄压并淡出
        }

        private BankState state;
        /// <summary>各格灌注量 0~1</summary>
        private readonly float[] cellFill = new float[MaxCells];
        /// <summary>各格闪光量，每帧衰减</summary>
        private readonly float[] cellFlash = new float[MaxCells];
        /// <summary>已完整点亮格数（灌注视觉的整数化）</summary>
        private int litCount;
        /// <summary>发射瞬间捕获的可放电格数</summary>
        private int litAtLaunch;
        /// <summary>下一个待放电格下标（FIFO）</summary>
        private int dischargeCursor;
        /// <summary>已送达脉冲数</summary>
        private int delivered;
        private int dischargeTimer;
        private int ventTimer;
        /// <summary>发射瞬间捕获的球基准伤害（owner 端）</summary>
        private int baseOrbDamage;
        /// <summary>发射瞬间捕获的球基准爆炸半径倍率（owner 端）</summary>
        private float baseRadiusMul;
        /// <summary>整架淡入淡出</summary>
        private float bankFade;
        /// <summary>阵列锚点（蓄力期平滑跟随，发射后凍结驻留）</summary>
        private Vector2 anchor;
        private bool anchorInit;
        /// <summary>满架就绪提示音是否已播放</summary>
        private bool readyPinged;
        /// <summary>蓄力期连续找不到球的帧数，吸收远端弹幕包乱序的空窗</summary>
        private int orbMissFrames;

        /// <summary>在途/残辉电弧</summary>
        private readonly List<FeedArc> arcs = new();
        /// <summary>电弧条带网格，逐条复用（顶点数固定）</summary>
        private Trail arcTrail;

        /// <summary>链接的充能球 whoAmI（生成端索引，远端经 ResolveOrb 校验兜底）</summary>
        private int OrbIndex => (int)Projectile.ai[0];

        /// <summary>
        /// 是否可承接蓄力球：处于蓄力态即可——同一玩家同时只有一颗蓄力球，
        /// 蓄力态的架体会在 ResolveOrb 中自动重挂新球；放电/泄压中的旧架不抢占配对。
        /// 不比对 OrbIndex：球快速取消重蓄可能换槽位，架体重挂发生在其 AI 帧，
        /// 若模块侧比对索引会在这一帧空窗内误开第二座架
        /// </summary>
        public bool IsServing(CyberChargeOrbProj orb) => state == BankState.Charging;

        /// <summary>供能电弧：start 固定于放电格，end 逐帧追踪球体；折跳偏移与直线基线分离，基线每帧重算保证弧头贴球</summary>
        private sealed class FeedArc
        {
            public Vector2 Start;
            public Vector2 End;
            public int Age;
            public float Seed;
            public bool Delivered;
            public readonly Vector2[] Points = new Vector2[ArcPointCount];
            private readonly float[] offsets = new float[ArcPointCount];

            public float Fade => Age <= PulseTravelFrames
                ? 1f
                : 1f - (Age - PulseTravelFrames) / (float)ArcFadeFrames;

            /// <summary>重掷法向折跳偏移（阶跃式抖动节拍调用）</summary>
            public void RerollOffsets() {
                for (int i = 0; i < ArcPointCount; i++) {
                    offsets[i] = Main.rand.NextFloat(-1f, 1f);
                }
            }

            /// <summary>按当前端点重建路径：两端钉死中段摆动</summary>
            public void RecomputePoints() {
                Vector2 dir = (End - Start).SafeNormalize(Vector2.UnitX);
                Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
                float len = Vector2.Distance(Start, End);
                float amp = MathHelper.Clamp(len * 0.06f, 6f, 18f);
                for (int i = 0; i < ArcPointCount; i++) {
                    float t = i / (float)(ArcPointCount - 1);
                    float swing = MathF.Sin(t * MathHelper.Pi);
                    Points[i] = Vector2.Lerp(Start, End, t) + normal * offsets[i] * amp * swing;
                }
            }
        }

        #endregion

        public override void SetStaticDefaults() {
            //与充能球同样豁免时停：球在时缓中继续飞行，供能节奏必须跟上
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 1800;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //锚点先于任何状态分支初始化，保证首帧即泄压时架体也在玩家头顶
            if (!anchorInit) {
                anchor = owner.Top + new Vector2(0f, -44f);
                anchorInit = true;
            }

            CyberChargeOrbProj orb = ResolveOrb(owner);

            switch (state) {
                case BankState.Charging:
                    AI_Charging(owner, orb);
                    break;
                case BankState.Discharging:
                    AI_Discharging(orb);
                    break;
                case BankState.Venting:
                    AI_Venting();
                    break;
            }

            //泄压态强制断链：防止球槽位被下一颗球复用后，残留在途电弧把旧基准伤害错误送达新球
            UpdateArcs(state == BankState.Venting ? null : orb);
            DecayFlash();

            //泄压完毕且电弧散尽才允许消亡
            if (state != BankState.Venting) {
                Projectile.timeLeft = 600;
            }

            if (bankFade > 0.05f) {
                Lighting.AddLight(anchor, BankGlow.ToVector3() * 0.10f * litCount * bankFade);
            }
        }

        /// <summary>
        /// 解析链接的充能球：先走生成端索引；仅蓄力阶段允许按 owner+type 扫描兜底并纠正 ai[0]
        /// （远端弹幕槽位可能错位），且只接受仍在蓄力的球——放电/泄压阶段严禁改挂新球，
        /// 否则泄压中的旧架会抢占新球的配对，导致模块不再为新球生成电容架
        /// </summary>
        private CyberChargeOrbProj ResolveOrb(Player owner) {
            int idx = OrbIndex;
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile p = Main.projectile[idx];
                if (p.active && p.owner == Projectile.owner
                    && p.ModProjectile is CyberChargeOrbProj direct) {
                    //放电期链接的球必须仍在飞行：槽位被下一颗蓄力球复用时视为失联，
                    //避免把旧基准伤害泵进新球
                    if (state == BankState.Discharging && direct.IsCharging) {
                        return null;
                    }
                    return direct;
                }
            }
            if (state != BankState.Charging) {
                return null;
            }
            int orbType = ModContent.ProjectileType<CyberChargeOrbProj>();
            if (owner.ownedProjectileCounts[orbType] <= 0) {
                return null;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == orbType
                    && p.ModProjectile is CyberChargeOrbProj candidate && candidate.IsCharging) {
                    Projectile.ai[0] = i;
                    return candidate;
                }
            }
            return null;
        }

        #region 蓄力阶段

        private void AI_Charging(Player owner, CyberChargeOrbProj orb) {
            if (orb == null) {
                //球被取消（蓄力不足释放等）：短暂宽限后泄压散场，吸收远端单帧空窗
                if (++orbMissFrames > 6) {
                    BeginVenting();
                }
                return;
            }
            orbMissFrames = 0;

            //锚点平滑跟随玩家头顶
            Vector2 target = owner.Top + new Vector2(0f, -44f);
            anchor = Vector2.Lerp(anchor, target, 0.25f);
            Projectile.Center = anchor;
            bankFade = MathHelper.Clamp(bankFade + 0.08f, 0f, 1f);

            //渐进灌注：前一格满后才灌下一格，ratio*MaxCells 的小数部分即当前格液面
            float ratio = orb.ChargeRatio;
            int prevLit = litCount;
            litCount = 0;
            for (int i = 0; i < MaxCells; i++) {
                float fillTarget = MathHelper.Clamp(ratio * MaxCells - i, 0f, 1f);
                //液面追赶目标，点亮瞬间干脆利落
                cellFill[i] = MathHelper.Lerp(cellFill[i], fillTarget, 0.3f);
                if (fillTarget >= 1f) {
                    cellFill[i] = 1f;
                }
                if (cellFill[i] >= 0.999f) {
                    litCount++;
                }
            }

            //新格点亮：闪光 + 上行电子音 + 顶端火花
            if (litCount > prevLit && Main.netMode != NetmodeID.Server) {
                int newIdx = litCount - 1;
                cellFlash[newIdx] = 1f;
                SoundEngine.PlaySound(SoundID.Item93 with {
                    Volume = 0.22f, Pitch = -0.2f + newIdx * 0.12f
                }, anchor);
                Vector2 top = CellPosition(newIdx) + new Vector2(0f, -CellH * 0.5f);
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_Spark>(top, Main.rand.NextVector2CircularEdge(2.5f, 2.5f) - Vector2.UnitY * 1.5f,
                        BankGlow, Main.rand.NextFloat(0.4f, 0.8f)).Configure(true, Main.rand.Next(8, 14));
                }
            }

            //满架就绪：一次性提示 + 顶端持续电光由着色器承担
            if (litCount >= MaxCells && !readyPinged) {
                readyPinged = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.3f, Pitch = 0.8f }, anchor);
                }
            }

            //球转入飞行：捕获放电基准，架体驻留原地
            if (!orb.IsCharging) {
                litAtLaunch = litCount;
                dischargeCursor = 0;
                delivered = 0;
                dischargeTimer = 0;
                baseOrbDamage = orb.Projectile.damage;
                baseRadiusMul = orb.ExplosionRadiusMul;
                //未灌满的尾格直接失效，泄压阶段一起排掉
                state = litAtLaunch > 0 ? BankState.Discharging : BankState.Venting;
            }
        }

        #endregion

        #region 放电阶段

        private void AI_Discharging(CyberChargeOrbProj orb) {
            //球提前消亡（撞墙/命中/超时）：停止放电，余格泄压
            if (orb == null || orb.IsCharging) {
                BeginVenting();
                return;
            }

            //驻留原地：锚点不再跟随玩家，悬浮感由 CellPosition 的逐格 bob 承担
            Projectile.Center = anchor;

            dischargeTimer++;
            if (dischargeTimer >= PulseInterval && dischargeCursor < litAtLaunch) {
                dischargeTimer = 0;
                FireFeedArc(dischargeCursor, orb);
                dischargeCursor++;
            }

            //全部放完且电弧散尽：进入收尾
            if (dischargeCursor >= litAtLaunch && arcs.Count == 0) {
                BeginVenting();
            }
        }

        /// <summary>从指定格向球射出供能电弧，格位随之熄灭</summary>
        private void FireFeedArc(int cellIdx, CyberChargeOrbProj orb) {
            cellFill[cellIdx] = 0f;
            cellFlash[cellIdx] = 1f;
            litCount = Math.Max(litCount - 1, 0);

            FeedArc arc = new() {
                Start = CellPosition(cellIdx),
                End = orb.Projectile.Center,
                Seed = Projectile.whoAmI * 0.173f + cellIdx * 1.317f + dischargeCursor,
            };
            arc.RerollOffsets();
            arc.RecomputePoints();
            arcs.Add(arc);

            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.32f, Pitch = 0.35f }, arc.Start);
                for (int k = 0; k < 5; k++) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(arc.Start,
                        Main.rand.NextVector2CircularEdge(3f, 3f), BankCore,
                        Main.rand.NextFloat(0.5f, 1.0f)).Configure(BankGlow, Main.rand.Next(10, 18));
                }
            }
        }

        /// <summary>电弧推进与送达结算：包头抵达时为球泵能（owner 端），并在球处过载闪光</summary>
        private void UpdateArcs(CyberChargeOrbProj orb) {
            bool orbFlying = orb != null && !orb.IsCharging;
            for (int i = arcs.Count - 1; i >= 0; i--) {
                FeedArc arc = arcs[i];
                arc.Age++;
                //端点持续追踪飞行中的球，球没了则凍结在最后位置淡出
                if (orbFlying) {
                    arc.End = orb.Projectile.Center;
                }
                //折跳形态按节拍重掷，基线每帧跟随端点重算
                if (arc.Age % 3 == 0) {
                    arc.RerollOffsets();
                }
                arc.RecomputePoints();

                //送达瞬间
                if (!arc.Delivered && arc.Age >= PulseTravelFrames) {
                    arc.Delivered = true;
                    if (orbFlying) {
                        DeliverPulse(orb);
                    }
                }

                if (arc.Age >= PulseTravelFrames + ArcFadeFrames) {
                    arcs.RemoveAt(i);
                }
            }
        }

        private void DeliverPulse(CyberChargeOrbProj orb) {
            delivered++;
            //伤害与半径都基于发射瞬间基准值加算，避免乘算滚雪球
            if (Projectile.owner == Main.myPlayer) {
                orb.Projectile.damage = (int)(baseOrbDamage * (1f + delivered * DamagePerPulse));
                orb.ExplosionRadiusMul = baseRadiusMul + delivered * RadiusPerPulse;
            }

            if (Main.netMode != NetmodeID.Server) {
                Vector2 pos = orb.Projectile.Center;
                SoundEngine.PlaySound(SoundID.Item93 with {
                    Volume = 0.38f, Pitch = 0.25f + delivered * 0.08f
                }, pos);
                //球体过载闪光：脉冲环 + 火花爆跳
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero,
                    BankGlow with { A = 0 }, 0.05f).Configure(0.05f, 0.30f + delivered * 0.04f, 14);
                for (int k = 0; k < 7; k++) {
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2CircularEdge(4f, 4f) + orb.Projectile.velocity * 0.2f,
                        BankCore, Main.rand.NextFloat(0.5f, 1.0f)).Configure(true, Main.rand.Next(8, 16));
                }
            }
            //个人节奏反馈，只震拥有者自己的屏幕
            if (Projectile.owner == Main.myPlayer) {
                SHPCNaturalFx.Shake(1.1f);
            }
        }

        #endregion

        #region 泄压散场

        private void BeginVenting() {
            if (state == BankState.Venting) return;
            state = BankState.Venting;
            ventTimer = 0;
        }

        private void AI_Venting() {
            Projectile.Center = anchor;

            //按节拍逐格泄掉残余电量：无伤害的安全排气
            ventTimer++;
            if (ventTimer >= VentInterval) {
                ventTimer = 0;
                int idx = -1;
                for (int i = 0; i < MaxCells; i++) {
                    if (cellFill[i] > 0.05f) { idx = i; break; }
                }
                if (idx >= 0) {
                    cellFill[idx] = 0f;
                    cellFlash[idx] = 0.6f;
                    litCount = Math.Max(litCount - 1, 0);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.18f, Pitch = -0.6f }, anchor);
                        Vector2 pos = CellPosition(idx);
                        for (int k = 0; k < 4; k++) {
                            PRTLoader.NewParticle<PRT_Spark>(pos, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2.5f)),
                                BankAura, Main.rand.NextFloat(0.4f, 0.8f)).Configure(true, Main.rand.Next(10, 18));
                        }
                    }
                }
            }

            //所有格排空且电弧散尽后整架淡出
            bool cellsEmpty = true;
            for (int i = 0; i < MaxCells; i++) {
                if (cellFill[i] > 0.05f) { cellsEmpty = false; break; }
            }
            if (cellsEmpty && arcs.Count == 0) {
                bankFade -= 0.06f;
                if (bankFade <= 0f) {
                    Projectile.Kill();
                }
            }
        }

        #endregion

        private void DecayFlash() {
            for (int i = 0; i < MaxCells; i++) {
                cellFlash[i] *= 0.85f;
                if (cellFlash[i] < 0.01f) cellFlash[i] = 0f;
            }
        }

        /// <summary>第 i 格世界坐标：以锚点为中心水平排开，逐格相位错开的轻微浮动</summary>
        private Vector2 CellPosition(int i) {
            float x = (i - (MaxCells - 1) * 0.5f) * CellSpacing;
            float bob = MathF.Sin((float)Main.timeForVisualEffects * 0.08f + i * 0.7f) * 2f;
            return anchor + new Vector2(x, bob);
        }

        #region 绘制

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (bankFade < 0.02f) return;
            Effect shader = EffectLoader.SHPCModCapacitorBank?.Value;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            shader.CurrentTechnique = shader.Techniques["CapacitorCell"];
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["coreColor"]?.SetValue(BankCore.ToVector3());
            shader.Parameters["glowColor"]?.SetValue(BankGlow.ToVector3());
            shader.Parameters["auraColor"]?.SetValue(BankAura.ToVector3());

            Vector2 canvasSize = canvas.Size();
            for (int i = 0; i < MaxCells; i++) {
                shader.Parameters["fillLevel"]?.SetValue(cellFill[i]);
                shader.Parameters["cellFlash"]?.SetValue(cellFlash[i]);
                shader.Parameters["cellSeed"]?.SetValue(Projectile.whoAmI * 0.173f + i * 0.61f);
                shader.Parameters["fadeAlpha"]?.SetValue(bankFade);
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(canvas, CellPosition(i) - Main.screenPosition, null, Color.White,
                    0f, canvasSize * 0.5f, new Vector2(CellW, CellH) / canvasSize, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (arcs.Count == 0) return;
            Effect shader = EffectLoader.SHPCModCapacitorBank?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || noise == null) return;

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            shader.CurrentTechnique = shader.Techniques["FeedArc"];
            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["coreColor"]?.SetValue(BankCore.ToVector3());
            shader.Parameters["glowColor"]?.SetValue(BankGlow.ToVector3());
            shader.Parameters["auraColor"]?.SetValue(BankAura.ToVector3());

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            foreach (FeedArc arc in arcs) {
                float fade = MathHelper.Clamp(arc.Fade, 0f, 1f) * bankFade;
                if (fade < 0.02f) continue;
                shader.Parameters["fadeAlpha"]?.SetValue(fade);
                shader.Parameters["arcSeed"]?.SetValue(arc.Seed);
                shader.Parameters["pulseT"]?.SetValue(arc.Age / (float)PulseTravelFrames);

                //所有电弧共用一个 Trail：顶点数固定，逐条换 positions 重建网格
                arcTrail ??= new Trail(arc.Points, ArcWidth, ArcColor);
                arcTrail.TrailPositions = arc.Points;
                arcTrail.DrawTrail(shader);
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        private static float ArcWidth(float progress) {
            float midSwell = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return 7f + midSwell * 9f;
        }

        private static Color ArcColor(Vector2 _) => Color.White;

        #endregion

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            //残余火花轻收尾
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(anchor + Main.rand.NextVector2Circular(40f, 8f),
                    Main.rand.NextVector2Circular(2f, 1f), BankGlow,
                    Main.rand.NextFloat(0.4f, 0.7f)).Configure(true, Main.rand.Next(8, 14));
            }
        }
    }
}

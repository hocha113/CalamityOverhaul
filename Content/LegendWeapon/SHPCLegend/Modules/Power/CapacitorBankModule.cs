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
    /// <summary>储能阵列，蓄力点亮身侧电容排，发射后驻留逐格放电泵伤与爆径</summary>
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
            //仅 owner 端生成，NewProjectile 自同步
            if (orb.Projectile.owner != Main.myPlayer) return;
            int bankType = ModContent.ProjectileType<SHPCCapacitorBankProj>();

            //已有服务架，O(1)
            if (bankIndex >= 0 && bankIndex < Main.maxProjectiles) {
                Projectile p = Main.projectile[bankIndex];
                if (p.active && p.type == bankType && p.owner == orb.Projectile.owner
                    && p.ModProjectile is SHPCCapacitorBankProj fast && fast.IsServing(orb)) {
                    return;
                }
            }
            //兜底重挂，防重复生成
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

    /// <summary>电容架，蓄力跟头顶灌注，发射后驻留逐格供能电弧；SHPCModCapacitorBank.fx</summary>
    internal sealed class SHPCCapacitorBankProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        #region 常量

        /// <summary>电容格数</summary>
        private const int MaxCells = 6;
        /// <summary>放电间隔（帧）</summary>
        private const int PulseInterval = 12;
        /// <summary>电弧包头送达耗时（帧）</summary>
        private const int PulseTravelFrames = 8;
        /// <summary>电弧送达后残辉帧数</summary>
        private const int ArcFadeFrames = 10;
        /// <summary>每次送达伤害倍率，按发射基准加算</summary>
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
            Charging = 0,   //跟玩家灌注
            Discharging = 1,//驻留逐格放电
            Venting = 2,    //余格泄压淡出
        }

        private BankState state;
        /// <summary>各格灌注量 0~1</summary>
        private readonly float[] cellFill = new float[MaxCells];
        /// <summary>各格闪光量，每帧衰减</summary>
        private readonly float[] cellFlash = new float[MaxCells];
        /// <summary>已点亮格数</summary>
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
        /// <summary>阵列锚点，蓄力跟随，发射后冻结</summary>
        private Vector2 anchor;
        private bool anchorInit;
        /// <summary>满架就绪提示音是否已播放</summary>
        private bool readyPinged;
        /// <summary>蓄力丢球帧数，吸收远端乱序空窗</summary>
        private int orbMissFrames;

        /// <summary>在途/残辉电弧</summary>
        private readonly List<FeedArc> arcs = new();
        /// <summary>电弧 Trail，顶点数固定复用</summary>
        private Trail arcTrail;

        /// <summary>链接球 whoAmI，远端 ResolveOrb 兜底</summary>
        private int OrbIndex => (int)Projectile.ai[0];

        /// <summary>蓄力态可承接，不比对 OrbIndex，否则空窗会误开第二座架</summary>
        public bool IsServing(CyberChargeOrbProj orb) => state == BankState.Charging;

        /// <summary>供能电弧，start 钉格，end 追球，折跳与基线分离</summary>
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

            /// <summary>重掷法向折跳偏移</summary>
            public void RerollOffsets() {
                for (int i = 0; i < ArcPointCount; i++) {
                    offsets[i] = Main.rand.NextFloat(-1f, 1f);
                }
            }

            /// <summary>按端点重建，两端钉死中段摆</summary>
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
            //同球豁免时停，供能跟上时缓球
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

            //锚点先于状态分支，首帧泄压也在头顶
            if (!anchorInit) {
                anchor = owner.Top + new Vector2(0f, -44f);
                anchorInit = true;
            }

            //owner 端裁决，卸改件/换预设即泄压（模块数据不同步）
            if (Projectile.owner == Main.myPlayer && state != BankState.Venting
                && !SHPCModificationSystem.HasModule<CapacitorBankModule>(owner)) {
                BeginVenting();
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

            //泄压断链，防槽复用把旧基准泵进新球
            UpdateArcs(state == BankState.Venting ? null : orb);
            DecayFlash();

            //泄压完且电弧散尽才消亡
            if (state != BankState.Venting) {
                Projectile.timeLeft = 600;
            }

            if (bankFade > 0.05f) {
                Lighting.AddLight(anchor, BankGlow.ToVector3() * 0.10f * litCount * bankFade);
            }
        }

        /// <summary>解析链接球，蓄力期可 owner+type 兜底；放电/泄压禁改挂</summary>
        private CyberChargeOrbProj ResolveOrb(Player owner) {
            int idx = OrbIndex;
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile p = Main.projectile[idx];
                if (p.active && p.owner == Projectile.owner
                    && p.ModProjectile is CyberChargeOrbProj direct) {
                    //放电期球须仍在飞，槽复用视为失联
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
                //球取消，宽限后泄压，吸远端空窗
                if (++orbMissFrames > 6) {
                    BeginVenting();
                }
                return;
            }
            orbMissFrames = 0;

            //锚点跟头顶
            Vector2 target = owner.Top + new Vector2(0f, -44f);
            anchor = Vector2.Lerp(anchor, target, 0.25f);
            Projectile.Center = anchor;
            bankFade = MathHelper.Clamp(bankFade + 0.08f, 0f, 1f);

            //渐进灌注，前格满才灌下一格
            float ratio = orb.ChargeRatio;
            int prevLit = litCount;
            litCount = 0;
            for (int i = 0; i < MaxCells; i++) {
                float fillTarget = MathHelper.Clamp(ratio * MaxCells - i, 0f, 1f);
                //液面追目标
                cellFill[i] = MathHelper.Lerp(cellFill[i], fillTarget, 0.3f);
                if (fillTarget >= 1f) {
                    cellFill[i] = 1f;
                }
                if (cellFill[i] >= 0.999f) {
                    litCount++;
                }
            }

            //新格点亮，闪光+音+火花
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

            //满架就绪提示
            if (litCount >= MaxCells && !readyPinged) {
                readyPinged = true;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.3f, Pitch = 0.8f }, anchor);
                }
            }

            //球起飞，捕获基准并驻留
            if (!orb.IsCharging) {
                litAtLaunch = litCount;
                dischargeCursor = 0;
                delivered = 0;
                dischargeTimer = 0;
                baseOrbDamage = orb.Projectile.damage;
                baseRadiusMul = orb.ExplosionRadiusMul;
                //未满尾格失效，泄压时排掉
                state = litAtLaunch > 0 ? BankState.Discharging : BankState.Venting;
            }
        }

        #endregion

        #region 放电阶段

        private void AI_Discharging(CyberChargeOrbProj orb) {
            //球提前消亡，停放电转泄压
            if (orb == null || orb.IsCharging) {
                BeginVenting();
                return;
            }

            //驻留，bob 由 CellPosition
            Projectile.Center = anchor;

            dischargeTimer++;
            if (dischargeTimer >= PulseInterval && dischargeCursor < litAtLaunch) {
                dischargeTimer = 0;
                FireFeedArc(dischargeCursor, orb);
                dischargeCursor++;
            }

            //放完且电弧散尽，收尾
            if (dischargeCursor >= litAtLaunch && arcs.Count == 0) {
                BeginVenting();
            }
        }

        /// <summary>从指定格射供能电弧并熄格</summary>
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

        /// <summary>电弧推进，包头抵达 owner 端泵能+闪光</summary>
        private void UpdateArcs(CyberChargeOrbProj orb) {
            bool orbFlying = orb != null && !orb.IsCharging;
            for (int i = arcs.Count - 1; i >= 0; i--) {
                FeedArc arc = arcs[i];
                arc.Age++;
                //端点追球，球没则冻结淡出
                if (orbFlying) {
                    arc.End = orb.Projectile.Center;
                }
                //折跳节拍重掷，基线跟端点
                if (arc.Age % 3 == 0) {
                    arc.RerollOffsets();
                }
                arc.RecomputePoints();

                //送达
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
            //按发射基准加算，不乘算滚雪球
            if (Projectile.owner == Main.myPlayer) {
                orb.Projectile.damage = (int)(baseOrbDamage * (1f + delivered * DamagePerPulse));
                orb.ExplosionRadiusMul = baseRadiusMul + delivered * RadiusPerPulse;
            }

            if (Main.netMode != NetmodeID.Server) {
                Vector2 pos = orb.Projectile.Center;
                SoundEngine.PlaySound(SoundID.Item93 with {
                    Volume = 0.38f, Pitch = 0.25f + delivered * 0.08f
                }, pos);
                //球过载闪光
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero,
                    BankGlow with { A = 0 }, 0.05f).Configure(0.05f, 0.30f + delivered * 0.04f, 14);
                for (int k = 0; k < 7; k++) {
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2CircularEdge(4f, 4f) + orb.Projectile.velocity * 0.2f,
                        BankCore, Main.rand.NextFloat(0.5f, 1.0f)).Configure(true, Main.rand.Next(8, 16));
                }
            }
            //只震 owner 屏
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

            //节拍泄余电
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

            //排空且电弧散尽后淡出
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

        /// <summary>第 i 格世界坐标，锚点水平排开+相位 bob</summary>
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
            Texture2D canvas = VaultAsset.placeholder2?.Value;
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

                //共用 Trail，换 positions 重建
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
            //残余火花收尾
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(anchor + Main.rand.NextVector2Circular(40f, 8f),
                    Main.rand.NextVector2Circular(2f, 1f), BankGlow,
                    Main.rand.NextFloat(0.4f, 0.7f)).Configure(true, Main.rand.Next(8, 14));
            }
        }
    }
}

using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>多格机匣：周身六格能量矩阵，光束命中逐格充能，六格集满向目标齐射六道定位光束</summary>
    internal sealed class MultiCellFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //多重荧绿
        public override Color TintColor => new(100, 255, 80);

        //光束/激光命中的充能内置冷却（帧）：激光命中频次高，冷却翻倍
        private const float ChargeICDBeam = 6f;
        private const float ChargeICDLaser = 12f;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 1;
            ctx.DamageMul += -0.10f;
            ctx.SpreadMul += 0.18f;
            ctx.ManaCostMul += 0.30f;
        }

        public override void OnPlayerUpdate(Player player) {
            if (player.whoAmI != Main.myPlayer) return;
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            int matrixType = ModContent.ProjectileType<SHPCMultiCellMatrixProj>();
            if (player.ownedProjectileCounts[matrixType] >= 1) return;
            Projectile.NewProjectile(player.GetSource_FromThis(),
                player.Center, Vector2.Zero, matrixType, 0, 0f, player.whoAmI);
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            //齐射光束与其他派生光束不回充，防止自循环
            if (beam.IsDerived) return;
            ChargeCell(beam.Projectile, target, ChargeICDBeam);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            ChargeCell(laser.Projectile, target, ChargeICDLaser);
        }

        /// <summary>为矩阵充能一格，拥有者端执行</summary>
        private static void ChargeCell(Projectile source, NPC target, float icdFrames) {
            if (source.owner != Main.myPlayer) return;
            if (target == null || !target.active || target.friendly) return;
            int matrixType = ModContent.ProjectileType<SHPCMultiCellMatrixProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != source.owner || proj.type != matrixType) continue;
                if (proj.ModProjectile is SHPCMultiCellMatrixProj matrix) {
                    matrix.TryCharge(target, icdFrames);
                }
                break;
            }
        }
    }

    /// <summary>
    /// 六格能量矩阵：环绕玩家的六边形单元格阵列（SHPCModHexCell.fx 逐格绘制）；
    /// ai[0]=充能格数（同步），ai[1]=齐射蓄势倒数；改件卸下或换武器自毁
    /// </summary>
    internal sealed class SHPCMultiCellMatrixProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int CellCount = 6;
        private const float RingRadius = 62f;
        //单格画布边长（px），六边形绘制在此 quad 内
        private const float CellQuadSize = 46f;
        //满格→齐射的蓄势帧数（预备动作窗口，全格脉冲可读）
        private const float SalvoDelayFrames = 14f;
        //齐射后冷却帧数，期间不充能
        private const float SalvoCooldownFrames = 45f;
        //每道齐射光束伤害占武器伤害比例
        private const float SalvoDamageRatio = 0.45f;
        //齐射光束追踪强度（定位光束的可靠命中来源）
        private const float SalvoHomingMul = 2.6f;
        //记录目标的有效追射距离与失效重索半径
        private const float TargetKeepRange = 1400f;
        private const float RetargetRange = 1100f;

        private static readonly Color MatrixMain = new(110, 255, 120);
        private static readonly Color MatrixDim = new(20, 120, 70);
        private static readonly Color MatrixCore = new(215, 255, 205);

        /// <summary>已充能格数 0~6，经 ai[0] 网络同步</summary>
        private int Charge {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        /// <summary>齐射蓄势倒数，>0 表示满格待发；仅拥有者端推进</summary>
        private float SalvoDelay {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        //───── 本地视觉状态（各端由 Charge 变化独立推演，不需同步）─────
        private readonly float[] cellFlash = new float[CellCount];
        private readonly float[] cellFill = new float[CellCount];
        private float salvoFlash;
        private float ringRotation;
        private float ringSpin;
        private float spawnFade;
        private float readyPulse;
        /// <summary>冷却可视量 1→0：齐射事件各端独立置 1，按冷却总帧数衰减，
        /// 与拥有者端真实 salvoCooldown 同节奏，让"空格但不充能"可读</summary>
        private float cooldownVisual;
        private int prevCharge;

        //───── 拥有者端逻辑状态 ─────
        private float chargeIcd;
        private float salvoCooldown;
        private int pendingTarget = -1;
        //目标 type 双重校验：蓄势期内 NPC 槽位可能被复用（参考 SHPCHeavyMaulProj 惯例）
        private int pendingTargetType = -1;

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

        /// <summary>阵位 i 的世界坐标（随环旋转）</summary>
        private Vector2 CellWorldPos(int index) =>
            Projectile.Center + (ringRotation + MathHelper.TwoPi * index / CellCount).ToRotationVector2() * RingRadius;

        /// <summary>
        /// 模块钩子调用：命中充能一格，拥有者端执行；
        /// 满格转入蓄势倒数，倒数结束由 AI 触发齐射
        /// </summary>
        public void TryCharge(NPC target, float icdFrames) {
            if (Projectile.owner != Main.myPlayer) return;
            if (SalvoDelay > 0f || salvoCooldown > 0f || chargeIcd > 0f) return;
            if (Charge >= CellCount) return;

            chargeIcd = icdFrames;
            Charge++;
            if (target != null && target.active && !target.friendly) {
                pendingTarget = target.whoAmI;
                pendingTargetType = target.type;
            }
            if (Charge >= CellCount) {
                SalvoDelay = SalvoDelayFrames;
            }
            Projectile.netUpdate = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead
                || owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID
                || !SHPCModificationSystem.HasModule<MultiCellFrameModule>(owner)) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 30;

            float timeScale = TimeGear.TimeScale;
            //弹性跟随玩家，环心带一点尾随感
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.3f);

            int chargeNow = Math.Clamp(Charge, 0, CellCount);
            bool ready = SalvoDelay > 0f || chargeNow >= CellCount;

            //环旋转：充能越多越快，待发提速蓄势，齐射瞬间反冲加转
            float targetSpin = 0.008f + chargeNow * 0.0035f + (ready ? 0.045f : 0f);
            ringSpin = MathHelper.Lerp(ringSpin, targetSpin, 0.1f);
            ringRotation += (ringSpin + salvoFlash * 0.06f) * timeScale;

            spawnFade = MathF.Min(spawnFade + 0.06f, 1f);
            salvoFlash = MathF.Max(salvoFlash - 0.05f, 0f);
            readyPulse = MathHelper.Lerp(readyPulse, ready ? 1f : 0f, 0.15f);
            cooldownVisual = MathF.Max(cooldownVisual - timeScale / SalvoCooldownFrames, 0f);
            for (int i = 0; i < CellCount; i++) {
                cellFlash[i] = MathF.Max(cellFlash[i] - 0.07f, 0f);
                //充能格填充平滑过渡：点亮渐涨、齐射排空渐落
                float fillTarget = i < chargeNow ? 1f : 0f;
                cellFill[i] = MathHelper.Lerp(cellFill[i], fillTarget, 0.16f);
            }

            //状态变化检测：各端由同步的 Charge 驱动音画反馈
            if (chargeNow != prevCharge) {
                if (chargeNow > prevCharge) {
                    OnCellCharged(chargeNow);
                }
                else if (chargeNow == 0 && prevCharge >= CellCount) {
                    OnSalvoFired();
                }
                prevCharge = chargeNow;
            }

            //拥有者端：计时推进与齐射触发
            if (Projectile.owner == Main.myPlayer) {
                chargeIcd = MathF.Max(chargeIcd - timeScale, 0f);
                salvoCooldown = MathF.Max(salvoCooldown - timeScale, 0f);
                if (SalvoDelay > 0f) {
                    SalvoDelay -= timeScale;
                    if (SalvoDelay <= 0f) {
                        SalvoDelay = 0f;
                        DoSalvo(owner);
                    }
                }
            }

            //充能格逸散微粒
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < chargeNow; i++) {
                    if (!Main.rand.NextBool(26)) continue;
                    Vector2 pos = CellWorldPos(i);
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(0.8f, 0.8f) - Vector2.UnitY * 0.4f,
                        MatrixMain, Main.rand.NextFloat(0.3f, 0.6f))
                        .Configure(MatrixDim, Main.rand.Next(10, 20));
                }
            }
            Lighting.AddLight(Projectile.Center, MatrixMain.ToVector3() * (0.12f + chargeNow * 0.05f) * spawnFade);
        }

        /// <summary>第 chargeNow 格点亮：逐格上升音 + 六边形碎屑；满格附加就绪提示</summary>
        private void OnCellCharged(int chargeNow) {
            int idx = Math.Clamp(chargeNow - 1, 0, CellCount - 1);
            cellFlash[idx] = 1f;
            if (Main.netMode == NetmodeID.Server) return;

            Vector2 pos = CellWorldPos(idx);
            SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.26f, Pitch = 0.05f + chargeNow * 0.1f }, pos);
            PRTLoader.NewParticle<PRT_SHPCHexBit>(pos, Vector2.Zero,
                MatrixMain, Main.rand.NextFloat(0.8f, 1.1f)).Configure(MatrixDim, 22);
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2CircularEdge(2f, 2f),
                    MatrixMain, Main.rand.NextFloat(0.4f, 0.8f)).Configure(MatrixDim, Main.rand.Next(10, 18));
            }
            if (chargeNow >= CellCount) {
                //六格集满：就绪提示音 + 扩散环
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.55f, Pitch = 0.35f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                    MatrixMain with { A = 0 }, 0.05f).Configure(0.05f, 0.4f, 16);
            }
        }

        /// <summary>齐射瞬间（Charge 6→0）：六格闪白爆发，各端本地播放</summary>
        private void OnSalvoFired() {
            salvoFlash = 1f;
            cooldownVisual = 1f;
            for (int i = 0; i < CellCount; i++) {
                cellFlash[i] = 1f;
            }
            if (Projectile.owner == Main.myPlayer && Main.LocalPlayer.TryGetModPlayer(out CWRPlayer cp)) {
                cp.GetScreenShake(2f);
            }
            if (Main.netMode == NetmodeID.Server) return;

            SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.5f, Pitch = 0.25f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < CellCount; i++) {
                Vector2 pos = CellWorldPos(i);
                PRTLoader.NewParticle<PRT_SHPCHexBit>(pos, Main.rand.NextVector2Circular(1.5f, 1.5f),
                    MatrixCore, Main.rand.NextFloat(0.9f, 1.3f)).Configure(MatrixMain, Main.rand.Next(18, 26));
                for (int k = 0; k < 5; k++) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2CircularEdge(4f, 4f),
                        MatrixCore, Main.rand.NextFloat(0.6f, 1.2f)).Configure(MatrixMain, Main.rand.Next(14, 26));
                }
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                MatrixMain with { A = 0 }, 0.05f).Configure(0.05f, 0.65f, 20);
        }

        /// <summary>矩阵齐射：从六个阵位向目标各射一道定位光束（IsDerived 防递归）</summary>
        private void DoSalvo(Player owner) {
            //目标解析：优先最后充能命中的目标，失效则就近重索；
            //type 比对拦截蓄势期内槽位被新怪复用的顶替者
            NPC target = null;
            if (pendingTarget >= 0 && pendingTarget < Main.maxNPCs) {
                NPC cand = Main.npc[pendingTarget];
                if (cand.active && cand.type == pendingTargetType
                    && !cand.friendly && cand.CanBeChasedBy(Projectile)
                    && Vector2.DistanceSquared(cand.Center, Projectile.Center) <= TargetKeepRange * TargetKeepRange) {
                    target = cand;
                }
            }
            target ??= Projectile.Center.FindClosestNPC(RetargetRange, false, true);
            if (target == null) {
                //无目标：保持满格待发，稍后重试
                SalvoDelay = 15f;
                return;
            }
            pendingTarget = -1;
            pendingTargetType = -1;

            int dmg = Math.Max((int)(owner.GetWeaponDamage(owner.HeldItem) * SalvoDamageRatio), 1);
            for (int i = 0; i < CellCount; i++) {
                Vector2 pos = CellWorldPos(i);
                Vector2 dir = (target.Center - pos).SafeNormalize(Vector2.UnitX);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    pos, dir * 14f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, 2f, Projectile.owner,
                    ai0: 0);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = SalvoHomingMul;
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                        child.IsDerived = true;
                        child.SpeedMul = 1.15f;
                    }
                }
            }
            Charge = 0;
            SalvoDelay = 0f;
            salvoCooldown = SalvoCooldownFrames;
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            //回收解体：六格碎屑，避免凭空消失
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < CellCount; i++) {
                PRTLoader.NewParticle<PRT_SHPCHexBit>(CellWorldPos(i), Main.rand.NextVector2Circular(2f, 2f),
                    MatrixMain, Main.rand.NextFloat(0.6f, 0.9f)).Configure(MatrixDim, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (spawnFade < 0.02f) return;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null || glow == null) return;

            int chargeNow = Math.Clamp(Charge, 0, CellCount);
            Vector2 centerScreen = Projectile.Center - Main.screenPosition;

            //中心枢纽微光
            spriteBatch.Draw(glow, centerScreen, null, MatrixDim * (0.35f * spawnFade), 0f,
                glow.Size() * 0.5f, 0.5f + salvoFlash * 0.4f, SpriteEffects.None, 0f);

            //输能线与充能格背光（当前批次为 Deferred+Additive，直接绘制）
            for (int i = 0; i < CellCount; i++) {
                bool lit = i < chargeNow;
                Vector2 cellScreen = CellWorldPos(i) - Main.screenPosition;
                Vector2 delta = cellScreen - centerScreen;
                float len = delta.Length();
                if (len > 4f) {
                    Color lineCol = (lit ? MatrixMain : MatrixDim) * ((lit ? 0.30f : 0.10f) * spawnFade);
                    spriteBatch.Draw(white, centerScreen, null, lineCol, delta.ToRotation(),
                        new Vector2(0f, 0.5f), new Vector2(len, lit ? 1.6f : 1f), SpriteEffects.None, 0f);
                    if (lit) {
                        //输能流点：中心向格心滑动的亮珠
                        float t = (float)Main.timeForVisualEffects * 0.02f + i * 0.37f;
                        t -= MathF.Floor(t);
                        spriteBatch.Draw(glow, centerScreen + delta * t, null,
                            MatrixMain * (0.5f * spawnFade), 0f, glow.Size() * 0.5f, 0.1f, SpriteEffects.None, 0f);
                    }
                }
                if (lit) {
                    spriteBatch.Draw(glow, cellScreen, null,
                        MatrixMain * ((0.32f + cellFlash[i] * 0.4f + salvoFlash * 0.5f) * spawnFade), 0f,
                        glow.Size() * 0.5f, 0.45f + cellFlash[i] * 0.2f, SpriteEffects.None, 0f);
                }
            }

            //切 Immediate 批次，用专属六边形着色器逐格绘制
            Effect shader = EffectLoader.SHPCModHexCell?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader != null && noise != null) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.graphics.GraphicsDevice.Textures[1] = noise;
                Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
                shader.Parameters["fadeAlpha"]?.SetValue(spawnFade);
                shader.Parameters["salvoFlash"]?.SetValue(salvoFlash);
                shader.Parameters["readyPulse"]?.SetValue(readyPulse);
                shader.Parameters["cooldown"]?.SetValue(cooldownVisual);
                shader.Parameters["mainColor"]?.SetValue(MatrixMain.ToVector3());
                shader.Parameters["coreColor"]?.SetValue(MatrixCore.ToVector3());

                for (int i = 0; i < CellCount; i++) {
                    shader.Parameters["fill"]?.SetValue(cellFill[i]);
                    shader.Parameters["flash"]?.SetValue(cellFlash[i]);
                    shader.Parameters["hexRot"]?.SetValue(ringRotation * 0.5f + i * 0.35f);
                    shader.CurrentTechnique.Passes[0].Apply();
                    Vector2 cellScreen = CellWorldPos(i) - Main.screenPosition;
                    spriteBatch.Draw(white, cellScreen, null, Color.White, 0f,
                        new Vector2(0.5f, 0.5f), CellQuadSize, SpriteEffects.None, 0f);
                }

                //恢复调用方期望的批次状态（Deferred + Additive + PointWrap）
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }
    }
}

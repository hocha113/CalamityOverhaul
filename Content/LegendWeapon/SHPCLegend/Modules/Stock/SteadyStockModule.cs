using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>稳压枪托，射击间歇蓄电荷，开火放电强化；满档有窗口，逾期泄压，奖励点射</summary>
    internal sealed class SteadyStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //稳压电金
        public override Color TintColor => new(255, 214, 90);

        /// <summary>稳压三相，蓄压→满档待发→泄压</summary>
        internal enum VoltPhase : byte
        {
            Charging,
            Ready,
            Leaking,
        }

        //═════ 可调参数 ═════
        private const float BaseAttackSpeedAdd = -0.10f; //基础攻速代价（沉稳节奏身份）
        private const float BaseDamageAdd = 0.05f;       //基础伤害底盘
        private const int ChargeFrames = 45;             //空闲蓄满所需帧数（约0.75s）
        private const int WindowFrames = 100;            //满档稳压窗口时长
        private const int WarnFrames = 40;               //窗口临尽警示起始帧
        private const int LeakFrames = 22;               //泄压相排空帧数
        private const int IdleCutoffFrames = 180;        //脱战阈值，停蓄压并静默放空
        private const float IdleDrainPerFrame = 0.004f;  //脱战静默放空速率
        private const float EmpowerMaxBonus = 1.0f;      //满档放电伤害加成（1+1=2×）
        private const int FullPierceAdd = 2;             //满档强化束额外穿透
        private const int LaserEmpowerMaxFrames = 150;   //激光倾泻回声窗口帧数
        private const float LaserEchoRatio = 0.25f;      //激光回声伤害占比

        //稳压电金 / 电离青（与 SHPCModSteadyVolt.fx 配色对齐）
        internal static readonly Color VoltGold = new(255, 214, 90);
        internal static readonly Color VoltIon = new(95, 220, 255);

        //═════ 稳压状态（per-玩家实例）═════
        private VoltPhase phase = VoltPhase.Charging;
        private float charge;
        private int windowTimer;
        private float tickCarry;
        private int warnSoundTimer;
        private int beepGate;           //蓄压阈值提示音档位（0/1/2）
        private int prevItemAnimation;
        private int sinceShot = int.MaxValue / 2;   //距上次击发帧数，脱战判定用
        private uint lastTick;
        private float flashVis;         //放电闪光显示量，指数衰减

        //本轮放电强化的登记窗口（拥有者端）
        private uint empowerTick;
        private float empowerMul;
        private float empowerCharge;
        private bool empowerFull;

        //强化束登记 whoAmI→电荷，消亡移除+兜底
        private readonly Dictionary<int, float> voltBeams = new();
        private readonly List<int> pruneScratch = new();
        private int pruneTimer;

        //激光倾泻状态
        private bool laserWasActive;
        private int laserEmpowerFrames;
        private float laserCarry;

        internal VoltPhase Phase => phase;
        internal float Charge01 => charge;
        internal float FlashVis => flashVis;
        internal int VoltBeamCount => voltBeams.Count;
        /// <summary>稳压窗口剩余比，满档 1→0</summary>
        internal float WindowRatio => phase == VoltPhase.Ready
            ? MathHelper.Clamp(windowTimer / (float)WindowFrames, 0f, 1f) : 1f;

        /// <summary>强化束电荷快照，未登记返回 -1</summary>
        internal float GetVoltCharge(int whoAmI) => voltBeams.TryGetValue(whoAmI, out float v) ? v : -1f;

        /// <summary>枪口位向，itemRotation 跨端可用</summary>
        internal static Vector2 GetMuzzle(Player player, out Vector2 aimDir) {
            float aim = player.direction == 1 ? player.itemRotation : player.itemRotation + MathHelper.Pi;
            aimDir = aim.ToRotationVector2();
            return player.RotatedRelativePoint(player.MountedCenter, true) + aimDir * 46f;
        }

        /// <summary>当前装备的本改件实例，未装备 null</summary>
        internal static SteadyStockModule GetOn(Player player) {
            if (player == null) {
                return null;
            }
            SHPCPlayer sp = SHPCPlayer.Get(player);
            if (sp == null) {
                return null;
            }
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                if (sp.GetModule(i)?.ModItem is SteadyStockModule m) {
                    return m;
                }
            }
            return null;
        }

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += BaseAttackSpeedAdd;
            ctx.DamageMul += BaseDamageAdd;
        }

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active) {
                return;
            }
            //卸改件/换预设全量复位
            if (Main.GameUpdateCount - lastTick > 4) {
                ResetVolt();
            }
            lastTick = Main.GameUpdateCount;

            if (player.dead) {
                ResetVolt();
                return;
            }

            int tick = TickUp(ref tickCarry);
            flashVis *= MathF.Pow(0.9f, tick);
            if (flashVis < 0.02f) {
                flashVis = 0f;
            }
            //登记窗口过期清除
            if (empowerMul > 1f && Main.GameUpdateCount - empowerTick > 1) {
                empowerMul = 0f;
            }

            bool holding = player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID;
            bool laserActive = player.ownedProjectileCounts[ModContent.ProjectileType<CyberPrismLaserProj>()] > 0;

            //itemAnimation 回跳=开火帧；点火帧须再查 LaserMode，防白耗电荷
            if (holding && player.ItemAnimationActive && player.altFunctionUse != 2 && !laserActive
                && player.itemAnimation > prevItemAnimation
                && !SHPCModificationSystem.Resolve(player).LaserMode) {
                OnShotFired(player);
            }
            prevItemAnimation = holding && player.ItemAnimationActive ? player.itemAnimation : 0;

            if (sinceShot < int.MaxValue / 2) {
                sinceShot += tick;
            }
            bool inCombatRhythm = sinceShot <= IdleCutoffFrames;

            switch (phase) {
                case VoltPhase.Charging:
                    //非挥械间歇才蓄压；脱战静默放空，防挂机音效循环
                    if (holding && !player.ItemAnimationActive && inCombatRhythm) {
                        charge = MathF.Min(charge + tick / (float)ChargeFrames, 1f);
                        TickBeeps(player);
                        if (charge >= 1f) {
                            EnterReady(player);
                        }
                    }
                    else if (!inCombatRhythm && charge > 0f) {
                        charge = MathF.Max(charge - IdleDrainPerFrame * tick, 0f);
                        if (charge <= 0f) {
                            beepGate = 0;
                        }
                    }
                    break;
                case VoltPhase.Ready:
                    windowTimer -= tick;
                    //窗口临尽升频警示
                    if (windowTimer <= WarnFrames) {
                        warnSoundTimer -= tick;
                        if (warnSoundTimer <= 0) {
                            warnSoundTimer = 12;
                            if (Main.netMode != NetmodeID.Server) {
                                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.55f }, player.Center);
                            }
                        }
                    }
                    if (windowTimer <= 0) {
                        EnterLeaking(player);
                    }
                    break;
                case VoltPhase.Leaking:
                    charge = MathF.Max(charge - tick / (float)LeakFrames, 0f);
                    if (charge <= 0f) {
                        phase = VoltPhase.Charging;
                        beepGate = 0;
                    }
                    break;
            }

            PruneVoltBeams(player, tick);

            //环规弹幕，仅 owner 端
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            bool needGauge = charge > 0.02f || phase != VoltPhase.Charging
                || voltBeams.Count > 0 || flashVis > 0.05f;
            if (!needGauge) {
                return;
            }
            int gaugeType = ModContent.ProjectileType<SHPCSteadyVoltGaugeProj>();
            if (player.ownedProjectileCounts[gaugeType] < 1) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Center, Vector2.Zero, gaugeType, 0, 0f, player.whoAmI);
            }
        }

        private void ResetVolt() {
            phase = VoltPhase.Charging;
            charge = 0f;
            windowTimer = 0;
            beepGate = 0;
            prevItemAnimation = 0;
            sinceShot = int.MaxValue / 2;
            tickCarry = 0f;
            flashVis = 0f;
            empowerMul = 0f;
            laserWasActive = false;
            laserEmpowerFrames = 0;
            laserCarry = 0f;
            voltBeams.Clear();
        }

        /// <summary>蓄压 1/3、2/3 提示音</summary>
        private void TickBeeps(Player player) {
            int tier = charge >= 2f / 3f ? 2 : charge >= 1f / 3f ? 1 : 0;
            if (tier <= beepGate) {
                return;
            }
            beepGate = tier;
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.22f, Pitch = 0.1f + tier * 0.25f }, player.Center);
            }
        }

        private void EnterReady(Player player) {
            phase = VoltPhase.Ready;
            charge = 1f;
            windowTimer = WindowFrames;
            warnSoundTimer = 0;
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            //满档就绪音+金环
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.42f, Pitch = 0.9f }, player.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center + new Vector2(0f, -58f),
                Vector2.Zero, VoltGold with { A = 0 }, 0.05f).Configure(0.05f, 0.3f, 14);
        }

        private void EnterLeaking(Player player) {
            phase = VoltPhase.Leaking;
            beepGate = 0;
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            //逾期泄压音+散逸
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.35f }, player.Center);
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(14f, 18f),
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), Main.rand.NextFloat(-1.8f, -0.5f)),
                    Color.Lerp(VoltIon, VoltGold, Main.rand.NextFloat()), Main.rand.NextFloat(0.6f, 1.1f))
                    .Configure(false, Main.rand.Next(14, 24));
            }
        }

        private void OnShotFired(Player player) {
            sinceShot = 0;
            float c = charge;
            bool full = phase == VoltPhase.Ready || c >= 0.999f;

            //击发即放电清空，空电荷=普射
            if (c > 0.02f) {
                if (player.whoAmI == Main.myPlayer) {
                    empowerTick = Main.GameUpdateCount;
                    empowerMul = 1f + c * EmpowerMaxBonus;
                    empowerCharge = c;
                    empowerFull = full;
                }
                flashVis = MathF.Max(flashVis, c);

                if (Main.netMode != NetmodeID.Server) {
                    //放电反馈随电荷缩放
                    SoundEngine.PlaySound(SoundID.Item94 with {
                        Volume = 0.18f + c * 0.3f,
                        Pitch = full ? -0.15f : 0.25f
                    }, player.Center);
                    Vector2 muzzle = GetMuzzle(player, out Vector2 aimDir);
                    int count = 3 + (int)(c * 7f);
                    for (int i = 0; i < count; i++) {
                        Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 6f + c * 3f);
                        PRTLoader.NewParticle<PRT_Spark>(muzzle, vel,
                            Color.Lerp(VoltGold, VoltIon, Main.rand.NextFloat(0.35f)),
                            Main.rand.NextFloat(0.7f, 1.2f)).Configure(false, Main.rand.Next(14, 26));
                    }
                    if (full) {
                        PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero,
                            VoltGold with { A = 0 }, 0.05f).Configure(0.05f, 0.4f, 16);
                    }
                }
            }

            charge = 0f;
            phase = VoltPhase.Charging;
            windowTimer = 0;
            beepGate = 0;
        }

        /// <summary>兜底清强化束登记</summary>
        private void PruneVoltBeams(Player player, int tick) {
            pruneTimer += tick;
            if (pruneTimer < 90 || voltBeams.Count == 0) {
                return;
            }
            pruneTimer = 0;
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            pruneScratch.Clear();
            foreach (int id in voltBeams.Keys) {
                Projectile p = Main.projectile[id];
                if (!p.active || p.type != beamType || p.owner != player.whoAmI) {
                    pruneScratch.Add(id);
                }
            }
            foreach (int id in pruneScratch) {
                voltBeams.Remove(id);
            }
        }

        //═════════════ 光束钩子 ═════════════

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) {
                return;                                 //派生束不回喂
            }
            Projectile proj = beam.Projectile;
            if (proj.owner != Main.myPlayer) {
                return;
            }
            //同 tick（+1）主光束强化登记
            if (empowerMul > 1f && Main.GameUpdateCount - empowerTick <= 1
                && !voltBeams.ContainsKey(proj.whoAmI)) {
                voltBeams[proj.whoAmI] = empowerCharge;
                proj.damage = Math.Max((int)(proj.damage * empowerMul), 1);
                if (empowerFull) {
                    proj.penetrate += FullPierceAdd;
                }
                proj.netUpdate = true;
            }
            //强化束偶发金火花
            if (Main.netMode != NetmodeID.Server && voltBeams.ContainsKey(proj.whoAmI)
                && Main.rand.NextBool(6)
                && VaultUtils.IsPointOnScreen(proj.Center - Main.screenPosition, 150)) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center,
                    -beam.FlightDirection.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(1f, 2.5f),
                    Color.Lerp(VoltGold, VoltIon, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.5f, 0.85f)).Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) {
                return;
            }
            Projectile proj = beam.Projectile;
            if (proj.owner != Main.myPlayer) {
                return;
            }
            if (!voltBeams.TryGetValue(proj.whoAmI, out float c)) {
                return;
            }
            if (Main.netMode == NetmodeID.Server
                || !VaultUtils.IsPointOnScreen(target.Center - Main.screenPosition, 150)) {
                return;
            }
            //稳压命中火花
            int count = 3 + (int)(c * 5f);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.5f + c * 3f, 2.5f + c * 3f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center + vel * 2f, vel,
                    Color.Lerp(VoltGold, VoltIon, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.6f, 1.05f)).Configure(false, Main.rand.Next(12, 22));
            }
            if (c >= 0.999f && Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.22f, Pitch = 0.45f }, target.Center);
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            voltBeams.Remove(beam.Projectile.whoAmI);
        }

        //═════════════ 激光点火倾泻 ═════════════

        public override void OnLaserAI(CyberPrismLaserProj laser) {
            Projectile proj = laser.Projectile;
            if (proj.owner == Main.myPlayer) {
                //压枪激光也算战斗节奏，松后蓄压再倾泻
                sinceShot = 0;
                //点火倾泻为回声窗口
                if (!laserWasActive) {
                    laserWasActive = true;
                    if (charge > 0.02f) {
                        laserEmpowerFrames = (int)(charge * LaserEmpowerMaxFrames);
                        flashVis = MathF.Max(flashVis, charge);
                        if (Main.netMode != NetmodeID.Server) {
                            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.16f + charge * 0.28f, Pitch = -0.05f }, proj.Center);
                        }
                        charge = 0f;
                        phase = VoltPhase.Charging;
                        windowTimer = 0;
                        beepGate = 0;
                    }
                }
                if (laserEmpowerFrames > 0) {
                    TickDown(ref laserEmpowerFrames, ref laserCarry);
                }
            }

            //回声窗口染稳压金
            if (laserEmpowerFrames > 0) {
                float q = MathHelper.Clamp(laserEmpowerFrames / (float)LaserEmpowerMaxFrames + 0.35f, 0f, 1f);
                laser.ThemeCore = Color.Lerp(laser.ThemeCore, Color.Lerp(VoltGold, Color.White, 0.4f), q);
                laser.ThemeGlow = Color.Lerp(laser.ThemeGlow, VoltGold, q);
                laser.ThemeAura = Color.Lerp(laser.ThemeAura, new Color(120, 90, 15), q);
                laser.ThemeParticleMain = Color.Lerp(laser.ThemeParticleMain, VoltGold, q);
                laser.ThemeParticleEdge = Color.Lerp(laser.ThemeParticleEdge, VoltIon, q);
            }
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer || laserEmpowerFrames <= 0) {
                return;
            }
            //倾泻窗口命中追加回声
            int echo = Math.Max((int)(damageDone * LaserEchoRatio), 1);
            target.SimpleStrikeNPC(echo, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)
                && VaultUtils.IsPointOnScreen(target.Center - Main.screenPosition, 150)) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2CircularEdge(2.5f, 2.5f),
                    Color.Lerp(VoltGold, VoltIon, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.5f, 0.9f)).Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnLaserKill(CyberPrismLaserProj laser) {
            laserWasActive = false;
            laserEmpowerFrames = 0;
            laserCarry = 0f;
        }
    }

    /// <summary>稳压环规表盘+强化束电压纹路，SHPCModSteadyVolt.fx，仅 owner</summary>
    internal sealed class SHPCSteadyVoltGaugeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float RingQuadSize = 150f;   //环规画布边长（像素）
        private const float VoltQuadLen = 150f;    //电压纹路条带长度
        private const float VoltQuadWidth = 30f;   //电压纹路条带宽度
        private const int MaxVoltQuads = 10;       //每帧最多绘制的强化束数

        private float dispCharge;
        private float resVis;
        private float windowVis = 1f;
        private float leakVis;
        private float flashVis;
        private float fade;

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
            SteadyStockModule module = owner != null && owner.active && !owner.dead
                ? SteadyStockModule.GetOn(owner) : null;
            if (module == null) {
                Projectile.Kill();
                return;
            }
            bool idle = module.Phase == SteadyStockModule.VoltPhase.Charging
                && module.Charge01 <= 0.01f && module.VoltBeamCount == 0 && module.FlashVis <= 0.03f;
            if (idle && fade < 0.04f) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 8;
            Projectile.Center = owner.Center + new Vector2(0f, -58f + owner.gfxOffY);

            //显示量平滑
            dispCharge = MathHelper.Lerp(dispCharge, module.Charge01, 0.25f);
            resVis = MathHelper.Lerp(resVis, module.Phase == SteadyStockModule.VoltPhase.Ready ? 1f : 0f, 0.15f);
            windowVis = MathHelper.Lerp(windowVis, module.WindowRatio, 0.3f);
            leakVis = MathHelper.Lerp(leakVis, module.Phase == SteadyStockModule.VoltPhase.Leaking ? 1f : 0f, 0.25f);
            flashVis = MathF.Max(module.FlashVis, flashVis * 0.88f);
            if (flashVis < 0.02f) {
                flashVis = 0f;
            }
            fade = MathHelper.Lerp(fade, idle ? 0f : 1f, idle ? 0.12f : 0.2f);

            Color lightCol = Color.Lerp(SteadyStockModule.VoltIon, SteadyStockModule.VoltGold, dispCharge);
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * fade * (0.12f + dispCharge * 0.3f + flashVis * 0.3f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (fade < 0.02f) {
                return false;
            }
            Player owner = Main.player[Projectile.owner];
            SteadyStockModule module = owner != null && owner.active
                ? SteadyStockModule.GetOn(owner) : null;
            if (module == null) {
                return false;
            }
            Effect shader = EffectLoader.SHPCModSteadyVolt?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return false;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(dispCharge, 0f, 1f));
            shader.Parameters["uResonance"]?.SetValue(resVis);
            shader.Parameters["uWindow"]?.SetValue(windowVis);
            shader.Parameters["uLeak"]?.SetValue(leakVis);
            shader.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flashVis, 0f, 1f));
            shader.Parameters["goldColor"]?.SetValue(SteadyStockModule.VoltGold.ToVector3());
            shader.Parameters["ionColor"]?.SetValue(SteadyStockModule.VoltIon.ToVector3());

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;

            //pass0 环规表盘
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(RingQuadSize, RingQuadSize), SpriteEffects.None, 0f);

            //pass1 强化束电压纹路，uv.x=0 束头
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            int drawn = 0;
            for (int i = 0; i < Main.maxProjectiles && drawn < MaxVoltQuads; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != beamType) {
                    continue;
                }
                float c = module.GetVoltCharge(p.whoAmI);
                if (c < 0f) {
                    continue;
                }
                if (!VaultUtils.IsPointOnScreen(p.Center - Main.screenPosition, 300)) {
                    continue;
                }
                shader.Parameters["fadeAlpha"]?.SetValue(0.55f + 0.4f * c);
                shader.CurrentTechnique.Passes[1].Apply();
                float rot = p.velocity.ToRotation() + MathHelper.Pi;
                Main.spriteBatch.Draw(canvas, p.Center - Main.screenPosition, null, Color.White,
                    rot, new Vector2(0f, 0.5f), new Vector2(VoltQuadLen, VoltQuadWidth), SpriteEffects.None, 0f);
                drawn++;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}

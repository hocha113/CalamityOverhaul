using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>狙击瞄具：停火稳瞄积累锁定，锁满上膛后下一发升格为全穿透贯空射线，射后清零重蓄</summary>
    internal sealed class SniperOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //狙击冷白
        public override Color TintColor => new(220, 240, 255);

        //═════ 可调参数 ═════
        /// <summary>锁定满所需稳瞄帧数</summary>
        internal const int LockFullFrames = 75;
        /// <summary>帧间瞄准角变化超过此弧度视为甩枪</summary>
        private const float AimStableTolerance = 0.045f;
        /// <summary>甩枪时每帧锁定流失</summary>
        private const float SwayDrainPerFrame = 1.6f;
        /// <summary>开火（任意模式）时每帧锁定流失</summary>
        private const float FireDrainPerFrame = 6f;
        /// <summary>收枪时每帧锁定流失</summary>
        private const float HolsterDrainPerFrame = 2.5f;
        /// <summary>贯空射线基础倍率，基数为合并前单束面值</summary>
        internal const float RayBaseMul = 7f;
        /// <summary>每吸收一束光束的补偿倍率；标准 3 束齐射 = 7+4.5 = 11.5×，
        /// 使单体锁定循环（75 帧蓄锁 + 28 帧射击）略优于持续开火</summary>
        internal const float RayPerBeamMul = 1.5f;
        /// <summary>左键基准束数，镜像 SHPCOverride.BeamCount（私有常量无法引用，上游改动需同步）</summary>
        private const int VolleyBeamCount = 3;

        //═════ per-玩家锁定状态（槽内为独立实例，勿放 static） ═════
        /// <summary>锁定值 0..LockFullFrames，仅所有者客户端演算</summary>
        internal float LockCharge;
        /// <summary>已上膛：锁满待发，直到下一次主射击被消费</summary>
        internal bool LockReady;
        /// <summary>瞄准晃动反馈 0~1，导引线绘制读取</summary>
        internal float AimJitter;

        private float lockCarry;
        private float lastAimAngle;
        private bool hasAimSample;
        /// <summary>贯空消费发生的 tick，用于吸收同一次射击的其余光束</summary>
        private uint consumeTick;
        private bool absorbing;
        /// <summary>已做过首帧判定的光束，OnBeamKill 清理</summary>
        private readonly HashSet<int> seenBeams = [];

        public override void Apply(ref ShootContext ctx) {
            //一击重狙底子（相对旧版弱化，主要输出由锁定机制承担）
            ctx.BeamSpeedMul += 0.6f;
            ctx.BeamLifeMul += 0.4f;
            ctx.DamageMul += 0.15f;
            ctx.AttackSpeedMul += -0.3f;
            //哨兵陷阱：CyberTraceBeamProj 把 ai[1]==0 视为"未注入"并还原为默认追踪 1f，
            //恰好 -1f 会让 ctx.HomingMul 归 0 反而满额追踪；-0.99f 贴近归零且绕开哨兵
            ctx.HomingMul += -0.99f;
            ctx.SpreadMul += -1f;
        }

        /// <summary>锁定进度 0~1，上膛时恒 1</summary>
        internal float ChargeRatio => LockReady ? 1f : MathHelper.Clamp(LockCharge / LockFullFrames, 0f, 1f);

        /// <summary>取该玩家槽上的本模块实例，导引线弹幕读取锁定状态用</summary>
        internal static SniperOpticModule FindOn(Player player) {
            if (player == null) {
                return null;
            }
            SHPCPlayer sp = player.GetModPlayer<SHPCPlayer>();
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                if (sp.GetModule(i)?.ModItem is SniperOpticModule m) {
                    return m;
                }
            }
            return null;
        }

        public override void OnPlayerUpdate(Player player) {
            AimJitter = MathF.Max(AimJitter - 0.06f, 0f);
            //吸收窗口只覆盖消费发生的那一个 tick
            if (absorbing && Main.GameUpdateCount != consumeTick) {
                absorbing = false;
            }
            //异常残留自愈（换世界等跳过 OnKill 的路径），正常量级远低于此
            if (seenBeams.Count > 400) {
                seenBeams.Clear();
            }

            //锁定依赖本地准星，仅所有者客户端演算；射线弹幕经生成后由引擎同步
            if (player.whoAmI != Main.myPlayer || !player.active) {
                return;
            }
            if (player.dead) {
                //死亡退膛
                LockReady = false;
                LockCharge = 0f;
                hasAimSample = false;
                return;
            }

            bool holding = player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID;
            if (!holding) {
                //收枪退膛：上膛状态不跨武器保留，锁定值缓慢流失
                hasAimSample = false;
                LockReady = false;
                LockCharge = MathF.Max(LockCharge - HolsterDrainPerFrame, 0f);
                return;
            }

            //维持唯一的导引线弹幕（开火/零锁定时它自行淡出，不反复生成销毁）
            int lineType = ModContent.ProjectileType<SHPCSniperLockLineProj>();
            if (player.ownedProjectileCounts[lineType] <= 0) {
                Projectile.NewProjectile(player.GetSource_FromThis(),
                    player.Center, Vector2.Zero, lineType, 0, 0f, player.whoAmI);
            }

            //开火中（左键/激光持续/右键蓄力都算）：蓄锁被打断快速流失，已上膛不受影响
            if (player.itemAnimation > 0 || player.channel) {
                hasAimSample = false;
                if (!LockReady) {
                    LockCharge = MathF.Max(LockCharge - FireDrainPerFrame, 0f);
                }
                return;
            }

            if (LockReady) {
                return; //已上膛，保持待发
            }

            //准星方向稳定度：比较帧间角速度，允许移动中蓄锁（不检测玩家位移）
            float aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX).ToRotation();
            if (!hasAimSample) {
                lastAimAngle = aim;
                hasAimSample = true;
                return;
            }
            float sway = MathF.Abs(MathHelper.WrapAngle(aim - lastAimAngle));
            lastAimAngle = aim;

            if (sway > AimStableTolerance) {
                //甩枪：流失而非清零，容忍小幅修正瞄准
                LockCharge = MathF.Max(LockCharge - SwayDrainPerFrame, 0f);
                AimJitter = MathF.Min(AimJitter + 0.35f, 1f);
                return;
            }

            //稳定瞄准：按 TimeGear 推进，时缓期间等比变慢
            LockCharge += TickUp(ref lockCarry);
            if (LockCharge >= LockFullFrames) {
                LockCharge = LockFullFrames;
                LockReady = true;
            }
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) {
                return;
            }
            if (!seenBeams.Add(beam.Projectile.whoAmI)) {
                return; //仅首帧判定
            }

            //同一次射击的其余光束并入射线：先于 LockReady 判定，消费当帧 ready 已置 false
            if (absorbing && Main.GameUpdateCount == consumeTick) {
                AbsorbBeam(beam);
                return;
            }

            if (!LockReady) {
                return;
            }

            //锁满后的第一束升格：吸收整轮齐射，沿其飞行方向发射贯空射线
            LockReady = false;
            LockCharge = 0f;
            absorbing = true;
            consumeTick = Main.GameUpdateCount;
            FireSkypierceRay(beam.Projectile, beam.FlightDirection, ResolveRayDamage(beam.Projectile));
            AbsorbBeam(beam);
        }

        /// <summary>
        /// 贯空射线伤害：基数取合并前单束面值（聚束枪管的 MergedDamageBonus 先除掉，防止合并加成
        /// 与射线倍率双吃），倍率 = 基础 + 每吸收一束补偿，束数按当前射击上下文推算（整轮齐射均被并入）
        /// </summary>
        private static int ResolveRayDamage(Projectile source) {
            ShootContext ctx = SHPCModificationSystem.Resolve(Main.player[source.owner]);
            float mergeDiv = ctx.MergeBeams ? MathF.Max(ctx.MergedDamageBonus, 1f) : 1f;
            int absorbed = ctx.MergeBeams ? 1 : Math.Max(1, VolleyBeamCount + ctx.BeamCountAdd);
            float mul = RayBaseMul + RayPerBeamMul * absorbed;
            return Math.Max((int)(source.damage / mergeDiv * mul), 1);
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            seenBeams.Remove(beam.Projectile.whoAmI);
        }

        //激光模式成对处理：上膛后开启光柱的瞬间，沿光柱方向先行释放一道贯空射线
        public override void OnLaserAI(CyberPrismLaserProj laser) {
            if (!LockReady || laser.Projectile.owner != Main.myPlayer) {
                return;
            }
            LockReady = false;
            LockCharge = 0f;
            //光柱不被吸收、持续照射，射线只取基础倍率不吃吸收补偿
            FireSkypierceRay(laser.Projectile, laser.Projectile.rotation.ToRotationVector2(),
                Math.Max((int)(laser.Projectile.damage * RayBaseMul), 1));
        }

        /// <summary>吸收光束：置 SuppressDeathEffects 并抑制自身派生字段后移除，
        /// 让第三方 OnBeamKill（如霰射碎裂）与自体分裂/爆炸都不在枪口触发</summary>
        private static void AbsorbBeam(CyberTraceBeamProj beam) {
            beam.SuppressDeathEffects = true;
            beam.SplitOnDeath = 0;
            beam.ExplodeOnHit = false;
            beam.ChainCount = 0;
            beam.Projectile.Kill();
        }

        private static void FireSkypierceRay(Projectile source, Vector2 dir, int dmg) {
            if (source.owner != Main.myPlayer) {
                return;
            }
            Player owner = Main.player[source.owner];
            if (owner == null || !owner.active) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source.GetSource_FromThis(),
                owner.Center + dir * 30f, dir,
                ModContent.ProjectileType<SHPCSkypierceRayProj>(),
                Math.Max(dmg, 1), 8f, source.owner);
        }
    }

    /// <summary>狙击导引线，纯视觉零伤害；随锁定进度自枪口渐亮延伸，锁满定格闪烁并播上膛音；SHPCModSniperLock.fx mode=0</summary>
    internal sealed class SHPCSniperLockLineProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float MuzzleOffset = 34f;
        private const float MaxLineLength = 2400f;
        private const float ProbeStep = 64f;

        private static readonly Color LineCore = new(205, 235, 255);
        private static readonly Color LineEdge = new(90, 160, 235);

        private Vector2 aimDir = Vector2.UnitX;
        private float lineLength = 96f;
        private float fadeAlpha;
        private float readyFlash;
        private float prevCharge;
        private float drawCharge;
        private float drawJitter;
        private int convergeTimer;
        /// <summary>已播报的最高进度档位，回落一段后才允许重播，防阈值附近抖动刷音</summary>
        private int tickLevel;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 30;

            //准星与锁定状态只在所有者客户端有意义，远端保持隐形等待同步消亡
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            if (owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID
                || SniperOpticModule.FindOn(owner) is not SniperOpticModule module) {
                Projectile.Kill();
                return;
            }

            float charge = module.ChargeRatio;
            drawCharge = charge;
            drawJitter = module.AimJitter;

            aimDir = (Main.MouseWorld - owner.Center).SafeNormalize(Vector2.UnitX);
            Projectile.Center = owner.Center + aimDir * MuzzleOffset;
            Projectile.rotation = aimDir.ToRotation();
            ResolveLength();

            //开火与零锁定时淡出，蓄锁越满越亮
            bool firing = owner.itemAnimation > 0 || owner.channel;
            float targetAlpha = firing || charge <= 0.01f ? 0f : 0.35f + 0.65f * charge;
            fadeAlpha = MathHelper.Lerp(fadeAlpha, targetAlpha, 0.2f);

            //进度滴答：每 25% 一声渐高电子音，带迟滞防抖
            int level = (int)(charge / 0.25f);
            if (level > tickLevel && level < 4) {
                tickLevel = level;
                SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.2f, Pitch = -0.45f + 0.3f * level }, Projectile.Center);
            }
            else if (charge < tickLevel * 0.25f - 0.08f) {
                tickLevel = (int)(charge / 0.25f);
            }

            //锁满瞬间：定格闪烁 + 上膛音 + 枪口星环
            if (prevCharge < 1f && charge >= 1f) {
                readyFlash = 1f;
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.65f, Pitch = 0.45f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.75f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                    LineCore with { A = 0 }, 0.04f).Configure(0.04f, 0.3f, 14);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        aimDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 5f),
                        LineEdge, Main.rand.NextFloat(0.5f, 0.9f)).Configure(true, Main.rand.Next(8, 14));
                }
            }
            prevCharge = charge;
            readyFlash *= 0.88f;

            //蓄锁期沿线稀疏汇聚微粒，锁越满越密
            if (!module.LockReady && charge > 0.12f && fadeAlpha > 0.1f) {
                convergeTimer++;
                int interval = (int)MathHelper.Lerp(14f, 6f, charge);
                if (convergeTimer >= interval) {
                    convergeTimer = 0;
                    float d = Main.rand.NextFloat(40f, MathF.Min(lineLength, 700f));
                    Vector2 linePos = Projectile.Center + aimDir * d;
                    Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_CyberConverge>(linePos + perp * Main.rand.NextFloat(-70f, 70f),
                        Vector2.Zero, LineCore, Main.rand.NextFloat(0.5f, 0.9f))
                        .Configure(linePos, LineEdge, Main.rand.Next(14, 24), charge);
                }
            }

            if (fadeAlpha > 0.05f) {
                for (int i = 0; i <= 3; i++) {
                    Lighting.AddLight(Projectile.Center + aimDir * (lineLength * i / 3f),
                        LineEdge.ToVector3() * 0.25f * fadeAlpha * charge);
                }
            }
        }

        /// <summary>沿瞄准方向分段步进探墙，单帧一次全程 march</summary>
        private void ResolveLength() {
            float len = ProbeStep;
            Vector2 prev = Projectile.Center;
            while (len < MaxLineLength) {
                Vector2 next = Projectile.Center + aimDir * (len + ProbeStep);
                if (!Collision.CanHitLine(prev, 1, 1, next, 1, 1)) {
                    break;
                }
                prev = next;
                len += ProbeStep;
            }
            lineLength = len;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.owner != Main.myPlayer || fadeAlpha < 0.02f) {
                return false;
            }
            Effect shader = EffectLoader.SHPCModSniperLock?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return false;
            }

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["mode"]?.SetValue(0f);
            shader.Parameters["progress"]?.SetValue(drawCharge);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["rayLength"]?.SetValue(lineLength);
            shader.Parameters["readyFlash"]?.SetValue(readyFlash);
            shader.Parameters["jitter"]?.SetValue(drawJitter);
            shader.Parameters["coreColor"]?.SetValue(LineCore.ToVector3());
            shader.Parameters["edgeColor"]?.SetValue(LineEdge.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                aimDir.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(lineLength, 48f), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //上膛待发时线终点定格一枚冷白十字准星，与自适应瞄具的旋转括弧区分
            if (Projectile.owner != Main.myPlayer || drawCharge < 1f || fadeAlpha < 0.05f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null) {
                return;
            }
            Vector2 endScreen = Projectile.Center + aimDir * lineLength - Main.screenPosition;
            float pulse = 1f + 0.12f * MathF.Sin((float)Main.timeForVisualEffects * 0.16f) + readyFlash * 0.8f;
            float armLen = 13f * pulse;
            Color c = LineCore * fadeAlpha;
            spriteBatch.Draw(white, endScreen, null, c, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(armLen, 2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(white, endScreen, null, c, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(armLen, 2f), SpriteEffects.None, 0f);
            if (glow != null) {
                spriteBatch.Draw(glow, endScreen, null, LineEdge with { A = 0 } * fadeAlpha * 0.5f, 0f,
                    glow.Size() * 0.5f, 0.3f * pulse, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>贯空射线：瞬时命中的全穿透狙击射线，音爆双响+沿线真空带；SHPCModSniperLock.fx mode=1</summary>
    internal sealed class SHPCSkypierceRayProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 34;
        private const int DamageWindow = 6;
        private const float MaxLength = 2400f;
        private const float HitWidth = 30f;
        /// <summary>音爆残响与真空回填的延迟帧</summary>
        private const int EchoFrame = 10;
        /// <summary>发射屏震满幅幅度</summary>
        private const float MuzzleShake = 4.5f;
        /// <summary>屏震衰减距离（像素），本地玩家距枪口超过此距离不再震动</summary>
        private const float ShakeFalloffDist = 1000f;

        private static readonly Color RayCore = new(240, 250, 255);
        private static readonly Color RayEdge = new(110, 185, 255);

        private Vector2 rayDir;
        private float rayLength;
        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一线贯穿，每个敌人只结算一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                rayDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Zero;
                ResolveLength();
                if (Main.netMode != NetmodeID.Server) {
                    //音爆第一响：瞬时高频裂响
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.7f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.55f, Pitch = 0.85f }, Projectile.Center);
                    SpawnVacuumImplosion();
                    //屏震随本地玩家与枪口距离线性衰减，远处旁观者不吃满幅震动
                    float falloff = 1f - MathHelper.Clamp(
                        Main.LocalPlayer.Distance(Projectile.Center) / ShakeFalloffDist, 0f, 1f);
                    SHPCNaturalFx.Shake(MuzzleShake * falloff);
                }
            }

            int age = Lifetime - Projectile.timeLeft;
            //音爆第二响：延迟低哑残响 + 真空回填冲击
            if (age == EchoFrame && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.5f, Pitch = -0.55f }, Projectile.Center);
                SpawnVacuumBackfill();
            }

            fadeAlpha = 1f - age / (float)Lifetime;
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Projectile.Center + rayDir * (rayLength * i / 3f),
                    RayEdge.ToVector3() * 0.7f * fadeAlpha);
            }
        }

        /// <summary>沿弹道以 32px 步进探测墙体，确定实际射线长度</summary>
        private void ResolveLength() {
            rayLength = 120f;
            while (rayLength < MaxLength) {
                Vector2 probe = Projectile.Center + rayDir * (rayLength + 32f);
                if (!Collision.CanHitLine(Projectile.Center, 1, 1, probe, 1, 1)) {
                    break;
                }
                rayLength += 32f;
            }
        }

        /// <summary>发射瞬间：沿线两侧微粒被吸向线心（真空带成形），枪口终点耀斑</summary>
        private void SpawnVacuumImplosion() {
            Vector2 perp = rayDir.RotatedBy(MathHelper.PiOver2);
            for (float d = 60f; d < rayLength; d += 130f) {
                Vector2 linePos = Projectile.Center + rayDir * d;
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 from = linePos + perp * (s * Main.rand.NextFloat(55f, 110f));
                    PRTLoader.NewParticle<PRT_CyberConverge>(from, Vector2.Zero,
                        RayCore, Main.rand.NextFloat(0.7f, 1.2f))
                        .Configure(linePos, RayEdge, Main.rand.Next(12, 20), 1f);
                }
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center + rayDir * 14f, Vector2.Zero,
                RayEdge with { A = 0 }, 0.05f).Configure(0.05f, 0.42f, 16);
            Vector2 endPos = Projectile.Center + rayDir * rayLength;
            PRTLoader.NewParticle<PRT_StarPulseRing>(endPos, Vector2.Zero,
                RayCore with { A = 0 }, 0.05f).Configure(0.05f, 0.55f, 20);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(endPos, Main.rand.NextVector2CircularEdge(6f, 6f),
                    RayEdge, Main.rand.NextFloat(0.6f, 1.1f)).Configure(true, Main.rand.Next(10, 18));
            }
        }

        /// <summary>残响时刻：空气回填，粒子自线心向两侧横向排开</summary>
        private void SpawnVacuumBackfill() {
            Vector2 perp = rayDir.RotatedBy(MathHelper.PiOver2);
            for (float d = 40f; d < rayLength; d += 110f) {
                Vector2 linePos = Projectile.Center + rayDir * d;
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 vel = perp * (s * Main.rand.NextFloat(2.5f, 6f)) + rayDir * Main.rand.NextFloat(-0.5f, 0.5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(linePos + perp * (s * 6f), vel,
                        RayCore, Main.rand.NextFloat(0.5f, 1.0f)).Configure(RayEdge, Main.rand.Next(12, 22));
                }
            }
        }

        public override bool? CanDamage() => Lifetime - Projectile.timeLeft <= DamageWindow;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                Projectile.Center, Projectile.Center + rayDir * rayLength, HitWidth, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit42 with { Volume = 0.35f, Pitch = 0.55f }, target.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2CircularEdge(5.5f, 5.5f),
                    RayCore, Main.rand.NextFloat(0.6f, 1.1f)).Configure(true, Main.rand.Next(10, 18));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center,
                    Main.rand.NextVector2CircularEdge(4f, 4f),
                    RayCore, Main.rand.NextFloat(0.7f, 1.2f)).Configure(RayEdge, Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCModSniperLock?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return false;
            }

            int age = Lifetime - Projectile.timeLeft;
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["mode"]?.SetValue(1f);
            shader.Parameters["progress"]?.SetValue(MathHelper.Clamp(age / (float)Lifetime, 0f, 1f));
            shader.Parameters["fadeAlpha"]?.SetValue(1f);
            shader.Parameters["rayLength"]?.SetValue(rayLength);
            shader.Parameters["readyFlash"]?.SetValue(0f);
            shader.Parameters["jitter"]?.SetValue(0f);
            shader.Parameters["coreColor"]?.SetValue(RayCore.ToVector3());
            shader.Parameters["edgeColor"]?.SetValue(RayEdge.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                rayDir.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(rayLength, 72f), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star == null) {
                return;
            }
            //枪口冷白十字耀星：一锤定音的高光记忆点
            Vector2 muzzleScreen = Projectile.Center - Main.screenPosition;
            float flash = MathF.Pow(fadeAlpha, 1.5f);
            spriteBatch.Draw(star, muzzleScreen, null, RayCore * flash * 0.9f,
                (float)Main.timeForVisualEffects * 0.035f, star.Size() * 0.5f, 0.18f * flash, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, muzzleScreen, null, RayEdge * flash * 0.6f,
                -(float)Main.timeForVisualEffects * 0.028f, star.Size() * 0.5f, 0.3f * flash, SpriteEffects.None, 0f);
        }
    }
}

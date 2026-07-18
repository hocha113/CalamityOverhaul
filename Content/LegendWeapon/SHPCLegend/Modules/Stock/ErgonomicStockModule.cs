using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 人体工学枪托：对同一目标（或近似同一方向）持续攻击积累"肌肉记忆"熟练度，
    /// 分档解锁散布收敛→攻速提升→满层"人枪合一"（回蓝+光束精准修正+韵律残影）；
    /// 切换目标或长时间停火则记忆快速消退
    /// </summary>
    internal sealed class ErgonomicStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //人机契合米白
        public override Color TintColor => new(230, 220, 180);

        #region 可调参数
        /// <summary>熟练度上限（满层=人枪合一）</summary>
        public const float MaxProficiency = 100f;
        /// <summary>T1 熟悉档阈值：解锁散布收敛</summary>
        public const float Tier1Threshold = 25f;
        /// <summary>T2 顺手档阈值：解锁攻速提升</summary>
        public const float Tier2Threshold = 60f;
        /// <summary>人枪合一滞回退出线，跌破才失去满层状态，避免边界闪烁</summary>
        public const float UnityExitThreshold = 85f;
        /// <summary>命中记忆目标的熟练度增益（光束约9次命中/秒→约5.5秒磨合满）</summary>
        private const float HitGain = 2f;
        /// <summary>近似同向换绑（蠕虫节/密集群）时的增益比例</summary>
        private const float DirectionGainRatio = 0.5f;
        /// <summary>"近似同一方向"判定锥（弧度，约±20°）</summary>
        private const float DirectionCone = 0.35f;
        /// <summary>真换目标时保留的熟练度比例（记忆快速消退）</summary>
        private const float SwitchKeepRatio = 0.30f;
        /// <summary>停火宽限帧数，超过后记忆开始消退</summary>
        private const int IdleGraceFrames = 60;
        /// <summary>消退期每帧熟练度流失（满层约1秒流干）</summary>
        private const float IdleDecayPerTick = 1.6f;
        /// <summary>满熟练散布收敛幅度</summary>
        private const float MaxSpreadReduce = 0.45f;
        /// <summary>满熟练攻速加成</summary>
        private const float MaxAttackSpeed = 0.15f;
        /// <summary>基础法力消耗减免（人机契合底子，唯一的静态数值）</summary>
        private const float BaseManaCostReduce = 0.10f;
        /// <summary>人枪合一命中记忆目标的回蓝量</summary>
        private const int ManaRefundAmount = 2;
        /// <summary>回蓝内置冷却（帧）</summary>
        private const int ManaRefundCooldown = 10;
        /// <summary>人枪合一光束对记忆目标的修正转向速率（弧度/次AI调用）</summary>
        private const float UnitySteerRate = 0.07f;
        /// <summary>修正锥（弧度，约±60°）：锥外不掰头，保持"修正"而非"追踪"</summary>
        private const float UnitySteerCone = 1.05f;
        #endregion

        //═════ 肌肉记忆状态（per-玩家：每个玩家槽位里是独立的模块实例，标杆 Momentum/Overwatch 同做法）═════
        private float _prof;
        private int _memoryNpcId = -1;
        private int _memoryNpcType = -1;
        private float _memoryAngle;
        private int _idleTimer;
        private float _idleCarry;
        private int _refundCooldown;
        private float _refundCarry;
        private int _lastTier;
        private bool _unityActive;
        private uint _lastSeenTick;

        /// <summary>人枪合一态（进入=满熟练，退出带滞回），残影弹幕消费</summary>
        public bool UnityActive => _unityActive;

        //香槟金配色体系：区别于动量的电橙与守望的蓝白
        internal static readonly Color EchoCore = new(255, 244, 205);
        internal static readonly Color EchoGlow = new(235, 190, 110);
        internal static readonly Color EchoEdge = new(190, 135, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -BaseManaCostReduce;
            //T1 熟悉：散布随熟练度线性收敛
            if (_prof >= Tier1Threshold) {
                ctx.SpreadMul += -MaxSpreadReduce * TierProgress(_prof, Tier1Threshold);
            }
            //T2 顺手：攻速渐进提升
            if (_prof >= Tier2Threshold) {
                ctx.AttackSpeedMul += MaxAttackSpeed * TierProgress(_prof, Tier2Threshold);
            }
        }

        /// <summary>档位内进度 0~1：从解锁阈值到满熟练线性铺开</summary>
        private static float TierProgress(float prof, float threshold)
            => MathHelper.Clamp((prof - threshold) / (MaxProficiency - threshold), 0f, 1f);

        /// <summary>清空全部肌肉记忆状态（改件被卸下过时调用）</summary>
        private void ResetMemory() {
            _prof = 0f;
            _memoryNpcId = -1;
            _memoryNpcType = -1;
            _idleTimer = 0;
            _idleCarry = 0f;
            _refundCooldown = 0;
            _refundCarry = 0f;
            _lastTier = 0;
            _unityActive = false;
        }

        private static int CurrentTier(float prof) {
            if (prof >= MaxProficiency) return 3;
            if (prof >= Tier2Threshold) return 2;
            if (prof >= Tier1Threshold) return 1;
            return 0;
        }

        //═════════════ 命中登记：光束与激光两模式共用 ═════════════

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            //派生光束（其他改件的链跳/分裂）命中方向杂乱，不参与肌肉记忆判定
            if (beam.IsDerived) {
                return;
            }
            Player player = Main.player[beam.Projectile.owner];
            if (player == null || !player.active) {
                return;
            }
            RegisterHit(player, target);
            TryUnityRefund(player, target);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Main.player[laser.Projectile.owner];
            if (player == null || !player.active) {
                return;
            }
            RegisterHit(player, target);
            TryUnityRefund(player, target);
        }

        /// <summary>
        /// 记忆判定核心：同目标全速磨合；近似同向半速换绑；真换目标记忆大幅消退后从头磨合
        /// </summary>
        private void RegisterHit(Player player, NPC target) {
            if (target == null || !target.active) {
                return;
            }
            float angleToTarget = (target.Center - player.Center).ToRotation();
            bool sameTarget = _memoryNpcId == target.whoAmI && _memoryNpcType == target.type;

            float gain;
            if (sameTarget) {
                gain = HitGain;
            }
            else if (_memoryNpcId < 0 && _prof <= 0f) {
                //白纸状态：直接开始磨合
                gain = HitGain;
            }
            else if (MathF.Abs(MathHelper.WrapAngle(angleToTarget - _memoryAngle)) <= DirectionCone) {
                //近似同一方向（蠕虫节/同一火线上的下一个敌人）：半速续接并换绑
                gain = HitGain * DirectionGainRatio;
            }
            else {
                //真换目标：记忆快速消退，重新磨合
                _prof *= SwitchKeepRatio;
                gain = HitGain;
            }

            _memoryNpcId = target.whoAmI;
            _memoryNpcType = target.type;
            _memoryAngle = angleToTarget;
            _idleTimer = 0;
            _prof = MathHelper.Clamp(_prof + gain, 0f, MaxProficiency);
        }

        /// <summary>人枪合一：命中记忆目标小额返还法力（仅本地玩家结算，带内置冷却）</summary>
        private void TryUnityRefund(Player player, NPC target) {
            if (!_unityActive || _refundCooldown > 0) {
                return;
            }
            if (player.whoAmI != Main.myPlayer || target.whoAmI != _memoryNpcId) {
                return;
            }
            _refundCooldown = ManaRefundCooldown;
            int refund = Math.Min(ManaRefundAmount, player.statManaMax2 - player.statMana);
            if (refund > 0) {
                player.statMana += refund;
                player.ManaEffect(refund);
            }
        }

        //═════════════ 人枪合一：光束追加精准修正 ═════════════

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (!_unityActive || _memoryNpcId < 0 || _memoryNpcId >= Main.maxNPCs) {
                return;
            }
            Projectile proj = beam.Projectile;
            //仅本地所有者修正，避免远端用不一致的记忆状态推演轨迹
            if (proj.owner != Main.myPlayer || beam.IsDerived || proj.numHits > 0) {
                return;
            }
            NPC target = Main.npc[_memoryNpcId];
            if (!target.active || target.type != _memoryNpcType) {
                return;
            }
            float flyAngle = beam.FlightDirection.ToRotation();
            float diff = MathHelper.WrapAngle((target.Center - proj.Center).ToRotation() - flyAngle);
            if (MathF.Abs(diff) > UnitySteerCone) {
                return;
            }
            float turn = MathHelper.Clamp(diff, -UnitySteerRate, UnitySteerRate);
            beam.SetFlightDirection((flyAngle + turn).ToRotationVector2());
        }

        //═════════════ 逐帧维护：消退、档位跃迁、残影维持 ═════════════

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active || player.dead) {
                return;
            }
            //钩子只在装备期间运行：检测到长间隔（改件曾被卸下）则清空记忆，
            //防止熟练度被冻结、重装后带着旧记忆复活（参考 SHPCPlayer 中超杀层数的同款坑）
            uint now = Main.GameUpdateCount;
            if (_lastSeenTick != 0 && now > _lastSeenTick + 5) {
                ResetMemory();
            }
            _lastSeenTick = now;

            //回蓝内置冷却按 TimeGear 推进，时缓期间与光束节奏同步变慢
            TickDown(ref _refundCooldown, ref _refundCarry);

            //记忆目标失效（死亡/槽位换体）：解除 id 绑定但保留方向与熟练度，
            //火线上补位的下一个敌人可通过同向判定无痛续接
            if (_memoryNpcId >= 0) {
                if (_memoryNpcId >= Main.maxNPCs
                    || !Main.npc[_memoryNpcId].active
                    || Main.npc[_memoryNpcId].type != _memoryNpcType) {
                    _memoryNpcId = -1;
                }
            }

            //停火消退：持续使用武器（含右键蓄力）即视为未停火——脱靶不涨熟练度，
            //但 Boss 无敌相/瞬移窗口内保持开火可维持记忆，避免满层态频繁跳档；
            //计时经 TickUp 走 TimeGear，时缓/冻结期间消退等比变慢/暂停
            bool usingWeapon = player.ItemAnimationActive
                && player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID;
            if (usingWeapon) {
                _idleTimer = 0;
            }
            else {
                int adv = TickUp(ref _idleCarry);
                _idleTimer += adv;
                if (_idleTimer > IdleGraceFrames && _prof > 0f) {
                    _prof = MathF.Max(_prof - IdleDecayPerTick * adv, 0f);
                }
            }

            //满层进入与滞回退出
            if (!_unityActive && _prof >= MaxProficiency) {
                _unityActive = true;
                UnityEnterFX(player);
            }
            else if (_unityActive && _prof < UnityExitThreshold) {
                _unityActive = false;
            }

            //档位跃迁反馈（只在上升沿提示；满层跃迁由 UnityEnterFX 专属承担，避免同帧双音效）
            int tier = CurrentTier(_prof);
            if (tier > _lastTier && tier < 3) {
                TierUpFX(player, tier);
            }
            _lastTier = tier;

            //磨合期手部流线：档位越高越密，玩家不看条也能感知记忆加深
            if (tier >= 1 && !_unityActive && Main.netMode != NetmodeID.Server
                && player.whoAmI == Main.myPlayer && player.ItemAnimationActive
                && player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID
                && player.altFunctionUse != 2 && Main.GameUpdateCount % (10 - tier * 2) == 0) {
                SpawnGripFlow(player);
            }

            //人枪合一：维持残影弹幕（仅本地玩家生成，网络自动同步）
            if (_unityActive && player.whoAmI == Main.myPlayer
                && player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID) {
                int ghostType = ModContent.ProjectileType<SHPCErgoEchoProj>();
                if (player.ownedProjectileCounts[ghostType] <= 0) {
                    Projectile.NewProjectile(player.GetSource_FromThis(),
                        player.Center, Vector2.Zero, ghostType, 0, 0f, player.whoAmI);
                }
            }
        }

        private static void SpawnGripFlow(Player player) {
            Vector2 hand = player.RotatedRelativePoint(player.MountedCenter, true);
            float aim = player.direction == 1 ? player.itemRotation : player.itemRotation + MathHelper.Pi;
            Vector2 aimDir = aim.ToRotationVector2();
            Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
            Vector2 pos = hand + aimDir * Main.rand.NextFloat(12f, 44f) + perp * Main.rand.NextFloat(-9f, 9f);
            PRTLoader.NewParticle<PRT_SHPCErgoFlow>(pos,
                -aimDir * Main.rand.NextFloat(1.2f, 2.4f) + perp * Main.rand.NextFloat(-0.3f, 0.3f),
                EchoGlow, Main.rand.NextFloat(0.4f, 0.7f)).Configure(Main.rand.Next(12, 20));
        }

        private static void TierUpFX(Player player, int tier) {
            if (Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer) {
                return;
            }
            //齿位卡入般的干脆音效，音调随档位递升
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.35f, Pitch = -0.2f + tier * 0.25f }, player.Center);
            Vector2 hand = player.RotatedRelativePoint(player.MountedCenter, true);
            int count = 4 + tier * 2;
            for (int i = 0; i < count; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / count).ToRotationVector2();
                PRTLoader.NewParticle<PRT_SHPCErgoFlow>(hand, dir * (1.6f + tier * 0.5f),
                    EchoGlow, Main.rand.NextFloat(0.5f, 0.8f)).Configure(Main.rand.Next(14, 22));
            }
        }

        private static void UnityEnterFX(Player player) {
            if (Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.35f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.5f }, player.Center);
            //人枪合一：环状流线爆发
            Vector2 hand = player.RotatedRelativePoint(player.MountedCenter, true);
            for (int i = 0; i < 18; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 18f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_SHPCErgoFlow>(hand + dir * 6f,
                    dir * Main.rand.NextFloat(3f, 5.5f),
                    i % 3 == 0 ? EchoCore : EchoGlow,
                    Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(18, 30));
            }
        }
    }

    /// <summary>
    /// 人枪合一残影：跟随玩家采样瞄准角历史，用 SHPCModErgoEcho 着色器绘制
    /// 武器的多重相位残影与持枪手臂光带；状态跌出或改件卸下后淡出自灭
    /// </summary>
    internal sealed class SHPCErgoEchoProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int HistLen = 20;
        //三重相位残影各自的滞后帧
        private static readonly int[] GhostLags = { 4, 9, 15 };

        private readonly float[] aimHistory = new float[HistLen];
        private float smoothAim;
        private float firingBlend;
        private float fade;
        private bool histInit;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        internal static ErgonomicStockModule FindModule(Player player) {
            ErgonomicStockModule found = null;
            SHPCModificationSystem.ForEachModule(player, mod => {
                if (mod is ErgonomicStockModule ergo) {
                    found = ergo;
                }
            });
            return found;
        }

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            ErgonomicStockModule module = FindModule(owner);
            bool valid = !owner.dead
                && owner.HeldItem != null && owner.HeldItem.type == SHPCOverride.ID
                && module != null && module.UnityActive;
            if (valid) {
                Projectile.timeLeft = 30;
                fade = MathF.Min(fade + 0.08f, 1f);
            }
            else {
                //状态跌出：淡出后自灭
                fade -= 0.06f;
                if (fade <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.Center = owner.RotatedRelativePoint(owner.MountedCenter, true);

            //瞄准采样：使用会被网络同步的 itemRotation，面朝左时 +π 还原世界角
            bool firing = owner.ItemAnimationActive && owner.altFunctionUse != 2;
            if (firing) {
                float aim = owner.direction == 1 ? owner.itemRotation : owner.itemRotation + MathHelper.Pi;
                smoothAim = histInit ? smoothAim.AngleLerp(aim, 0.55f) : aim;
            }
            firingBlend = MathHelper.Lerp(firingBlend, firing ? 1f : 0f, firing ? 0.25f : 0.08f);

            if (!histInit) {
                for (int i = 0; i < HistLen; i++) {
                    aimHistory[i] = smoothAim;
                }
                histInit = true;
            }
            Array.Copy(aimHistory, 0, aimHistory, 1, HistLen - 1);
            aimHistory[0] = smoothAim;

            //流线粒子：沿枪身向后掠过的气流线
            if (Main.netMode != NetmodeID.Server && firingBlend > 0.4f && Main.GameUpdateCount % 3 == 0) {
                Vector2 aimDir = smoothAim.ToRotationVector2();
                Vector2 perp = aimDir.RotatedBy(MathHelper.PiOver2);
                Vector2 pos = Projectile.Center + aimDir * Main.rand.NextFloat(8f, 58f)
                    + perp * Main.rand.NextFloat(-13f, 13f);
                PRTLoader.NewParticle<PRT_SHPCErgoFlow>(pos,
                    -aimDir * Main.rand.NextFloat(2f, 3.6f) + perp * Main.rand.NextFloat(-0.4f, 0.4f),
                    Main.rand.NextBool(4) ? ErgonomicStockModule.EchoCore : ErgonomicStockModule.EchoGlow,
                    Main.rand.NextFloat(0.5f, 0.9f)).Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, ErgonomicStockModule.EchoGlow.ToVector3() * 0.25f * fade);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fade < 0.02f) {
                return;
            }
            Player owner = Owner;
            if (owner == null || !owner.active) {
                return;
            }
            Texture2D weaponTex = TextureAssets.Item[SHPCOverride.ID]?.Value;
            if (weaponTex == null) {
                return;
            }

            Vector2 pivot = Projectile.Center - Main.screenPosition;
            //韵律相位：约1.2Hz 呼吸脉动，残影随节拍收放
            float beat = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.125f);
            //待机时保留微弱轮廓，开火时残影全开
            float alphaBase = fade * (0.30f + 0.70f * firingBlend);

            Effect fx = EffectLoader.SHPCModErgoEcho?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (fx != null && noise != null) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                fx.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
                fx.Parameters["uBeat"]?.SetValue(beat);
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
                fx.Parameters["uCoreColor"]?.SetValue(ErgonomicStockModule.EchoCore.ToVector3());
                fx.Parameters["uEdgeColor"]?.SetValue(ErgonomicStockModule.EchoEdge.ToVector3());

                //旧残影先画，新残影覆盖其上
                for (int i = GhostLags.Length - 1; i >= 0; i--) {
                    float lagAim = aimHistory[Math.Min(GhostLags[i], HistLen - 1)];
                    bool flipped = MathF.Cos(lagAim) < 0f;
                    fx.Parameters["uPhase"]?.SetValue((i + 1f) / GhostLags.Length);
                    fx.Parameters["uOpacity"]?.SetValue(alphaBase * (0.62f - 0.16f * i));
                    fx.CurrentTechnique.Passes[0].Apply();
                    DrawWeaponGhost(spriteBatch, weaponTex, pivot, lagAim, flipped);
                }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //持枪手臂残影：从肩锚点沿滞后角扫出的细光带
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak != null) {
                for (int i = 0; i < GhostLags.Length; i++) {
                    float lagAim = aimHistory[Math.Min(GhostLags[i], HistLen - 1)];
                    float armAlpha = alphaBase * (0.30f - 0.07f * i) * (0.7f + 0.3f * beat);
                    spriteBatch.Draw(streak, pivot, null, ErgonomicStockModule.EchoGlow * armAlpha, lagAim,
                        new Vector2(0f, streak.Height * 0.5f), new Vector2(0.16f, 0.05f), SpriteEffects.None, 0f);
                }
            }
            //掌心融合光核：随节拍呼吸
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pivot, null,
                    ErgonomicStockModule.EchoCore * (alphaBase * (0.35f + 0.25f * beat)), 0f,
                    glow.Size() * 0.5f, 0.55f + 0.15f * beat, SpriteEffects.None, 0f);
            }
        }

        private static void DrawWeaponGhost(SpriteBatch sb, Texture2D tex, Vector2 pivot, float aim, bool flipped) {
            //origin 取握把位置；贴图默认朝右，翻转时镜像 Y 侧 origin 保持握点不变
            Vector2 origin = new(tex.Width * 0.24f, flipped ? tex.Height * 0.38f : tex.Height * 0.62f);
            SpriteEffects flip = flipped ? SpriteEffects.FlipVertically : SpriteEffects.None;
            sb.Draw(tex, pivot, null, Color.White, aim, origin, SHPCOverride.ItemScale, flip, 0f);
        }
    }
}

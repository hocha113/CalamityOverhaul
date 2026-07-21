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
    /// <summary>人体工学枪托，同目标磨合熟练度，分档散布→攻速→人枪合一；换目标/停火消退</summary>
    internal sealed class ErgonomicStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //人机契合米白
        public override Color TintColor => new(230, 220, 180);

        #region 可调参数
        /// <summary>熟练度上限（满层=人枪合一）</summary>
        public const float MaxProficiency = 100f;
        /// <summary>T1 熟悉档，解锁散布收敛</summary>
        public const float Tier1Threshold = 25f;
        /// <summary>T2 顺手档，解锁攻速</summary>
        public const float Tier2Threshold = 60f;
        /// <summary>人枪合一滞回退出线</summary>
        public const float UnityExitThreshold = 85f;
        /// <summary>命中记忆目标熟练度增益（约5.5s满）</summary>
        private const float HitGain = 2f;
        /// <summary>近似同向换绑增益比例</summary>
        private const float DirectionGainRatio = 0.5f;
        /// <summary>近似同向判定锥，约±20°</summary>
        private const float DirectionCone = 0.35f;
        /// <summary>真换目标保留熟练度比例</summary>
        private const float SwitchKeepRatio = 0.30f;
        /// <summary>停火宽限帧数，超过后记忆开始消退</summary>
        private const int IdleGraceFrames = 60;
        /// <summary>消退期每帧熟练度流失（满层约1秒流干）</summary>
        private const float IdleDecayPerTick = 1.6f;
        /// <summary>满熟练散布收敛幅度</summary>
        private const float MaxSpreadReduce = 0.45f;
        /// <summary>满熟练攻速加成</summary>
        private const float MaxAttackSpeed = 0.15f;
        /// <summary>基础法力减免（唯一静态值）</summary>
        private const float BaseManaCostReduce = 0.10f;
        /// <summary>人枪合一命中记忆目标的回蓝量</summary>
        private const int ManaRefundAmount = 2;
        /// <summary>回蓝内置冷却（帧）</summary>
        private const int ManaRefundCooldown = 10;
        /// <summary>人枪合一光束修正转向速率</summary>
        private const float UnitySteerRate = 0.07f;
        /// <summary>修正锥约±60°，锥外不掰头</summary>
        private const float UnitySteerCone = 1.05f;
        #endregion

        //═════ 肌肉记忆状态（per-玩家实例）═════
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

        /// <summary>人枪合一态，滞回退出，残影消费</summary>
        public bool UnityActive => _unityActive;

        //香槟金，区别动量橙/守望蓝白
        internal static readonly Color EchoCore = new(255, 244, 205);
        internal static readonly Color EchoGlow = new(235, 190, 110);
        internal static readonly Color EchoEdge = new(190, 135, 60);

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -BaseManaCostReduce;
            //T1 散布收敛
            if (_prof >= Tier1Threshold) {
                ctx.SpreadMul += -MaxSpreadReduce * TierProgress(_prof, Tier1Threshold);
            }
            //T2 攻速提升
            if (_prof >= Tier2Threshold) {
                ctx.AttackSpeedMul += MaxAttackSpeed * TierProgress(_prof, Tier2Threshold);
            }
        }

        /// <summary>档位内进度 0~1</summary>
        private static float TierProgress(float prof, float threshold)
            => MathHelper.Clamp((prof - threshold) / (MaxProficiency - threshold), 0f, 1f);

        /// <summary>清空肌肉记忆（卸改件时）</summary>
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

        //═════════════ 命中登记 ═════════════

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            //派生束不参与肌肉记忆
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

        /// <summary>同目标全速；近同向半速换绑；真换目标大幅消退</summary>
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
                //白纸，开始磨合
                gain = HitGain;
            }
            else if (MathF.Abs(MathHelper.WrapAngle(angleToTarget - _memoryAngle)) <= DirectionCone) {
                //近同向，半速换绑
                gain = HitGain * DirectionGainRatio;
            }
            else {
                //真换目标，记忆消退
                _prof *= SwitchKeepRatio;
                gain = HitGain;
            }

            _memoryNpcId = target.whoAmI;
            _memoryNpcType = target.type;
            _memoryAngle = angleToTarget;
            _idleTimer = 0;
            _prof = MathHelper.Clamp(_prof + gain, 0f, MaxProficiency);
        }

        /// <summary>人枪合一回蓝，仅本地+内置冷却</summary>
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

        //═════════════ 人枪合一光束修正 ═════════════

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (!_unityActive || _memoryNpcId < 0 || _memoryNpcId >= Main.maxNPCs) {
                return;
            }
            Projectile proj = beam.Projectile;
            //仅本地修正，防远端记忆不一致
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

        //═════════════ 逐帧维护 ═════════════

        public override void OnPlayerUpdate(Player player) {
            if (player == null || !player.active || player.dead) {
                return;
            }
            //久未 tick 清空记忆，防重装带旧层（同 SHPCPlayer 超杀坑）
            uint now = Main.GameUpdateCount;
            if (_lastSeenTick != 0 && now > _lastSeenTick + 5) {
                ResetMemory();
            }
            _lastSeenTick = now;

            //回蓝冷却跟 TimeGear
            TickDown(ref _refundCooldown, ref _refundCarry);

            //目标失效解 id，保留方向与熟练度，同向可续接
            if (_memoryNpcId >= 0) {
                if (_memoryNpcId >= Main.maxNPCs
                    || !Main.npc[_memoryNpcId].active
                    || Main.npc[_memoryNpcId].type != _memoryNpcType) {
                    _memoryNpcId = -1;
                }
            }

            //挥械中视为未停火；TickUp 走 TimeGear
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

            //满层进/滞回退
            if (!_unityActive && _prof >= MaxProficiency) {
                _unityActive = true;
                UnityEnterFX(player);
            }
            else if (_unityActive && _prof < UnityExitThreshold) {
                _unityActive = false;
            }

            //档位跃迁上升沿提示，满层走 UnityEnterFX
            int tier = CurrentTier(_prof);
            if (tier > _lastTier && tier < 3) {
                TierUpFX(player, tier);
            }
            _lastTier = tier;

            //磨合期手部流线
            if (tier >= 1 && !_unityActive && Main.netMode != NetmodeID.Server
                && player.whoAmI == Main.myPlayer && player.ItemAnimationActive
                && player.HeldItem != null && player.HeldItem.type == SHPCOverride.ID
                && player.altFunctionUse != 2 && Main.GameUpdateCount % (10 - tier * 2) == 0) {
                SpawnGripFlow(player);
            }

            //人枪合一残影，仅本地生成
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
            //档位卡入音
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
            //人枪合一环状流线
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

    /// <summary>人枪合一残影，SHPCModErgoEcho，跌出/卸改件淡出</summary>
    internal sealed class SHPCErgoEchoProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int HistLen = 20;
        //三重相位滞后帧
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
                //跌出淡出自灭
                fade -= 0.06f;
                if (fade <= 0f) {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.Center = owner.RotatedRelativePoint(owner.MountedCenter, true);

            //瞄准用同步的 itemRotation，朝左 +π
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

            //枪身流线粒子
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
            //韵律相位约1.2Hz
            float beat = 0.5f + 0.5f * MathF.Sin((float)Main.timeForVisualEffects * 0.125f);
            //待机弱轮廓，开火全开
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

                //旧残影先画
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

            //手臂残影光带
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak != null) {
                for (int i = 0; i < GhostLags.Length; i++) {
                    float lagAim = aimHistory[Math.Min(GhostLags[i], HistLen - 1)];
                    float armAlpha = alphaBase * (0.30f - 0.07f * i) * (0.7f + 0.3f * beat);
                    spriteBatch.Draw(streak, pivot, null, ErgonomicStockModule.EchoGlow * armAlpha, lagAim,
                        new Vector2(0f, streak.Height * 0.5f), new Vector2(0.16f, 0.05f), SpriteEffects.None, 0f);
                }
            }
            //掌心光核
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                spriteBatch.Draw(glow, pivot, null,
                    ErgonomicStockModule.EchoCore * (alphaBase * (0.35f + 0.25f * beat)), 0f,
                    glow.Size() * 0.5f, 0.55f + 0.15f * beat, SpriteEffects.None, 0f);
            }
        }

        private static void DrawWeaponGhost(SpriteBatch sb, Texture2D tex, Vector2 pivot, float aim, bool flipped) {
            //origin 握把，翻转镜像 Y 侧 origin
            Vector2 origin = new(tex.Width * 0.24f, flipped ? tex.Height * 0.38f : tex.Height * 0.62f);
            SpriteEffects flip = flipped ? SpriteEffects.FlipVertically : SpriteEffects.None;
            sb.Draw(tex, pivot, null, Color.White, aim, origin, SHPCOverride.ItemScale, flip, 0f);
        }
    }
}

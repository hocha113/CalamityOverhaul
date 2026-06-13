using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>赛博领域每玩家状态承载</summary>
    public class CyberspacePlayer : ModPlayer
    {
        //是否激活
        public bool Active { get; internal set; }

        //内部强度原值，对外通过 Intensity 暴露
        internal float intensityRaw;

        /// <summary>当前强度，含 RestartCollapse 抑制</summary>
        public float Intensity {
            get => intensityRaw * (1f - MathHelper.Clamp(RestartCollapse, 0f, 1f));
            set => intensityRaw = value;
        }

        /// <summary>重启演出视觉抑制系数</summary>
        public float RestartCollapse { get; set; }

        /// <summary>领域中心</summary>
        public Vector2 DomainCenter { get; private set; }

        //领域中心缓动剩余帧
        private int domainEaseTimer;

        //RAM 崩溃锁定计时
        private int crashLockoutTimer;

        public bool IsCrashLockedOut => crashLockoutTimer > 0;

        //每层独立展开进度
        internal readonly float[] layerExpand = new float[Cyberspace.MaxLayerCount];
        internal readonly int[] layerBurstTimer = new int[Cyberspace.MaxLayerCount];

        /// <summary>当前领域层数</summary>
        public int CurrentLayer { get; internal set; }

        /// <summary>收缩前层数，收缩动画期间仍参与渲染</summary>
        public int RenderLayerCount {
            get {
                int count = 0;
                for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                    if (layerExpand[i] > 0.01f) count = i + 1;
                }
                return count;
            }
        }

        /// <summary>最外层目标半径；Active 只看 CurrentLayer，关闭时回退 RenderLayerCount</summary>
        public float Radius {
            get {
                int rLayer = Active && CurrentLayer > 0 ? CurrentLayer : RenderLayerCount;
                return rLayer > 0 ? Cyberspace.GetLayerRadius(rLayer - 1) : Cyberspace.BaseRadius;
            }
        }

        /// <summary>全局展开进度 = 有效外半径 / 目标外半径</summary>
        public float ExpandProgress {
            get {
                float r = Radius;
                if (r <= 0f) return 0f;
                return MathHelper.Clamp(EffectiveOuterRadius / r, 0f, 1f);
            }
        }

        /// <summary>实际有效最外层半径</summary>
        public float EffectiveOuterRadius {
            get {
                float maxR = 0f;
                for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                    float r = Cyberspace.GetLayerRadius(i) * layerExpand[i];
                    if (r > maxR) maxR = r;
                }
                return maxR * (1f - MathHelper.Clamp(RestartCollapse, 0f, 1f));
            }
        }

        /// <summary>视觉层级，由有效外半径对各层半径插值；边界环样式连续渐变</summary>
        public float VisualTier {
            get {
                float r = EffectiveOuterRadius;
                float prev = Cyberspace.GetLayerRadius(0);
                if (r <= prev) {
                    return 1f;
                }
                for (int i = 1; i < Cyberspace.MaxLayerCount; i++) {
                    float cur = Cyberspace.GetLayerRadius(i);
                    if (r <= cur) {
                        return i + (r - prev) / (cur - prev);
                    }
                    prev = cur;
                }
                return Cyberspace.MaxLayerCount;
            }
        }

        /// <summary>着色器累计时间</summary>
        public float EffectTime { get; private set; }

        /// <summary>玩家移动淡化系数</summary>
        public float MotionFade { get; private set; }

        internal float targetIntensity;

        //环境故障闪电计时器
        private int ambientBoltTimer;

        //上次关闭前的层数
        private int lastLayer = 1;

        //同帧内防重入开关
        private long lastManualToggleFrame = -1;

        //是否因切换出 SHPC 而自动收起：切回 SHPC 时据此自动展开，避免玩家被迫重新点开
        private bool autoSuspendedBySwap;

        //极速换武器静默窗口计时（帧）：> 0 时重新激活会跳过 VFX/音效，避免反复切换时的演出鬼畜感
        private int swapSilenceTimer;
        //静默窗口长度，约 0.25 秒，覆盖正常鼠标滚轮的操作间隔
        private const int SwapSilenceFrames = 15;

        public float GetLayerExpand(int layerIndex) {
            layerIndex = Math.Clamp(layerIndex, 0, Cyberspace.MaxLayerCount - 1);
            return layerExpand[layerIndex];
        }

        /// <summary>RAM 余量能否维持指定层最低秒数</summary>
        public bool CanAffordLayer(int layer) {
            if (HackTime.InfiniteHack) {
                return true;
            }
            if (layer < 1 || layer > Cyberspace.MaxLayerCount) {
                return false;
            }
            float required = Cyberspace.LayerRamDrainPerSecond[layer - 1] * Cyberspace.MinSustainSeconds;
            return RamSystem.CurrentRam >= required;
        }

        /// <summary>同帧防重入手动切换</summary>
        public bool Toggle() {
            long frame = (long)Main.GameUpdateCount;
            if (lastManualToggleFrame == frame) {
                return false;
            }
            lastManualToggleFrame = frame;

            //手动切换属于明确的玩家意图，必须清掉自动挂起标志，避免下一帧自动重开
            autoSuspendedBySwap = false;
            if (Active) {
                Deactivate();
            }
            else {
                Activate();
            }
            return true;
        }

        /// <summary>激活第一层或恢复上次层数</summary>
        public void Activate() {
            //崩溃锁定期内拒绝
            if (crashLockoutTimer > 0) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.45f, Pitch = -0.4f }, Player.Center);
                }
                return;
            }

            int resumeLayer = Math.Clamp(lastLayer, 1, Cyberspace.MaxLayerCount);

            if (!HackTime.InfiniteHack) {
                while (resumeLayer >= 1 && !CanAffordLayer(resumeLayer)) {
                    resumeLayer--;
                }
                if (resumeLayer < 1) {
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.45f, Pitch = -0.4f }, Player.Center);
                        RamSystem.NotifyInsufficient();
                        Color denyColor = new(255, 90, 80);
                        CombatText.NewText(Player.Hitbox, denyColor, "// LOW RAM", true);
                    }
                    return;
                }
            }

            for (int i = resumeLayer; i < Cyberspace.MaxLayerCount; i++) {
                layerBurstTimer[i] = 0;
            }
            Active = true;
            CurrentLayer = resumeLayer;
            targetIntensity = 1f;

            //静默窗口内重新激活：只做平滑插值展开，跳过爆发动画/闪电/音效，避免快速切换时的演出鬼畜感
            bool silent = swapSilenceTimer > 0;
            if (!silent) {
                for (int i = 0; i < resumeLayer; i++) {
                    layerBurstTimer[i] = Cyberspace.BurstDurations[i];
                }
                SpawnActivationVFX();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent, Player.Center);
                    SoundEngine.PlaySound(CWRSound.Faultrelease, Player.Center);
                }
            }
        }

        /// <summary>升降层</summary>
        public void SetLayer(int layer) {
            layer = Math.Clamp(layer, 1, Cyberspace.MaxLayerCount);
            if (!Active) return;
            if (layer == CurrentLayer) return;

            if (layer > CurrentLayer && !CanAffordLayer(layer)) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.4f, Pitch = -0.3f }, Player.Center);
                    RamSystem.NotifyInsufficient();
                    Color denyColor = new(255, 90, 80);
                    CombatText.NewText(Player.Hitbox, denyColor, $"// L{layer} - LOW RAM", true);
                }
                return;
            }

            int oldLayer = CurrentLayer;
            CurrentLayer = layer;

            if (layer > oldLayer) {
                for (int i = oldLayer; i < layer; i++) {
                    layerBurstTimer[i] = Cyberspace.BurstDurations[i];
                }
                SpawnLayerVFX(oldLayer, layer);
            }
        }

        /// <summary>关闭领域；silent 跳过关闭音效</summary>
        public void Deactivate(bool silent = false) {
            Active = false;
            targetIntensity = 0f;
            if (CurrentLayer > 0) {
                lastLayer = CurrentLayer;
            }
            CurrentLayer = 0;
            for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                layerBurstTimer[i] = 0;
            }

            if (!silent && !VaultUtils.isServer && Player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(CWRSound.Faultrelease, Player.Center);
            }
        }

        /// <summary>RAM 耗尽系统崩溃</summary>
        public void TriggerSystemCrash() {
            if (!Active && CurrentLayer == 0 && crashLockoutTimer > 0) {
                return;
            }

            Deactivate();
            crashLockoutTimer = Cyberspace.CrashLockoutFrames;

            if (!VaultUtils.isServer && Player.whoAmI == Main.myPlayer) {
                Color crashColor = new(255, 70, 70);
                CombatText.NewText(Player.Hitbox, crashColor, "// SYSTEM CRASH", true);
                SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.85f, Pitch = -0.3f }, Player.Center);
                SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.7f, Pitch = -0.5f }, Player.Center);
            }
        }

        /// <summary>主更新；远端仅视觉插值</summary>
        public void Update() {
            //远端玩家只跑视觉插值，状态机（RAM消耗/崩溃/切枪检测）由其本机负责
            if (Player.whoAmI != Main.myPlayer) {
                UpdateRemoteVisuals();
                return;
            }

            //SHPC 上下文校验：切出武器自动挂起、切回武器自动恢复，避免给玩家制造"卡手重开"的感觉
            bool holdingShpc = Player.HeldItem.type == SHPCOverride.ID;
            if (Active && !holdingShpc) {
                //静默窗口内的关闭不再播放关闭音效，避免快速切换时重复叠声
                bool silentClose = swapSilenceTimer > 0;
                autoSuspendedBySwap = true;
                swapSilenceTimer = SwapSilenceFrames;
                Deactivate(silentClose);
            }
            else if (!Active && holdingShpc && autoSuspendedBySwap && crashLockoutTimer == 0) {
                //切回 SHPC：尝试恢复到挂起前的层数；若 RAM/锁定不足，Activate 内部会自行兜底
                autoSuspendedBySwap = false;
                Activate();
            }

            if (swapSilenceTimer > 0) {
                swapSilenceTimer--;
            }

            if (crashLockoutTimer > 0) {
                crashLockoutTimer--;
            }

            UpdateDomainCenter();

            if (Active && CurrentLayer >= 1 && !HackTime.InfiniteHack) {
                float drain = Cyberspace.LayerRamDrainPerSecond[CurrentLayer - 1] * TimeGear.TimeScale;
                RamSystem.ConsumeOverTime(drain);
                if (RamSystem.CurrentRam <= 0f) {
                    TriggerSystemCrash();
                }
            }

            float dt = 1f / 60f;
            float timeSpeed = TimeGear.IsTimeSlowed ? MathHelper.Max(TimeGear.TimeScale, 0.1f) : 1f;
            EffectTime += dt * timeSpeed;

            UpdateMotionFade();

            float intensityLerp;
            if (Active && CurrentLayer > 0) {
                intensityLerp = 0.045f;
                if (layerBurstTimer[0] > 0) {
                    float burstFactor = (float)layerBurstTimer[0] / Cyberspace.BurstDurations[0];
                    intensityLerp = MathHelper.Lerp(0.08f, 0.25f, burstFactor);
                }
            }
            else {
                intensityLerp = 0.015f;
            }
            intensityRaw = MathHelper.Lerp(intensityRaw, targetIntensity, intensityLerp);

            for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                float target = (i < CurrentLayer) ? 1f : 0f;
                int burstDur = Cyberspace.BurstDurations[i];

                if (layerBurstTimer[i] > 0) {
                    layerBurstTimer[i]--;
                    float burstFactor = (float)layerBurstTimer[i] / burstDur;
                    float burstLerpMin = MathHelper.Lerp(0.06f, 0.025f, (float)i / (Cyberspace.MaxLayerCount - 1));
                    float burstLerpMax = MathHelper.Lerp(0.22f, 0.10f, (float)i / (Cyberspace.MaxLayerCount - 1));
                    float expandLerp = MathHelper.Lerp(burstLerpMin, burstLerpMax, burstFactor);
                    layerExpand[i] = MathHelper.Lerp(layerExpand[i], target, expandLerp);
                }
                else {
                    float expandLerp = target > 0f ? Cyberspace.ExpandLerps[i] : Cyberspace.ContractLerps[i];
                    layerExpand[i] = MathHelper.Lerp(layerExpand[i], target, expandLerp);
                }

                if (target <= 0f && layerExpand[i] < 0.005f)
                    layerExpand[i] = 0f;
            }

            if (CurrentLayer >= 2 && intensityRaw > 0.5f && RestartCollapse < 0.2f) {
                ambientBoltTimer--;
                if (ambientBoltTimer <= 0) {
                    SpawnAmbientBolts();
                    ambientBoltTimer = 40 + Main.rand.Next(-8, 12);
                }
            }

            if (!Active && CurrentLayer == 0) {
                bool allCollapsed = true;
                for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                    if (layerExpand[i] >= 0.005f) {
                        allCollapsed = false;
                        break;
                    }
                }
                if (allCollapsed && intensityRaw < 0.005f) {
                    intensityRaw = 0f;
                    EffectTime = 0f;
                    ambientBoltTimer = 0;
                }
            }
        }

        /// <summary>立即重置全部状态</summary>
        public void Reset() {
            Active = false;
            intensityRaw = 0f;
            RestartCollapse = 0f;
            EffectTime = 0f;
            MotionFade = 0f;
            CurrentLayer = 0;
            lastLayer = 1;
            lastManualToggleFrame = -1;
            autoSuspendedBySwap = false;
            swapSilenceTimer = 0;
            targetIntensity = 0f;
            ambientBoltTimer = 0;
            crashLockoutTimer = 0;
            DomainCenter = Vector2.Zero;
            domainEaseTimer = 0;
            for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                layerExpand[i] = 0f;
                layerBurstTimer[i] = 0;
            }
        }

        /// <summary>领域中心暂留出发点</summary>
        public void NotifyTeleport(Vector2 anchorCenter) {
            DomainCenter = anchorCenter;
            domainEaseTimer = Cyberspace.DomainEaseTotal;
        }

        public bool IsInsideDomain(Vector2 worldPos) {
            if (Intensity < 0.01f) return false;
            float dx = worldPos.X - DomainCenter.X;
            float dy = worldPos.Y - DomainCenter.Y;
            return dx * dx + dy * dy <= EffectiveOuterRadius * EffectiveOuterRadius;
        }

        private void UpdateDomainCenter() {
            if (Player == null || !Player.active) {
                domainEaseTimer = 0;
                return;
            }

            Vector2 target = Player.Center;
            if (!Active && Intensity < 0.001f && domainEaseTimer == 0) {
                DomainCenter = target;
                return;
            }

            if (domainEaseTimer > 0) {
                float remain = (float)domainEaseTimer / Cyberspace.DomainEaseTotal;
                float prog = 1f - remain;
                float lerpRate = MathHelper.Lerp(0.06f, 0.25f, MathF.Pow(prog, 0.65f));
                DomainCenter = Vector2.Lerp(DomainCenter, target, lerpRate);
                domainEaseTimer--;
                if (domainEaseTimer == 0) {
                    DomainCenter = target;
                }
            }
            else {
                DomainCenter = target;
            }
        }

        private void UpdateMotionFade() {
            float target = 0f;
            if (Intensity > 0.001f) {
                if (Player != null && Player.active && !Player.dead) {
                    float speed = Player.velocity.Length();
                    target = MathHelper.Clamp(speed / Cyberspace.MotionFadeFullSpeed, 0f, 1f);
                }
            }

            float lerpRate = target > MotionFade ? 0.18f : 0.06f;
            MotionFade = MathHelper.Lerp(MotionFade, target, lerpRate);
            if (MotionFade < 0.001f) {
                MotionFade = 0f;
            }
        }

        private void SpawnActivationVFX() {
            if (Main.myPlayer != Player.whoAmI) return;

            IEntitySource source = Player.GetSource_FromThis();
            Vector2 center = Player.Center;

            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0, Player.whoAmI);

            //闪电数量收敛，激活演出重点交给冲击波与边界环展开
            int boltCount = Main.rand.Next(5, 7);
            float baseAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
            for (int i = 0; i < boltCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / boltCount
                    + Main.rand.NextFloat(-0.28f, 0.28f);
                int delay = Main.rand.Next(0, 5);
                Projectile.NewProjectile(source, center, Vector2.Zero,
                    ModContent.ProjectileType<CyberGlitchBoltProj>(), 0, 0, Player.whoAmI,
                    ai0: angle, ai1: delay);
            }
        }

        private void SpawnLayerVFX(int oldLayer, int newLayer) {
            if (Main.myPlayer != Player.whoAmI) return;

            IEntitySource source = Player.GetSource_FromThis();
            Vector2 center = Player.Center;

            //冲击波从旧层边界扫掠到新层边界，与单环边界的连续形变同步，读作"领域生长"
            float sweepStart = oldLayer >= 1 ? Cyberspace.GetLayerRadius(oldLayer - 1) : 0f;
            float sweepEnd = Cyberspace.GetLayerRadius(newLayer - 1);
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0, Player.whoAmI,
                ai0: sweepStart, ai1: sweepEnd);

            //闪电数量随层级温和递增，不再翻倍堆叠
            int boltCount = Main.rand.Next(3 + newLayer, 6 + newLayer);
            float baseAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
            for (int i = 0; i < boltCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / boltCount
                    + Main.rand.NextFloat(-0.28f, 0.28f);
                int delay = Main.rand.Next(0, 4);
                Projectile.NewProjectile(source, center, Vector2.Zero,
                    ModContent.ProjectileType<CyberGlitchBoltProj>(), 0, 0, Player.whoAmI,
                    ai0: angle, ai1: delay);
            }
        }

        private void SpawnAmbientBolts() {
            if (Main.myPlayer != Player.whoAmI) return;
            if (Player == null || !Player.active) return;

            IEntitySource source = Player.GetSource_FromThis();
            float outerR = EffectiveOuterRadius;
            Vector2 center = DomainCenter;

            int count = Main.rand.Next(1, 1 + CurrentLayer);
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                float spawnDist = outerR * Main.rand.NextFloat(0.4f, 0.85f);
                Vector2 spawnPos = center + angle.ToRotationVector2() * spawnDist;
                int delay = Main.rand.Next(0, 6);
                Projectile.NewProjectile(source, spawnPos, Vector2.Zero,
                    ModContent.ProjectileType<CyberGlitchBoltProj>(), 0, 0, Player.whoAmI,
                    ai0: angle, ai1: delay);
            }
        }

        //仅跑远端玩家的视觉插值，不触碰状态机
        private void UpdateRemoteVisuals() {
            if (crashLockoutTimer > 0) crashLockoutTimer--;

            DomainCenter = Player.Center;

            float dt = 1f / 60f;
            float timeSpeed = TimeGear.IsTimeSlowed ? MathHelper.Max(TimeGear.TimeScale, 0.1f) : 1f;
            EffectTime += dt * timeSpeed;

            float intensityLerp = Active && CurrentLayer > 0 ? 0.045f : 0.015f;
            intensityRaw = MathHelper.Lerp(intensityRaw, targetIntensity, intensityLerp);

            for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                float target = i < CurrentLayer ? 1f : 0f;
                int burstDur = Cyberspace.BurstDurations[i];
                if (layerBurstTimer[i] > 0) {
                    layerBurstTimer[i]--;
                    float burstFactor = (float)layerBurstTimer[i] / burstDur;
                    float bMin = MathHelper.Lerp(0.06f, 0.025f, (float)i / (Cyberspace.MaxLayerCount - 1));
                    float bMax = MathHelper.Lerp(0.22f, 0.10f, (float)i / (Cyberspace.MaxLayerCount - 1));
                    layerExpand[i] = MathHelper.Lerp(layerExpand[i], target, MathHelper.Lerp(bMin, bMax, burstFactor));
                }
                else {
                    float expandLerp = target > 0f ? Cyberspace.ExpandLerps[i] : Cyberspace.ContractLerps[i];
                    layerExpand[i] = MathHelper.Lerp(layerExpand[i], target, expandLerp);
                }
                if (target <= 0f && layerExpand[i] < 0.005f) layerExpand[i] = 0f;
            }

            float motionTarget = 0f;
            if (Intensity > 0.001f && Player != null && Player.active && !Player.dead) {
                float speed = Player.velocity.Length();
                motionTarget = MathHelper.Clamp(speed / Cyberspace.MotionFadeFullSpeed, 0f, 1f);
            }
            float motionLerp = motionTarget > MotionFade ? 0.18f : 0.06f;
            MotionFade = MathHelper.Lerp(MotionFade, motionTarget, motionLerp);
            if (MotionFade < 0.001f) MotionFade = 0f;
        }

        //原子写入从网络包收到的远端权威状态
        internal void ApplyRemoteState(bool active, int currentLayer, float restartCollapse) {
            int prevLayer = CurrentLayer;
            Active = active;
            //层数增加时在远端侧触发爆发动画，让视觉效果与本机一致
            if (currentLayer > prevLayer) {
                for (int i = prevLayer; i < currentLayer && i < Cyberspace.MaxLayerCount; i++) {
                    layerBurstTimer[i] = Cyberspace.BurstDurations[i];
                }
            }
            CurrentLayer = currentLayer;
            RestartCollapse = restartCollapse;
            targetIntensity = active && currentLayer > 0 ? 1f : 0f;
        }

        //快照字段，供 CopyClientState/SendClientChanges 对比用
        private bool _snapActive;
        private int _snapCurrentLayer;
        private float _snapRestartCollapse;

        public override void CopyClientState(ModPlayer targetCopy) {
            CyberspacePlayer copy = (CyberspacePlayer)targetCopy;
            copy._snapActive = Active;
            copy._snapCurrentLayer = CurrentLayer;
            copy._snapRestartCollapse = RestartCollapse;
        }

        /// <summary>加入/重连全量同步，避免 IsInsideDomain 误判</summary>
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberspaceStateSync);
            packet.Write((byte)Player.whoAmI);
            packet.Write(Active);
            packet.Write((byte)CurrentLayer);
            packet.Write(RestartCollapse);
            packet.Send(toWho, fromWho);
        }

        public override void SendClientChanges(ModPlayer clientPlayer) {
            if (VaultUtils.isSinglePlayer) return;

            CyberspacePlayer snap = (CyberspacePlayer)clientPlayer;

            bool changed = snap._snapActive != Active
                || snap._snapCurrentLayer != CurrentLayer
                || MathF.Abs(snap._snapRestartCollapse - RestartCollapse) > 0.04f;

            if (!changed) return;

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberspaceStateSync);
            packet.Write((byte)Player.whoAmI);
            packet.Write(Active);
            packet.Write((byte)CurrentLayer);
            packet.Write(RestartCollapse);
            packet.Send();
        }

        internal static void HandleNetSync(BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadByte();
            bool active = reader.ReadBoolean();
            int currentLayer = reader.ReadByte();
            float restartCollapse = reader.ReadSingle();

            if (playerIndex >= 0 && playerIndex < Main.maxPlayers) {
                Player p = Main.player[playerIndex];
                if (p != null && p.active) {
                    p.GetModPlayer<CyberspacePlayer>().ApplyRemoteState(active, currentLayer, restartCollapse);
                }
            }

            //服务端转发给其他所有客户端
            if (VaultUtils.isServer) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.CyberspaceStateSync);
                packet.Write((byte)playerIndex);
                packet.Write(active);
                packet.Write((byte)currentLayer);
                packet.Write(restartCollapse);
                packet.Send(-1, whoAmI);
            }
        }
    }
}

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
    /// <summary>
    /// 领域演出提示。多人下开关领域跑在服务端，音效与特效在那边发不出来，
    /// 只能随权威状态包告诉各客户端"这一次是真的变了"。
    /// <br/>重同步快照（入世、补发、回放）必须留 <see cref="None"/>，否则演出会被反复重播
    /// </summary>
    internal enum CyberspaceCue : byte
    {
        None,
        Activate,
        Deactivate,
        LayerUp,
        Crash,
    }

    /// <summary>赛博领域每玩家状态承载</summary>
    public class CyberspacePlayer : ModPlayer
    {
        public bool Active { get; internal set; }
        internal uint AuthorityRevision { get; private set; } = 1;

        //强度原值，对外 Intensity
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

        //中心缓动剩余帧
        private int domainEaseTimer;

        //崩溃锁定计时
        private int crashLockoutTimer;
        private float crashLockoutCarry;

        public bool IsCrashLockedOut => crashLockoutTimer > 0;

        //每层展开进度
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

        /// <summary>最外目标半径，关时回退 RenderLayerCount</summary>
        public float Radius {
            get {
                int rLayer = Active && CurrentLayer > 0 ? CurrentLayer : RenderLayerCount;
                return rLayer > 0 ? Cyberspace.GetLayerRadius(rLayer - 1) : Cyberspace.BaseRadius;
            }
        }

        /// <summary>展开进度=有效外半径/目标</summary>
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

        /// <summary>视觉层，外半径插值</summary>
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

        /// <summary>
        /// 三层几何权重(方格/蜂巢/流场)，由 <see cref="VisualTier"/> 平滑推导，恒和为 1。
        /// <br/>着色器内三套几何全算后按此加权混合，规避全屏 effect 的动态分支禁令
        /// </summary>
        public Vector3 TierWeights {
            get {
                float tier = VisualTier;
                float t2 = MathHelper.Clamp(tier - 1f, 0f, 1f);
                float t3 = MathHelper.Clamp(tier - 2f, 0f, 1f);
                t2 = t2 * t2 * (3f - 2f * t2);
                t3 = t3 * t3 * (3f - 2f * t3);
                //t3>0 时必有 t2==1，故三项恒和为 1
                return new Vector3(1f - t2, t2 * (1f - t3), t3);
            }
        }

        /// <summary>着色器累计时间</summary>
        public float EffectTime { get; private set; }

        /// <summary>玩家移动淡化系数</summary>
        public float MotionFade { get; private set; }

        internal float targetIntensity;

        //环境故障雷计时
        private int ambientBoltTimer;

        //关闭前层数
        private int lastLayer = 1;

        //同帧防重入
        private long lastManualToggleFrame = -1;

        //切出 SHPC 自动收起标记，切回据此展开
        private bool autoSuspendedBySwap;

        //换武静默窗（帧），>0 重激活跳过 VFX/音效
        private int swapSilenceTimer;
        //静默窗约0.25s
        private const int SwapSilenceFrames = 15;

        public float GetLayerExpand(int layerIndex) {
            layerIndex = Math.Clamp(layerIndex, 0, Cyberspace.MaxLayerCount - 1);
            return layerExpand[layerIndex];
        }

        /// <summary>RAM 余量能否维持指定层最低秒数</summary>
        public bool CanAffordLayer(int layer) {
            if (HackTime.InfiniteHackAuthority
                || Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
                    && HackTime.InfiniteHack) {
                return true;
            }
            if (layer < 1 || layer > Cyberspace.MaxLayerCount) {
                return false;
            }
            float required = Cyberspace.LayerRamDrainPerSecond[layer - 1] * Cyberspace.MinSustainSeconds;
            return RamSystem.CanAfford(Player, required);
        }

        /// <summary>同帧防重入手动切换</summary>
        public bool Toggle() {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                return CyberspaceActionNet.SendDomainRequest(Player,
                    CyberspaceActionKind.Toggle, 0, Vector2.Zero);
            }
            long frame = (long)Main.GameUpdateCount;
            if (lastManualToggleFrame == frame) {
                return false;
            }
            lastManualToggleFrame = frame;

            //手动切换清自动挂起
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
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                CyberspaceActionNet.SendDomainRequest(Player,
                    CyberspaceActionKind.Activate, 0, Vector2.Zero);
                return;
            }
            ActivateAuthority();
        }

        internal void ActivateAuthority() {
            //崩溃锁定拒
            if (crashLockoutTimer > 0) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.45f, Pitch = -0.4f }, Player.Center);
                }
                return;
            }

            int resumeLayer = Math.Clamp(lastLayer, 1, Cyberspace.MaxLayerCount);

            if (!HackTime.InfiniteHackAuthority) {
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

            //静默窗内重激活，仅插值展开
            bool silent = swapSilenceTimer > 0;
            if (!silent) {
                for (int i = 0; i < resumeLayer; i++) {
                    layerBurstTimer[i] = Cyberspace.BurstDurations[i];
                }
                PlayActivateCue();
            }
            CommitAuthorityState(silent ? CyberspaceCue.None : CyberspaceCue.Activate);
        }

        /// <summary>升降层</summary>
        public bool SetLayer(int layer) {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                return CyberspaceActionNet.SendDomainRequest(Player,
                    CyberspaceActionKind.SetLayer, Math.Clamp(layer, 1,
                        Cyberspace.MaxLayerCount), Vector2.Zero);
            }
            return SetLayerAuthority(layer);
        }

        internal bool SetLayerAuthority(int layer) {
            layer = Math.Clamp(layer, 1, Cyberspace.MaxLayerCount);
            if (!Active) return false;
            if (layer == CurrentLayer) return true;

            if (layer > CurrentLayer && !CanAffordLayer(layer)) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.4f, Pitch = -0.3f }, Player.Center);
                    RamSystem.NotifyInsufficient();
                    Color denyColor = new(255, 90, 80);
                    CombatText.NewText(Player.Hitbox, denyColor, $"// L{layer} - LOW RAM", true);
                }
                return false;
            }

            int oldLayer = CurrentLayer;
            CurrentLayer = layer;

            bool raised = layer > oldLayer;
            if (raised) {
                for (int i = oldLayer; i < layer; i++) {
                    layerBurstTimer[i] = Cyberspace.BurstDurations[i];
                }
                SpawnLayerVFX(oldLayer, layer);
            }
            CommitAuthorityState(raised ? CyberspaceCue.LayerUp : CyberspaceCue.None);
            return true;
        }

        /// <summary>关闭领域；silent 跳过关闭音效</summary>
        public void Deactivate(bool silent = false) {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                CyberspaceActionNet.SendDomainRequest(Player,
                    CyberspaceActionKind.Deactivate, 0, Vector2.Zero);
                return;
            }
            DeactivateAuthority(silent);
        }

        internal void DeactivateAuthority(bool silent = false) {
            bool changed = Active || CurrentLayer > 0;
            Active = false;
            targetIntensity = 0f;
            if (CurrentLayer > 0) {
                lastLayer = CurrentLayer;
            }
            CurrentLayer = 0;
            for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                layerBurstTimer[i] = 0;
            }

            //本来就没开就不必演出，也不占一次状态广播
            if (!changed) {
                return;
            }
            if (!silent) {
                PlayDeactivateCue();
            }
            CommitAuthorityState(silent ? CyberspaceCue.None : CyberspaceCue.Deactivate);
        }

        /// <summary>RAM 耗尽系统崩溃</summary>
        public void TriggerSystemCrash() {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient
                && Player.whoAmI == Main.myPlayer) {
                return;
            }
            TriggerSystemCrashAuthority();
        }

        internal void TriggerSystemCrashAuthority() {
            if (!Active && CurrentLayer == 0 && crashLockoutTimer > 0) {
                return;
            }

            DeactivateAuthority();
            crashLockoutTimer = Cyberspace.CrashLockoutFrames;
            crashLockoutCarry = 0f;

            PlayCrashCue();
            CommitAuthorityState(CyberspaceCue.Crash);
        }

        /// <summary>主更新；远端仅视觉插值</summary>
        public void Update() {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) {
                UpdateRemoteVisuals();
                return;
            }

            //切出挂起、切回恢复
            bool holdingShpc = Player.HeldItem.type == SHPCOverride.ID;
            if (Active && !holdingShpc) {
                //静默窗关闭无音
                bool silentClose = swapSilenceTimer > 0;
                autoSuspendedBySwap = true;
                swapSilenceTimer = SwapSilenceFrames;
                DeactivateAuthority(silentClose);
            }
            else if (!Active && holdingShpc && autoSuspendedBySwap && crashLockoutTimer == 0) {
                //切回恢复挂起层，不足由 Activate 兜底
                autoSuspendedBySwap = false;
                ActivateAuthority();
            }

            if (swapSilenceTimer > 0) {
                swapSilenceTimer--;
            }

            if (crashLockoutTimer > 0) {
                TimeGear.ConsumeFrames(ref crashLockoutTimer, ref crashLockoutCarry);
            }

            UpdateDomainCenter();

            if (Active && CurrentLayer >= 1
                && !HackTime.InfiniteHackAuthority) {
                float drain = Cyberspace.LayerRamDrainPerSecond[CurrentLayer - 1] * TimeGear.TimeScale;
                RamSystem.TryConsumeOverTime(Player, drain, out _);
                if (Player.GetModPlayer<RAMPlayer>().CurrentRam <= 0f) {
                    TriggerSystemCrashAuthority();
                }
            }

            float dt = 1f / 60f;
            EffectTime += dt * TimeGear.TimeScale;

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

            UpdateAmbientBolts();

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
            crashLockoutCarry = 0f;
            DomainCenter = Vector2.Zero;
            domainEaseTimer = 0;
            AuthorityRevision = 1;
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

        /// <summary>
        /// 展开演出。声音按施术者位置播，本机自带距离衰减，所以同场的人都能听见近处开域；
        /// 投射物只由 owner 端生成，靠 <see cref="Projectile.NewProjectile"/> 自己的同步包铺给其他人
        /// </summary>
        private void PlayActivateCue() {
            if (Main.dedServ) {
                return;
            }
            SpawnActivationVFX();
            SoundEngine.PlaySound(CWRSound.FailureCurrent, Player.Center);
            SoundEngine.PlaySound(CWRSound.Faultrelease, Player.Center);
        }

        private void PlayDeactivateCue() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.Faultrelease, Player.Center);
        }

        private void PlayCrashCue() {
            if (Main.dedServ) {
                return;
            }
            //崩溃提示是自己的状态播报，只给本人
            if (Player.whoAmI == Main.myPlayer) {
                Color crashColor = new(255, 70, 70);
                CombatText.NewText(Player.Hitbox, crashColor, "// SYSTEM CRASH", true);
            }
            SoundEngine.PlaySound(CWRSound.FailureCurrent with { Volume = 0.85f, Pitch = -0.3f }, Player.Center);
            SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.7f, Pitch = -0.5f }, Player.Center);
        }

        //收到权威变更时补演出，权威端自己已经放过的那一次不会再走到这里
        private void PlayCue(CyberspaceCue cue, int prevLayer) {
            switch (cue) {
                case CyberspaceCue.Activate:
                    PlayActivateCue();
                    break;
                case CyberspaceCue.Deactivate:
                    PlayDeactivateCue();
                    break;
                case CyberspaceCue.LayerUp:
                    if (CurrentLayer > prevLayer) {
                        SpawnLayerVFX(prevLayer, CurrentLayer);
                    }
                    break;
                case CyberspaceCue.Crash:
                    PlayCrashCue();
                    break;
            }
        }

        private void SpawnActivationVFX() {
            if (Main.myPlayer != Player.whoAmI) return;

            IEntitySource source = Player.GetSource_FromThis();
            Vector2 center = Player.Center;

            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0, Player.whoAmI);

            //激活雷数收敛
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

            //冲击波旧边→新边扫掠
            float sweepStart = oldLayer >= 1 ? Cyberspace.GetLayerRadius(oldLayer - 1) : 0f;
            float sweepEnd = Cyberspace.GetLayerRadius(newLayer - 1);
            Projectile.NewProjectile(source, center, Vector2.Zero,
                ModContent.ProjectileType<CyberShockwaveProj>(), 0, 0, Player.whoAmI,
                ai0: sweepStart, ai1: sweepEnd);

            //雷数随层温和递增
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

        /// <summary>
        /// 环境故障雷。多人下权威在服务端，那边生成不了也发不出同步包，
        /// 所以改由 owner 端按同步来的层数自己推进，非 owner 直接退出，不空转随机数
        /// </summary>
        private void UpdateAmbientBolts() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (CurrentLayer >= 2 && intensityRaw > 0.5f && RestartCollapse < 0.2f) {
                ambientBoltTimer--;
                if (ambientBoltTimer <= 0) {
                    SpawnAmbientBolts();
                    ambientBoltTimer = 40 + Main.rand.Next(-8, 12);
                }
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

        //远端仅视觉插值
        private void UpdateRemoteVisuals() {
            if (crashLockoutTimer > 0) {
                TimeGear.ConsumeFrames(ref crashLockoutTimer, ref crashLockoutCarry);
            }

            DomainCenter = Player.Center;

            float dt = 1f / 60f;
            EffectTime += dt * TimeGear.TimeScale;

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

            UpdateAmbientBolts();

            float motionTarget = 0f;
            if (Intensity > 0.001f && Player != null && Player.active && !Player.dead) {
                float speed = Player.velocity.Length();
                motionTarget = MathHelper.Clamp(speed / Cyberspace.MotionFadeFullSpeed, 0f, 1f);
            }
            float motionLerp = motionTarget > MotionFade ? 0.18f : 0.06f;
            MotionFade = MathHelper.Lerp(MotionFade, motionTarget, motionLerp);
            if (MotionFade < 0.001f) MotionFade = 0f;
        }

        internal void ApplyRemoteState(uint revision, bool active,
            int currentLayer, float restartCollapse, int crashLockout,
            CyberspaceCue cue) {
            if (revision == 0 || currentLayer < 0
                || currentLayer > Cyberspace.MaxLayerCount
                || active != currentLayer > 0
                || !float.IsFinite(restartCollapse)
                || restartCollapse < 0f || restartCollapse > 1f
                || crashLockout < 0
                || crashLockout > Cyberspace.CrashLockoutFrames
                || !IsRevisionAtLeast(revision, AuthorityRevision)) {
                return;
            }
            int prevLayer = CurrentLayer;
            //同版本重发只更新状态：补发的包哪怕带着提示也不再演一遍
            bool advanced = revision != AuthorityRevision;
            AuthorityRevision = revision;
            Active = active;
            //远端升层播爆发
            if (currentLayer > prevLayer) {
                for (int i = prevLayer; i < currentLayer && i < Cyberspace.MaxLayerCount; i++) {
                    layerBurstTimer[i] = Cyberspace.BurstDurations[i];
                }
            }
            CurrentLayer = currentLayer;
            RestartCollapse = restartCollapse;
            crashLockoutTimer = crashLockout;
            crashLockoutCarry = 0f;
            targetIntensity = active && currentLayer > 0 ? 1f : 0f;
            //演出放在状态落地之后：冲击波要按新的层半径取扫掠范围
            PlayCue(advanced ? cue : CyberspaceCue.None, prevLayer);
        }

        /// <summary>加入/重连全量同步</summary>
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) {
                SendAuthorityState(toWho);
            }
        }

        /// <summary>
        /// 下发权威状态。<paramref name="cue"/> 只由真实状态变更填写，
        /// 入世补发与请求回放一律留空，否则收包端会把同一次开域重播一遍
        /// </summary>
        internal void SendAuthorityState(int toWho = -1,
            CyberspaceCue cue = CyberspaceCue.None) {
            if (Main.netMode != Terraria.ID.NetmodeID.Server
                || Player?.active != true) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.CyberspaceStateSync);
            packet.Write((byte)Player.whoAmI);
            packet.Write(AuthorityRevision);
            packet.Write(Active);
            packet.Write((byte)CurrentLayer);
            packet.Write(MathHelper.Clamp(RestartCollapse, 0f, 1f));
            packet.Write((ushort)Math.Clamp(crashLockoutTimer, 0,
                Cyberspace.CrashLockoutFrames));
            packet.Write((byte)cue);
            packet.Send(toWho);
        }

        internal static void HandleNetSync(BinaryReader reader, int whoAmI) {
            if (reader == null
                || Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) {
                return;
            }
            try {
                int playerIndex = reader.ReadByte();
                uint revision = reader.ReadUInt32();
                bool active = reader.ReadBoolean();
                int currentLayer = reader.ReadByte();
                float restartCollapse = reader.ReadSingle();
                int crashLockout = reader.ReadUInt16();
                //越界的提示当没有：不能因为一个演出字节丢掉整包状态
                CyberspaceCue cue = (CyberspaceCue)reader.ReadByte();
                if (cue > CyberspaceCue.Crash) {
                    cue = CyberspaceCue.None;
                }
                if (playerIndex >= 0 && playerIndex < Main.maxPlayers) {
                    Player player = Main.player[playerIndex];
                    if (player?.active == true) {
                        player.GetModPlayer<CyberspacePlayer>()
                            .ApplyRemoteState(revision, active, currentLayer,
                                restartCollapse, crashLockout, cue);
                    }
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private void CommitAuthorityState(CyberspaceCue cue = CyberspaceCue.None) {
            if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) {
                return;
            }
            AuthorityRevision++;
            if (AuthorityRevision == 0) {
                AuthorityRevision = 1;
            }
            SendAuthorityState(cue: cue);
        }

        private static bool IsRevisionAtLeast(uint candidate, uint baseline)
            => candidate == baseline || unchecked((int)(candidate - baseline)) > 0;
    }
}

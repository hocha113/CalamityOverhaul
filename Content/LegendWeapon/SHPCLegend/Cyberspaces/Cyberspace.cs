using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>赛博领域静态门面，状态在 CyberspacePlayer；静态属性默认本地玩家</summary>
    internal class Cyberspace : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        //====== 常量配置（跨玩家共享） ======

        /// <summary>最大层数</summary>
        public const int MaxLayerCount = 3;

        /// <summary>激活/升层最低维持秒数</summary>
        public const float MinSustainSeconds = 1f;

        /// <summary>RAM 崩溃锁定帧</summary>
        internal const int CrashLockoutFrames = 90;

        /// <summary>领域中心缓动总帧</summary>
        internal const int DomainEaseTotal = 28;

        /// <summary>MotionFade 满速度阈值</summary>
        internal const float MotionFadeFullSpeed = 5.5f;

        //每层半径相对于基础半径的倍率
        private static readonly float[] LayerRadiusScale = { 1.0f, 1.7f, 2.6f };

        /// <summary>各层每秒 RAM 消耗</summary>
        public static readonly float[] LayerRamDrainPerSecond = { 0.4f, 1.6f, 6f };

        //爆发阶段每层持续帧数
        internal static readonly int[] BurstDurations = { 14, 24, 36 };

        //常规展开 lerp 速率，高层更缓
        internal static readonly float[] ExpandLerps = { 0.035f, 0.020f, 0.013f };

        //收缩 lerp 速率，高层更缓
        internal static readonly float[] ContractLerps = { 0.050f, 0.030f, 0.020f };

        /// <summary>基础半径，跨玩家共享</summary>
        public static float BaseRadius = 600f;

        /// <summary>栅格单元边长</summary>
        public static float GridSize = 24f;

        /// <summary>场景压暗强度</summary>
        public static float DimStrength = 0.85f;

        /// <summary>受术实体特效强度下限，见 <see cref="EffectIntensityOf"/></summary>
        public const float MinEffectIntensity = 0.6f;

        //====== 玩家访问器 ======

        /// <summary>指定玩家领域状态，未就绪 null</summary>
        internal static CyberspacePlayer For(Player p) {
            if (p == null || !p.active) {
                return null;
            }
            return p.GetModPlayer<CyberspacePlayer>();
        }

        /// <summary>按索引取领域状态，越界/未就绪 null</summary>
        internal static CyberspacePlayer For(int whoAmI) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            return For(Main.player[whoAmI]);
        }

        /// <summary>本地玩家领域状态，未就绪 null</summary>
        internal static CyberspacePlayer Local => For(Main.LocalPlayer);

        /// <summary>
        /// 本机屏幕上正在生效的那个域：自己的优先，否则取范围内最近的他人领域。
        /// 世界级表现（天空/光照/全屏后处理/装饰）一律读它，HUD 仍读 <see cref="Local"/>
        /// </summary>
        public static CyberspacePlayer Viewed { get; private set; }

        //观看半径：域是屏幕级效果，施术者进视野一圈才卷进来
        private static float ViewRange
            => MathF.Max(Main.screenWidth, Main.screenHeight) * 0.75f + 480f;

        //已在观看的那份放宽半径，边界来回走不闪断
        private const float ViewRangeHysteresis = 1.35f;

        private static int viewedIndex = -1;

        /// <summary>观看域的 L3 接管强度 0~1，天空/光照/日光读它</summary>
        public static float ViewedTakeover {
            get {
                CyberspacePlayer cp = Viewed;
                if (cp == null) return 0f;
                return cp.TakeoverProgress * MathHelper.Clamp(cp.Intensity, 0f, 1f);
            }
        }

        /// <summary>逐帧重选主导域，须在推进各玩家状态之前调用</summary>
        internal static void RefreshViewed() {
            Viewed = null;
            Player local = Main.dedServ || Main.gameMenu ? null : Main.LocalPlayer;
            if (local?.active != true) {
                viewedIndex = -1;
                return;
            }

            CyberspacePlayer own = local.GetModPlayer<CyberspacePlayer>();
            if (own.Intensity > 0.001f) {
                Viewed = own;
                viewedIndex = local.whoAmI;
                return;
            }

            float range = ViewRange;
            float nearest = float.MaxValue;
            int nearestIndex = -1;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player other = Main.player[i];
                if (i == local.whoAmI || other?.active != true
                    || !other.TryGetModPlayer(out CyberspacePlayer domain)
                    || domain.Intensity <= 0.001f) {
                    continue;
                }
                float limit = i == viewedIndex ? range * ViewRangeHysteresis : range;
                float distance = Vector2.Distance(other.Center, local.Center);
                if (distance > limit || distance >= nearest) {
                    continue;
                }
                nearest = distance;
                nearestIndex = i;
                Viewed = domain;
            }
            viewedIndex = nearestIndex;
        }

        /// <summary>枚举视觉仍活跃的玩家领域</summary>
        internal static IEnumerable<CyberspacePlayer> EnumerateRenderable() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p == null || !p.active) continue;
                CyberspacePlayer cp = p.GetModPlayer<CyberspacePlayer>();
                //按视觉强度，含关闭收缩尾
                if (cp.Intensity < 0.001f) continue;
                yield return cp;
            }
        }

        //====== 转发本地玩家属性（UI/按键/HUD） ======

        public static bool Active => Local?.Active ?? false;

        public static float Intensity {
            get => Local?.Intensity ?? 0f;
            set { if (Local is { } lp) lp.Intensity = value; }
        }

        public static float RestartCollapse {
            get => Local?.RestartCollapse ?? 0f;
            set { if (Local is { } lp) lp.RestartCollapse = value; }
        }

        public static Vector2 DomainCenter => Local?.DomainCenter ?? Vector2.Zero;

        public static int CurrentLayer => Local?.CurrentLayer ?? 0;

        public static int RenderLayerCount => Local?.RenderLayerCount ?? 0;

        public static float Radius => Local?.Radius ?? BaseRadius;

        public static float ExpandProgress => Local?.ExpandProgress ?? 0f;

        public static float EffectiveOuterRadius => Local?.EffectiveOuterRadius ?? 0f;

        public static float EffectTime => Local?.EffectTime ?? 0f;

        public static float MotionFade => Local?.MotionFade ?? 0f;

        public static bool IsCrashLockedOut => Local?.IsCrashLockedOut ?? false;

        //====== 静态计算方法 ======

        /// <summary>指定层完整半径</summary>
        public static float GetLayerRadius(int layerIndex) {
            layerIndex = Math.Clamp(layerIndex, 0, MaxLayerCount - 1);
            return BaseRadius * LayerRadiusScale[layerIndex];
        }

        /// <summary>指定层(1..Max)每秒 RAM</summary>
        public static float GetLayerDrainRate(int layer) {
            if (layer < 1 || layer > MaxLayerCount) {
                return 0f;
            }
            return LayerRamDrainPerSecond[layer - 1];
        }

        /// <summary>本地当前层每秒 RAM</summary>
        public static float GetCurrentDrainRate() {
            int layer = CurrentLayer;
            if (!Active || layer < 1 || layer > MaxLayerCount) {
                return 0f;
            }
            return LayerRamDrainPerSecond[layer - 1];
        }

        public static float GetLayerExpand(int layerIndex) => Local?.GetLayerExpand(layerIndex) ?? 0f;

        public static bool CanAffordLayer(int layer) => Local?.CanAffordLayer(layer) ?? false;

        public static bool IsInsideDomain(Vector2 worldPos) => Local?.IsInsideDomain(worldPos) ?? false;

        /// <summary>owner 领域是否覆盖 worldPos</summary>
        public static bool IsInsideDomainOf(int ownerWho, Vector2 worldPos) {
            CyberspacePlayer cp = For(ownerWho);
            return cp != null && cp.IsInsideDomain(worldPos);
        }

        /// <summary>
        /// 施术者领域强度，供受术实体的特效着色器使用。
        /// <br/>不能取 <see cref="Intensity"/>：那是本地玩家的领域，队友视角恒为 0。
        /// <br/>下限保证领域中途收起时特效只是变淡，不会把实体本身调没。
        /// </summary>
        public static float EffectIntensityOf(int ownerWho) {
            float intensity = For(ownerWho)?.Intensity ?? 0f;
            if (!float.IsFinite(intensity)) {
                intensity = 0f;
            }
            return MathHelper.Clamp(intensity, MinEffectIntensity, 1f);
        }

        /// <summary>任意玩家领域是否覆盖 worldPos</summary>
        public static bool IsInsideAnyDomain(Vector2 worldPos) {
            foreach (CyberspacePlayer cp in EnumerateRenderable()) {
                if (cp.IsInsideDomain(worldPos)) return true;
            }
            return false;
        }

        //====== 操作方法 ======

        /// <summary>切换指定玩家领域</summary>
        public static bool Toggle(Player owner) {
            if (owner == null) return false;
            return owner.GetModPlayer<CyberspacePlayer>().Toggle();
        }

        /// <summary>激活指定玩家领域</summary>
        public static void Activate(Player owner) {
            if (owner == null) return;
            owner.GetModPlayer<CyberspacePlayer>().Activate();
        }

        /// <summary>设层数，owner 默认 LocalPlayer</summary>
        public static void SetLayer(int layer, Player owner = null) {
            owner ??= Main.LocalPlayer;
            if (owner == null) return;
            owner.GetModPlayer<CyberspacePlayer>().SetLayer(layer);
        }

        /// <summary>关领域(myPlayer)</summary>
        public static void Deactivate() => Local?.Deactivate();

        /// <summary>触发 RAM 崩溃(myPlayer)</summary>
        public static void TriggerSystemCrash() => Local?.TriggerSystemCrash();

        /// <summary>瞬移锚点(myPlayer)</summary>
        public static void NotifyTeleport(Vector2 anchorCenter) => Local?.NotifyTeleport(anchorCenter);

        /// <summary>遍历在线玩家 Update</summary>
        public static void Update() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p == null || !p.active) continue;
                p.GetModPlayer<CyberspacePlayer>().Update();
            }
        }

        /// <summary>重置在线玩家领域状态</summary>
        public static void Reset() {
            Viewed = null;
            viewedIndex = -1;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p == null || !p.active) continue;
                p.GetModPlayer<CyberspacePlayer>().Reset();
            }
        }
    }
}

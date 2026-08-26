using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>装填风格：决定可打断性与音画节拍的语义标签，每枪的具体节拍仍由各自 Cue 重写呈现</summary>
    internal enum GsReloadStyle
    {
        Muzzle,     //前装（三段杵压）
        Cylinder,   //转轮（逐膛咔嗒，可打断）
        Break,      //折管（折开抛壳合膛）
        Tube,       //管式（逐发压弹，可打断）
        Drum,       //鼓匣（整鼓拔插）
        Breath,     //气息（呼吸渐强，可打断）
        Hopper,     //沙斗（沙沙倒灌）
        Canister,   //彩罐（气罐旋换）
        Chain,      //链回收（无计时装填，链收回即完成）
        Music,      //音匣（音阶上行）
        Box,        //匣式（整匣拔插）
        Ember       //火巢（火星回吸）
    }

    /// <summary>
    /// 枪·前困难族的本地玩家态。装填/弹匣是纯本地节拍层（联机纪律见计划 §1.4）：
    /// 全部字段只在 Main.myPlayer 路径读写，不同步；远端看到的射击节奏由弹幕生成自然呈现。
    /// 兼作族级共享文案的本地化载体（ILocalizedModType）
    /// </summary>
    internal class GsGunsEarlyPlayer : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "GodSmithGunsEarly";

        public static LocalizedText PerfectText { get; private set; }
        public static LocalizedText HighNoonText { get; private set; }

        public override void SetStaticDefaults() {
            PerfectText = this.GetLocalization("Perfect", () => "Perfect reload!");
            HighNoonText = this.GetLocalization("HighNoon", () => "High Noon!");
        }

        //==================== 通用弹匣态 ====================
        public int heldType;            //当前方案武器类型，切枪重置
        public int magLeft;             //弹匣余弹（虚拟）
        public float reloadTimer;       //装填已进行 tick（吹管站定加速故用 float）
        public int reloadDuration;      //本次装填总时长，0=未在装填
        public int reloadMagStart;      //起装时余弹，逐发装填按进度补弹用
        public bool reloadTactical;     //本次是否战术装填
        public float perfectStart;      //完美窗起点（tick）
        public float perfectEnd;        //完美窗终点（tick）
        public int barLinger;           //装填条完成后的余显帧
        public bool perfectNextShot;    //完美奖励：下一发增伤
        public bool perfectMag;         //完美奖励：整匣增益（语义由各枪自定）
        public uint lastShotTick;       //上次开火的世界帧
        public uint idleTicksAtShot;    //本次开火时已停火多久（GsShoot 覆盖 lastShotTick 前记录）

        //==================== 各枪专属态 ====================
        public int nirvanaStacks;       //凤凰爆破枪涅槃层（死亡清零，换枪保留）
        public bool healUsedThisMag;    //夺命枪本匣治疗已用
        public int comboTarget;         //手枪点穴：连击目标
        public int comboHits;           //手枪点穴：连击数
        public bool comboReady;         //手枪点穴：第 4 发增伤待发
        public int paintTarget;         //彩弹：上次命中目标
        public int paintColor;          //彩弹：上次命中色号
        public int paintStreak;         //彩弹：同目标同色连击数
        public int rouletteHold;        //左轮：右键长按计时
        public bool noonArmed;          //左轮：正午一发待发
        public int pullTimer;           //鱼叉：拽己剩余帧
        public int pullNpc;             //鱼叉：拽己目标 NPC
        public bool pullArmed;          //鱼叉：叉中重敌，待绞盘拽己
        public int shellCounter;        //抛壳节流计数

        /// <summary>切枪/死亡时清空瞬时态（涅槃层单独按死亡清）</summary>
        public void ResetTransient() {
            magLeft = 0;
            reloadTimer = 0f;
            reloadDuration = 0;
            reloadMagStart = 0;
            reloadTactical = false;
            perfectStart = perfectEnd = 0f;
            barLinger = 0;
            perfectNextShot = false;
            perfectMag = false;
            healUsedThisMag = false;
            comboTarget = -1;
            comboHits = 0;
            comboReady = false;
            paintTarget = -1;
            paintStreak = 0;
            rouletteHold = 0;
            noonArmed = false;
            pullTimer = 0;
            pullArmed = false;
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            ResetTransient();
            heldType = 0;
            nirvanaStacks = 0;
        }

        public override void PostUpdate() {
            if (barLinger > 0) {
                barLinger--;
            }
        }
    }

    /// <summary>路由 LocalState 通用小包：弹跳/一次性初始化等每弹幕本地计数</summary>
    internal class GsProjLocalState
    {
        public int Bounces;
        public bool InitDone;
    }

    /// <summary>
    /// 弹匣装填共享框架（枪·前困难族基类）。统一流转：
    /// 装填中禁射（逐发风格可打断）；GsShoot 扣 1 虚拟弹 + 后坐冲量，末发走 <see cref="FireLastRound"/>；
    /// 空匣自动起装填；右键=战术装填，装填中右键=完美装填判定（甜点窗内立即完成 + 奖励）。
    /// 弹药经济：每次 use 仍只消耗原版的 1 发，弹匣是节拍层；右键路径永不进 use 流程，零耗弹。
    /// 联机：状态全在本地玩家 ModPlayer；GsShoot/GsModifyShootStats 只在 owner 端执行，
    /// GsCanUseItem/GsHoldItem 各端都会执行，故一律先守 myPlayer 再碰状态
    /// </summary>
    internal abstract class GsMagazineScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "GunsEarly";

        //==================== 子类参数面 ====================

        /// <summary>弹匣容量</summary>
        public abstract int MagSize { get; }

        /// <summary>空匣装填时长（tick）</summary>
        public abstract int ReloadTicks { get; }

        /// <summary>战术装填（有余弹换弹）时长，默认省 30%</summary>
        public virtual int TacticalReloadTicks => (int)(ReloadTicks * 0.7f);

        /// <summary>装填风格</summary>
        public abstract GsReloadStyle Style { get; }

        /// <summary>完美装填甜点窗宽度（tick），0=无完美机制</summary>
        public virtual int PerfectWindow => 8;

        /// <summary>完美窗在装填时长中的位置（0~1）</summary>
        public virtual float PerfectWindowPos => 0.55f;

        /// <summary>默认完美奖励（perfectNextShot）的增伤倍率</summary>
        public virtual float PerfectShotDamageMul => 1.2f;

        /// <summary>逐发装填风格可开火打断（装几发打几发）</summary>
        public virtual bool InterruptibleReload
            => Style is GsReloadStyle.Cylinder or GsReloadStyle.Tube or GsReloadStyle.Breath;

        /// <summary>false = 不走计时装填（鱼叉：链收回即完成）</summary>
        public virtual bool UsesTimedReload => true;

        /// <summary>每发后坐冲量（px/f）；坐骑减半、空中 ×1.5、钩爪锚定禁用</summary>
        protected virtual float GetRecoil(bool lastRound) => 1f;

        /// <summary>装填推进速率（吹管站定回气 +25% 用）</summary>
        protected virtual float ReloadRate(Player player) => 1f;

        /// <summary>非逐发风格的装填节拍数（音画 cue 次数）</summary>
        protected virtual int ReloadCueCount => 3;

        /// <summary>本枪是否抛壳</summary>
        protected virtual bool EjectsShell => true;

        /// <summary>每几发抛一枚壳（高射速枪节流）</summary>
        protected virtual int ShellEvery => 1;

        //==================== 打标暂存（owner 端同帧消费） ====================

        /// <summary>本次射击要写进 router.MarkData 的档位；Fire*/ModifyShot 里设，OnSpawnMarked 里消费</summary>
        protected float pendingMark;

        //==================== 小工具 ====================

        protected static GsGunsEarlyPlayer State(Player player) => player.GetModPlayer<GsGunsEarlyPlayer>();

        protected static bool IsLocal(Player player) => player.whoAmI == Main.myPlayer;

        /// <summary>切枪归位：弹匣视作满装上阵</summary>
        protected void SyncHeld(GsGunsEarlyPlayer mp) {
            if (mp.heldType != TargetItemID) {
                mp.ResetTransient();
                mp.heldType = TargetItemID;
                mp.magLeft = MagSize;
            }
        }

        //==================== 使用流 ====================

        public override bool? GsCanUseItem(Item item, Player player) {
            if (!IsLocal(player)) {
                return null;    //远端与服务端不掺和本地弹匣闸，动作由弹幕同步自然呈现
            }
            GsGunsEarlyPlayer mp = State(player);
            SyncHeld(mp);
            if (player.altFunctionUse == 2) {
                return false;   //右键永不进 use 流程（GsAltFunctionUse 已返回 false，此为兜底）
            }
            if (mp.reloadDuration > 0) {
                if (InterruptibleReload && mp.magLeft > 0) {
                    CancelReload(mp);   //逐发装填：装几发打几发
                    return OnTryUse(item, player, mp);
                }
                return false;
            }
            if (mp.magLeft <= 0) {
                if (UsesTimedReload) {
                    StartReload(item, player, mp, false);
                }
                else {
                    OnBlockedUse(item, player, mp);     //鱼叉：链在外时点击=绞盘
                }
                return false;
            }
            return OnTryUse(item, player, mp);
        }

        /// <summary>弹匣允许开火时的额外闸（默认放行）</summary>
        protected virtual bool? OnTryUse(Item item, Player player, GsGunsEarlyPlayer mp) => null;

        /// <summary>空匣且不走计时装填时的点击回调（鱼叉绞盘）</summary>
        protected virtual void OnBlockedUse(Item item, Player player, GsGunsEarlyPlayer mp) { }

        public override void GsHoldItem(Item item, Player player) {
            if (!IsLocal(player)) {
                return;
            }
            GsGunsEarlyPlayer mp = State(player);
            SyncHeld(mp);
            if (mp.reloadDuration > 0) {
                TickReload(item, player, mp);
            }
            HoldTick(item, player, mp);
        }

        /// <summary>手持每帧（myPlayer 已守）</summary>
        protected virtual void HoldTick(Item item, Player player, GsGunsEarlyPlayer mp) { }

        //==================== 右键：战术装填 / 完美判定 ====================

        public override bool? GsAltFunctionUse(Item item, Player player) {
            if (IsLocal(player)) {
                GsGunsEarlyPlayer mp = State(player);
                SyncHeld(mp);
                OnRightClick(item, player, mp);
            }
            return false;   //不进入 alt use 流程：右键任何情况下不掉一发弹药
        }

        /// <summary>右键按下（myPlayer 已守）。默认：装填中=完美判定，否则=战术装填</summary>
        protected virtual void OnRightClick(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (mp.reloadDuration > 0) {
                TryPerfect(item, player, mp);
                return;
            }
            if (UsesTimedReload && mp.magLeft < MagSize) {
                StartReload(item, player, mp, tactical: mp.magLeft > 0);
            }
        }

        //==================== 装填状态机（全部 myPlayer 路径） ====================

        protected void StartReload(Item item, Player player, GsGunsEarlyPlayer mp, bool tactical) {
            if (!UsesTimedReload || mp.reloadDuration > 0) {
                return;
            }
            mp.perfectMag = false;      //上一匣的完美整匣增益到换弹为止
            mp.reloadTactical = tactical && mp.magLeft > 0;
            mp.reloadDuration = Math.Max(1, mp.reloadTactical ? TacticalReloadTicks : ReloadTicks);
            mp.reloadTimer = 0f;
            mp.reloadMagStart = mp.magLeft;
            if (PerfectWindow > 0) {
                mp.perfectStart = PerfectWindowPos * mp.reloadDuration;
                mp.perfectEnd = mp.perfectStart + PerfectWindow;
            }
            else {
                mp.perfectStart = mp.perfectEnd = -1f;
            }
            OnReloadStart(item, player, mp);
        }

        private void TickReload(Item item, Player player, GsGunsEarlyPlayer mp) {
            float prev = mp.reloadTimer;
            mp.reloadTimer += ReloadRate(player);

            if (InterruptibleReload) {
                //逐发补弹：按进度把 [起装余弹 → 满匣] 均匀补齐
                int total = MagSize - mp.reloadMagStart;
                if (total > 0) {
                    int loaded = (int)(mp.reloadTimer / mp.reloadDuration * total);
                    int want = mp.reloadMagStart + Math.Min(loaded, total);
                    while (mp.magLeft < want) {
                        mp.magLeft++;
                        OnRoundLoaded(item, player, mp, mp.magLeft);
                    }
                }
            }
            else {
                //整体装填：按节拍数派 cue
                int cues = Math.Max(1, ReloadCueCount);
                int prevIdx = (int)(prev * cues / mp.reloadDuration);
                int nowIdx = (int)(mp.reloadTimer * cues / mp.reloadDuration);
                if (nowIdx > prevIdx && nowIdx <= cues) {
                    OnReloadCue(item, player, mp, nowIdx, cues);
                }
            }

            if (mp.reloadTimer >= mp.reloadDuration) {
                CompleteReload(item, player, mp, false);
            }
        }

        protected void CancelReload(GsGunsEarlyPlayer mp) {
            mp.reloadDuration = 0;
            mp.reloadTimer = 0f;
            mp.barLinger = 6;
        }

        private void TryPerfect(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (PerfectWindow <= 0 || mp.perfectStart < 0f) {
                return;
            }
            if (mp.reloadTimer >= mp.perfectStart && mp.reloadTimer <= mp.perfectEnd) {
                CompleteReload(item, player, mp, true);
            }
            else if (!VaultUtils.isServer) {
                //脱靶只给轻咔提示，不惩罚
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.3f }, player.Center);
            }
        }

        private void CompleteReload(Item item, Player player, GsGunsEarlyPlayer mp, bool perfect) {
            mp.reloadDuration = 0;
            mp.reloadTimer = 0f;
            mp.magLeft = MagSize;
            mp.barLinger = 14;
            mp.healUsedThisMag = false;
            if (perfect) {
                OnPerfectReload(item, player, mp);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.35f }, player.Center);
                    CombatText.NewText(player.getRect(), GameModeTheme.GodSmithEmber, GsGunsEarlyPlayer.PerfectText.Value);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero,
                        GameModeTheme.GodSmithEmber, 0f)?.Configure(0.04f, 0.4f, 12);
                }
            }
            OnReloadComplete(item, player, mp, perfect);
        }

        /// <summary>装填开始（起装音、抛壳雨等）</summary>
        protected virtual void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.2f }, player.Center);
            }
        }

        /// <summary>整体装填的节拍点（index 从 1 到 total）</summary>
        protected virtual void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.2f + 0.15f * index }, player.Center);
            }
        }

        /// <summary>逐发装填的每发落膛（roundIndex = 当前余弹数）</summary>
        protected virtual void OnRoundLoaded(Item item, Player player, GsGunsEarlyPlayer mp, int roundIndex) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.1f + 0.05f * roundIndex }, player.Center);
            }
        }

        /// <summary>装填完成（perfect = 是否完美完成）</summary>
        protected virtual void OnReloadComplete(Item item, Player player, GsGunsEarlyPlayer mp, bool perfect) {
            if (!VaultUtils.isServer && !perfect) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f, Pitch = 0.15f }, player.Center);
            }
        }

        /// <summary>完美奖励，默认下一发 +PerfectShotDamageMul；整匣类奖励重写此处设 perfectMag</summary>
        protected virtual void OnPerfectReload(Item item, Player player, GsGunsEarlyPlayer mp) {
            mp.perfectNextShot = true;
        }

        //==================== 射击流（owner 端） ====================

        public override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            GsGunsEarlyPlayer mp = State(player);
            if (mp.perfectNextShot) {
                damage = (int)(damage * PerfectShotDamageMul);
            }
            ModifyShot(item, player, mp, ref position, ref velocity, ref type, ref damage, ref knockback, mp.magLeft <= 1);
        }

        /// <summary>射击参数修改（lastRound = 本发是末发）</summary>
        protected virtual void ModifyShot(Item item, Player player, GsGunsEarlyPlayer mp, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback, bool lastRound) { }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            GsGunsEarlyPlayer mp = State(player);
            SyncHeld(mp);
            bool last = mp.magLeft <= 1;
            mp.magLeft = Math.Max(0, mp.magLeft - 1);
            mp.idleTicksAtShot = Main.GameUpdateCount - mp.lastShotTick;
            mp.lastShotTick = Main.GameUpdateCount;
            pendingMark = 0f;

            ApplyRecoil(player, velocity, GetRecoil(last));
            MuzzleAndShell(player, mp, position, velocity, last);

            bool? result = last
                ? FireLastRound(item, player, mp, source, position, velocity, type, damage, knockback)
                : FireNormalRound(item, player, mp, source, position, velocity, type, damage, knockback);

            mp.perfectNextShot = false;     //完美增伤只吃一发（ModifyShootStats 已应用）
            if (mp.magLeft <= 0 && UsesTimedReload) {
                StartReload(item, player, mp, false);
            }
            return result;
        }

        /// <summary>常规发。返回 null 走原版弹幕 + 路由打标</summary>
        protected virtual bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => null;

        /// <summary>末发签名变形（每枪必异）</summary>
        protected abstract bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback);

        /// <summary>后坐位移：owner 权威写自身速度。钩爪锚定禁用、坐骑减半、空中 ×1.5（向下打即火箭跳）</summary>
        protected static void ApplyRecoil(Player player, Vector2 shotVelocity, float impulse) {
            if (impulse <= 0f || player.grapCount > 0) {
                return;
            }
            Vector2 aim = shotVelocity.SafeNormalize(Vector2.UnitX * player.direction);
            if (player.mount != null && player.mount.Active) {
                impulse *= 0.5f;
            }
            if (player.velocity.Y != 0f) {
                impulse *= 1.5f;
            }
            player.velocity -= aim * impulse;
        }

        /// <summary>枪口烟火 + 抛壳（owner 本地视觉，预算每发 ≤3 粒）</summary>
        private void MuzzleAndShell(Player player, GsGunsEarlyPlayer mp, Vector2 position, Vector2 velocity, bool last) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Lighting.AddLight(position, 0.42f, 0.3f, 0.12f);
            PRTLoader.NewParticle<PRT_Smoke>(position + aim * 6f, aim * 1.4f + new Vector2(0f, -0.5f),
                new Color(168, 158, 140), Main.rand.NextFloat(0.05f, 0.08f))
                ?.Configure(Main.rand.Next(14, 20), 0.35f, 0.02f);
            if (last) {
                PRTLoader.NewParticle<PRT_Spark>(position, aim * Main.rand.NextFloat(3f, 5f),
                    GameModeTheme.GodSmithEmber, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, 10);
            }
            if (EjectsShell) {
                mp.shellCounter++;
                if (mp.shellCounter % Math.Max(1, ShellEvery) == 0) {
                    PRTLoader.NewParticle<PRT_ProcChip>(position - aim * 10f,
                        new Vector2(-aim.X * Main.rand.NextFloat(1f, 2f), -Main.rand.NextFloat(2f, 3.4f)),
                        new Color(190, 150, 70), Main.rand.NextFloat(0.5f, 0.7f))
                        ?.Configure(new Color(255, 224, 150), Main.rand.Next(24, 36), 0.6f);
                }
            }
        }

        //==================== 弹药经济 ====================

        public override bool? GsCanConsumeAmmo(Item weapon, Item ammo, Player player) {
            //装填期不该有射击发生，此处兜底保证装填期零耗弹；其余一律交回原版（迷你鲨 33% 省弹等原样）
            if (IsLocal(player) && State(player).reloadDuration > 0) {
                return false;
            }
            return null;
        }

        //==================== 打标默认转发 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            router.MarkData = pendingMark;
            OnSpawnMarkedExtra(proj, router);
        }

        /// <summary>打标追加处理（改 penetrate 等，owner 端；penetrate 加法必须带 &gt;0 守卫）</summary>
        protected virtual void OnSpawnMarkedExtra(Projectile proj, GodSmithProjRouter router) { }
    }

    /// <summary>
    /// 本机玩家脚下的装填进度条（镜像 DivineSourceChargeBarLayer 先例，owner-only）。
    /// 金色推进 + 完美窗亮带；完成后余显一拍
    /// </summary>
    internal class GsGunsEarlyReloadBarLayer : PlayerDrawLayer
    {
        private const int BarWidth = 46;
        private const int BarHeight = 4;

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.FrontAccFront);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            if (Main.gameMenu || drawInfo.shadow != 0f || !GameModeSystem.GodSmithActive) {
                return false;
            }
            Player player = drawInfo.drawPlayer;
            if (!player.active || player.dead || player.ghost || player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (player.HeldItem == null
                || !GodSmithScheme.TryGetScheme(player.HeldItem.type, out GodSmithScheme scheme)
                || scheme is not GsMagazineScheme) {
                return false;
            }
            GsGunsEarlyPlayer mp = player.GetModPlayer<GsGunsEarlyPlayer>();
            return mp.reloadDuration > 0 || mp.barLinger > 0;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player player = drawInfo.drawPlayer;
            GsGunsEarlyPlayer mp = player.GetModPlayer<GsGunsEarlyPlayer>();
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle px = new(0, 0, 1, 1);

            bool reloading = mp.reloadDuration > 0;
            float fillT = reloading
                ? MathHelper.Clamp(mp.reloadTimer / mp.reloadDuration, 0f, 1f)
                : 1f;
            float alpha = reloading ? 0.95f : mp.barLinger / 14f * 0.9f;

            Vector2 anchor = player.Bottom + new Vector2(0f, 10f + player.gfxOffY) - Main.screenPosition;
            Vector2 topLeft = anchor - new Vector2(BarWidth * 0.5f, 0f);

            Color frame = new Color(46, 34, 18) * alpha;
            Color backing = new Color(12, 10, 8) * (0.85f * alpha);
            drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft - Vector2.One, px, frame,
                0f, Vector2.Zero, new Vector2(BarWidth + 2, BarHeight + 2), SpriteEffects.None));
            drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, backing,
                0f, Vector2.Zero, new Vector2(BarWidth, BarHeight), SpriteEffects.None));

            //完美窗亮带垫底，玩家看准了掐右键
            if (reloading && mp.perfectStart >= 0f) {
                float ws = MathHelper.Clamp(mp.perfectStart / mp.reloadDuration, 0f, 1f);
                float we = MathHelper.Clamp(mp.perfectEnd / mp.reloadDuration, 0f, 1f);
                Color win = GameModeTheme.GodSmithEmber * (0.5f * alpha);
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft + new Vector2(BarWidth * ws, 0f), px, win,
                    0f, Vector2.Zero, new Vector2(MathF.Max(1f, BarWidth * (we - ws)), BarHeight), SpriteEffects.None));
            }

            int fillPx = (int)MathF.Round(BarWidth * fillT);
            if (fillPx > 0) {
                Color fillA = GameModeTheme.GodSmithAccent * (0.9f * alpha);
                Color fillB = GameModeTheme.GodSmithEmber * (0.8f * alpha);
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, fillA,
                    0f, Vector2.Zero, new Vector2(fillPx, BarHeight), SpriteEffects.None));
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, fillB,
                    0f, Vector2.Zero, new Vector2(fillPx, 2f), SpriteEffects.None));
                if (reloading && fillPx < BarWidth) {
                    Color tick = Color.White * (0.85f * alpha);
                    tick.A = 0;
                    drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft + new Vector2(fillPx - 1, 0f), px, tick,
                        0f, Vector2.Zero, new Vector2(1f, BarHeight), SpriteEffects.None));
                }
            }
        }
    }

    /// <summary>
    /// 族内共享爆发/滞留区弹幕：ai0=半径px，ai1=风味（0火团/1咬合/2调色爆/3毒雾云），ai2=彩弹色号。
    /// 命中判定各端由 owner 权威，视觉各端按同步的 ai 参数自绘；无贴图本体，全靠粒子
    /// </summary>
    internal class GsGunsEarlyBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float Radius => Projectile.ai[0];
        private int Flavor => (int)Projectile.ai[1];

        /// <summary>调色爆配色表，下标由 ai[2] 给出，取值 0 至 6</summary>
        private static readonly Color[] PaintPalette = [
            new Color(226, 72, 72), new Color(232, 143, 58), new Color(226, 208, 74),
            new Color(96, 200, 96), new Color(72, 148, 226), new Color(140, 92, 208),
            new Color(226, 108, 178)];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 8;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                int size = (int)MathHelper.Clamp(Radius * 2f, 24f, 220f);
                Projectile.Resize(size, size);
                if (Flavor == 3) {
                    //毒雾云：滞留 2 秒，间歇结算
                    Projectile.timeLeft = 120;
                    Projectile.localNPCHitCooldown = 30;
                }
                SpawnBurstVisuals();
            }
            if (Flavor == 3) {
                Projectile.velocity *= 0.95f;
                if (!VaultUtils.isServer && Projectile.timeLeft % 9 == 0) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(
                        Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.6f, Radius * 0.4f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.1f, 0.4f)),
                        Color.White, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(Main.rand.Next(40, 60), Main.rand.NextFloat(0.4f, 0.8f));
                }
            }
            else {
                Projectile.velocity = Vector2.Zero;
            }
        }

        private void SpawnBurstVisuals() {
            if (VaultUtils.isServer) {
                return;
            }
            switch (Flavor) {
                case 0: //火团
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                        new Color(255, 150, 60), 0f)?.Configure(0.05f, Radius / 90f, 12);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_HellFire>(
                            Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, Radius * 0.5f),
                            Main.rand.NextVector2Circular(1.4f, 1.4f) - Vector2.UnitY * 1.2f,
                            Color.White, Main.rand.NextFloat(0.5f, 0.9f));
                    }
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center,
                            Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY,
                            new Color(255, 176, 88), Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(20, 32));
                    }
                    break;
                case 1: //咬合冲击
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        new Color(150, 210, 230) * 0.8f, Radius / 300f)?.Configure(Vector2.One, 0f, 1.6f, 14);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2Circular(4f, 4f),
                            new Color(170, 220, 235), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, 12);
                    }
                    break;
                case 2: //调色爆
                    Color paint = PaintPalette[(int)MathHelper.Clamp(Projectile.ai[2], 0f, 6f)];
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, paint, 0f)
                        ?.Configure(0.04f, Radius / 100f, 10);
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                            Main.rand.NextVector2Circular(3.5f, 3.5f), Color.White, Main.rand.NextFloat(0.5f, 0.9f))
                            ?.Configure(paint, Main.rand.Next(14, 24), 0.1f, 0.8f);
                    }
                    break;
                case 3: //毒雾云初雾
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_ToxicMist>(
                            Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.4f, Radius * 0.3f),
                            Main.rand.NextVector2Circular(0.6f, 0.4f),
                            Color.White, Main.rand.NextFloat(0.6f, 0.9f))
                            ?.Configure(Main.rand.Next(50, 80), Main.rand.NextFloat(0.4f, 0.8f));
                    }
                    break;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Flavor == 0) {
                target.AddBuff(BuffID.OnFire, 180);
            }
            else if (Flavor == 3) {
                target.AddBuff(BuffID.Poisoned, 120);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}

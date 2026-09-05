using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
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
    /// 全部字段只在 Main.myPlayer 路径读写，不同步；远端看到的射击节奏由弹幕生成自然呈现
    /// </summary>
    internal class GsGunsEarlyPlayer : ModPlayer
    {
        //==================== 通用弹匣态 ====================
        public int heldType;            //当前方案武器类型，切枪重置
        public int magLeft;             //弹匣余弹（虚拟）
        public float reloadTimer;       //装填已进行 tick（吹管站定加速故用 float）
        public int reloadDuration;      //本次装填总时长，0=未在装填
        public int reloadMagStart;      //起装时余弹，逐发装填按进度补弹用
        public int barLinger;           //装填条完成后的余显帧
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
        public int pullTimer;           //鱼叉：拽己剩余帧
        public int pullNpc;             //鱼叉：拽己目标 NPC
        public bool pullArmed;          //鱼叉：叉中重敌，待绞盘拽己
        public float kickApplied;       //角度踢差分记账：已施加的绝对偏移（各端各持）
        public int kickLastAnim;        //角度踢差分记账：上帧 itemAnimation（动画重启检测）

        /// <summary>切枪/死亡时清空瞬时态（涅槃层单独按死亡清）</summary>
        public void ResetTransient() {
            magLeft = 0;
            reloadTimer = 0f;
            reloadDuration = 0;
            reloadMagStart = 0;
            barLinger = 0;
            healUsedThisMag = false;
            comboTarget = -1;
            comboHits = 0;
            comboReady = false;
            paintTarget = -1;
            paintStreak = 0;
            pullTimer = 0;
            pullArmed = false;
            kickApplied = 0f;
            kickLastAnim = 0;
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
    /// 空匣自动起装填。弹药经济：每次 use 仍只消耗原版的 1 发，弹匣是节拍层。
    /// 联机：状态全在本地玩家 ModPlayer；GsShoot/GsModifyShootStats 只在 owner 端执行，
    /// GsCanUseItem/GsHoldItem 各端都会执行，故一律先守 myPlayer 再碰状态
    /// </summary>
    internal abstract class GsMagazineScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Guns";

        //==================== 子类参数面 ====================

        /// <summary>弹匣容量</summary>
        public abstract int MagSize { get; }

        /// <summary>空匣装填时长（tick）</summary>
        public abstract int ReloadTicks { get; }

        /// <summary>装填风格</summary>
        public abstract GsReloadStyle Style { get; }

        /// <summary>逐发装填风格可开火打断（装几发打几发）</summary>
        public virtual bool InterruptibleReload
            => Style is GsReloadStyle.Cylinder or GsReloadStyle.Tube or GsReloadStyle.Breath;

        /// <summary>false = 不走计时装填（鱼叉：链收回即完成）</summary>
        public virtual bool UsesTimedReload => true;

        /// <summary>每发后坐冲量（px/f）；坐骑减半、空中 ×1.5、钩爪锚定禁用</summary>
        protected virtual float GetRecoil(bool lastRound) => 1f;

        /// <summary>装填推进速率（吹管站定回气 +25% 用）</summary>
        protected virtual float ReloadRate(Player player) => 1f;

        /// <summary>非逐发风格的装填节拍数（音效 cue 次数）</summary>
        protected virtual int ReloadCueCount => 3;

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

        /// <summary>
        /// 族共享后坐姿态（各枪 GsUseStyle 一行调用）：出膛帧沿瞄准向后挫 + 角度踢（kick 正=上踢、负=下压），
        /// 随后按 <see cref="GsGunRecoilProfile.Derive"/> 派生的阻尼颠动抖回原位，重枪附带 owner 端震屏。
        /// 位移/角度数学与联机纪律见 <see cref="GsGunRecoil"/>；各端同式，无 myPlayer 守门，旁观者可见踢
        /// </summary>
        protected static void GunKickStyle(Player player, float shift, float kick) {
            GsGunsEarlyPlayer mp = State(player);
            GsGunRecoil.Apply(player, ref mp.kickApplied, ref mp.kickLastAnim, GsGunRecoilProfile.Derive(shift, kick));
        }

        /// <summary>族默认后坐后挫幅度（px）；未自写 GsUseStyle 的枪由此获得基础后坐</summary>
        protected virtual float DefaultRecoilShift => 1.5f;

        /// <summary>族默认角度踢（弧度，正=上抬）</summary>
        protected virtual float DefaultRecoilKick => 0.05f;

        /// <summary>族默认后坐姿态：转轮/前装/折管等未自定义姿态的枪一律有后挫与颠动（子类可覆写换参数）</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, DefaultRecoilShift, DefaultRecoilKick);

        //==================== 使用流 ====================

        public override bool? GsCanUseItem(Item item, Player player) {
            if (!IsLocal(player)) {
                return null;    //远端与服务端不掺和本地弹匣闸，动作由弹幕同步自然呈现
            }
            GsGunsEarlyPlayer mp = State(player);
            SyncHeld(mp);
            if (mp.reloadDuration > 0) {
                if (InterruptibleReload && mp.magLeft > 0) {
                    CancelReload(mp);   //逐发装填：装几发打几发
                    return OnTryUse(item, player, mp);
                }
                return false;
            }
            if (mp.magLeft <= 0) {
                if (UsesTimedReload) {
                    StartReload(item, player, mp);
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
                //装填期枪体在手：useStyle-5 动画外原版不绘制 held item，由持枪姿态件补位
                //（TickReload 本帧可能完成装填，二次判空防多续一帧）
                if (mp.reloadDuration > 0) {
                    GsGunHoldPoseProj.Ensure(player, TargetItemID, GsGunHoldPoseProj.ReloadPitch);
                }
            }
            HoldTick(item, player, mp);
        }

        /// <summary>手持每帧（myPlayer 已守）</summary>
        protected virtual void HoldTick(Item item, Player player, GsGunsEarlyPlayer mp) { }

        //==================== 装填状态机（全部 myPlayer 路径） ====================

        protected void StartReload(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!UsesTimedReload || mp.reloadDuration > 0) {
                return;
            }
            mp.reloadDuration = Math.Max(1, ReloadTicks);
            mp.reloadTimer = 0f;
            mp.reloadMagStart = mp.magLeft;
            //起装帧即持枪补位（held 枪换鼓自杀与本件同帧交接，无空手帧）；此后由 GsHoldItem 逐帧续命
            GsGunHoldPoseProj.Ensure(player, TargetItemID, GsGunHoldPoseProj.ReloadPitch);
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
                CompleteReload(item, player, mp);
            }
        }

        protected void CancelReload(GsGunsEarlyPlayer mp) {
            mp.reloadDuration = 0;
            mp.reloadTimer = 0f;
            mp.barLinger = 6;
        }

        private void CompleteReload(Item item, Player player, GsGunsEarlyPlayer mp) {
            mp.reloadDuration = 0;
            mp.reloadTimer = 0f;
            mp.magLeft = MagSize;
            mp.barLinger = 14;
            mp.healUsedThisMag = false;
            OnReloadComplete(item, player, mp);
        }

        /// <summary>装填开始（起装音）</summary>
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

        /// <summary>装填完成</summary>
        protected virtual void OnReloadComplete(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f, Pitch = 0.15f }, player.Center);
            }
        }

        //==================== 射击流（owner 端） ====================

        public override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            GsGunsEarlyPlayer mp = State(player);
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
            //原版 snap 在本射击帧稍后绝对赋值 itemRotation（同帧晚于 UseStyle），踢记账随之归零
            mp.kickApplied = 0f;
            pendingMark = 0f;

            ApplyRecoil(player, velocity, GetRecoil(last));

            bool? result = last
                ? FireLastRound(item, player, mp, source, position, velocity, type, damage, knockback)
                : FireNormalRound(item, player, mp, source, position, velocity, type, damage, knockback);

            if (mp.magLeft <= 0 && UsesTimedReload) {
                StartReload(item, player, mp);
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

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //爆环由牙弹/尽息镖 OnHit 经 GetSource_FromAI 生成，承签会把父弹 MarkData 原样拷过来
            //爆环再走方案 OnHit 就会继续产环，敌不死链不断（迷你鲨反馈五 #37）
            if (proj.type == ModContent.ProjectileType<GsGunsEarlyBurstProj>()) {
                router.MarkData = 0f;
                router.MarkData2 = 0f;
            }
        }

        /// <summary>打标追加处理（改 penetrate 等，owner 端；penetrate 加法必须带 &gt;0 守卫）</summary>
        protected virtual void OnSpawnMarkedExtra(Projectile proj, GodSmithProjRouter router) { }
    }

    /// <summary>
    /// 本机玩家脚下的装填进度条（镜像 DivineSourceChargeBarLayer 先例，owner-only）。
    /// 朴素单色推进；完成后余显一拍
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

            int fillPx = (int)MathF.Round(BarWidth * fillT);
            if (fillPx > 0) {
                Color fill = GameModeTheme.GodSmithAccent * (0.9f * alpha);
                drawInfo.DrawDataCache.Add(new DrawData(pixel, topLeft, px, fill,
                    0f, Vector2.Zero, new Vector2(fillPx, BarHeight), SpriteEffects.None));
            }
        }
    }

    /// <summary>
    /// 族内共享爆发/滞留区弹幕：ai0=半径px，ai1=风味（0火团/1咬合/2调色爆/3毒雾云），ai2=彩弹色号。
    /// 命中判定各端由 owner 权威；视觉按风味取最接近的原版弹幕贴图，按判定直径一笔画在中心
    /// </summary>
    internal class GsGunsEarlyBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private float Radius => Projectile.ai[0];
        private int Flavor => (int)Projectile.ai[1];

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
                //父源承签会把方案形态标带过来；爆环再走方案 OnHit 会继续产环（迷你鲨 #37）
                //非弹匣方案（鱼叉/雏凤）不走上面的族承签钩，这里兜住所有爆环
                if (Projectile.TryGetGlobalProjectile(out GodSmithProjRouter router)) {
                    router.MarkData = 0f;
                    router.MarkData2 = 0f;
                }
                int size = (int)MathHelper.Clamp(Radius * 2f, 24f, 220f);
                Projectile.Resize(size, size);
                if (Flavor == 3) {
                    //毒雾云：滞留 2 秒，间歇结算
                    Projectile.timeLeft = 120;
                    Projectile.localNPCHitCooldown = 30;
                }
            }
            if (Flavor == 3) {
                Projectile.velocity *= 0.95f;
            }
            else {
                Projectile.velocity = Vector2.Zero;
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

        /// <summary>区域一笔：按风味取原版贴图，缩放到判定直径画在中心</summary>
        public override bool PreDraw(ref Color lightColor) {
            int id = Flavor switch {
                1 => ProjectileID.Bone,
                2 => ProjectileID.PartyBullet,
                3 => ProjectileID.ToxicCloud,
                _ => ProjectileID.BallofFire
            };
            Main.instance.LoadProjectile(id);
            Texture2D tex = TextureAssets.Projectile[id].Value;
            int frames = Math.Max(1, Main.projFrames[id]);
            Rectangle src = new(0, 0, tex.Width, tex.Height / frames);
            float scale = Projectile.width / (float)Math.Max(src.Width, src.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, src, lightColor,
                0f, src.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

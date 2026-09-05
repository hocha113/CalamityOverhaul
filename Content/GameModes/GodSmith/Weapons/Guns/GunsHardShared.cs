using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>瞄准线形态（多模式枪专用，owner 本地绘制）</summary>
    internal enum GsAimLineKind
    {
        /// <summary>无瞄准线</summary>
        None,
        /// <summary>贯通直线（狙击）</summary>
        Line,
    }

    /// <summary>
    /// 一档射击模式的静态参数包。数据只读，运行期状态全在 <see cref="GsGunsHardPlayer"/>
    /// </summary>
    internal sealed class GsFireMode
    {
        /// <summary>模式名 loc 键后缀（族 loc 文件内 类名.键 结构）</summary>
        public string Key;
        /// <summary>模式名英文默认值（zh 正典写族 loc 文件）</summary>
        public string EnName = "";
        /// <summary>射速乘数（经 GlobalItem 桥，同时缩放 useTime/useAnimation）</summary>
        public float UseSpeed = 1f;
        /// <summary>伤害乘数（GsModifyShootStats 内结算）</summary>
        public float DamageMul = 1f;
        /// <summary>收束度 0~1：出膛速度向光标方向插值，抵消原版随机散布</summary>
        public float Converge;
        /// <summary>附加散布半角（弧度，owner 端掷随机后随生成包过线）</summary>
        public float ExtraSpread;
        /// <summary>瞄准线形态（owner 本地绘制，不广播）</summary>
        public GsAimLineKind AimLine = GsAimLineKind.None;
        /// <summary>运行时缓存的模式名（加载期由基类注册）</summary>
        public LocalizedText Name;
    }

    /// <summary>
    /// 枪·困难族共享框架。<br/>
    /// 默认每枪一档固定射击模式（<see cref="Modes"/>[0]），右键回归原版行为，不做任何接管。<br/>
    /// 保留 R2 重做的枪（模式表多于一档）走右键切换契约：<see cref="GsAltFunctionUse"/> 打开右键通道，
    /// <see cref="GsCanUseItem"/> 在 altFunctionUse==2 分支里 myPlayer 守门执行切换后返回 false，
    /// 右键因此永不进入 use 流程：不耗弹、不触发动画、零网络包。单档枪对该契约零足迹。<br/>
    /// 运行态全部是本地玩家态（<see cref="GsGunsHardPlayer"/>，myPlayer 路径消费）；
    /// 远端玩家看到的射击节奏由 owner 端生成的弹幕自然呈现。<br/>
    /// 子类扩展点一律 GsGun* 前缀；GsCanUseItem/GsShoot/GsModifyShootStats/GsHoldItem/
    /// GsUseSpeedMultiplier/GsAltFunctionUse 已密封，防止破坏右键与记账契约
    /// </summary>
    internal abstract class GsFireModeScheme : GodSmithScheme
    {
        /// <summary>模式表：默认只有 0 号生效；多于一档即启用右键切换</summary>
        public abstract GsFireMode[] Modes { get; }

        /// <summary>切换后再切换的锁定时长（tick），防右键长按每帧循环</summary>
        public virtual int ModeSwitchLock => 14;

        //==================== 模式访问 ====================

        /// <summary>是否为多档枪（右键切换契约只对它们生效）</summary>
        private bool MultiMode => Modes.Length > 1;

        /// <summary>按索引取模式（越界折回 0 档，换持残留索引安全）</summary>
        public GsFireMode ModeOf(int index) {
            GsFireMode[] modes = Modes;
            return index >= 0 && index < modes.Length ? modes[index] : modes[0];
        }

        /// <summary>该玩家当前生效的模式（远端玩家因状态不同步恒为 0 档，仅影响动画速率）</summary>
        public GsFireMode CurrentMode(Player player)
            => ModeOf(player.GetModPlayer<GsGunsHardPlayer>().ModeIndex);

        //==================== 打标编码（MarkData 低位 = 模式索引，高位 = 每枪私有 flag） ====================

        /// <summary>打包私有 flag 进 MarkData（单档枪，模式位恒 0）</summary>
        protected static float PackMark(int flag) => flag * 16;

        /// <summary>打包模式索引与私有 flag 进 MarkData</summary>
        protected static float PackMark(int modeIndex, int flag) => modeIndex + flag * 16;

        /// <summary>从 MarkData 解出模式索引</summary>
        protected static int MarkModeOf(float markData) => (int)markData % 16;

        /// <summary>从 MarkData 解出私有 flag</summary>
        protected static int MarkFlagOf(float markData) => (int)markData / 16;

        //==================== 加载期：模式名本地化注册 ====================

        public override void GsSetStaticDefaults() {
            foreach (GsFireMode mode in Modes) {
                string en = mode.EnName;
                mode.Name = this.GetLocalization(mode.Key, () => en);
            }
        }

        //==================== 右键切换契约（密封；仅多档枪） ====================

        public sealed override bool? GsAltFunctionUse(Item item, Player player) => MultiMode ? true : null;

        public sealed override bool? GsCanUseItem(Item item, Player player) {
            if (MultiMode && player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer) {
                    OnRightPress(item, player);
                }
                //右键永不进入 use 流程：不耗弹、无动画、零网络包
                return false;
            }
            return GsGunCanUse(item, player);
        }

        /// <summary>右键按下（已守 myPlayer）。默认立即循环模式；狙击类覆写成点按/长按分离</summary>
        protected virtual void OnRightPress(Item item, Player player) => CycleMode(item, player);

        /// <summary>循环切换模式（切换不白嫖节奏：只换索引，射击记账原样）</summary>
        protected void CycleMode(Item item, Player player) {
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            if (mp.ModeLock > 0 || !MultiMode) {
                return;
            }
            mp.ModeIndex = (mp.ModeIndex + 1) % Modes.Length;
            mp.ModeLock = ModeSwitchLock;
            GsFireMode mode = Modes[mp.ModeIndex];
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.65f, Pitch = 0.2f }, player.Center);
                CombatText.NewText(player.getRect(), GameModeTheme.GodSmithAccent, mode.Name?.Value ?? mode.Key);
            }
            OnModeSwitched(item, player, mp.ModeIndex);
        }

        /// <summary>模式切换完成后（已守 myPlayer；播私有切换演出用）</summary>
        protected virtual void OnModeSwitched(Item item, Player player, int newIndex) { }

        //==================== 使用流转发（密封） ====================

        /// <summary>子类的可用性追加闸（右键已由基类处理）</summary>
        protected virtual bool? GsGunCanUse(Item item, Player player) => null;

        public sealed override float GsUseSpeedMultiplier(Item item, Player player)
            => CurrentMode(player).UseSpeed * GsGunUseSpeed(item, player);

        /// <summary>子类附加射速乘数（狂潮梯度等），默认 1</summary>
        protected virtual float GsGunUseSpeed(Item item, Player player) => 1f;

        public sealed override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsGunHoldLocal(item, player, player.GetModPlayer<GsGunsHardPlayer>());
        }

        /// <summary>手持每帧（已守 myPlayer；稳息推进/私有计时放这）</summary>
        protected virtual void GsGunHoldLocal(Item item, Player player, GsGunsHardPlayer mp) { }

        /// <summary>换持/死亡时清理枪私有实例字段（由 <see cref="GsGunsHardPlayer"/> 回调，已守 myPlayer）</summary>
        internal virtual void GsGunHeldReset(Player player) { }

        //==================== 射击流转发（密封；只在 owner 端执行） ====================

        public sealed override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            GsFireMode mode = ModeOf(mp.ModeIndex);
            //动画内连发序号推进：间隔 ≤ useTime+4 视为同一动画链
            mp.CurAnimShot = Main.GameUpdateCount - mp.LastShotTick <= (uint)(item.useTime + 4)
                ? mp.CurAnimShot + 1 : 0;
            damage = (int)(damage * mode.DamageMul);
            if (mode.Converge > 0f) {
                velocity = Vector2.Lerp(velocity, GsAimUnit(player) * velocity.Length(), mode.Converge);
            }
            if (mode.ExtraSpread > 0f) {
                //owner 端掷散布，随生成包过线
                velocity = velocity.RotatedBy(Main.rand.NextFloat(-mode.ExtraSpread, mode.ExtraSpread));
            }
            GsGunModifyShoot(item, player, ref position, ref velocity, ref type, ref damage, ref knockback, mode, mp);
        }

        /// <summary>子类射击参数修改（读 mp.CurAnimShot 判动画内第 N 发）</summary>
        protected virtual void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) { }

        public sealed override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            bool? result = GsGunShoot(item, player, source, position, velocity, type, damage, knockback, ModeOf(mp.ModeIndex), mp);
            RecordShot(mp);
            return result;
        }

        /// <summary>子类射击（语义同 GsShoot：null 原版弹、false 自建弹、true 原版弹跳过后续链）</summary>
        protected virtual bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) => null;

        /// <summary>射击记账：时间戳与踢记账。弹药消耗不在此处（原版管线自理，守恒）</summary>
        private static void RecordShot(GsGunsHardPlayer mp) {
            mp.LastShotTick = Main.GameUpdateCount;
            //原版 snap 在本射击帧稍后绝对赋值 itemRotation（同帧晚于 UseStyle），踢记账随之归零
            mp.KickApplied = 0f;
        }

        //==================== 族级后坐姿态（密封；后挫 + 角度踢 + 阻尼颠动 + 震屏，参数子类可调） ====================

        /// <summary>后坐位移幅度 px（沿瞄准反向后挫，出膛帧最大）；0 = 关闭</summary>
        protected virtual float RecoilShift => 3f;

        /// <summary>后坐角度踢幅度（弧度，枪口上抬，差分施加各端可见）</summary>
        protected virtual float RecoilKick => 0.045f;

        /// <summary>按当前状态缩放后坐，默认恒 1</summary>
        protected virtual float RecoilScale(Item item, Player player, GsFireMode mode) => 1f;

        /// <summary>
        /// 完整后坐剖面。默认由 <see cref="RecoilShift"/> / <see cref="RecoilKick"/> 派生颠动与震屏
        /// （见 <see cref="GsGunRecoilProfile.Derive"/>）；想单独调颠动频率或震屏的枪覆写本属性
        /// </summary>
        protected virtual GsGunRecoilProfile RecoilProfile => GsGunRecoilProfile.Derive(RecoilShift, RecoilKick);

        /// <summary>
        /// 族级后坐：出膛帧沿瞄准向后挫 + 角度踢，其后按阻尼正弦颠动抖回原位，重枪附带 owner 端震屏。
        /// 位移/角度数学与联机纪律（差分施加、动画重启清账、无 myPlayer 守门）见 <see cref="GsGunRecoil"/>；
        /// owner 端射击帧由 <see cref="RecordShot"/> 清账对齐原版 snap
        /// </summary>
        public sealed override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            GsGunRecoil.Apply(player, ref mp.KickApplied, ref mp.KickLastAnim,
                RecoilProfile.Scaled(RecoilScale(item, player, ModeOf(mp.ModeIndex))));
            GsGunUseStyle(item, player, heldItemFrame);
        }

        /// <summary>后坐之后的姿态追加（枪口下压/自定义持握放这）</summary>
        protected virtual void GsGunUseStyle(Item item, Player player, Rectangle heldItemFrame) { }

        //==================== 瞄准线（owner 本地绘制，绘制路径禁 Main.rand） ====================

        /// <summary>本帧是否显示瞄准线（默认看模式表；点亮式枪覆写这里）</summary>
        public virtual bool AimLineVisible(Item item, Player player, GsGunsHardPlayer mp, GsFireMode mode)
            => mode.AimLine != GsAimLineKind.None;

        /// <summary>瞄准线主色（狙击稳息渐变覆写这里）</summary>
        public virtual Color AimLineColor(Item item, Player player, GsGunsHardPlayer mp)
            => GameModeTheme.GodSmithAccent;

        /// <summary>瞄准线绘制（默认贯通直线；需要整线定制再覆写）</summary>
        public virtual void DrawAimLine(ref PlayerDrawSet drawInfo, Item item, Player player,
            GsGunsHardPlayer mp, GsFireMode mode) {
            if (mode.AimLine != GsAimLineKind.Line) {
                return;
            }
            Vector2 start = player.MountedCenter;
            Vector2 unit = (Main.MouseWorld - start).SafeNormalize(Vector2.UnitX * player.direction);
            start += unit * 26f;
            GsAimLineDraw.DrawLine(ref drawInfo, start, unit, 1500f, AimLineColor(item, player, mp), 0.5f);
        }

        //==================== 稳息表帮手 ====================

        /// <summary>
        /// 推进稳息表：站稳且光标不甩则积累，移动/甩枪清零。
        /// 只在 GsGunHoldLocal 内调用（myPlayer 已守门）；开火清零由调用方在射击路径做
        /// </summary>
        protected static float TickSteady(Player player, GsGunsHardPlayer mp, int fillTicks) {
            bool moving = player.velocity.LengthSquared() > 0.6f;
            Vector2 aim = Main.MouseWorld;
            bool jerked = Vector2.DistanceSquared(aim, mp.LastAimWorld) > 26f * 26f;
            mp.LastAimWorld = aim;
            if (moving || jerked) {
                mp.SteadyMeter = 0f;
                return 0f;
            }
            mp.SteadyMeter = Math.Min(1f, mp.SteadyMeter + 1f / fillTicks);
            return mp.SteadyMeter;
        }
    }

    /// <summary>
    /// 枪·困难族本地玩家态：火控模式、稳息表、连发序号、踢记账。
    /// 全部字段只在 myPlayer 路径读写、不同步；远端端上恒为默认值，
    /// 仅影响远端观察到的持枪动画速率（弹幕节奏由 owner 生成权威呈现）
    /// </summary>
    internal class GsGunsHardPlayer : ModPlayer
    {
        /// <summary>当前持枪物品 type（换持检测）</summary>
        public int HeldType;
        /// <summary>当前模式索引（单档枪恒 0）</summary>
        public int ModeIndex;
        /// <summary>切换锁定倒计时</summary>
        public int ModeLock;
        /// <summary>稳息表 0~1（狙击/伏击类消费）</summary>
        public float SteadyMeter;
        /// <summary>上次射击的世界帧</summary>
        public uint LastShotTick;
        /// <summary>本次动画链内的连发序号（0 起）</summary>
        public int CurAnimShot;
        /// <summary>上帧光标世界坐标（稳息甩枪检测）</summary>
        public Vector2 LastAimWorld;
        /// <summary>角度踢差分记账：当前已施加在 itemRotation 上的绝对偏移（各端各持不同步）</summary>
        public float KickApplied;
        /// <summary>角度踢差分记账：上帧观察到的 itemAnimation（动画重启检测）</summary>
        public int KickLastAnim;

        public override void PreUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            int held = Player.HeldItem?.type ?? ItemID.None;
            if (held != HeldType) {
                //换持：通知旧枪方案清私有字段，再整表重置
                NotifyHeldReset(HeldType);
                HeldType = held;
                ResetAll();
            }
            if (ModeLock > 0) {
                ModeLock--;
            }
        }

        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            NotifyHeldReset(HeldType);
            ResetAll();
        }

        private void ResetAll() {
            ModeIndex = 0;
            ModeLock = 0;
            SteadyMeter = 0f;
            CurAnimShot = 0;
            KickApplied = 0f;
            KickLastAnim = 0;
        }

        private void NotifyHeldReset(int oldHeldType) {
            if (oldHeldType > ItemID.None
                && GodSmithScheme.TryGetScheme(oldHeldType, out GodSmithScheme scheme)
                && scheme is GsFireModeScheme gun) {
                gun.GsGunHeldReset(Player);
            }
        }
    }

    /// <summary>
    /// 瞄准线绘制层：只画给本机玩家（owner-only，不广播），模式关闭即整层消失。
    /// 单档枪的模式表不带瞄准线，本层对它们恒不可见。绘制路径禁 Main.rand，脉动一律取全局时间
    /// </summary>
    internal class GsAimLineLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.FrontAccFront);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            if (Main.gameMenu || drawInfo.shadow != 0f) {
                return false;
            }
            Player player = drawInfo.drawPlayer;
            if (!player.active || player.dead || player.ghost || player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (!GameModeSystem.GodSmithActive) {
                return false;
            }
            Item held = player.HeldItem;
            if (held == null || held.IsAir) {
                return false;
            }
            if (!GodSmithScheme.TryGetScheme(held.type, out GodSmithScheme scheme)
                || scheme is not GsFireModeScheme gun) {
                return false;
            }
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            return gun.AimLineVisible(held, player, mp, gun.ModeOf(mp.ModeIndex));
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player player = drawInfo.drawPlayer;
            Item held = player.HeldItem;
            if (!GodSmithScheme.TryGetScheme(held.type, out GodSmithScheme scheme)
                || scheme is not GsFireModeScheme gun) {
                return;
            }
            GsGunsHardPlayer mp = player.GetModPlayer<GsGunsHardPlayer>();
            gun.DrawAimLine(ref drawInfo, held, player, mp, gun.ModeOf(mp.ModeIndex));
        }
    }

    /// <summary>瞄准线分段渐隐绘制帮手（LaserScan 裁剪到砖面，采样数组复用零分配）</summary>
    internal static class GsAimLineDraw
    {
        private static readonly float[] scanSamples = new float[3];

        /// <summary>激光扫描求线长（撞砖裁剪）</summary>
        public static float ScanLength(Vector2 start, Vector2 unit, float maxLength) {
            Collision.LaserScan(start, unit, 4f, maxLength, scanSamples);
            float total = 0f;
            for (int i = 0; i < scanSamples.Length; i++) {
                total += scanSamples[i];
            }
            return total / scanSamples.Length;
        }

        /// <summary>
        /// 画一条枪口亮、远端渐隐的分段芯线 + 末端光点。
        /// intensity 缩放整体透明度与线宽；颜色一律转 A=0 走加色语义
        /// </summary>
        public static void DrawLine(ref PlayerDrawSet drawInfo, Vector2 start, Vector2 unit,
            float maxLength, Color color, float intensity) {
            float length = ScanLength(start, unit, maxLength);
            if (length < 24f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            color.A = 0;
            float rotation = unit.ToRotation();
            const int Segments = 6;
            float segLen = length / Segments;
            //呼吸脉动取全局时间，绘制路径不掷随机
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6.2f);
            for (int i = 0; i < Segments; i++) {
                float fade = (1f - i / (float)Segments) * intensity * pulse;
                Vector2 segStart = start + unit * (segLen * i) - Main.screenPosition;
                drawInfo.DrawDataCache.Add(new DrawData(pixel, segStart, new Rectangle(0, 0, 1, 1),
                    color * (0.62f * fade), rotation, new Vector2(0f, 0.5f),
                    new Vector2(segLen + 1f, 1.6f), SpriteEffects.None));
            }
            //末端光点：撞砖处或最大射程处
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Vector2 end = start + unit * length - Main.screenPosition;
                drawInfo.DrawDataCache.Add(new DrawData(glow, end, null, color * (0.5f * intensity * pulse),
                    0f, glow.Size() * 0.5f, 0.09f + 0.02f * pulse, SpriteEffects.None));
            }
        }
    }

    /// <summary>
    /// 族共用区域弹幕：向心拉扯/滞留 dot（玛瑙暗渍、玛瑙黑洞共用）。
    /// 「拉扯」不写 NPC.velocity（服务器权威），一律用命中击退向心实现：
    /// 短命中冷却多跳 + 击退方向恒指爆心。伤害与半径随生成实参过线，各端判定同源
    /// </summary>
    internal class GsGunsHardZoneProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>作用半径（px），生成实参随包</summary>
        public float Radius => Projectile.ai[0];

        /// <summary>风格：0=玛瑙暗渍（弱 dot 微拉） 1=玛瑙黑洞（强拉） 2=中拉</summary>
        public int Style => (int)Projectile.ai[1];

        /// <summary>风格 → 存活时长（tick），各端同源换算不吃 timeLeft 同步差</summary>
        private static int DurationOf(int style) => style switch { 1 => 20, 2 => 18, _ => 30 };

        private static Color StyleColor(int style) => style switch {
            1 => new Color(168, 92, 255),
            2 => new Color(96, 218, 190),
            _ => new Color(126, 70, 196),
        };

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
            Projectile.timeLeft = 40;
        }

        public override void AI() {
            //时长按风格钳制：ai 随生成包，各端一致收敛
            int duration = DurationOf(Style);
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }
            Lighting.AddLight(Projectile.Center, StyleColor(Style).ToVector3() * 0.4f);
            if (VaultUtils.isServer) {
                return;
            }
            //吸入粒子：从环缘向心飞（AI 内粒子发散允许 Main.rand）
            int budget = Style == 0 ? 2 : 3;
            for (int i = 0; i < budget; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(Radius, Radius);
                Vector2 inward = (Projectile.Center - rim).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(3f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(rim, inward, StyleColor(Style),
                    Main.rand.NextFloat(0.2f, 0.38f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Vector2.Distance(targetHitbox.Center.ToVector2(), Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退向心：目标在爆心左侧则向右推（朝爆心），反之向左
            modifiers.HitDirectionOverride = target.Center.X < Projectile.Center.X ? 1 : -1;
            modifiers.Knockback *= Style switch { 1 => 2.2f, 2 => 1.6f, _ => 0.8f };
        }

        public override bool PreDraw(ref Color lightColor) {
            //收缩淡出的范围环（实体批内调用 ShockRingDraw 合法）
            int duration = DurationOf(Style);
            float life = 1f - Projectile.timeLeft / (float)duration;
            Color main = StyleColor(Style);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, Radius * (1f - life * 0.35f),
                7f, Color.Lerp(main, Color.White, 0.4f), main, main * 0.5f,
                (1f - life) * 0.55f, innerGlow: 0.25f, timeSeed: Projectile.identity * 0.37f);
            return false;
        }
    }
}

using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Yoyos
{
    /// <summary>指令模式常量（写进 <see cref="GodSmithProjRouter.MarkData"/> 跨端同步）</summary>
    internal static class GsYoyoMode
    {
        /// <summary>跟随：不覆写，原版 aiStyle 99 全权</summary>
        public const int Follow = 0;
        /// <summary>驻场：锚定光标点小半径自旋巡逻</summary>
        public const int Anchor = 1;
        /// <summary>折返：高速直线穿刺回手（一次性）</summary>
        public const int Lash = 2;
        /// <summary>环绕：绕玩家贴身防御轨道</summary>
        public const int Orbit = 3;
        /// <summary>路径编程：贝塞尔巡回（仅泰拉悠悠球）</summary>
        public const int Path = 4;
    }

    /// <summary>
    /// 悠悠球族方案基类。全员 Router 类型通道增强：原版 aiStyle 99 每帧先跑
    /// （线绳/飞行时限/回收/悠悠球袋/配重球生态全部无损），指令激活时在
    /// <see cref="GsProjPostAI"/> 覆写速度实现轨迹指令；模式关闭当帧路由停发即回原版。<br/>
    /// 三指令按 <see cref="Tier"/> 解锁：T1 驻场，T2 +折返，T3 +环绕，T4 +路径编程。
    /// 指令输入 = owner 侧原始右键边沿检测（channel 占用左键，AltFunctionUse 在
    /// itemAnimation 归零前不可达，见计划 §0.1）。<br/>
    /// 跨端契约：指令模式写 MarkData、热度层写 MarkData2（netUpdate 过线），
    /// 锚点/路径点/输入机是 owner 权威 <see cref="GsYoyoState"/> 本地量，
    /// 远端靠弹幕位置同步呈现轨迹
    /// </summary>
    internal abstract class GsYoyoScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Yoyos";

        //==================== 类型通道注册 ====================

        /// <summary>本方案悠悠球弹幕 type（加载期从原版 item.shoot 读取，不硬编码）</summary>
        internal int YoyoProjType { get; private set; } = -1;

        /// <summary>全族已注册的悠悠球弹幕 type 集合（配重球/蜂/绿珠等承签子弹幕靠它早退）</summary>
        internal static readonly HashSet<int> YoyoTypeSet = [];

        public override void GsSetStaticDefaults() {
            //从原版物品模板取弹幕 type，天平自动跟原版走
            YoyoProjType = new Item(TargetItemID).shoot;
            if (YoyoProjType <= ProjectileID.None) {
                CWRMod.Instance.Logger.Error($"[GodSmith] 悠悠球方案 {FullName} 读取 item.shoot 失败，通道未注册");
                return;
            }
            GsRegisterProjChannel(YoyoProjType);
            YoyoTypeSet.Add(YoyoProjType);
        }

        //==================== 参数面（21 件参数行按需覆写） ====================

        /// <summary>指令解锁档：1 驻场 / 2 +折返 / 3 +环绕 / 4 +路径编程</summary>
        internal virtual int Tier => 1;

        /// <summary>基础伤害倍率（有效 DPS 口径的静态部分，机制收益另算）</summary>
        internal virtual float DamageMul => 1.05f;

        /// <summary>驻场自旋巡逻半径 px</summary>
        internal virtual float AnchorRadius => 26f;

        /// <summary>驻场自旋角速度 rad/帧</summary>
        internal virtual float AnchorSpin => 0.35f;

        /// <summary>驻场热度：每 hit 伤害增幅</summary>
        internal virtual float HeatPerHit => 0.04f;

        /// <summary>驻场热度上限（总增幅）</summary>
        internal virtual float HeatCap => 0.40f;

        /// <summary>折返速度倍率（基于原版 YoyosTopSpeed）</summary>
        internal virtual float LashSpeedMul => 2.2f;

        /// <summary>折返伤害倍率</summary>
        internal virtual float LashDamageMul => 1.35f;

        /// <summary>环绕轨道半径 = 原版 YoyosMaximumRange × 本比例</summary>
        internal virtual float OrbitRadiusRatio => 0.45f;

        /// <summary>环绕角速度 rad/帧</summary>
        internal virtual float OrbitSpin => 0.22f;

        /// <summary>环绕期时限流速倍率（防挂机，1.5 = 每帧多走 0.5）</summary>
        internal virtual float OrbitTimeDrain => 1.5f;

        /// <summary>路径编程点数上限（仅泰拉悠悠球 &gt;0）</summary>
        internal virtual int PathPoints => 0;

        /// <summary>主题辉光色（指令激活时球体加色层）</summary>
        internal virtual Color GlowColor => new(255, 214, 120);

        /// <summary>热度辉光色（层数越高越亮）</summary>
        internal virtual Color HeatColor => new(255, 150, 60);

        /// <summary>热度层数上限（由 HeatCap/HeatPerHit 折算）</summary>
        internal int HeatCapLayers => (int)MathF.Round(HeatCap / HeatPerHit);

        //==================== 个性钩子（tick 类各端都调，钩子体内自守端别） ====================

        /// <summary>任意模式每帧（含跟随态；冷却递减/形态复位放这，端别纪律同下）</summary>
        internal virtual void OnGlobalTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode) { }

        /// <summary>驻场每帧（含服务器；粒子守 !VaultUtils.isServer，生成守 IsOwnedByLocalPlayer，权威判定守非多人客户端）</summary>
        internal virtual void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) { }

        /// <summary>环绕每帧（端别纪律同上）</summary>
        internal virtual void OnOrbitTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) { }

        /// <summary>折返每帧（端别纪律同上）</summary>
        internal virtual void OnLashTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) { }

        /// <summary>路径巡回每帧（端别纪律同上）</summary>
        internal virtual void OnPathTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) { }

        /// <summary>折返下达瞬间（仅 owner 端，镜像弹等伴生生成放这）</summary>
        internal virtual void OnLashBeginOwner(Projectile proj, GodSmithProjRouter router, GsYoyoState st) { }

        /// <summary>指令态命中（仅 owner 端；mode 为命中时的指令模式）</summary>
        internal virtual void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) { }

        /// <summary>指令态伤害修饰（判定端执行；基类已做热度与折返倍率，这里追加个性）</summary>
        internal virtual void ModifyCommandHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GsYoyoState st, int mode) { }

        /// <summary>个性绘制层（PostDraw 末尾；heatRatio = 热度层/上限）</summary>
        internal virtual void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) { }

        //==================== 数值行 ====================

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= DamageMul;
        }

        //==================== Router 回调 → 指令层 ====================

        public sealed override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //承签传染的子弹幕（蜂/绿珠/配重球/血珠等）不接管，原版行为无损
            if (proj.type != YoyoProjType) {
                return;
            }
            GsYoyoCommandLayer.PostAI(this, proj, router);
        }

        public sealed override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type != YoyoProjType) {
                return;
            }
            GsYoyoCommandLayer.ModifyHit(this, proj, target, ref modifiers, router);
        }

        public sealed override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != YoyoProjType) {
                return;
            }
            GsYoyoCommandLayer.OnHit(this, proj, target, hit, router);
        }

        public sealed override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != YoyoProjType) {
                return;
            }
            GsYoyoCommandLayer.PostDraw(this, proj, router);
        }
    }
}

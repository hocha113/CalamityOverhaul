using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions
{
    /// <summary>
    /// 召唤·仆从族方案中间基类。把全族公共面封死：<br/>
    /// 1. 右键 = 军团指挥（GsAltFunctionUse 放行 + GsCanUseItem 右键分支 owner 守门执行，
    ///    压掉本次使用，不打扰左键原版召唤）；<br/>
    /// 2. 弹幕类型通道自动注册 kit 与路由；<br/>
    /// 3. GsProjPostAI 公共头 = 军旗续命 + 阵型引导（只对仆从本体，伴生弹幕跳过）；<br/>
    /// 4. GsProjModifyHitNPC 公共头 = 指挥官光环与灵慰领域加成；<br/>
    /// 5. tooltip 统一追加右键指挥说明行。<br/>
    /// 子类（含 S3b 切片）只重写 GsMinion* 虚方法，禁再碰被密封的原钩子
    /// </summary>
    internal abstract class GsMinionScheme : GodSmithScheme
    {
        /// <summary>本武器的仆从条令（阵型参数）；返回 null = 不入阵型系统</summary>
        protected abstract GsMinionKit Kit { get; }

        /// <summary>注册进类型通道的弹幕（仆从本体 + 需要增强的伴生弹幕）</summary>
        protected abstract int[] MinionProjTypes { get; }

        public sealed override void GsSetStaticDefaults() {
            GsRegisterProjChannel(MinionProjTypes);
            if (Kit != null) {
                MinionDoctrine.RegisterKit(Kit, MinionProjTypes);
            }
            GsMinionStaticDefaults();
        }

        /// <summary>子类补充静态初始化（缓存额外 loc 键等）</summary>
        protected virtual void GsMinionStaticDefaults() { }

        //==================== 右键指挥接管 ====================

        public sealed override bool? GsAltFunctionUse(Item item, Player player) => true;

        public sealed override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                //指挥只在本地玩家路径执行；军旗生成/改写全走 owner 端，其余端等弹幕同步
                if (player.whoAmI == Main.myPlayer) {
                    MinionDoctrine.ExecuteCommandAt(player, Main.MouseWorld);
                }
                return false;
            }
            return GsMinionCanUseItem(item, player);
        }

        /// <summary>左键使用分支（默认走原版召唤）</summary>
        protected virtual bool? GsMinionCanUseItem(Item item, Player player) => null;

        //==================== 弹幕公共分发头 ====================

        public sealed override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //军旗续命 + 阵型引导只对仆从本体；毒刺/火球等伴生弹幕直通子类
            if (proj.minion) {
                MinionDoctrine.MinionUpkeep(proj, Kit);
            }
            GsMinionPostAI(proj, router);
        }

        /// <summary>仆从/伴生弹幕的每帧增强（公共维护已代做）</summary>
        protected virtual void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) { }

        public sealed override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            MinionDoctrine.ApplyCommandBonuses(proj, target, ref modifiers);
            GsMinionModifyHit(proj, target, ref modifiers, router);
        }

        /// <summary>命中伤害修饰（光环/灵慰公共加成已代做，owner 端执行）</summary>
        protected virtual void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) { }

        //==================== tooltip ====================

        public sealed override void GsModifyTooltips(Item item, List<TooltipLine> tooltips) {
            tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_GsMinionCommandHint",
                GsLegionBannerProj.CommandHint.Value) {
                OverrideColor = new Color(150, 190, 186)
            });
            GsMinionModifyTooltips(item, tooltips);
        }

        /// <summary>指挥说明行之后追加自定义行</summary>
        protected virtual void GsMinionModifyTooltips(Item item, List<TooltipLine> tooltips) { }

        //==================== 集结场维持 helper（子类在 GsMinionPostAI 里调用） ====================

        /// <summary>
        /// 集结指令下于旗点维持一座集结场（owner 端生成，形态位掩码防重，防抖窗防同帧多只连发）。
        /// damage/knockback 按触发仆从当前面板折算；readyTick 由子类持有（owner 命中路径消费契约）
        /// </summary>
        protected static void TryKeepRallyField(Projectile minion, int stance, float damageMul,
            float knockback, ref uint readyTick, int spawnGap = 45) {
            if (!minion.minion || !minion.IsOwnedByLocalPlayer()
                || Main.GameUpdateCount < readyTick
                || MinionDoctrine.GetCommand(minion.owner) != MinionDoctrine.CommandRally
                || MinionDoctrine.RallyFieldAlive(minion.owner, stance)
                || !MinionDoctrine.TryGetRallyPoint(minion.owner, out Vector2 point)) {
                return;
            }
            readyTick = Main.GameUpdateCount + (uint)spawnGap;
            Projectile.NewProjectile(minion.GetSource_FromAI(), point, Vector2.Zero,
                ModContent.ProjectileType<GsRallyFieldProj>(),
                (int)(minion.damage * damageMul), knockback, minion.owner,
                stance, 0f, minion.type);
        }
    }
}

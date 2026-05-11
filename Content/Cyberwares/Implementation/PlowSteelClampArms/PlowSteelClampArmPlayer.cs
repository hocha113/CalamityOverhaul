using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂的玩家组件
    /// <br/>承担两件事：
    /// <list type="bullet">
    ///   <item>由 <see cref="PlowSteelClampArmSkill"/> 通过 <see cref="TryFireWire"/>
    ///         驱动单分子线的释放，本身不直接监听任何快捷键</item>
    ///   <item>维护本机玩家的技能冷却倒计时，供雷达扇区填充与失败反馈引用</item>
    /// </list>
    /// 真正的伤害判定与渲染交由 <see cref="MonomolecularWire"/> 弹幕承担，本组件只是触发器
    /// </summary>
    internal class PlowSteelClampArmPlayer : ModPlayer
    {
        /// <summary>
        /// 单分子线技能剩余冷却帧数，0 时可再次释放
        /// </summary>
        public int SkillCooldownTimer { get; private set; }

        /// <summary>
        /// 公开冷却比例（0 = 已冷却完毕，1 = 刚释放），主要用于雷达或工具提示
        /// </summary>
        public float CooldownRatio => PlowSteelClampArm.SkillCooldown <= 0
            ? 0f : MathHelper.Clamp((float)SkillCooldownTimer / PlowSteelClampArm.SkillCooldown, 0f, 1f);

        public override void ResetEffects() {
            if (SkillCooldownTimer > 0) {
                SkillCooldownTimer--;
            }
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (PlowSteelClampArm.GetEquipped(Player) == null) {
                //卸下义体后即时清理冷却
                SkillCooldownTimer = 0;
            }
        }

        /// <summary>
        /// 单分子线统一发射入口：按 <paramref name="longMode"/> 选择短线或长线
        /// <list type="bullet">
        ///   <item><paramref name="aimWorld"/>：当前鼠标世界坐标，作为短线方向 / 长线锚点搜索的起点</item>
        ///   <item><paramref name="longMode"/>：true 表示尝试钉锚点的长线，找不到时自动降级为短线</item>
        ///   <item>所有失败路径都通过短促音效给出反馈，与原版直接按键的失败体验一致</item>
        /// </list>
        /// </summary>
        public void TryFireWire(Vector2 aimWorld, bool longMode) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (PlowSteelClampArm.GetEquipped(Player) == null) {
                return;
            }
            if (SkillCooldownTimer > 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.4f, Volume = 0.5f }, Player.Center);
                return;
            }

            //长线优先尝试找锚点；找不到 → 静默 fallback 到短线
            //该 fallback 是核心设计：玩家永远能打出线，避免"指不到物块就哑火"
            if (longMode && FindAnchorTile(aimWorld, out Vector2 anchor)) {
                FireLongWire(anchor);
                return;
            }
            FireShortWire(aimWorld);
        }

        /// <summary>
        /// 长线模式实际发射：锚点钉在指定 tile，跟随玩家位置形成 owner→anchor 的高热线段
        /// </summary>
        private void FireLongWire(Vector2 anchor) {
            int type = ModContent.ProjectileType<MonomolecularWire>();
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(PlowSteelClampArm.WireBaseDamage);

            Projectile proj = Projectile.NewProjectileDirect(new EntitySource_ItemUse(Player, new Item()),
                Player.Center, Vector2.Zero,
                type, damage, 0f, Player.whoAmI,
                ai0: anchor.X, ai1: anchor.Y, ai2: 0f);//ai2 = 0 → 长线/动态模式
            if (proj != null && proj.ModProjectile is MonomolecularWire wire) {
                wire.AnchorWorld = anchor;
                wire.IsStatic = false;
                proj.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            //装填火花：朝锚点方向喷射
            for (int i = 0; i < 12; i++) {
                Vector2 vel = (anchor - Player.Center).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(2f, 4.5f)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.MartianSaucerSpark, vel, 100, default, 1.2f);
                dust.noGravity = true;
            }

            SkillCooldownTimer = PlowSteelClampArm.SkillCooldown;
        }

        /// <summary>
        /// 短线模式实际发射：以 <paramref name="aimWorld"/> 方向为基线在玩家身前铺一条静态高热线
        /// <list type="bullet">
        ///   <item>无需锚点，永远成功</item>
        ///   <item>线段两端都被冻结在生成瞬间的位置（不会跟随玩家），形成"空中绊线"质感</item>
        ///   <item>持续时间显著短于长线，作为"无门槛"模式的平衡</item>
        /// </list>
        /// </summary>
        private void FireShortWire(Vector2 aimWorld) {
            Vector2 dir = (aimWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            //"from" 取生成瞬间的玩家中心，"to" 取沿方向走固定长度的点。
            //后续 MonomolecularWire 在 IsStatic 状态下不再跟随玩家移动
            Vector2 from = Player.Center;
            Vector2 to = from + dir * PlowSteelClampArm.ShortWireLengthPixels;

            int type = ModContent.ProjectileType<MonomolecularWire>();
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(PlowSteelClampArm.WireBaseDamage);

            Projectile proj = Projectile.NewProjectileDirect(new EntitySource_ItemUse(Player, new Item()),
                Player.Center, Vector2.Zero,
                type, damage, 0f, Player.whoAmI,
                ai0: to.X, ai1: to.Y, ai2: 1f);//ai2 = 1 → 短线/静态模式
            if (proj != null && proj.ModProjectile is MonomolecularWire wire) {
                wire.AnchorWorld = to;
                wire.StaticFromWorld = from;
                wire.IsStatic = true;
                wire.Projectile.timeLeft = PlowSteelClampArm.ShortWireLifetime;
                proj.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.55f, Volume = 0.55f }, Player.Center);
            //冷射粒子：沿线段路径分布，强化"瞬间铺线"质感
            int steps = 8;
            for (int i = 0; i < steps; i++) {
                float t = (float)i / steps;
                Vector2 pos = Vector2.Lerp(from, to, t);
                Vector2 normal = new(-dir.Y, dir.X);
                Vector2 vel = normal * Main.rand.NextFloat(-2f, 2f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.MartianSaucerSpark, vel, 100, default, 1.0f);
                dust.noGravity = true;
            }

            //短线冷却更短，鼓励玩家频繁使用作为常规手段
            SkillCooldownTimer = PlowSteelClampArm.SkillCooldown / 2;
        }

        /// <summary>
        /// 在指定的光标世界坐标附近寻找一块可作为锚点的实心物块，找到后返回锚点世界坐标
        /// <br/>策略：先取光标命中的格子；若该格子非实心，则从光标向四方各 4 格内寻找最近的实心格子
        /// <br/>使用显式 <paramref name="cursorWorld"/>，让上层（蓄力释放时）可以传入当前鼠标坐标
        /// </summary>
        private bool FindAnchorTile(Vector2 cursorWorld, out Vector2 anchorWorld) {
            anchorWorld = default;

            int targetX = (int)MathF.Floor(cursorWorld.X / 16f);
            int targetY = (int)MathF.Floor(cursorWorld.Y / 16f);
            if (Vector2.DistanceSquared(cursorWorld, Player.Center)
                > PlowSteelClampArm.MaxAnchorDistance * PlowSteelClampArm.MaxAnchorDistance) {
                return false;
            }

            if (IsAnchorTile(targetX, targetY)) {
                anchorWorld = new Vector2(targetX * 16f + 8f, targetY * 16f + 8f);
                return true;
            }

            //周围 4 格半径搜索最近的实心物块
            const int searchRadius = 4;
            int bestX = -1, bestY = -1;
            int bestDistSq = int.MaxValue;
            for (int dx = -searchRadius; dx <= searchRadius; dx++) {
                for (int dy = -searchRadius; dy <= searchRadius; dy++) {
                    int tx = targetX + dx;
                    int ty = targetY + dy;
                    if (!IsAnchorTile(tx, ty)) {
                        continue;
                    }
                    int distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq) {
                        bestDistSq = distSq;
                        bestX = tx;
                        bestY = ty;
                    }
                }
            }

            if (bestX < 0) {
                return false;
            }
            anchorWorld = new Vector2(bestX * 16f + 8f, bestY * 16f + 8f);
            return true;
        }

        /// <summary>
        /// 判定指定格子是否可作为单分子线的锚点：必须存在、是激活的、是有实体的实心格
        /// </summary>
        private static bool IsAnchorTile(int x, int y) {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) {
                return false;
            }
            Tile tile = Main.tile[x, y];
            if (!tile.HasUnactuatedTile) {
                return false;
            }
            return Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }
    }
}

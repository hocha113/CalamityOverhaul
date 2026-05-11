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
    ///   <item>由 <see cref="PlowSteelClampArmSkill"/> 通过 <see cref="TryFireWireFromRadial"/>
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
        /// 由 <see cref="PlowSteelClampArmSkill.OnInstantTrigger"/> 调用，尝试发射单分子线
        /// <list type="bullet">
        ///   <item><paramref name="aimWorld"/> 是要瞄准的世界坐标。雷达路径下传入的是
        ///     按键瞬间的鼠标快照，单技能直触路径下传入的是当前真实鼠标</item>
        ///   <item>所有失败路径都通过短促音效给出反馈，与原版直接按键的失败体验一致</item>
        /// </list>
        /// </summary>
        public void TryFireWireFromRadial(Vector2 aimWorld) {
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
            TryFireWire(aimWorld);
        }

        /// <summary>
        /// 实际尝试发射单分子线：以 <paramref name="aimWorld"/> 为光标，寻找附近的有效锚点
        /// </summary>
        private void TryFireWire(Vector2 aimWorld) {
            if (!FindAnchorTile(aimWorld, out Vector2 anchor)) {
                //没有可用的物块作为锚点，给出短促的"目标无效"反馈
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.6f, Volume = 0.55f }, Player.Center);
                return;
            }

            //生成弹幕：起点为玩家中心，终点编码进 ai[0]/ai[1] 以便多人同步
            int type = ModContent.ProjectileType<MonomolecularWire>();
            //依据玩家通用伤害对基础数值进行实时缩放，让长线性技能与玩家进度同步
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(PlowSteelClampArm.WireBaseDamage);

            Projectile proj = Projectile.NewProjectileDirect(new EntitySource_ItemUse(Player, new Item()),
                Player.Center, Vector2.Zero,
                type, damage, 0f, Player.whoAmI,
                ai0: anchor.X, ai1: anchor.Y);
            if (proj != null && proj.ModProjectile is MonomolecularWire wire) {
                wire.AnchorWorld = anchor;
                proj.netUpdate = true;
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 0.7f }, Player.Center);
            //装填火花
            for (int i = 0; i < 12; i++) {
                Vector2 vel = (anchor - Player.Center).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(2f, 4.5f)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.MartianSaucerSpark, vel, 100, default, 1.2f);
                dust.noGravity = true;
            }

            SkillCooldownTimer = PlowSteelClampArm.SkillCooldown;
        }

        /// <summary>
        /// 在指定的光标世界坐标附近寻找一块可作为锚点的实心物块，找到后返回锚点世界坐标
        /// <br/>策略：先取光标命中的格子；若该格子非实心，则从光标向四方各 4 格内寻找最近的实心格子
        /// <br/>使用显式 <paramref name="cursorWorld"/> 而非 <c>Player.tileTargetX/Y</c>，
        /// 让雷达可以传入"按键瞬间的鼠标快照"，避免开盘期间鼠标方向被劫持
        /// </summary>
        private bool FindAnchorTile(Vector2 cursorWorld, out Vector2 anchorWorld) {
            anchorWorld = default;

            int targetX = (int)MathF.Floor(cursorWorld.X / 16f);
            int targetY = (int)MathF.Floor(cursorWorld.Y / 16f);
            //超出最大触发距离直接判定无效（注意距离用真正的鼠标坐标，而非格子中心）
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

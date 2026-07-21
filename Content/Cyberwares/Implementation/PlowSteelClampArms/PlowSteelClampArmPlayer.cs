using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.PlowSteelClampArms
{
    /// <summary>
    /// 犁钢钳臂 ModPlayer，单分子线发射与冷却
    /// <br/>由 <see cref="PlowSteelClampArmSkill"/> 驱动，伤害判定见 <see cref="MonomolecularWire"/>
    /// </summary>
    internal class PlowSteelClampArmPlayer : ModPlayer
    {
        /// <summary>技能冷却剩余帧，0 可释放</summary>
        public int SkillCooldownTimer { get; private set; }
        private float skillCooldownCarry;

        /// <summary>冷却比例 0~1，雷达扇区填充用</summary>
        public float CooldownRatio => PlowSteelClampArm.SkillCooldown <= 0
            ? 0f : MathHelper.Clamp((float)SkillCooldownTimer / PlowSteelClampArm.SkillCooldown, 0f, 1f);

        public override void ResetEffects() {
            int timer = SkillCooldownTimer;
            BaseCyberware.TickFrameDown(ref timer, ref skillCooldownCarry);
            SkillCooldownTimer = timer;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (PlowSteelClampArm.GetEquipped(Player) == null) {
                SkillCooldownTimer = 0;
                skillCooldownCarry = 0f;
            }
        }

        /// <summary>单分子线发射入口，longMode 尝试长线，无锚点降级短线</summary>
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

            //长线搜锚点，无则短线
            if (longMode && FindAnchorTile(aimWorld, out Vector2 anchor)) {
                FireLongWire(anchor);
                return;
            }
            FireShortWire(aimWorld);
        }

        /// <summary>长线发射，ai2=0 动态模式，from 跟随玩家</summary>
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
            //朝锚点喷火花
            for (int i = 0; i < 12; i++) {
                Vector2 vel = (anchor - Player.Center).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(2f, 4.5f)
                    + Main.rand.NextVector2Circular(1.5f, 1.5f);
                Dust dust = Dust.NewDustPerfect(Player.Center, DustID.MartianSaucerSpark, vel, 100, default, 1.2f);
                dust.noGravity = true;
            }

            SkillCooldownTimer = PlowSteelClampArm.SkillCooldown;
            skillCooldownCarry = 0f;
        }

        /// <summary>短线发射，ai2=1 静态模式，两端冻结于生成瞬间</summary>
        private void FireShortWire(Vector2 aimWorld) {
            Vector2 dir = (aimWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            //from/to 冻结
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
            //沿线段冷射粒子
            int steps = 8;
            for (int i = 0; i < steps; i++) {
                float t = (float)i / steps;
                Vector2 pos = Vector2.Lerp(from, to, t);
                Vector2 normal = new(-dir.Y, dir.X);
                Vector2 vel = normal * Main.rand.NextFloat(-2f, 2f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.MartianSaucerSpark, vel, 100, default, 1.0f);
                dust.noGravity = true;
            }

            //短线半冷却
            SkillCooldownTimer = PlowSteelClampArm.SkillCooldown / 2;
            skillCooldownCarry = 0f;
        }

        /// <summary>光标附近搜实心物块作锚点，超 MaxAnchorDistance 失败</summary>
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

            //周围 4 格搜最近实心块
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

        /// <summary>锚点格须为激活实心格</summary>
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

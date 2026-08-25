using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.LaserTurrets
{
    /// <summary>
    /// 激光塔TP:远距单体狙击,家族里射程最远/单发耗电仅次于特斯拉。
    /// 锁定前用 Collision.CanHitLine 校验视线;标准攻击循环走基类骨架,
    /// 光束弹为普通 ModProjectile 由权威端生成
    /// </summary>
    internal class LaserTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<LaserTurretTile>();
        public override int TargetItem => ModContent.ItemType<LaserTurret>();
        public override float MaxUEValue => 1200;
        public override float AttackRange => 1200;
        public override float ShotCost => 10;
        public override int FireInterval => 90;
        /// <summary>粗筛不做瓦片检查,细筛在 <see cref="AcquireTarget"/> 里走 CanHitLine</summary>
        public override bool TargetingIgnoresTiles => true;

        /// <summary>单发伤害:狙击档,机械后数值</summary>
        internal const int ShotDamage = 120;

        internal float GlowIntensity;
        private int textIdleTime;

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止运行
        }

        /// <summary>塔顶发射口</summary>
        internal Vector2 MuzzlePosition => PosInWorld + new Vector2(Width * 0.5f, 8f);

        /// <summary>索敌:范围内最近敌怪,要求发射口到目标的直线视线(文档指定 CanHitLine)</summary>
        protected override NPC AcquireTarget() {
            Vector2 muzzle = MuzzlePosition;
            return CenterInWorld.FindClosestNPC(EffectiveRange, true, BossPriorityTargeting,
                chasedByNPC: npc => Collision.CanHitLine(muzzle, 1, 1, npc.Center, 1, 1));
        }

        protected override void UpdateTurret() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            if (AttackPattern) {
                RunAttackCycle();

                if (MachineData.UEvalue < EffectiveShotCost && textIdleTime <= 0
                    && CenterInWorld.FindClosestNPC(EffectiveRange, true, false) != null) {
                    //有敌无电才提示,避免刷屏
                    Defer(() => CombatText.NewText(HitBox, LaserTurret.Tint, LaserTurret.NoEnergyText.Value));
                    textIdleTime = 300;
                }
            }

            UpdateGlow();
        }

        /// <summary>生成一发光束弹:普通 ModProjectile,权威端生成,owner 取默认(服务器即255)</summary>
        protected override void Fire(NPC target) {
            Vector2 muzzle = MuzzlePosition;
            Vector2 dir = muzzle.To(target.Center).UnitVector();
            //并行阶段弹幕生成延迟到主线程执行(串行阶段立即执行)
            DeferSpawnProjectile(this.FromObjectGetParent(), muzzle, dir * 8f,
                ModContent.ProjectileType<LaserTurretBolt>(), ShotDamage, 4f);
        }

        /// <summary>充能辉光:冷却越满越亮,开火后从暗处重新蓄起</summary>
        private void UpdateGlow() {
            bool lit = AttackPattern && MachineData.UEvalue >= EffectiveShotCost;
            float target = lit ? MathHelper.Clamp(FireCoolden / (float)EffectiveFireInterval, 0.25f, 1f) : 0f;
            GlowIntensity = MathHelper.Lerp(GlowIntensity, target, 0.06f);
            if (GlowIntensity < 0.015f) {
                GlowIntensity = 0f;
            }
        }

        /// <summary>权威 gate 下客户端的表现帧:客户端不知冷却,辉光按模式与电量近似推进</summary>
        protected override void UpdateTurretClient() {
            bool lit = AttackPattern && MachineData.UEvalue >= EffectiveShotCost;
            GlowIntensity = MathHelper.Lerp(GlowIntensity, lit ? 0.8f : 0f, 0.05f);
            if (GlowIntensity < 0.015f) {
                GlowIntensity = 0f;
            }
        }

        /// <summary>模式翻转的本地反馈</summary>
        protected override void OnModeToggleEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f }, CenterInWorld);
            CombatText.NewText(HitBox, LaserTurret.Tint,
                AttackPattern ? LaserTurret.TurretOnText.Value : LaserTurret.TurretOffText.Value);
        }

        protected override void OnModeChangedByNet() {
            if (VaultUtils.isServer) {
                return;
            }
            CombatText.NewText(HitBox, LaserTurret.Tint,
                AttackPattern ? LaserTurret.TurretOnText.Value : LaserTurret.TurretOffText.Value);
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
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

        //---- 蓄能预警视觉状态:纯客户端表现,零网络 ----
        /// <summary>蓄能进度0~1:权威端=真实冷却;客户端=观测弹幕生成重置的伪冷却(误差=包延迟)</summary>
        internal float VisualCharge { get; private set; }
        /// <summary>预警是否活跃:蓄能末段且本地索敌有候选,给敌人反应窗</summary>
        internal bool TelegraphActive { get; private set; }
        /// <summary>开火闪光包络,光束弹首帧回调点亮</summary>
        internal float MuzzleFlash;
        private int clientCoolden;
        private int telegraphScanTimer;
        private bool oldTelegraph;

        /// <summary>预警进入蓄能末段的进度阈值</summary>
        internal const float TelegraphStart = 0.72f;

        /// <summary>光束弹首帧回调(全端):重置伪冷却+点亮炮口闪光</summary>
        internal void NotifyFired() {
            clientCoolden = 0;
            MuzzleFlash = 1f;
        }

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

            //蓄能进度:权威端直读真实冷却
            bool armed = AttackPattern && MachineData.UEvalue >= EffectiveShotCost;
            VisualCharge = armed
                ? MathHelper.Clamp(FireCoolden / (float)EffectiveFireInterval, 0f, 1f)
                : Math.Max(0f, VisualCharge - 0.06f);
            UpdateTelegraph(armed);
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

            //伪冷却:自上次观测到出膛起计帧,包延迟带来的数帧误差可接受
            if (lit) {
                if (clientCoolden < EffectiveFireInterval) {
                    clientCoolden++;
                }
                VisualCharge = MathHelper.Clamp(clientCoolden / (float)EffectiveFireInterval, 0f, 1f);
            }
            else {
                VisualCharge = Math.Max(0f, VisualCharge - 0.06f);
            }
            UpdateTelegraph(lit);
        }

        /// <summary>
        /// 预警活跃判定:蓄能末段每10帧扫一次本地索敌(NPC全端同步,客户端可自查),
        /// 有候选才亮收束线;预警起始边沿给一声低音蓄能提示音。
        /// 两端共用,炮口闪光包络也在此消退
        /// </summary>
        private void UpdateTelegraph(bool armed) {
            if (MuzzleFlash > 0f) {
                MuzzleFlash = Math.Max(0f, MuzzleFlash - 0.08f);
            }

            if (armed && VisualCharge >= TelegraphStart) {
                if (--telegraphScanTimer <= 0) {
                    telegraphScanTimer = 10;
                    TelegraphActive = AcquireTarget() != null;
                }
            }
            else {
                TelegraphActive = false;
                telegraphScanTimer = 0;
            }

            //预警起始边沿:低音蓄能提示(仅游戏端)
            if (TelegraphActive && !oldTelegraph && !VaultUtils.isServer) {
                Defer(() => SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.32f, Pitch = -0.35f }, MuzzlePosition));
            }
            oldTelegraph = TelegraphActive;
        }

        /// <summary>蓄能预警绘制:收束线+核点增亮,开火拍过曝闪;画在充能条同层</summary>
        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();

            float charge = VisualCharge;
            float flash = MuzzleFlash;
            bool telegraphOn = TelegraphActive && charge >= TelegraphStart;
            if (!telegraphOn && flash < 0.02f) {
                return;
            }

            var star = CWRAsset.StarTexture?.Value;
            var glowTex = CWRAsset.SoftGlow?.Value;
            if (star == null || glowTex == null) {
                return;
            }
            Vector2 muzzle = MuzzlePosition - Main.screenPosition;
            Color red = LaserTurretBolt.LaserRed;
            red.A = 0;
            Color white = Color.White;
            white.A = 0;

            if (telegraphOn) {
                //预警窗内进度:收束线自外滑向炮口,越近越亮
                float t = (charge - TelegraphStart) / (1f - TelegraphStart);
                float baseAng = Main.GlobalTimeWrappedHourly * 1.2f + Position.X * 0.7f;
                float dist = MathHelper.Lerp(27f, 7f, t);
                for (int i = 0; i < 5; i++) {
                    float ang = baseAng + MathHelper.TwoPi * i / 5f;
                    Vector2 dir = ang.ToRotationVector2();
                    Vector2 pos = muzzle + dir * dist;
                    //细梭沿半径指向炮口
                    spriteBatch.Draw(star, pos, null, red * (0.35f + 0.5f * t), ang + MathHelper.PiOver2,
                        star.Size() * 0.5f, new Vector2(0.016f, 0.05f + 0.035f * t), SpriteEffects.None, 0f);
                }
                //核点增亮:红核+末段白心
                spriteBatch.Draw(glowTex, muzzle, null, red * (0.30f + 0.55f * t), 0f,
                    glowTex.Size() * 0.5f, 0.36f + 0.24f * t, SpriteEffects.None, 0f);
                spriteBatch.Draw(glowTex, muzzle, null, white * (0.30f * t * t), 0f,
                    glowTex.Size() * 0.5f, 0.16f + 0.12f * t, SpriteEffects.None, 0f);
            }

            if (flash > 0.02f) {
                //开火拍:炮口过曝闪快速退潮
                float grow = 0.8f + 0.4f * (1f - flash);
                spriteBatch.Draw(glowTex, muzzle, null, white * (0.55f * flash), 0f,
                    glowTex.Size() * 0.5f, 0.5f + 0.3f * (1f - flash), SpriteEffects.None, 0f);
                spriteBatch.Draw(star, muzzle, null, red * (0.75f * flash), 0f,
                    star.Size() * 0.5f, new Vector2(0.11f, 0.05f) * grow, SpriteEffects.None, 0f);
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

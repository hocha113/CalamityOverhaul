using CalamityOverhaul.Content.Scenarios.Kiyume.NPCs;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Stealth
{
    /// <summary>
    /// 潜行 per-player 状态页：静止计时、落地/开火脉冲（帧事件转脉冲量）、
    /// 光源档与藏身因子缓存（6t 重算）。全部实例字段零 static（联机纪律）；
    /// 输入（velocity/itemAnimation/HeldItem/buff）皆原版同步，各端各算同式，本类零发包
    /// </summary>
    internal class KiyumeStealthPlayer : ModPlayer
    {
        //══ 静止（动词「站住别动」） ══
        private int stillTicks;
        /// <summary>速度门下持续 30t 即静止：听觉归零由速度档天然给出，视觉走 StillMul</summary>
        internal bool IsStill => stillTicks >= KiyumeHoundMetrics.StillGateTicks;

        //══ 脉冲（0..1，线性衰减；SoundExposure 乘档案倍率消费） ══
        /// <summary>落地冲击脉冲</summary>
        internal float LandPulse { get; private set; }
        /// <summary>开火/挥械脉冲</summary>
        internal float FirePulse { get; private set; }
        private float prevVelY;
        private int prevItemAnimation;

        //══ 6t 缓存（惰性重算，无人问不算） ══
        private uint lightStamp;
        private float lightCache;
        private uint shelterStamp;
        private float shelterCache = 1f;

        /// <summary>光源档 0/0.5/1（真算在 KiyumeStealthSense.ComputeLightRaw）</summary>
        internal float LightTier {
            get {
                if (Main.GameUpdateCount - lightStamp >= KiyumeHoundMetrics.SenseCacheTicks) {
                    lightStamp = Main.GameUpdateCount;
                    lightCache = KiyumeStealthSense.ComputeLightRaw(Player);
                }
                return lightCache;
            }
        }

        /// <summary>藏身因子 1/0.3（真算在 KiyumeStealthSense.ComputeShelterRaw）</summary>
        internal float Shelter {
            get {
                if (Main.GameUpdateCount - shelterStamp >= KiyumeHoundMetrics.SenseCacheTicks) {
                    shelterStamp = Main.GameUpdateCount;
                    shelterCache = KiyumeStealthSense.ComputeShelterRaw(Player);
                }
                return shelterCache;
            }
        }

        public override void PostUpdate() {
            if (!KiyumeWorld.Active) {
                //梦外清零：回主世界不留残迹，再入梦从干净页起步
                stillTicks = 0;
                LandPulse = 0f;
                FirePulse = 0f;
                prevVelY = Player.velocity.Y;
                prevItemAnimation = Player.itemAnimation;
                return;
            }

            stillTicks = Player.velocity.Length() < KiyumeHoundMetrics.StillSpeedGate
                ? stillTicks + 1 : 0;

            //嗅迹记点（点子 13）：奔跑（速度门与听觉 RunSpeedGate 同源）且贴地才留迹，
            //静止/走路无迹；Record 内含权威端门，客户端调用空转
            if (Main.GameUpdateCount % KiyumeHoundMetrics.ScentRecordIntervalTicks == 0
                && Player.velocity.Y == 0f
                && Player.velocity.Length() >= KiyumeHoundMetrics.RunSpeedGate) {
                KiyumeScentTrail.Record(Player.Center, Player.whoAmI);
            }

            //落地脉冲：上帧还在下落、本帧落稳，冲击随下落速度放大
            if (Player.velocity.Y == 0f && prevVelY >= KiyumeHoundMetrics.LandFallGate) {
                LandPulse = MathHelper.Clamp(prevVelY / KiyumeHoundMetrics.LandFallFull, 0.4f, 1f);
            }
            //开火脉冲：使用动画上升沿 + 持武器（含近战，挥刀也是响动）
            if (Player.itemAnimation > prevItemAnimation && Player.HeldItem.damage > 0) {
                FirePulse = 1f;
            }
            float fade = 1f / KiyumeHoundMetrics.PulseFadeTicks;
            LandPulse = MathF.Max(0f, LandPulse - fade);
            FirePulse = MathF.Max(0f, FirePulse - fade);

            prevVelY = Player.velocity.Y;
            prevItemAnimation = Player.itemAnimation;
        }

        public override void UpdateDead() {
            //死亡帧不走 PostUpdate（tML 契约），残余脉冲手动归零
            stillTicks = 0;
            LandPulse = 0f;
            FirePulse = 0f;
        }

        //══ W3 P2-D：惊醒死亡语义（裁决 3：梦里血尽不死，只会醒） ══
        //per-player hook 在跑到 KillMe 的各端各自拦截；锁血/演出/出梦全在 KiyumeDreamWake
        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource) {
            if (KiyumeWorld.Active && KiyumeDreamWake.InterceptDeath(Player, damageSource)) {
                playSound = false;
                genGore = false;
                return false;
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genGore, ref damageSource);
        }
    }
}

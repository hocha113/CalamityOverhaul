using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos
{
    /// <summary>
    /// 太空高层逐玩家状态：「气薄」缺氧累积与「星屑升流」的上升助力。
    /// 缺氧是本机玩家私有演出量（屏边渐晕与呼吸声都由它驱动），满格施加短暂原版虚弱后回落半程，
    /// 落回低空快速恢复；档位只调累积速度。Boss 在场时累积冻结（减益机制暂停）。
    /// 升流助力按同步弹幕几何在各模拟端确定性施加，位置最终以拥有端为权威
    /// </summary>
    internal class StratosPlayer : ModPlayer
    {
        /// <summary>缺氧累积 0~1（逐玩家实例态）</summary>
        private float hypoxia;
        /// <summary>呼吸相位，渐晕脉动与呼吸声共用同一时钟</summary>
        private float breathPhase;

        /// <summary>充满所需秒数，档位只调累积速度不换机制</summary>
        private static readonly int[] FillSecondsByTier = [78, 58, 42];
        /// <summary>满格触发的原版虚弱时长</summary>
        private const int WeakFrames = 360;
        /// <summary>触发后回落的水位，避免贴着满格反复刷减益</summary>
        private const float ReliefFloor = 0.55f;
        /// <summary>低空恢复速度：约 5 秒清空</summary>
        private const float RecoverPerFrame = 1f / 300f;

        public float Hypoxia => hypoxia;
        /// <summary>呼吸波 0~1</summary>
        public float BreathWave => 0.5f + 0.5f * MathF.Sin(breathPhase);
        /// <summary>呼吸声强度 0~1：缺氧过三成才可闻，随深度渐重</summary>
        public float BreathLoud => MathHelper.Clamp((hypoxia - 0.30f) / 0.70f, 0f, 1f);

        public override void PostUpdateMiscEffects() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;//缺氧由拥有端结算：视觉音效皆本机，虚弱走原生 buff 同步
            }
            bool inZone = GameModeSystem.BrutalActive && Player.ZoneSkyHeight;
            if (inZone && !CWRWorld.HasBoss) {
                int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
                hypoxia += 1f / (60f * FillSecondsByTier[tier - 1]);
                if (hypoxia >= 1f) {
                    hypoxia = ReliefFloor;
                    Player.AddBuff(BuffID.Weak, WeakFrames);
                    SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.55f, Pitch = 0.25f }, Player.Center);
                }
            }
            else if (!inZone) {
                hypoxia = MathHelper.Clamp(hypoxia - RecoverPerFrame, 0f, 1f);
            }
            //Boss 在场且仍在高空：不涨不掉，冻结等待

            if (hypoxia > 0.01f) {
                breathPhase += 0.032f + 0.034f * hypoxia;//缺氧越深呼吸越急
            }
        }

        public override void UpdateDead() {
            if (Player.whoAmI == Main.myPlayer) {
                hypoxia = MathHelper.Clamp(hypoxia - RecoverPerFrame * 4f, 0f, 1f);
            }
        }

        /// <summary>升流助力：柱内温和上推并清空坠落计数（进柱即免摔伤，空中机动的甜头）</summary>
        public override void PreUpdateMovement() {
            if (Player.dead || !GameModeSystem.BrutalActive || CWRWorld.HasBoss) {
                return;
            }
            int type = ModContent.ProjectileType<StratosUpdraftProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != type || proj.ModProjectile is not StratosUpdraftProj column) {
                    continue;
                }
                if (!column.TryGetLift(out Rectangle zone, out float strength) || !zone.Intersects(Player.Hitbox)) {
                    continue;
                }
                Player.velocity.Y -= StratosUpdraftProj.LiftAccel * strength;
                if (Player.velocity.Y < -StratosUpdraftProj.LiftMax) {
                    Player.velocity.Y = -StratosUpdraftProj.LiftMax;
                }
                Player.fallStart = (int)(Player.position.Y / 16f);
                break;
            }
        }
    }
}

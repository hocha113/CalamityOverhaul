using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories.Offense
{
    /// <summary>
    /// 【狙击镜链】步枪镜/狙击镜/侦察镜多认领共用「稳像狙击」：站稳蓄稳像，
    /// 蓄满后下一次远程命中放出贯穿曳光；伤害比例与穿透数随链递进，高速移动流失稳像。<br/>
    /// 曳光为 DamageClass.Default（远程过滤防自喂）；稳像在同文件私有 <see cref="ScopeSteadyPlayer"/>，
    /// 触发用链共用冷却键（TargetItemIDs[0]）防两镜同时结算
    /// </summary>
    internal class GodSmithRifleScope : GodSmithAccEffect
    {
        /// <summary>蓄满稳像所需帧数</summary>
        internal const int StabilityMax = 75;

        public override int[] TargetItemIDs => [ItemID.RifleScope, ItemID.SniperScope, ItemID.ReconScope];

        protected override string EffectDescFallback =>
            "Steady Aim: standing still for 1.25s steadies the scope; your next ranged hit looses a piercing tracer\nThe tracer deals 45% / 60% / 75% of that hit (Rifle / Sniper / Recon) and pierces 2 / 3 / 4 foes\nMoving fast drains your aim";

        /// <summary>档位：1 步枪镜 / 2 狙击镜 / 3 侦察镜</summary>
        internal static int TierOf(int itemType)
            => itemType == ItemID.RifleScope ? 1 : itemType == ItemID.SniperScope ? 2 : 3;

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            //登记本帧最高档位；稳像积累在私有 ModPlayer 里每帧只走一次
            ScopeSteadyPlayer steady = player.GetModPlayer<ScopeSteadyPlayer>();
            steady.BestTier = Math.Max(steady.BestTier, TierOf(item.type));
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged)) {
                return;
            }
            ScopeSteadyPlayer steady = player.GetModPlayer<ScopeSteadyPlayer>();
            //链共用冷却键：两镜同佩也只结算一发
            if (steady.Stability < StabilityMax || !state.TryUseCooldown(TargetItemIDs[0], 10)) {
                return;
            }
            steady.Stability = 0;
            int tier = Math.Max(steady.BestTier, 1);
            SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.45f, Pitch = 0.5f }, player.Center);
            if (player.whoAmI == Main.myPlayer) {
                float ratio = tier == 1 ? 0.45f : tier == 2 ? 0.60f : 0.75f;
                int pierce = tier + 1;
                int tracerDamage = Math.Clamp((int)(damageDone * ratio), 10, 320);
                Vector2 vel = (target.Center - player.Center).SafeNormalize(Vector2.UnitX) * 20f;
                Projectile.NewProjectile(player.GetSource_Accessory(item), player.Center, vel,
                    ModContent.ProjectileType<GodSmithRifleScopeTracerProj>(), tracerDamage, 3f, player.whoAmI,
                    pierce);
            }
        }
    }

    /// <summary>精准曳光：一发被稳像压出的贯穿弹道，笔直、快、不回头</summary>
    internal class GodSmithRifleScopeTracerProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        private ref float PierceParam => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //穿透数随生成参数定档（ai 随生成包过线，各端一致）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.penetrate = (int)MathHelper.Clamp(PierceParam <= 0f ? 2f : PierceParam, 1f, 5f);
            }
            //原版子弹贴图竖向朝上，旋转补四分之一圈
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
        }
    }

    /// <summary>狙击镜链私有状态载体：稳像积累与本帧最高档位。攻击方端本地量，无需同步</summary>
    internal class ScopeSteadyPlayer : ModPlayer
    {
        /// <summary>稳像积累（帧）</summary>
        internal int Stability;

        /// <summary>本帧佩戴的最高镜档（UpdateAccessory 登记，ResetEffects 清零）</summary>
        internal int BestTier;

        private bool wasFull;

        public override void ResetEffects() => BestTier = 0;

        public override void PostUpdateMiscEffects() {
            if (BestTier <= 0) {
                Stability = 0;
                wasFull = false;
                return;
            }
            float speed = Player.velocity.Length();
            if (speed < 0.5f) {
                Stability = Math.Min(Stability + 1, GodSmithRifleScope.StabilityMax);
            }
            else if (speed > 3f) {
                Stability = Math.Max(0, Stability - 3);
            }
            bool full = Stability >= GodSmithRifleScope.StabilityMax;
            //蓄满一瞬：镜心咔哒定格（个人读数，仅佩戴者本端）
            if (full && !wasFull && Player.whoAmI == Main.myPlayer && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.35f, Pitch = 0.7f }, Player.Center);
            }
            wasFull = full;
        }

        public override void UpdateDead() {
            Stability = 0;
            wasFull = false;
        }
    }
}

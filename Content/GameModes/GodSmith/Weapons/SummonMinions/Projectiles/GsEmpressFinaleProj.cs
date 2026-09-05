using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 虹彩终幕：棱镜剑群的处决仪式。四相 = 展扇 10 帧（锚定目标，无伤害）/
    /// 连刺 24 帧（每 4 帧一柄剑序贯贯穿，伤害窗）/ 碎光 8 帧（无伤害）/ 余彩 10 帧。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验
    /// </summary>
    internal class GsEmpressFinaleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EmpressBlade;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int FanFrames = 10;
        private const int PlungeFrames = 24;
        private const int ShatterFrames = 8;
        private const int AfterFrames = 10;
        private const int TotalFrames = FanFrames + PlungeFrames + ShatterFrames + AfterFrames;
        private const int BladeCount = 6;
        /// <summary>每柄剑的贯穿间隔</summary>
        private const int PlungeGap = PlungeFrames / BladeCount;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Fanning => Elapsed < FanFrames;

        private bool Plunging => Elapsed >= FanFrames && Elapsed < FanFrames + PlungeFrames;

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //连刺节拍：一剑一段
            Projectile.localNPCHitCooldown = PlungeGap;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //展扇期咬住目标，连刺起锚定
            if (Fanning) {
                NPC target = BoundTarget;
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = target.Center;
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item162 with { Volume = 0.55f, Pitch = 0.2f },
                    Projectile.Center);
            }
            //连刺节拍音：每柄剑落下时一声棱鸣
            if (Plunging && (Elapsed - FanFrames) % PlungeGap == 0) {
                int idx = (Elapsed - FanFrames) / PlungeGap;
                SoundEngine.PlaySound(SoundID.Item163 with {
                    Volume = 0.4f,
                    Pitch = -0.3f + idx * 0.12f
                }, Projectile.Center);
            }
            //碎光首帧：棱镜炸裂
            if (Elapsed == FanFrames + PlungeFrames) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, Pitch = 0.1f },
                    Projectile.Center);
            }
        }

        /// <summary>只有连刺相结算伤害</summary>
        public override bool? CanDamage() => Plunging ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(92f, 124f))
                .Intersects(targetHitbox);
    }
}

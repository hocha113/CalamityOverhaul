using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 附体绞锯：致命球拆下一环锯齿铆进猎物，原地空转研磨。跟随目标，
    /// 三相 = 咬合 8 帧（无伤害）/ 研磨 44 帧（每 15 帧一段锯伤，磨得越久转得越快）/
    /// 脱转 8 帧（无伤害）。ai[0] = 目标索引，ai[1] = 目标类型校验
    /// </summary>
    internal class GsDeadlySphereSawProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornChakram;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int BiteFrames = 8;
        private const int GrindFrames = 44;
        private const int LooseFrames = 8;
        private const int TotalFrames = BiteFrames + GrindFrames + LooseFrames;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Grinding => Elapsed >= BiteFrames && Elapsed < BiteFrames + GrindFrames;

        /// <summary>研磨热度 0~1（决定空转角速度）</summary>
        private float HeatT => MathHelper.Clamp((Elapsed - BiteFrames) / (float)GrindFrames, 0f, 1f);

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
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //研磨 44 帧内约 3 段锯伤
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            NPC target = BoundTarget;
            if (target == null) {
                //目标失效：各端本地同判甩脱
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;
            //空转角速度随热度提升
            Projectile.rotation += 0.22f + 0.3f * HeatT;

            if (VaultUtils.isServer) {
                return;
            }
            //咬合首帧：锯环铆入
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.6f, Pitch = -0.15f },
                    Projectile.Center);
            }
            //脱转首帧：锯环甩脱
            if (Elapsed == BiteFrames + GrindFrames) {
                SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.45f, Pitch = 0.2f },
                    Projectile.Center);
            }
        }

        /// <summary>只有研磨相结算伤害</summary>
        public override bool? CanDamage() => Grinding ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.3f, Pitch = 0.3f },
                Projectile.Center);
        }
    }
}

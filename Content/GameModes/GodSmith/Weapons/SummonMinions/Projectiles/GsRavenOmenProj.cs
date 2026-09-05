using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 凶兆坍缩：鸦群啄出的不祥征兆在猎物身上聚拢成形。跟随目标，
    /// 三相 = 收羽 14 帧（无伤害）/ 爆鸣 6 帧（伤害窗 + 暗影焰）/ 落羽 16 帧（无伤害）。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验（随生成包过线）
    /// </summary>
    internal class GsRavenOmenProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowFlame;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int GatherFrames = 14;
        private const int BurstFrames = 6;
        private const int DriftFrames = 16;
        private const int TotalFrames = GatherFrames + BurstFrames + DriftFrames;
        private const float BurstRadius = 70f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool InBurst => Elapsed >= GatherFrames && Elapsed < GatherFrames + BurstFrames;

        private bool Drifting => Elapsed >= GatherFrames + BurstFrames;

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
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = TotalFrames;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //收羽期跟住目标，爆鸣起锚定原地（死鸟不追尸）
            NPC target = BoundTarget;
            if (Elapsed < GatherFrames) {
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
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.2f },
                    Projectile.Center);
            }
            //爆鸣首帧：凶兆炸裂
            if (Elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.6f, Pitch = -0.4f },
                    Projectile.Center);
            }
        }

        /// <summary>只有爆鸣窗结算伤害</summary>
        public override bool? CanDamage() => InBurst ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(BurstRadius * 2f))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.ShadowFlame, 180);

        /// <summary>区域尺寸提示：原版暗影焰贴图按爆鸣半径缩放一笔（收羽相张开、落羽相渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float grow = MathHelper.Clamp(Elapsed / (float)GatherFrames, 0.2f, 1f);
            float fade = Drifting
                ? MathHelper.Clamp(Projectile.timeLeft / (float)DriftFrames, 0f, 1f) : 1f;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f, BurstRadius * 2f / tex.Width * grow, SpriteEffects.None, 0);
            return false;
        }
    }
}

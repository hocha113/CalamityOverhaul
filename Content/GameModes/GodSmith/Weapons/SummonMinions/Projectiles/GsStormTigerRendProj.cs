using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 三连爪撕：沙漠虎的伏杀仪式在猎物身上留下的三道爪痕。跟随目标，
    /// 节奏 = 第 0/8/16 帧各落一道爪痕（各结算一段伤害），
    /// 24 帧后进入 16 帧沙散收尾（无伤害）。ai[0] = 目标索引，ai[1] = 目标类型校验
    /// </summary>
    internal class GsStormTigerRendProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallGun;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int TearGap = 8;
        private const int TearCount = 3;
        private const int RendFrames = TearGap * TearCount;
        private const int SettleFrames = 16;
        private const int TotalFrames = RendFrames + SettleFrames;
        /// <summary>判定方区边长</summary>
        private const float RendSpan = 84f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Rending => Elapsed < RendFrames;

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
            Projectile.width = 70;
            Projectile.height = 70;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //一爪一段
            Projectile.localNPCHitCooldown = TearGap;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //撕扯期跟住目标；沙散期留在原地风化
            if (Rending) {
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
            //每道爪痕落下：破风声
            if (Rending && Elapsed % TearGap == 0) {
                int idx = Elapsed / TearGap;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.7f,
                    Pitch = -0.2f + idx * 0.15f
                }, Projectile.Center);
            }
        }

        /// <summary>只有撕扯期结算伤害</summary>
        public override bool? CanDamage() => Rending ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(RendSpan))
                .Intersects(targetHitbox);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.25f },
                Projectile.Center);
        }

        /// <summary>区域尺寸提示：原版沙球贴图按判定尺寸缩放一笔（沙散相渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float settleFade = Rending
                ? 1f : MathHelper.Clamp(Projectile.timeLeft / (float)SettleFrames, 0f, 1f);
            if (settleFade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor * settleFade, 0f, tex.Size() / 2f, RendSpan / tex.Width,
                SpriteEffects.None, 0);
            return false;
        }
    }
}

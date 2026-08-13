using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 舌鞭：沿锁定线暴射的舌体，命中把玩家往嘴里拽一程。
    /// ai[0]=墙whoAmI ai[1]=总寿命；spawn时velocity=单位方向(锁线)。
    /// 本体位置=舌尖；链体自口器铺到舌尖
    /// </summary>
    internal class WofTongueLashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float ExtendSpeed = 30f;
        private const float MaxReach = 950f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Wall => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        /// <summary>锁定方向常驻 velocity 槽(spawn包携带、引擎不积分位移)</summary>
        private Vector2 LashDir => Projectile.velocity.SafeNormalize(Vector2.UnitX);

        /// <summary>当前伸出长度(各端由本地Timer确定性推进)</summary>
        private float reach;
        /// <summary>0伸出 1回收</summary>
        private int stage;

        //方向存velocity只作数据槽，不做位移积分
        public override bool ShouldUpdatePosition() => false;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 140;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC wall = Wall;
            if (!wall.Alives()) {
                Projectile.Kill();
                return;
            }

            Vector2 lashDir = LashDir;
            Timer++;

            Vector2 mouth = wall.Center;
            int totalLife = (int)Math.Max(Projectile.ai[1], 40f);

            if (stage == 0) {
                //暴射伸出
                reach += ExtendSpeed;
                if (reach >= MaxReach || Timer >= totalLife * 0.5f) {
                    reach = Math.Min(reach, MaxReach);
                    stage = 1;
                }
            }
            else {
                //加速回收
                reach -= ExtendSpeed * 1.35f;
                if (reach <= 10f) {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.Center = mouth + lashDir * reach;
            Projectile.rotation = lashDir.ToRotation();

            if (VaultUtils.isServer) {
                return;
            }
            //舌尖甩涎
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    lashDir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(1f, 4f),
                    WofMotionFX.BloodMid, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(14, 26), 0.32f);
            }
            Lighting.AddLight(Projectile.Center, WofMotionFX.BloodHot.ToVector3() * 0.4f);
        }

        /// <summary>线体碰撞：口器→舌尖全段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC wall = Wall;
            if (!wall.Alives()) {
                return false;
            }
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                wall.Center, wall.Center + LashDir * reach, 26f, ref p);
        }

        /// <summary>命中：往嘴里拽一程(受击端本地结算)，并转入回收</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            NPC wall = Wall;
            if (wall.Alives()) {
                Vector2 yank = (wall.Center - target.Center).SafeNormalize(Vector2.Zero) * 13f;
                target.velocity = yank;
                if (Main.expertMode) {
                    target.AddBuff(BuffID.Bleeding, 300);
                }
            }
            stage = 1;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.2f, Volume = 1f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC wall = Wall;
            if (!wall.Alives() || reach < 8f) {
                return false;
            }

            Texture2D chainTex = TextureAssets.Chain12.Value;
            Texture2D drop = CWRAsset.Extra_98.Value;
            Vector2 mouth = wall.Center;
            Vector2 lashDir = LashDir;
            float segLen = chainTex.Height;
            int segments = (int)(reach / segLen) + 1;
            float chainRot = lashDir.ToRotation() + MathHelper.PiOver2;
            Vector2 perp = lashDir.RotatedBy(MathHelper.PiOver2);

            //回收期舌体松弛下垂
            float slack = stage == 1 ? 18f : 5f;

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float sag = (float)Math.Sin(t * MathHelper.Pi) * slack
                    + (float)Math.Sin(t * 9f + Main.GlobalTimeWrappedHourly * 14f) * 2.5f;
                Vector2 pos = mouth + lashDir * (t * reach) + perp * sag;
                Color light = Lighting.GetColor((int)pos.X / 16, (int)(pos.Y / 16f));
                //舌肉着色偏红
                Color tint = Color.Lerp(light, WofMotionFX.BloodMid, 0.4f);
                spriteBatchDraw(chainTex, pos, tint, chainRot);
            }

            //舌尖肉锤：暗核+湿高光
            Vector2 tipScreen = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(drop, tipScreen, null, WofMotionFX.BloodDark,
                Projectile.rotation + MathHelper.PiOver2, drop.Size() / 2f,
                new Vector2(0.6f, 0.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(drop, tipScreen - new Vector2(2f, 3f), null, WofMotionFX.BloodHot * 0.7f,
                Projectile.rotation + MathHelper.PiOver2, drop.Size() / 2f,
                new Vector2(0.36f, 0.45f), SpriteEffects.None, 0);
            return false;
        }

        private static void spriteBatchDraw(Texture2D tex, Vector2 worldPos, Color color, float rotation) {
            Main.EntitySpriteDraw(tex, worldPos - Main.screenPosition, null, color, rotation,
                tex.Size() / 2f, 1f, SpriteEffects.None, 0);
        }
    }
}

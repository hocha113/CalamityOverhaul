using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin.Projectiles
{
    /// <summary>
    /// 超级跳落地冲击波：沿地面单向推进的凝胶浪，贴地爬坡、高墙与断崖息浪。
    /// 浪高有限可跳越（具名阀门），射程走完淡出，淡出期无伤害。
    /// ai[0]=方向(±1)，ai[1]=射程px，ai[2]=凝胶色；地形数据全端同步，推进各端确定性一致
    /// </summary>
    internal class SlimeQuakeWave : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private const float WaveSpeed = 7f;
        private const int WaveWidth = 26;
        /// <summary>浪高（判定=可视）：常规跳跃即可越过，是本机制的具名逃生阀门</summary>
        private const int WaveHeight = 34;
        /// <summary>出膛淡入帧，期间无伤害</summary>
        private const int FadeInFrames = 4;
        private const int FadeOutFrames = 8;
        /// <summary>单帧爬坡上限（px），更高的墙直接息浪——躲上台阶是第二逃生阀门</summary>
        private const float ClimbLimit = 26f;
        /// <summary>向下寻地上限（格），断崖息浪</summary>
        private const int DropTilesMax = 3;

        private int Dir => Projectile.ai[0] >= 0f ? 1 : -1;
        private float MaxRange => Projectile.ai[1] <= 0f ? 300f : Projectile.ai[1];
        private Color Gel => SlimeKinFlavor.UnpackColor(Projectile.ai[2]);

        private ref float Age => ref Projectile.localAI[0];
        private ref float Traveled => ref Projectile.localAI[1];

        /// <summary>-1 = 推进中；≥0 = 淡出计帧（射程尽/地形止）</summary>
        private int fadeTimer = -1;

        public override void SetDefaults() {
            Projectile.width = WaveWidth;
            Projectile.height = WaveHeight;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>伤害窗口 = 可见推进窗口：淡入未完与淡出期都无伤害</summary>
        public override bool? CanDamage() => Age > FadeInFrames && fadeTimer < 0 ? null : false;

        public override void AI() {
            Age++;
            if (Age == 1f && !VaultUtils.isServer) {
                //落地反馈只播一次（右浪代表整次落地）
                if (Dir == 1) {
                    SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.35f, Volume = 0.8f, MaxInstances = 4 }, Projectile.Center);
                }
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Bottom, DustID.t_Slime,
                        new Vector2(Dir * Main.rand.NextFloat(0.5f, 2.5f), -Main.rand.NextFloat(1.5f, 4f)), 110, Gel, 1.3f);
                    dust.noGravity = Main.rand.NextBool();
                }
            }

            if (fadeTimer >= 0) {
                if (++fadeTimer >= FadeOutFrames) {
                    Projectile.Kill();
                }
                return;
            }

            //推进 + 贴地
            Projectile.position.X += Dir * WaveSpeed;
            Traveled += WaveSpeed;
            if (!SnapToGround() || Traveled >= MaxRange) {
                fadeTimer = 0;
                return;
            }

            //浪尖溅胶
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Dir * 10f, -Main.rand.NextFloat(4f, WaveHeight)),
                    DustID.t_Slime, new Vector2(Dir * 1.2f, -1.4f), 120, Gel, 1.05f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, Gel.ToVector3() * 0.2f);
        }

        /// <summary>把浪底吸附到地表；无地可依（高墙/断崖/出界）返回 false 息浪</summary>
        private bool SnapToGround() {
            int tileX = (int)(Projectile.Center.X / 16f);
            int bottomTileY = (int)(Projectile.Bottom.Y / 16f);
            if (tileX < 10 || tileX > Main.maxTilesX - 10) {
                return false;
            }
            for (int y = bottomTileY - 2; y <= bottomTileY + DropTilesMax; y++) {
                if (y < 10 || y > Main.maxTilesY - 10) {
                    return false;
                }
                if (!WorldGen.SolidTile(tileX, y)) {
                    continue;
                }
                float newBottom = y * 16f;
                float climb = Projectile.Bottom.Y - newBottom;
                if (climb > ClimbLimit) {
                    return false;
                }
                Projectile.Bottom = new Vector2(Projectile.Center.X, newBottom);
                return true;
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 90);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            //底边锚定，浪从地面隆起
            Vector2 origin = new Vector2(tex.Width * 0.5f, tex.Height);
            float fadeIn = MathHelper.Clamp(Age / FadeInFrames, 0f, 1f);
            float fadeOut = fadeTimer < 0 ? 1f : 1f - fadeTimer / (float)FadeOutFrames;
            float vis = fadeIn * fadeOut;
            if (vis <= 0f) {
                return false;
            }
            Color gel = Gel;
            float lean = Dir * 0.12f;

            //三峰浪体：前峰最高，后峰是同材质拖尾（横向厚度≥前峰一半）
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Bottom - new Vector2(Dir * i * 15f, -2f) - Main.screenPosition;
                float hump = 1f - i * 0.24f;
                Vector2 scale = new Vector2(WaveWidth * 2.2f / tex.Width * (1f - i * 0.12f),
                    WaveHeight * 1.35f / tex.Height * hump);
                //真 alpha 暗胶层
                Main.EntitySpriteDraw(tex, pos, null, gel * (0.72f * vis * (1f - i * 0.22f)),
                    lean, origin, scale, SpriteEffects.None, 0);
                //加色亮芯
                Main.EntitySpriteDraw(tex, pos, null, (Color.Lerp(gel, Color.White, 0.3f) with { A = 0 }) * (0.30f * vis * (1f - i * 0.28f)),
                    lean, origin, scale * new Vector2(0.55f, 0.8f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 鹦鹉盯梢标记：ai[0]=被盯玩家索引。零伤害纯视觉骚扰，掠过玩家的鹦鹉在其头顶
    /// 插一撮盯梢羽标（羽毛环绕+聒噪音效），存续期结束自然消散，不挂任何减益、不参与任何判定
    /// </summary>
    internal class PrtParrotMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>标记存续帧数</summary>
        internal const int MarkFrames = 240;
        /// <summary>头顶悬浮高度</summary>
        private const float HoverHeight = 34f;

        //鹦鹉三色
        private static readonly Color ParrotRed = new Color(230, 72, 60);
        private static readonly Color ParrotYellow = new Color(255, 214, 90);
        private static readonly Color ParrotBlue = new Color(80, 150, 235);
        private static readonly Color[] TriColors = [ParrotRed, ParrotYellow, ParrotBlue];

        private int TargetIndex => (int)Projectile.ai[0];
        private int Age => MarkFrames - Projectile.timeLeft;

        /// <summary>该玩家是否已有存活标记（服务端触发前查重）</summary>
        internal static bool HasMarkOn(int playerIndex) {
            int type = ModContent.ProjectileType<PrtParrotMark>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == playerIndex) {
                    return true;
                }
            }
            return false;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MarkFrames;
            Projectile.netImportant = true;
        }

        /// <summary>零伤害标记，永不判定</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (TargetIndex < 0 || TargetIndex >= Main.maxPlayers) {
                Projectile.Kill();
                return;
            }
            Player target = Main.player[TargetIndex];
            if (!target.Alives()) {
                if (!VaultUtils.isClient) {
                    Projectile.Kill();
                }
                return;
            }

            //gfxOffY：上台阶步进的绘制补偿
            Projectile.Center = target.Top + new Vector2(0f, target.gfxOffY - HoverHeight + (float)Math.Sin(Age * 0.08f) * 4f);

            if (Age == 1 && !Main.dedServ) {
                //聒噪：挂在已同步实体的出生帧，各端本地触发
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.55f, Pitch = 0.85f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    Dust feather = Dust.NewDustPerfect(Projectile.Center,
                        DustID.YellowTorch, Main.rand.NextVector2Circular(2.2f, 1.6f), 120, default, 1.1f);
                    feather.noGravity = true;
                }
            }

            //存续期零星羽屑（≤1 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust drift = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 6f),
                    Main.rand.NextBool() ? DustID.RedTorch : DustID.YellowTorch,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), 0.5f), 150, default, 0.7f);
                drift.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.Feather);
            Texture2D feather = TextureAssets.Item[ItemID.Feather].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float fadeIn = MathHelper.Clamp(Age / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f) {
                return false;
            }

            //三色羽标：真 alpha 羽毛贴图环绕旋摆（实体感），底下垫一点加色晕
            Main.EntitySpriteDraw(glow, center, null, (ParrotYellow with { A = 0 }) * (0.3f * alpha),
                0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            for (int i = 0; i < 3; i++) {
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + i * 2.1f + Projectile.identity) * 0.35f;
                float baseRot = -0.5f + 0.5f * i;
                Vector2 offset = new Vector2((i - 1) * 8f, MathF.Abs(i - 1) * 3f);
                Main.EntitySpriteDraw(feather, center + offset, null,
                    Color.Lerp(TriColors[i], lightColor, 0.25f) * alpha,
                    baseRot + sway, new Vector2(feather.Width / 2f, feather.Height), 0.8f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

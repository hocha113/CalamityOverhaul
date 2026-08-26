using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles
{
    /// <summary>
    /// 惑真残影：ai[0]=宿主NPC索引 ai[1]=宿主类型。
    /// 永不造成伤害、弹丸直接穿过（穿过=假身，这是第二条读法）；
    /// 用宿主自己的原版贴图绘制但更暗，真体由镜像加亮——真体更亮即可读性。
    /// 追踪走确定性参数（宿主目标+标识哈希摆动），各端模拟一致无需同步
    /// </summary>
    internal class EMDecoyProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>残影存续帧数</summary>
        internal const int DecoyLife = 240;
        /// <summary>残影相对真体的亮度（更暗=假）</summary>
        internal const float DecoyDim = 0.5f;

        private ref float Age => ref Projectile.localAI[0];
        private int HostIndex => (int)Projectile.ai[0];
        private int HostType => (int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DecoyLife;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;
            if (Age == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            }

            bool hostValid = HostIndex.TryGetNPC(out NPC npc) && npc.type == HostType;
            if (hostValid && npc.TryGetGlobalNPC(out EliteMoveNPC global)) {
                global.StampDecoyGlow();    //真体加亮窗口
            }
            else if (Projectile.timeLeft > 20) {
                Projectile.timeLeft = 20;   //宿主没了：快速消散
            }

            //确定性追踪：奔向宿主的目标玩家，摆动由标识哈希驱动（不掷骰）
            if (hostValid) {
                Player target = Main.player[npc.target];
                if (target.Alives()
                    && EliteMoveSets.Profiles.TryGetValue(HostType, out EliteProfile profile)) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * profile.Power;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.045f);
                }
            }
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Age * 0.11f + Projectile.identity * 1.7f) * 0.045f);

            //残影余屑（≤1粒/4帧）
            if (!VaultUtils.isServer && Age % 4f == 0f) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Shadowflame, -Projectile.velocity * 0.1f, 160, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>淡入淡出透明度</summary>
        private float FadeAlpha() {
            float fadeIn = MathHelper.Clamp(Age / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            return fadeIn * fadeOut;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(HostType);
            Texture2D tex = TextureAssets.Npc[HostType].Value;
            int frameCount = Math.Max(Main.npcFrameCount[HostType], 1);
            Rectangle frame = tex.Frame(1, frameCount, 0, (int)(Age / 6f) % frameCount);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float alpha = FadeAlpha();
            bool rotateStyle = EliteMoveSets.Profiles.TryGetValue(HostType, out EliteProfile p) && p.Style == 1;
            float rotation = rotateStyle ? Projectile.velocity.ToRotation() + MathHelper.PiOver2 : 0f;
            SpriteEffects effects = !rotateStyle && Projectile.velocity.X > 0f
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //残影本体：宿主贴图调暗 + 微偏主题色（更暗=假身）
            Color lit = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
            Color body = Color.Lerp(lit, p.Tint, 0.3f) * (DecoyDim * alpha);
            Main.EntitySpriteDraw(tex, drawPos, frame, body, rotation,
                frame.Size() / 2f, 1f, effects, 0);
            //幻影薄光：一层淡加色，标记"这是能量体"
            Color veil = p.Tint with { A = 0 } * (0.22f * alpha);
            Main.EntitySpriteDraw(tex, drawPos, frame, veil, rotation,
                frame.Size() / 2f, 1.04f, effects, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.Shadowflame, Main.rand.NextVector2Circular(1.8f, 1.8f), 140, default, 1f);
                dust.noGravity = true;
            }
        }
    }
}

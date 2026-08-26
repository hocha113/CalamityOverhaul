using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 处刑破绽态：ai[0]=锚NPC索引 ai[1]=登记类型 ai[2]=持续帧（服务端按档位定值）。
    /// 重击挥空后由服务端生成，实体原生同步=破绽在所有端可见可读；
    /// 每帧向锚怪的机制层盖破绽镜像戳（承伤加深与踉跄减速都只读镜像戳），
    /// 吸血鬼中途变形不豁免（锚校验按形态对放行）。永不造成伤害
    /// </summary>
    internal class EclOpeningProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>ai[2] 异常时的兜底持续帧（落在 60-90 契约带内）</summary>
        private const int FallbackFrames = 70;
        /// <summary>晕眩光点数量与环绕半径</summary>
        private const int DizzyDots = 4;
        private const float DizzyRadius = 26f;

        private int AnchorIndex => (int)Projectile.ai[0];
        private int RecordedType => (int)Projectile.ai[1];
        private int Duration => Projectile.ai[2] >= 30f ? (int)Projectile.ai[2] : FallbackFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //占位时长，首个 AI 帧按 ai[2] 套定（迟入端会重放全长破绽，失败方向偏玩家有利）
            Projectile.timeLeft = 96;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Duration;
                Projectile.localAI[1] = Projectile.timeLeft;
                if (!VaultUtils.isServer) {
                    //破绽揭示音：低哑钟鸣，各端本地在实体首帧触发
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = -0.8f }, Projectile.Center);
                }
            }

            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || !EclEclipseSets.TypeMatches(RecordedType, anchor.type)) {
                //锚怪已死或槽位被复用：破绽随之消散（型别校验防错怪吃惩罚）
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            //破绽镜像戳：承伤加深与踉跄减速的唯一门（两个机制层各自读own镜像）
            if (anchor.TryGetGlobalNPC(out EclipseNPC eclipse)) {
                eclipse.StampOpening();
            }
            else if (anchor.TryGetGlobalNPC(out EclMothronNPC mothron)) {
                mothron.StampOpening();
            }

            //踉跄尘：金色碎星低频冒出（预算：至多 1 粒/帧）
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust daze = Dust.NewDustPerfect(
                    anchor.Top + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * anchor.width, anchor.gfxOffY - 6f),
                    DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.4f, 1.1f)),
                    120, default, Main.rand.NextFloat(0.7f, 1.1f));
                daze.noGravity = true;
            }

            Lighting.AddLight(anchor.Center, 0.2f, 0.16f, 0.06f);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives()) {
                return false;
            }

            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            float strength = fadeIn * fadeOut;
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color gold = EclEclipseSets.OpeningGold with { A = 0 };
            //gfxOffY：上坡步进的绘制补偿，缺了它标记会在走台阶时与身体脱节
            Vector2 bodyPos = anchor.Center + new Vector2(0f, anchor.gfxOffY);
            Vector2 headPos = new Vector2(anchor.Center.X, anchor.Top.Y + anchor.gfxOffY - 16f);
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.identity);

            //脚下暗金压环：暗色实底 + 金芯（真 alpha 底才能压亮背景）
            Vector2 feetPos = new Vector2(bodyPos.X, anchor.Bottom.Y + anchor.gfxOffY - 4f) - Main.screenPosition;
            float ringW = anchor.width + 34f;
            Main.EntitySpriteDraw(rim, feetPos, null, new Color(34, 24, 8) * (0.7f * strength), 0f,
                rim.Size() / 2f, new Vector2(ringW / rim.Width, 26f / rim.Height) * 1.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, feetPos, null, gold * (0.55f * strength * pulse), 0f,
                glow.Size() / 2f, new Vector2(ringW / glow.Width, 24f / glow.Height), SpriteEffects.None, 0);

            //头顶眩晕星点：环绕旋转的金色光点=通行的"可反打"读法
            float spin = Main.GlobalTimeWrappedHourly * 5f;
            Vector2 headDraw = headPos - Main.screenPosition;
            for (int i = 0; i < DizzyDots; i++) {
                float angle = spin + MathHelper.TwoPi * i / DizzyDots;
                //椭圆轨道：绕头顶横向环绕
                Vector2 offset = new Vector2(MathF.Cos(angle) * DizzyRadius, MathF.Sin(angle) * DizzyRadius * 0.35f);
                float depth = 0.6f + 0.4f * (0.5f + 0.5f * MathF.Sin(angle));
                Main.EntitySpriteDraw(glow, headDraw + offset, null, gold * (strength * 0.75f * depth), 0f,
                    glow.Size() / 2f, 0.2f * depth, SpriteEffects.None, 0);
            }

            //身体金色薄晕：破绽期整体镀金提示
            Main.EntitySpriteDraw(glow, bodyPos - Main.screenPosition, null, gold * (0.22f * strength * pulse), 0f,
                glow.Size() / 2f, anchor.width / 38f + 0.9f, SpriteEffects.None, 0);
            return false;
        }
    }
}

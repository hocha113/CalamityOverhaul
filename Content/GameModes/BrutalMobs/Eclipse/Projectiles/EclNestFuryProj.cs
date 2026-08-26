using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Eclipse.Projectiles
{
    /// <summary>
    /// 蛾怪护巢狂怒：ai[0]=Mothron索引。
    /// 玩家打碎蛾卵触发（预告=卵破裂视觉本身，豁免实体预告条款：狂怒由玩家亲手引爆，
    /// 因果与时机完全掌握在玩家手里，注释即豁免声明）。
    /// 狂怒期巡航提速：本实体在所有端对锚怪做确定性位置推进（签名俯冲执行窗经俯冲预兆的
    /// 镜像戳豁免，速度阈值兜住原版快速机动，不污染已承诺的俯冲弹道），
    /// 并向机制层盖狂怒镜像戳（服务端据此加速冷却）。
    /// 可见红怒气环=狂怒状态本身。永不造成伤害
    /// </summary>
    internal class EclNestFuryProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>狂怒持续帧（4 秒；再破卵=旧实体销毁重生，全端同步刷新）</summary>
        internal const int FuryFrames = 240;
        /// <summary>巡航提速系数（叠加在既有移动之上的位置推进）</summary>
        private const float FuryBonus = 0.30f;
        /// <summary>速度豁免阈：超过此速视为原版 AI 快速机动，不追加推进（签名俯冲另有镜像戳豁免）</summary>
        private const float SuppressSpeed = 8f;

        private static readonly Color FuryRed = new Color(255, 68, 40, 0);
        private static readonly Color FuryDark = new Color(58, 12, 8);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int Elapsed => FuryFrames - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FuryFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.55f, Pitch = 0.65f }, Projectile.Center);
                }
            }

            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != NPCID.Mothron) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            //狂怒镜像戳：服务端冷却加速只读这扇窗
            bool inDive = false;
            if (anchor.TryGetGlobalNPC(out EclMothronNPC mothron)) {
                mothron.StampFury();
                inDive = mothron.InDiveWindow;
            }

            //巡航提速：所有端从同步原语（锚怪速度）确定性推进，模拟一致。
            //签名俯冲的豁免走俯冲预兆的镜像戳（补偿后档3俯冲速度 14/1.8≈7.78 低于阈值，
            //速度阈值单独兜不住）；阈值仍保留，兜住原版 AI 自身的快速机动
            if (!inDive && anchor.velocity.Length() < SuppressSpeed) {
                Vector2 advance = anchor.velocity * FuryBonus;
                if (!anchor.noTileCollide) {
                    advance = Collision.TileCollision(anchor.position, advance, anchor.width, anchor.height);
                }
                anchor.position += advance;
            }

            //怒火余烬（预算：至多 1 粒/帧）
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust ember = Dust.NewDustPerfect(anchor.Center + Main.rand.NextVector2Circular(anchor.width * 0.5f, anchor.height * 0.4f),
                    DustID.Torch, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    100, default, Main.rand.NextFloat(1f, 1.5f));
                ember.noGravity = true;
            }

            Lighting.AddLight(anchor.Center, 0.32f, 0.1f, 0.04f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.localAI[0] == 0f) {
                //首个 AI 帧之前 ai 槽可能尚未套定，不解析锚
                return false;
            }
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives()) {
                return false;
            }

            Texture2D rim = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = anchor.Center + new Vector2(0f, anchor.gfxOffY) - Main.screenPosition;
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            //翅拍节律的双频脉动
            float beat = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Projectile.identity);

            //触发瞬间的怒气扩环（前 20 帧）
            if (Elapsed < 20) {
                float ring = Elapsed / 20f;
                Main.EntitySpriteDraw(glow, drawPos, null, FuryRed * (0.7f * (1f - ring)), 0f,
                    glow.Size() / 2f, 0.6f + 2.6f * ring, SpriteEffects.None, 0);
            }

            //暗底怒气 + 红怒晕（暗层走真 alpha 底）
            Main.EntitySpriteDraw(rim, drawPos, null, FuryDark * (0.5f * fadeOut), 0f,
                rim.Size() / 2f, new Vector2((anchor.width + 70f) / rim.Width, (anchor.height + 54f) / rim.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, FuryRed * (0.5f * fadeOut * beat), 0f,
                glow.Size() / 2f, anchor.width / 34f + 1.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 200, 120, 0) * (0.28f * fadeOut * beat), 0f,
                glow.Size() / 2f, anchor.width / 52f + 0.6f, SpriteEffects.None, 0);
            return false;
        }
    }
}

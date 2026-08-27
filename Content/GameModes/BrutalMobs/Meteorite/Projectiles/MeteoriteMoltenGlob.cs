using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Meteorite.Projectiles
{
    /// <summary>
    /// 熔滴：灼热俯冲沿途滴落的抛物线小弹，落地留火斑。ai[0]=档位。
    /// 暗橙真 alpha 外壳 + 亮芯双层配方（镜像 VileLanceProj.DrawGlob），同材质拖尾；
    /// 淡入完成才有杀伤（伤害窗口=可见窗口）。火斑生成走权威端，
    /// 布点时以 <see cref="MeteoriteFireSpot.EmberGapPx"/> 复查与既有火斑的最小间距
    /// </summary>
    internal class MeteoriteMoltenGlob : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出膛淡入帧数，未淡入无判定（公平阀）</summary>
        private const int FadeInFrames = 8;
        /// <summary>每帧坠速与落速上限（抛物线）</summary>
        private const float GravityPerFrame = 0.2f;
        private const float MaxFallSpeed = 12f;
        /// <summary>火斑伤害 = 熔滴伤害 × 此值</summary>
        private const float SpotDamageFrac = 0.7f;
        /// <summary>向下寻找可站立地表的最大瓦格数（落在墙面/顶面时放弃布斑）</summary>
        private const int GroundSearchTiles = 6;

        /// <summary>暗壳与亮芯（暗层必须真 alpha 才能压住亮背景）</summary>
        private static readonly Color Deep = new Color(118, 42, 16);
        private static readonly Color Bright = new Color(255, 176, 72);

        private int Tier => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 255;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            //出膛淡入（可见度与判定同一时间轴）
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));

            Projectile.velocity.Y += GravityPerFrame;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(4)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.12f, 110, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, Bright.ToVector3() * 0.28f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中方本机结算，减益原生同步；时长随档位
            target.AddBuff(BuffID.OnFire, 120 + 60 * (Tier - 1));
        }

        public override void OnKill(int timeLeft) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.3f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                        new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.5f, 2f)), 100, default, 1.1f);
                    dust.noGravity = true;
                }
            }
            //火斑布点只在权威端；Kill 经弹幕原生同步下发
            if (VaultUtils.isClient) {
                return;
            }
            TryPlaceSpot();
        }

        /// <summary>
        /// 落点布斑：向下找可站立地表；与既有火斑复查 <see cref="MeteoriteFireSpot.EmberGapPx"/> 最小间距，
        /// 过近或并发到 <see cref="MeteoriteFireSpot.FireSpotCap"/> 上限则放弃（缺斑=安全方向）
        /// </summary>
        private void TryPlaceSpot() {
            if (MeteoriteBrutalNPC.CountActive(ModContent.ProjectileType<MeteoriteFireSpot>())
                >= MeteoriteFireSpot.FireSpotCap) {
                return;
            }
            Point tile = Projectile.Center.ToTileCoordinates();
            Vector2 basePos = default;
            bool found = false;
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = tile.Y + dy;
                if (!WorldGen.InWorld(tile.X, tileY, 10)) {
                    return;
                }
                if (WorldGen.SolidTile(tile.X, tileY)) {
                    basePos = new Vector2(tile.X * 16f + 8f, tileY * 16f);
                    found = true;
                    break;
                }
            }
            if (!found) {
                return;
            }

            //间距复查：与任何既有火斑的距离不得小于 EmberGapPx（布点循环与此处同读一个常量）
            int spotType = ModContent.ProjectileType<MeteoriteFireSpot>();
            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type == spotType
                    && Vector2.Distance(other.Center, basePos) < MeteoriteFireSpot.EmberGapPx) {
                    return;
                }
            }

            int damage = Math.Max(1, (int)(Projectile.damage * SpotDamageFrac));
            Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                basePos + new Vector2(0f, -MeteoriteFireSpot.SpotHeight * 0.5f), Vector2.Zero,
                spotType, damage, 0f, Main.myPlayer, Tier);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;

            //旧位残迹（同材质拖尾，横轴 ≥0.5 倍体宽）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawGlob(tex, origin, oldDrawPos, t * 0.35f * opacity, 0.55f * t);
            }
            DrawGlob(tex, origin, pos, opacity, 1f);
            return false;
        }

        private void DrawGlob(Texture2D tex, Vector2 origin, Vector2 drawPos, float alpha, float scaleMul) {
            //快成线、慢成珠的熔液拉伸
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1f);
            Vector2 scale = new Vector2(0.3f * (1f - stretch * 0.4f), 0.42f * (1f + stretch * 1.6f)) * scaleMul;
            Color dark = Deep * (0.92f * alpha);
            Color core = Bright with { A = 0 } * (0.85f * alpha);
            Main.EntitySpriteDraw(tex, drawPos, null, dark, Projectile.rotation, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation, origin, scale * 0.78f, SpriteEffects.None, 0);
        }
    }
}

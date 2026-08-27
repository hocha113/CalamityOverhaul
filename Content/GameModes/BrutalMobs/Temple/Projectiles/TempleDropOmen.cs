using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Temple.Projectiles
{
    /// <summary>
    /// 蜥蜴爬虫天花板突袭的落点预兆：ai[0]=来源NPC槽+1|类型&lt;&lt;8 ai[1]=起坠顶端Y。
    /// 生成即钉在锁定落点（落点即承诺，此后不再移动），画顶部潜伏阴影+垂直警示柱+落点标记；
    /// 预告 ≥34 帧后爬虫沿柱垂直扑落，余痕窗=扑落窗。来源死亡或槽位被复用即消散（反制有效）
    /// </summary>
    internal class TempleDropOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>预告帧数（契约 ≥34，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 34;
        /// <summary>扑落包络三段：爬升/保持/衰减帧（NPC 侧塑形同用，落地即提前收束）</summary>
        internal const int PlungeRise = 5;
        internal const int PlungeHold = 34;
        internal const int PlungeDecay = 8;
        /// <summary>扑落窗总帧数（余痕可见窗）</summary>
        internal const int PlungeWindowFrames = PlungeRise + PlungeHold + PlungeDecay;

        /// <summary>警示柱芯宽与柔光宽（宽于爬虫体宽，滚身余量也包进警示带）</summary>
        private const float ColumnCoreWidth = 30f;
        private const float ColumnGlowWidth = 68f;

        /// <summary>警示暖橙（加色层，A=0）</summary>
        private static readonly Color Warn = new Color(255, 168, 66, 0);
        /// <summary>顶部潜伏阴影暗色（真 alpha 暗层，A 满）</summary>
        private static readonly Color ShadowDark = new Color(24, 14, 8, 235);

        private int SrcPacked => (int)Projectile.ai[0];
        private float TopY => Projectile.ai[1];
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool InStrike => Elapsed >= TelegraphFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + PlungeWindowFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = Projectile.timeLeft;
            }

            //来源校验：施法者死亡或槽位被新怪复用即消散，NPC 侧回读失败自动放弃突袭
            int src = (SrcPacked & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != SrcPacked >> 8) {
                Projectile.Kill();
                return;
            }

            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 4 }, Projectile.Center);
            }

            //预告期碎屑先落：沿柱顶撒下带重力石屑，把「上面有东西要砸下来」写在路径上
            if (!InStrike && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 spawn = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-10f, 10f), TopY + 8f);
                Dust crumb = Dust.NewDustPerfect(spawn, DustID.Stone,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1.5f, 3f)), 110, default, 1f);
                crumb.noGravity = false;
            }

            float urgency = MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);
            Lighting.AddLight(Projectile.Center, 0.22f + 0.1f * urgency, 0.13f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)PlungeWindowFrames, 0f, 1f) * 0.25f;
            }
            else {
                strength = fadeIn * (0.55f + 0.45f * (Elapsed / (float)TelegraphFrames));
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D line = TextureAssets.Projectile[Type].Value;
            Texture2D glob = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 landPos = Projectile.Center - Main.screenPosition;
            Vector2 topPos = new Vector2(Projectile.Center.X, TopY) - Main.screenPosition;
            float columnLen = Math.Max(24f, Projectile.Center.Y - TopY);
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);
            float urgency = MathHelper.Clamp(Elapsed / (float)TelegraphFrames, 0f, 1f);

            //垂直警示柱：从顶端指向落点（宽度随临近扑落收紧，读作聚焦）
            Vector2 lineOrigin = new Vector2(0f, line.Height / 2f);
            float scaleX = columnLen / line.Width;
            Main.EntitySpriteDraw(line, topPos, null, Warn * (0.42f * strength * pulse), MathHelper.PiOver2,
                lineOrigin, new Vector2(scaleX, (ColumnCoreWidth - 8f * urgency) / line.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, topPos, null, Warn * (0.25f * strength), MathHelper.PiOver2,
                lineOrigin, new Vector2(scaleX, ColumnGlowWidth / line.Height), SpriteEffects.None, 0);

            //暗层透明度：淡入后即保持满值，只随余痕尾段一起收（真 alpha 暗斑，暗层不吃加色陷阱）
            float shadowAlpha = Math.Min(1f, strength * 4f);

            //顶部潜伏阴影：盖在起坠点
            Main.EntitySpriteDraw(glob, topPos, null, ShadowDark * (0.8f * shadowAlpha),
                0f, glob.Size() / 2f, new Vector2(0.5f, 0.3f) * (0.9f + 0.2f * pulse), SpriteEffects.None, 0);

            //落点标记：暗底+暖芯双层椭斑（临近扑落越亮越宽）
            Main.EntitySpriteDraw(glob, landPos, null, ShadowDark * (0.65f * shadowAlpha),
                0f, glob.Size() / 2f, new Vector2(0.62f + 0.14f * urgency, 0.2f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, landPos, null, Warn * (0.55f * strength * pulse),
                0f, glow.Size() / 2f, new Vector2(0.42f + 0.1f * urgency, 0.12f), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI)
            => behindNPCsAndTiles.Add(index);
    }
}

using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>
    /// 汐的演出与潮钟集中处：潮息/洼龄/涌潮的确定性解算、
    /// 洼身位移与白沫，及浪缘伴生弹幕
    /// </summary>
    internal static class FuXiFX
    {
        /// <summary>海青身份色，定义与演出同源取此</summary>
        internal static readonly Color Accent = new(88, 168, 172);

        /// <summary>浪缘白沫色</summary>
        internal static readonly Color FoamWhite = new(224, 240, 238);

        /// <summary>确定性潮息 -1..1：timeLeft 随生成包同步，各端一致</summary>
        internal static float Breath(Projectile puddle)
            => MathF.Sin(puddle.timeLeft * 0.075f + puddle.identity * 1.313f);

        /// <summary>
        /// 洼龄：同源取洼身访问器（出生寿命减当前 timeLeft）。
        /// 墨滴合并续命把 timeLeft 顶回出生值 → 洼龄归零重新蓄潮
        /// </summary>
        internal static int Age(Projectile puddle)
            => (puddle.ModProjectile as KikasaInkPuddle)?.Age ?? 0;

        /// <summary>涌潮进度 0..1；未在涌潮窗（含收干尾段禁潮）返回 -1</summary>
        internal static float SurgeT(Projectile puddle) {
            int age = Age(puddle);
            if (age < FuXi.SurgeArmAge || age >= FuXi.SurgeArmAge + FuXi.SurgeFrames
                || puddle.timeLeft <= 40) {
                return -1f;
            }
            return (age - FuXi.SurgeArmAge) / (float)FuXi.SurgeFrames;
        }

        /// <summary>白沫强度 0..1：涌潮期拉满渐退，平时只在潮峰泛一点</summary>
        internal static float FoamT(Projectile puddle) {
            float surge = SurgeT(puddle);
            if (surge >= 0f) {
                return 0.75f * (1f - surge * 0.5f);
            }
            return MathF.Max(Breath(puddle) - 0.55f, 0f) * 0.5f;
        }

        /// <summary>洼周最近的可追敌，无则 null</summary>
        internal static NPC NearestPrey(Projectile puddle, float range) {
            NPC best = null;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(puddle)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, puddle.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>
        /// 潮涌一步：横向压向猎物（先急后缓），落点重新吸附地表；
        /// 无地可依或嵌进实心则原地不动——潮不越崖、不钻墙
        /// </summary>
        internal static void SurgeStep(Projectile puddle, float dir, float t) {
            float speed = 4.4f * (1f - t) * (1f - t * 0.4f);
            Vector2 next = puddle.Center + new Vector2(dir * speed, 0f);
            //自新位上方向下探地，允许上下几格台阶
            if (!TryFindGroundBelow(next - Vector2.UnitY * 24f, 72f, out float surfaceY)) {
                return;
            }
            //对齐洼身吸附姿态：同源取洼深常量的半高
            next.Y = surfaceY - KikasaInkPuddle.DepthPx * 0.5f + 3f;
            Tile tile = Framing.GetTileSafely((int)(next.X / 16f), (int)(next.Y / 16f));
            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                return;
            }
            puddle.Center = next;
        }

        /// <summary>自探点向下找实心地表，镜像洼身的吸附逻辑</summary>
        private static bool TryFindGroundBelow(Vector2 from, float maxDown, out float surfaceY) {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int endY = (int)((from.Y + maxDown) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    surfaceY = y * 16f;
                    return true;
                }
            }
            surfaceY = 0f;
            return false;
        }

        /// <summary>起潮拍：一记水响+前缘沫环（各端本地）</summary>
        internal static void SurgeCrash(Projectile puddle, float dir) {
            if (Main.dedServ) {
                return;
            }
            KikasaInk.Play(KikasaInk.InkSplash, puddle.Center, 0.4f, -0.15f, 3);
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(
                puddle.Center + new Vector2(dir * 18f, -4f), Vector2.Zero,
                FoamWhite * 0.4f, 0.07f)?.Configure(0.07f, 0.5f, 10);
        }

        /// <summary>涌潮期前缘白沫：贴浪头甩白珠与细雾（端本地、低频）</summary>
        internal static void SurgeFoam(Projectile puddle, float dir, float t) {
            if (Main.dedServ || !Main.rand.NextBool(2)) {
                return;
            }
            float radiusMul = puddle.ai[0] > 0.01f ? puddle.ai[0] : 1f;
            float edge = KikasaInkPuddle.WidthPx * radiusMul * 0.45f;
            Vector2 crest = puddle.Center + new Vector2(dir * edge, -3f);
            PRTLoader.NewParticle<PRT_KikasaInkBead>(
                crest + Main.rand.NextVector2Circular(6f, 3f),
                new Vector2(dir * Main.rand.NextFloat(1.5f, 3.2f), -Main.rand.NextFloat(0.6f, 2f)),
                Main.rand.NextBool(3) ? Accent : FoamWhite,
                Main.rand.NextFloat(0.16f, 0.28f))?.Configure(Main.rand.Next(12, 20));
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(crest, new Vector2(dir * 0.8f, -0.3f),
                    Accent * 0.5f, 0.5f * (1f - t * 0.4f))?.Configure(Main.rand.Next(12, 18));
            }
        }
    }

    /// <summary>
    /// 汐·涌潮浪缘：起潮拍从洼身推出的一道半月浪，短寿贴地滑行，
    /// 对撞上的敌人拍一记半份洼伤。浪体三层半月+沫冠，各端本地绘制
    /// </summary>
    internal class FuXiTideWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 24;

        private float life;

        /// <summary>确定性相位：浪身抖动各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            life++;
            //浪劲一路耗散，只滑不坠
            Projectile.velocity *= 0.90f;
            Projectile.velocity.Y = 0f;

            if (Main.dedServ) {
                return;
            }
            //浪冠碎沫
            if (Main.rand.NextBool(2)) {
                float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + new Vector2(dir * 12f, -8f) + Main.rand.NextVector2Circular(5f, 4f),
                    new Vector2(dir * Main.rand.NextFloat(0.8f, 2f), -Main.rand.NextFloat(0.4f, 1.4f)),
                    FuXiFX.FoamWhite, Main.rand.NextFloat(0.14f, 0.24f))?.Configure(Main.rand.Next(10, 16));
            }
        }

        /// <summary>半月浪光：深海青垫底、海青浪身前倾、沫冠压顶，随浪劲衰减一并淡去</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float t = life / (float)LifeFrames;
            float alpha = MathF.Sin(MathHelper.Pi * MathF.Min(t * 1.6f, 1f));
            if (alpha <= 0.02f) {
                return false;
            }
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
            float lean = dir * -0.20f;
            float wob = 1f + 0.05f * MathF.Sin(life * 0.5f + Seed * 4f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //浪身两层半月：深托浅，往前倾
            Main.EntitySpriteDraw(tex, pos, null, new Color(34, 82, 88) * (alpha * 0.7f),
                lean, origin, new Vector2(44f, 26f) * wob / tex.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(dir * 5f, -3f), null,
                FuXiFX.Accent * (alpha * 0.8f), lean, origin,
                new Vector2(32f, 19f) * wob / tex.Width, SpriteEffects.None, 0);
            //沫冠：一线白压在浪头
            Main.EntitySpriteDraw(tex, pos + new Vector2(dir * 9f, -10f), null,
                FuXiFX.FoamWhite * (alpha * 0.85f), lean, origin,
                new Vector2(24f, 7f) * wob / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}

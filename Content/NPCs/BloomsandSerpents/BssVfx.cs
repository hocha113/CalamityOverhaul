using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>荒花沙蟒表现辅助：沙尘、花瓣、吼声、震屏、清弹（沙是漫反射材质，全程不走加色辉光）</summary>
    internal static class BssVfx
    {
        /// <summary>暖沙</summary>
        internal static readonly Color SandWarm = new(206, 172, 116);
        /// <summary>沙影深棕</summary>
        internal static readonly Color SandDark = new(142, 108, 66);
        /// <summary>红花绯色</summary>
        internal static readonly Color BloomRed = new(198, 46, 54);
        /// <summary>仙人掌绿</summary>
        internal static readonly Color CactusGreen = new(104, 132, 64);

        /// <summary>向下扫地：返回第一格实心地面的世界 Y；扫不到给兜底深度</summary>
        internal static float FindGroundY(Vector2 from, float maxDepth = 1600f) {
            int tx = (int)(from.X / 16f);
            int startTy = Math.Max((int)(from.Y / 16f), 10);
            int maxTy = Math.Min((int)((from.Y + maxDepth) / 16f), Main.maxTilesY - 10);
            for (int y = startTy; y <= maxTy; y++) {
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return y * 16f;
                }
            }
            return from.Y + maxDepth;
        }

        /// <summary>位置是否露出地表（埋沙的发射器不开火，看不见的炮口不算预告）</summary>
        internal static bool IsAboveGround(Vector2 pos)
            => FindGroundY(pos - new Vector2(0f, 40f), 400f) > pos.Y - 6f;

        /// <summary>破土/入土沙爆：漫反射沙尘喷泉，规模随 scale</summary>
        internal static void SandBurst(Vector2 pos, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            int count = (int)(26 * scale);
            for (int i = 0; i < count; i++) {
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(24f, 10f) * scale,
                    DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-3.6f, 3.6f), -Main.rand.NextFloat(2f, 7.5f)) * scale,
                    Main.rand.Next(60, 120), default, Main.rand.NextFloat(1f, 1.7f));
                d.velocity.Y -= 1f;
            }
            for (int i = 0; i < (int)(6 * scale); i++) {
                Dust stone = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(18f, 8f),
                    DustID.Dirt,
                    new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(3f, 6f)) * scale,
                    90, default, Main.rand.NextFloat(0.8f, 1.2f));
                stone.noGravity = false;
            }
        }

        /// <summary>体表持续渗沙（钻行/预告的细流）</summary>
        internal static void SandTrickle(Vector2 pos, float intensity = 1f) {
            if (Main.dedServ || !Main.rand.NextBool(2)) {
                return;
            }
            Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(12f, 6f),
                DustID.Sand,
                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 2.2f) * intensity),
                110, default, Main.rand.NextFloat(0.7f, 1.1f));
            d.noGravity = false;
        }

        /// <summary>绯红花瓣飘落（纯表现粒子，伤害花瓣走 BssPetalProj）</summary>
        internal static void PetalDrift(Vector2 pos, Vector2 vel, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            Color tint = Color.Lerp(BloomRed, new Color(150, 30, 40), Main.rand.NextFloat(0.4f));
            PRTLoader.NewParticle<PRT_BrideDryPetal>(pos, vel, tint, Main.rand.NextFloat(0.8f, 1.25f) * scale)
                ?.Configure(Main.rand.Next(80, 130), 0.7f);
        }

        /// <summary>沙兽怒吼</summary>
        internal static void Roar(Vector2 pos, float pitch = -0.4f, float volume = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.SendRoar with { Pitch = pitch, Volume = volume, MaxInstances = 3 }, pos);
        }

        /// <summary>就近震屏：只震看得见战斗的本地玩家</summary>
        internal static void Shake(Vector2 pos, float amount, float range = 1300f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, pos) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        /// <summary>
        /// 破土/砸地喷发沙弹扇（权威端）。
        /// 公平口径：扇面顶心朝上、总角 BreachEruptArcDeg=200，贴地两侧各 80 度不发射 = 逃生道；
        /// 慢弧重力弹可读可躲。
        /// </summary>
        internal static void BreachEruption(NPC source, Vector2 ground, int count) {
            if (VaultUtils.isClient || count <= 1) {
                return;
            }
            int damage = Core.BssDirector.ScaleProjectileDamage(source, Core.BssDirector.SandGlobDamage);
            int type = ModContent.ProjectileType<Projectiles.BssSandGlob>();
            float arc = MathHelper.ToRadians(Core.BssDirector.BreachEruptArcDeg);
            for (int i = 0; i < count; i++) {
                float ang = -MathHelper.PiOver2 + (i / (float)(count - 1) - 0.5f) * arc;
                float speed = Main.rand.NextFloat(7f, 11.5f);
                Projectile.NewProjectile(source.GetSource_FromAI(), ground - new Vector2(0f, 8f),
                    ang.ToRotationVector2() * speed, type, damage, 0.6f, Main.myPlayer);
            }
        }

        /// <summary>
        /// 转阶段公平阀：清掉本 boss 已发出的全部敌对弹幕与滞留演出实体（只清自家类型）。
        /// 漩涡与隆包非 hostile 也要清：转场后残留的蓄力涡/待爆泉是失主的旧预告
        /// （两者的爆点逻辑都有自然到期守卫，被 Kill 清掉不会放沙球）。
        /// 沙丘柱只清未成形的威胁（鼓包/钻出中缓沉），滞留柱留作场地与爆震燃料；
        /// 全场收尾由柱的孤儿守卫兜底（头消失即缓沉）。
        /// </summary>
        internal static void ClearOwnHostileProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            BssSandPillar.CancelPending();
            int sand = ModContent.ProjectileType<Projectiles.BssSandGlob>();
            int needle = ModContent.ProjectileType<Projectiles.BssNeedleProj>();
            int ball = ModContent.ProjectileType<Projectiles.BssCactusBallProj>();
            int petal = ModContent.ProjectileType<Projectiles.BssPetalProj>();
            int vortex = ModContent.ProjectileType<Projectiles.BssSandVortexProj>();
            int omen = ModContent.ProjectileType<Projectiles.BssBreachOmen>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == sand || p.type == needle || p.type == ball || p.type == petal
                    || p.type == vortex || p.type == omen) {
                    p.Kill();
                }
            }
        }
    }
}

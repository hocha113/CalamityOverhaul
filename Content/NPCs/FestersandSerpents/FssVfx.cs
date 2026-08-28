using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// 脓蕾沙蟒表现辅助：腐沙、灵液、吼声、震屏、清弹。
    /// 材质口径：腐沙是漫反射（乘光照不加色），灵液是发光液体（金色可走加色），
    /// 两种材质分家 = 变异体"死物上长活疮"的读数来源。
    /// </summary>
    internal static class FssVfx
    {
        /// <summary>坏死暗紫（变异皮肤主底）</summary>
        internal static readonly Color NecroPlum = new(70, 52, 92);
        /// <summary>坏死深影</summary>
        internal static readonly Color NecroShadow = new(46, 36, 60);
        /// <summary>灵液亮金（脉络/高光）</summary>
        internal static readonly Color IchorBright = new(255, 231, 140);
        /// <summary>灵液中金</summary>
        internal static readonly Color IchorGold = new(232, 186, 82);
        /// <summary>灵液深琥珀（弹幕打底）</summary>
        internal static readonly Color IchorDeep = new(168, 112, 34);
        /// <summary>污沙（腐化后的沙色）</summary>
        internal static readonly Color TaintedSand = new(152, 124, 94);

        /// <summary>
        /// 体表乘色（着色器未接管时的手染回退：把 BSS 暖沙贴图压向坏死紫，
        /// 保细节不糊剪影；着色器上线后仍作为腿/残影的廉价同源染色）。
        /// </summary>
        internal static readonly Color SkinMul = new(172, 152, 205);

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

        /// <summary>破土/入土腐沙爆：暗染沙尘 + 腐化碎屑 + 少量金滴，规模随 scale</summary>
        internal static void CorruptSandBurst(Vector2 pos, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            int count = (int)(24 * scale);
            for (int i = 0; i < count; i++) {
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(26f, 10f) * scale,
                    DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-3.6f, 3.6f), -Main.rand.NextFloat(2f, 7.5f)) * scale,
                    Main.rand.Next(60, 120), TaintedSand, Main.rand.NextFloat(1f, 1.7f));
                d.velocity.Y -= 1f;
            }
            for (int i = 0; i < (int)(8 * scale); i++) {
                Dust rot = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(20f, 8f),
                    DustID.CorruptGibs,
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(2.5f, 5.5f)) * scale,
                    90, default, Main.rand.NextFloat(0.9f, 1.3f));
                rot.noGravity = false;
            }
            for (int i = 0; i < (int)(5 * scale); i++) {
                Dust gold = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(16f, 8f),
                    DustID.Ichor,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(3f, 6.5f)) * scale,
                    40, default, Main.rand.NextFloat(0.9f, 1.4f));
                gold.noGravity = false;
            }
        }

        /// <summary>体表持续渗漏：暗沙细流掺灵液珠（钻行/预告）</summary>
        internal static void FesterTrickle(Vector2 pos, float intensity = 1f) {
            if (Main.dedServ || !Main.rand.NextBool(2)) {
                return;
            }
            if (Main.rand.NextBool(4)) {
                Dust gold = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(12f, 6f),
                    DustID.Ichor,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.8f, 2f) * intensity),
                    30, default, Main.rand.NextFloat(0.7f, 1f));
                gold.noGravity = false;
            }
            else {
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(12f, 6f),
                    DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 2.2f) * intensity),
                    110, TaintedSand, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = false;
            }
        }

        /// <summary>灵液迸溅：金珠喷洒 + 发光碎滴（命中/爆点/口沫）</summary>
        internal static void IchorBurst(Vector2 pos, float power = 1f, Vector2? dir = null) {
            if (Main.dedServ) {
                return;
            }
            Vector2 baseDir = dir ?? -Vector2.UnitY;
            int count = (int)(10 * power);
            for (int i = 0; i < count; i++) {
                Vector2 vel = baseDir.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4.5f) * power
                    + Main.rand.NextVector2Circular(1f, 1f);
                Dust d = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(10f, 10f) * power,
                    DustID.Ichor, vel, 30, default, Main.rand.NextFloat(0.9f, 1.5f) * MathF.Sqrt(power));
                d.noGravity = false;
            }
            for (int i = 0; i < (int)(4 * power); i++) {
                Dust glow = Dust.NewDustPerfect(pos, DustID.IchorTorch,
                    baseDir.RotatedByRandom(1.4f) * Main.rand.NextFloat(1f, 3f) * power,
                    0, default, Main.rand.NextFloat(1f, 1.6f));
                glow.noGravity = true;
            }
            Lighting.AddLight(pos, IchorGold.ToVector3() * 0.4f * MathF.Min(power, 2f));
        }

        /// <summary>变异嘶吼（比原版沙蟒更低哑，湿息尾音）</summary>
        internal static void Roar(Vector2 pos, float pitch = -0.6f, float volume = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.SendRoar with { Pitch = pitch, Volume = volume, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = pitch + 0.35f, Volume = volume * 0.5f, MaxInstances = 3 }, pos);
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
        /// 破土/砸地喷发灵液扇（权威端）。
        /// 公平口径：扇面顶心朝上、总角 BreachEruptArcDeg=200，贴地两侧各 80 度不发射 = 逃生道；
        /// 慢弧重力弹可读可躲，落点顺带播种脓池。
        /// </summary>
        internal static void IchorBreachFan(NPC source, Vector2 ground, int count, float speedScale = 1f) {
            if (VaultUtils.isClient || count <= 1) {
                return;
            }
            int damage = Core.FssDirector.ScaleProjectileDamage(source, Core.FssDirector.IchorGlobDamage);
            int type = ModContent.ProjectileType<Projectiles.FssIchorGlob>();
            float arc = MathHelper.ToRadians(Core.FssDirector.BreachEruptArcDeg);
            for (int i = 0; i < count; i++) {
                float ang = -MathHelper.PiOver2 + (i / (float)(count - 1) - 0.5f) * arc;
                float speed = Main.rand.NextFloat(7f, 11.5f) * speedScale;
                Projectile.NewProjectile(source.GetSource_FromAI(), ground - new Vector2(0f, 8f),
                    ang.ToRotationVector2() * speed, type, damage, 0.6f, Main.myPlayer);
            }
        }

        /// <summary>
        /// 转阶段公平阀：清掉本 boss 已发出的全部敌对弹幕与滞留演出实体（只清自家类型）。
        /// 待爆隆包/黏疮非 hostile 也要清：转场后残留的旧预告是失主的（两者的喷发逻辑
        /// 都只认自然到期，被 Kill 清掉不会放弹）。脓池是场地经济的持久层，转阶段不清
        /// ——满场引爆要吃它们；泉柱短寿命自净。
        /// </summary>
        internal static void ClearOwnHostileProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            int omen = ModContent.ProjectileType<Projectiles.FssBreachOmen>();
            int glob = ModContent.ProjectileType<Projectiles.FssIchorGlob>();
            int cyst = ModContent.ProjectileType<Projectiles.FssStickyCyst>();
            int shell = ModContent.ProjectileType<Projectiles.FssMortarShell>();
            int shard = ModContent.ProjectileType<Projectiles.FssMortarShard>();
            int drop = ModContent.ProjectileType<Projectiles.FssCascadeDrop>();
            int husk = ModContent.ProjectileType<Projectiles.FssHuskShard>();
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == omen || p.type == glob || p.type == cyst
                    || p.type == shell || p.type == shard || p.type == drop || p.type == husk) {
                    p.Kill();
                }
            }
        }
    }
}

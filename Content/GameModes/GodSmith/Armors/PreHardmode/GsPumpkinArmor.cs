using CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【南瓜套·爆瓜农事】（P10a 移交，键族归 ArmorsB）丰收祭的诡异农law：
    /// ①命中积攒瓜藤，满六层后下一击在目标脚下种一颗爆瓜 ②爆瓜三秒熟透自爆，期间再打目标可催熟提前引爆
    /// ③炸开三瓣飞旋瓜瓣弹散射。原版套装奖励（+10% 伤害）保留，神赋叠加
    /// </summary>
    internal class GsPumpkinArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.PumpkinHelmet];

        public override int BodyID => ItemID.PumpkinBreastplate;

        public override int LegsID => ItemID.PumpkinLeggings;

        protected override string EndowLineFallback =>
            "Gourd Harvest: strikes build vines; at 6 stacks the next strike plants a blast gourd that ripens in 3s (strike the victim again to force it), bursting into three spinning slices";

        //南瓜橙 + 藤绿色板
        internal static readonly Color PumpkinGlow = new(255, 222, 112);
        internal static readonly Color PumpkinOrange = new(255, 150, 44);
        internal static readonly Color PumpkinDeep = new(150, 70, 22);
        internal static readonly Color VineGreen = new(112, 182, 62);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => PumpkinOrange;

        protected override Color ThemeBright => PumpkinGlow;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsPumpkinBombProj>()
            || proj.type == ModContent.ProjectileType<GsPumpkinSliceProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }
            //催熟：目标身上挂着自家爆瓜时，再打即提前引爆（佩戴者端持瓜权威）
            if (player.whoAmI == Main.myPlayer) {
                int type = ModContent.ProjectileType<GsPumpkinBombProj>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner == player.whoAmI && proj.type == type
                        && proj.ai[0] < 999f && (int)proj.ai[1] == target.whoAmI) {
                        proj.ai[0] = 999f;
                        proj.netUpdate = true;
                        break;
                    }
                }
            }
            base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.3f }, target.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int sliceDamage = Math.Clamp((int)(damageDone * 0.35f), 6, 70);
            //种在目标脚下：向下探地最多 8 格
            Vector2 plant = target.Bottom;
            Point tile = plant.ToTileCoordinates();
            for (int dy = 0; dy < 8; dy++) {
                Point at = new(tile.X, tile.Y + dy);
                if (!WorldGen.InWorld(at.X, at.Y, 10)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(at.X, at.Y);
                if (t.HasTile && Main.tileSolid[t.TileType]) {
                    plant = new Vector2(at.X * 16f + 8f, at.Y * 16f - 12f);
                    break;
                }
            }
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithPumpkinEndow"),
                plant, Vector2.Zero,
                ModContent.ProjectileType<GsPumpkinBombProj>(),
                sliceDamage, 2f, player.whoAmI, 0f, target.whoAmI);
        }
    }

    /// <summary>
    /// 爆瓜：种在敌人脚下的诡异圆瓜，藤蔓固定、瓜身随成熟膨胀发烫；
    /// 三秒熟透自爆（ai[0] 置 999 即被催熟），炸开三瓣飞旋瓜瓣与一圈瓜瓤
    /// </summary>
    internal class GsPumpkinBombProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>生长计时；999=被催熟立爆</summary>
        private ref float Life => ref Projectile.ai[0];

        private ref float VictimIndex => ref Projectile.ai[1];

        private float Seed => Projectile.identity * 0.8311f % 3.83f;

        /// <summary>熟透帧数</summary>
        private const int RipeFrames = 180;

        private float Ripeness => MathHelper.Clamp(Life / RipeFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = RipeFrames + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>瓜体不撞人，瓜瓣才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            if (Life >= 999f || Life >= RipeFrames) {
                Burst();
                return;
            }

            //将熟未熟的瓜皮热气（客户端装饰）
            if (!Main.dedServ && Ripeness > 0.5f && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), -8f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    GsPumpkinArmor.PumpkinGlow, Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, Main.rand.Next(10, 16));
            }
            Lighting.AddLight(Projectile.Center, GsPumpkinArmor.PumpkinOrange.ToVector3() * (0.16f + 0.2f * Ripeness));
        }

        private void Burst() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                //瓜瓤四溅
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                        Main.rand.NextBool() ? GsPumpkinArmor.PumpkinOrange : GsPumpkinArmor.PumpkinGlow,
                        Main.rand.NextFloat(0.28f, 0.48f))?.Configure(true, Main.rand.Next(14, 24));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    GsPumpkinArmor.PumpkinGlow, 0.14f)?.Configure(9, 0.8f);
            }
            //三瓣飞旋瓜瓣：朝受害者所在方向扇开（佩戴者端裁定）
            if (Projectile.owner == Main.myPlayer) {
                NPC victim = VictimIndex >= 0 && VictimIndex < Main.maxNPCs ? Main.npc[(int)VictimIndex] : null;
                float baseAng = victim != null && victim.active
                    ? (victim.Center - Projectile.Center).ToRotation()
                    : -MathHelper.PiOver2;
                for (int i = 0; i < 3; i++) {
                    float ang = baseAng + (i - 1) * 0.55f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center, ang.ToRotationVector2() * Main.rand.NextFloat(8f, 10f),
                        ModContent.ProjectileType<GsPumpkinSliceProj>(),
                        Projectile.damage, 2f, Projectile.owner);
                }
            }
            Projectile.Kill();
        }

        //==================== 绘制：膨胀圆瓜 + 棱线 + 藤把 + 熟透透光 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (core == null || glow == null || shot == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //成熟膨胀 + 将熟抖动
            float size = 0.55f + Ripeness * 0.45f;
            float tremble = Ripeness > 0.75f ? MathF.Sin(Life * 0.9f + Seed * 5f) * 0.05f * Ripeness : 0f;
            Vector2 gourd = new Vector2(1f + tremble, 1f - tremble) * size;

            //瓜身双层（真 alpha 占体积）
            Main.EntitySpriteDraw(core, pos, null,
                GsPumpkinArmor.PumpkinDeep * 0.95f, 0f, core.Size() * 0.5f,
                new Vector2(0.24f, 0.20f) * gourd, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                GsPumpkinArmor.PumpkinOrange * 0.9f, 0f, core.Size() * 0.5f,
                new Vector2(0.20f, 0.165f) * gourd, SpriteEffects.None, 0);
            //棱线两道
            for (int i = -1; i <= 1; i += 2) {
                Main.EntitySpriteDraw(core, pos + new Vector2(i * 6f * size, 0f), null,
                    GsPumpkinArmor.PumpkinDeep * 0.5f, 0f, core.Size() * 0.5f,
                    new Vector2(0.025f, 0.15f) * gourd, SpriteEffects.None, 0);
            }
            //藤把
            Main.EntitySpriteDraw(shot, pos - new Vector2(0f, 13f * size), null,
                (GsPumpkinArmor.VineGreen with { A = 0 }) * 0.9f, -MathHelper.PiOver2 + 0.4f + tremble, shot.Size() * 0.5f,
                new Vector2(0.05f, 0.02f), SpriteEffects.None, 0);
            //熟透内焰透光
            Main.EntitySpriteDraw(glow, pos, null,
                (GsPumpkinArmor.PumpkinGlow with { A = 0 }) * (0.55f * Ripeness), 0f, glow.Size() * 0.5f,
                0.42f * size * (1f + tremble * 2f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 瓜瓣弹：炸开的月牙瓜瓣，飞旋带坠弧，命中溅瓤
    /// </summary>
    internal class GsPumpkinSliceProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9679f % 4.27f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //飞旋 + 坠弧
            Projectile.velocity.Y += 0.14f;
            Projectile.rotation += 0.4f * (Projectile.velocity.X >= 0f ? 1f : -1f);
            if (!Main.dedServ && Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity * 0.08f, GsPumpkinArmor.PumpkinGlow,
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(false, Main.rand.Next(6, 10));
            }
            Lighting.AddLight(Projectile.Center, GsPumpkinArmor.PumpkinOrange.ToVector3() * (0.14f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    Main.rand.NextBool() ? GsPumpkinArmor.PumpkinOrange : GsPumpkinArmor.PumpkinDeep,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        //==================== 绘制：飞旋月牙瓜瓣三层 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (crescent == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crescent.Size() * 0.5f;
            float wob = 1f + MathF.Sin(Life * 0.5f + Seed * 4f) * 0.06f;

            Main.EntitySpriteDraw(crescent, pos, null,
                GsPumpkinArmor.PumpkinDeep * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.10f, 0.07f) * wob, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsPumpkinArmor.PumpkinOrange with { A = 0 }) * fade, Projectile.rotation, origin,
                new Vector2(0.085f, 0.055f) * wob, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsPumpkinArmor.PumpkinGlow with { A = 0 }) * (0.7f * fade), Projectile.rotation, origin,
                new Vector2(0.06f, 0.03f) * wob, SpriteEffects.None, 0);
            return false;
        }
    }
}

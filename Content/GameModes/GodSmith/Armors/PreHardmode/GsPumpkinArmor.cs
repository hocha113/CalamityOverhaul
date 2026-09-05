using CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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

        //南瓜橙色板（基类抽象色板签名仍需实现）
        internal static readonly Color PumpkinGlow = new(255, 222, 112);
        internal static readonly Color PumpkinOrange = new(255, 150, 44);

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
    /// 爆瓜：种在敌人脚下的诡异圆瓜；三秒熟透自爆（ai[0] 置 999 即被催熟），
    /// 炸开三瓣飞旋瓜瓣。借原版南瓜灯弹贴图默认绘制
    /// </summary>
    internal class GsPumpkinBombProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.JackOLantern;

        /// <summary>生长计时；999=被催熟立爆</summary>
        private ref float Life => ref Projectile.ai[0];

        private ref float VictimIndex => ref Projectile.ai[1];

        /// <summary>熟透帧数</summary>
        private const int RipeFrames = 180;

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
        }

        private void Burst() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
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
    }

    /// <summary>
    /// 瓜瓣弹：炸开的瓜瓣，飞旋带坠弧；借原版糖玉米贴图默认绘制
    /// </summary>
    internal class GsPumpkinSliceProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CandyCorn;

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
            //飞旋 + 坠弧
            Projectile.velocity.Y += 0.14f;
            Projectile.rotation += 0.4f * (Projectile.velocity.X >= 0f ? 1f : -1f);
        }
    }
}

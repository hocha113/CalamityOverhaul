using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// 深层地牢通用伤害弹体（全部经各自的预告实体提交后才出膛，伤害窗=飞行可见期）。
    /// ai[0]=模式 ai[1]/ai[2]=模式副参（咒焰=追踪帧数/每帧限转弧度）。
    /// 各模式复用原版贴图作弹体（M5 遮挡像素），拖尾同贴图降比重画。
    /// 咒焰缓追为限追踪弹：限转率+追踪截止帧为具名副参，超时直飞——追踪弹的公平承诺是这两个常量而非弹道
    /// </summary>
    internal class DdBoltProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        //==== 模式 ====
        /// <summary>三连水矢（DarkCaster）</summary>
        internal const int ModeWater = 0;
        /// <summary>缓追咒焰（Ragged 系；限转率追踪，截止后直飞）</summary>
        internal const int ModeCursed = 1;
        /// <summary>影束（Necromancer 系）</summary>
        internal const int ModeShadow = 2;
        /// <summary>咒火直飞（大颅四向十字；固定向发射即非追踪保证）</summary>
        internal const int ModeCross = 3;
        /// <summary>狙击弹（SkeletonSniper）</summary>
        internal const int ModeSniper = 4;
        /// <summary>霰弹粒（TacticalSkeleton）</summary>
        internal const int ModePellet = 5;
        /// <summary>震地骨刺（Paladin 锤震地）</summary>
        internal const int ModeSpike = 6;

        /// <summary>各模式的原版贴图供体</summary>
        private static readonly int[] DonorProj = [
            ProjectileID.WaterBolt, ProjectileID.CursedFlameHostile, ProjectileID.ShadowBeamHostile,
            ProjectileID.CursedFlameHostile, ProjectileID.SniperBullet, ProjectileID.BulletDeadeye,
            ProjectileID.Bone,
        ];
        /// <summary>各模式自施重力（位移承诺已由预告实体表达，重力不受模式提速影响）</summary>
        private static readonly float[] ModeGravity = [0.02f, 0f, 0f, 0f, 0f, 0.015f, 0.22f];
        /// <summary>各模式存活帧</summary>
        private static readonly int[] ModeLife = [240, 300, 180, 200, 120, 150, 210];
        /// <summary>贴图朝向补偿：原版子弹/束类贴图指向上方</summary>
        private static readonly float[] ModeRotOffset = [0f, 0f, MathHelper.PiOver2, 0f, MathHelper.PiOver2, MathHelper.PiOver2, 0f];
        /// <summary>自发光弹体（魔法类不吃环境光）</summary>
        private static readonly bool[] ModeSelfLit = [true, true, true, true, false, false, false];

        private static readonly Color[] ModeTint = [
            new Color(120, 180, 255),
            new Color(170, 255, 80),
            new Color(170, 110, 255),
            new Color(170, 255, 80),
            new Color(255, 220, 160),
            new Color(235, 210, 170),
            new Color(226, 222, 196),
        ];

        private int Mode => Math.Clamp((int)Projectile.ai[0], 0, DonorProj.Length - 1);
        /// <summary>咒焰追踪截止帧（具名副参：预告给出的追踪承诺上限）</summary>
        private int HomingFrames => (int)Projectile.ai[1];
        /// <summary>咒焰每帧限转弧度（具名副参：转率封顶=可甩掉）</summary>
        private float TurnRate => Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];
        private int SpinDir => Projectile.identity % 2 == 0 ? 1 : -1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                Projectile.timeLeft = ModeLife[Mode];
            }
            Age++;
            int mode = Mode;

            if (mode == ModeCursed && Age <= HomingFrames) {
                //缓追：向最近玩家限转率转向；截止帧后不再改向（直飞），追踪承诺见类注释
                int idx = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                if (idx >= 0 && idx < Main.maxPlayers && Main.player[idx].Alives()) {
                    float speed = Projectile.velocity.Length();
                    float current = Projectile.velocity.ToRotation();
                    float desired = (Main.player[idx].Center - Projectile.Center).ToRotation();
                    float turn = MathHelper.Clamp(MathHelper.WrapAngle(desired - current), -TurnRate, TurnRate);
                    Projectile.velocity = (current + turn).ToRotationVector2() * speed;
                }
            }

            float gravity = ModeGravity[mode];
            if (gravity > 0f) {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + gravity, 15f);
            }

            //朝向：骨刺自旋，水矢/咒焰缓旋，其余顺速度
            if (mode == ModeSpike) {
                Projectile.rotation += 0.3f * SpinDir;
            }
            else if (mode is ModeWater or ModeCursed or ModeCross) {
                Projectile.rotation += 0.18f * SpinDir;
            }
            else {
                Projectile.rotation = Projectile.velocity.ToRotation() + ModeRotOffset[mode];
            }

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, ModeDust(mode),
                    -Projectile.velocity * 0.15f, 130, default, 0.9f);
                dust.noGravity = true;
            }
            if (ModeSelfLit[mode]) {
                Lighting.AddLight(Projectile.Center, ModeTint[mode].ToVector3() * 0.28f);
            }
        }

        private static int ModeDust(int mode) => mode switch {
            ModeWater => DustID.DungeonSpirit,
            ModeCursed or ModeCross => DustID.CursedTorch,
            ModeShadow => DustID.Shadowflame,
            ModeSpike => DustID.Bone,
            _ => DustID.Smoke,
        };

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            int mode = Mode;
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, ModeDust(mode),
                    Main.rand.NextVector2Circular(2.2f, 2.2f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = mode != ModeSpike;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int mode = Mode;
            int donor = DonorProj[mode];
            Main.instance.LoadProjectile(donor);
            Texture2D tex = TextureAssets.Projectile[donor].Value;
            int donorFrames = Math.Max(1, Main.projFrames[donor]);
            Rectangle frameRect = new(0, 0, tex.Width, tex.Height / donorFrames);
            Vector2 origin = frameRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color tint = ModeTint[mode];
            Color bodyColor = ModeSelfLit[mode]
                ? Color.Lerp(Color.White, tint, 0.35f)
                : Color.Lerp(tint, lightColor, 0.6f);

            //拖尾：同贴图降比重画（横轴 ≥0.5×弹体）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, frameRect, bodyColor * (0.40f * t),
                    Projectile.oldRot[i], origin, Projectile.scale * 0.72f, SpriteEffects.None, 0);
            }

            //自发光模式加一层暖衬（A=0 加色），弹体本体保留遮挡像素
            if (ModeSelfLit[mode]) {
                Main.EntitySpriteDraw(tex, drawPos, frameRect, (tint with { A = 0 }) * 0.45f,
                    Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, drawPos, frameRect, bodyColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

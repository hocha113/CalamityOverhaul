using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 荷兰飞船·舷炮齐射预兆：ai[0]=飞船NPC索引 ai[1]=开火朝向(±1) ai[2]=Pack(伤害,档位)。<br/>
    /// 生成瞬间整套车道几何冻结（发射侧、车道高度、空车道全部定格，飞船漂移不改承诺），
    /// 五条水平弹道车道自上而下逐门亮起炮口，空车道由 identity 确定性推得且永不亮起，
    /// 预演与发射循环共用同一个空车道判定：黑着的那条车道就是安全的那条。<br/>
    /// 与部件无关的降级形态：炮口挂在船体几何位上，不读 PirateShipCannon 部件关系
    /// </summary>
    internal class PrtBroadsideOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 车道几何（预演与发射共用；具名空车道=公平阀门）====
        /// <summary>车道总数</summary>
        internal const int LaneCount = 5;
        /// <summary>车道纵向间距</summary>
        internal const float LaneSpacing = 46f;
        /// <summary>车道长度（弹体寿命按它封顶，危险不越过画出的车道）</summary>
        internal const float LaneLength = 1150f;
        /// <summary>空车道下限与跨度：空车道只落在内侧 1..3（边缘车道当缺口没有价值）</summary>
        internal const int EmptyLaneMin = 1;
        internal const int EmptyLaneSpan = LaneCount - 2;
        /// <summary>炮口相对船心的横向探出</summary>
        private const float MuzzleOffsetX = 96f;

        //==== 节奏（小Boss 签名技预告 ≥40 帧）====
        /// <summary>预告总帧数</summary>
        internal const int TelegraphFrames = 68;
        /// <summary>首门炮亮起帧与逐门间隔</summary>
        private const int FirstIgniteAge = 8;
        private const int MuzzleInterval = 12;

        /// <summary>铁弹初速（档位 1/2/3）</summary>
        private static readonly float[] BallSpeedByTier = [8.5f, 9.2f, 10f];

        private static readonly Color CannonAmber = new Color(255, 176, 84);
        private static readonly Color LaneDim = new Color(255, 150, 90);

        private int ShipIndex => (int)Projectile.ai[0];
        private int Dir => Projectile.ai[1] >= 0f ? 1 : -1;
        private int Damage => (int)Projectile.ai[2] / 4;
        private int Tier => Math.Clamp((int)Projectile.ai[2] % 4, 1, 3);
        private int Age => TelegraphFrames - Projectile.timeLeft;

        internal static float Pack(int damage, int tier) => damage * 4 + tier;

        /// <summary>空车道：由同步的 identity 确定性推得，预演与发射读同一函数（公平阀门）</summary>
        private int EmptyLane => EmptyLaneMin + Projectile.identity % EmptyLaneSpan;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不判定</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>车道纵坐标（相对冻结的生成中心）</summary>
        private float LaneY(int lane) => Projectile.Center.Y + (lane - (LaneCount - 1) / 2f) * LaneSpacing;

        /// <summary>炮口坐标</summary>
        private Vector2 MuzzlePos(int lane) => new Vector2(Projectile.Center.X + Dir * MuzzleOffsetX, LaneY(lane));

        /// <summary>该车道炮口的点火帧龄：按发火顺序（自上而下，跳过空车道）逐门排队</summary>
        private int IgniteAge(int lane) {
            int order = lane - (lane > EmptyLane ? 1 : 0);
            return FirstIgniteAge + order * MuzzleInterval;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            //飞船没了（被击破/撤离）：齐射不会发生，预兆消散
            if (!VaultUtils.isClient
                && !(ShipIndex.TryGetNPC(out NPC ship) && ship.type == NPCID.PirateShip)) {
                Projectile.Kill();
                return;
            }

            //逐门点火：帧龄由常量表确定，各端本地同拍触发音效与火星
            if (!Main.dedServ) {
                for (int lane = 0; lane < LaneCount; lane++) {
                    if (lane == EmptyLane || Age != IgniteAge(lane)) {
                        continue;
                    }
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 5 }, MuzzlePos(lane));
                    for (int i = 0; i < 4; i++) {
                        Dust fuse = Dust.NewDustPerfect(MuzzlePos(lane), DustID.Torch,
                            new Vector2(Dir * Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-1f, 1f)),
                            80, default, Main.rand.NextFloat(0.9f, 1.4f));
                        fuse.noGravity = true;
                    }
                }
            }

            Lighting.AddLight(Projectile.Center + new Vector2(Dir * MuzzleOffsetX, 0f),
                CannonAmber.ToVector3() * 0.3f);

            if (Projectile.timeLeft == 1 && !VaultUtils.isClient) {
                FireBroadside();
            }
        }

        /// <summary>齐射：跳过空车道，其余车道从冻结炮口水平出膛（车道即弹道）</summary>
        private void FireBroadside() {
            float speed = BallSpeedByTier[Tier - 1];
            bool voiced = false;
            for (int lane = 0; lane < LaneCount; lane++) {
                //具名空车道：这里跳过的车道就是预演里黑着的车道
                if (lane == EmptyLane) {
                    continue;
                }
                //ai[0]=1 只标记头弹：齐射轰鸣挂在它的出生帧各端本地播
                //（预兆自己的倒计时死亡帧会与击杀包竞速，音效放那里会被偶发吞掉）
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), MuzzlePos(lane),
                    new Vector2(Dir * speed, 0f),
                    ModContent.ProjectileType<PrtBroadsideBall>(), Damage, 0f, Main.myPlayer,
                    voiced ? 0f : 1f);
                voiced = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.instance.LoadProjectile(ProjectileID.CannonballHostile);
            Texture2D ballTex = TextureAssets.Projectile[ProjectileID.CannonballHostile].Value;
            Vector2 lineOrigin = new Vector2(0f, line.Height / 2f);

            float fadeIn = MathHelper.Clamp(Age / 10f, 0f, 1f);
            float urgency = MathHelper.Clamp(Age / (float)TelegraphFrames, 0f, 1f);
            float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.6f);

            for (int lane = 0; lane < LaneCount; lane++) {
                //空车道永不点亮：黑着的车道=安全车道（所见即所射）
                if (lane == EmptyLane) {
                    continue;
                }
                bool lit = Age >= IgniteAge(lane);
                Vector2 muzzle = MuzzlePos(lane);
                Vector2 muzzleScreen = muzzle - Main.screenPosition;
                float laneAlpha = lit ? (0.3f + 0.35f * urgency) * pulse : 0.1f;

                //水平弹道车道：窄芯 + 宽晕（横排舷炮的视觉语言，区别于扇面/落点阵）
                Main.EntitySpriteDraw(line, muzzleScreen, null,
                    LaneDim with { A = 0 } * (laneAlpha * fadeIn),
                    Dir > 0 ? 0f : MathHelper.Pi, lineOrigin,
                    new Vector2(LaneLength / line.Width, 8f / line.Height), SpriteEffects.None, 0);
                if (lit) {
                    Main.EntitySpriteDraw(line, muzzleScreen, null,
                        LaneDim with { A = 0 } * (laneAlpha * 0.5f * fadeIn),
                        Dir > 0 ? 0f : MathHelper.Pi, lineOrigin,
                        new Vector2(LaneLength / line.Width, 26f / line.Height), SpriteEffects.None, 0);
                    //炮口辉光 + 待发铁弹幽灵（真 alpha 弹体贴图，宣告弹体将从此出膛）
                    Main.EntitySpriteDraw(glow, muzzleScreen, null,
                        (CannonAmber with { A = 0 }) * (0.55f * pulse * fadeIn),
                        0f, glow.Size() / 2f, 0.38f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(ballTex, muzzleScreen, null,
                        Color.Lerp(CannonAmber, lightColor, 0.4f) * (0.5f * pulse * fadeIn),
                        0f, ballTex.Size() / 2f, 0.85f, SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}

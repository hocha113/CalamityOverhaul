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
    /// 船长压制齐射预兆：ai[0]=船长NPC索引 ai[1]=锁定瞄角 ai[2]=Pack(伤害,档位)。<br/>
    /// 生成瞬间冻结在枪口、瞄角锁死（预告即承诺），预演画出每条将射的水平扇面射线，
    /// 缺口弹位由 Projectile.identity 确定性推得，预演与发射循环共用同一个缺口判定，
    /// 看见空着的那条弹道就是安全的那条
    /// </summary>
    internal class PrtVolleyOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（≥30 帧契约）</summary>
        internal const int TelegraphFrames = 42;
        /// <summary>扇面弹位总数</summary>
        internal const int FanLanes = 7;
        /// <summary>扇面半张角（弧度）</summary>
        internal const float FanHalfArc = 0.46f;
        /// <summary>缺口弹位下限与跨度：缺口只落在内侧弹位 1..5（边缘弹位当缺口没有价值）</summary>
        internal const int GapLaneMin = 1;
        internal const int GapLaneSpan = FanLanes - 2;
        /// <summary>铅弹初速（档位 1/2/3）</summary>
        private static readonly float[] ShotSpeedByTier = [8.2f, 8.8f, 9.5f];
        /// <summary>预演射线长度区间（催迫感：随倒计时拉长）</summary>
        private const float RayLenBase = 130f;
        private const float RayLenGrow = 110f;

        private static readonly Color PowderGold = new Color(255, 214, 120);

        private int CaptainIndex => (int)Projectile.ai[0];
        private float Aim => Projectile.ai[1];
        private int Damage => (int)Projectile.ai[2] / 4;
        private int Tier => Math.Clamp((int)Projectile.ai[2] % 4, 1, 3);
        private int Age => TelegraphFrames - Projectile.timeLeft;

        internal static float Pack(int damage, int tier) => damage * 4 + tier;

        /// <summary>缺口弹位：由同步的 identity 确定性推得，各端一致；预演与发射读同一函数（公平阀门）</summary>
        private int GapLane => GapLaneMin + Projectile.identity % GapLaneSpan;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
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

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = Aim;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //船长没了（死亡/槽位易主）：齐射不会发生，军令消散
            if (!VaultUtils.isClient
                && !(CaptainIndex.TryGetNPC(out NPC captain) && captain.type == NPCID.PirateCaptain)) {
                Projectile.Kill();
                return;
            }

            //火药凝聚尘（≤2 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Torch, Aim.ToRotationVector2() * Main.rand.NextFloat(0.5f, 1.4f), 130, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, PowderGold.ToVector3() * 0.2f);

            if (Projectile.timeLeft == 1 && !VaultUtils.isClient) {
                FireVolley();
            }
        }

        /// <summary>弹位角：预演与发射共用（所见即所射）</summary>
        private float LaneAngle(int lane) => Aim - FanHalfArc + 2f * FanHalfArc * lane / (FanLanes - 1);

        /// <summary>瞬发齐射：跳过缺口弹位，其余弹位沿预演射线出膛</summary>
        private void FireVolley() {
            float speed = ShotSpeedByTier[Tier - 1];
            bool voiced = false;
            for (int lane = 0; lane < FanLanes; lane++) {
                //具名缺口：这里跳过的弹位就是预演里空着的弹位
                if (lane == GapLane) {
                    continue;
                }
                //ai[0]=1 只标记头弹：齐射轰响挂在它的出生帧各端本地播
                //（预兆自己的倒计时死亡帧会与击杀包竞速，音效放那里会被偶发吞掉）
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    LaneAngle(lane).ToRotationVector2() * speed,
                    ModContent.ProjectileType<PrtFanShot>(), Damage, 0f, Main.myPlayer,
                    voiced ? 0f : 1f);
                voiced = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D core = CWRAsset.Extra_98.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 lineOrigin = new Vector2(0f, line.Height / 2f);

            float fadeIn = MathHelper.Clamp(Age / 8f, 0f, 1f);
            float urgency = MathHelper.Clamp(Age / (float)TelegraphFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity * 0.7f);
            float rayLen = RayLenBase + RayLenGrow * urgency;

            //弹位射线：细直线束（与他组的幽灵弹位/落点标记语言区分），缺口弹位空出
            for (int lane = 0; lane < FanLanes; lane++) {
                if (lane == GapLane) {
                    continue;
                }
                float angle = LaneAngle(lane);
                Main.EntitySpriteDraw(line, center, null,
                    PowderGold with { A = 0 } * (fadeIn * (0.3f + 0.4f * urgency) * pulse),
                    angle, lineOrigin, new Vector2(rayLen / line.Width, 9f / line.Height), SpriteEffects.None, 0);
            }

            //枪口凝核：真透贴图打底 + 加色晕
            Main.EntitySpriteDraw(core, center, null, PowderGold * (0.7f * fadeIn),
                Aim, core.Size() / 2f, 0.26f + 0.08f * urgency, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, center, null, (PowderGold with { A = 0 }) * (0.45f * fadeIn * pulse),
                0f, core.Size() / 2f, 0.42f + 0.1f * urgency, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 近战怪姿态前摇载体（两风味共用）：ai[0]=锚NPC索引 ai[1]=风味×1000+锚NPC类型
    /// ai[2]=锁定方向+10（0=未锁定，姜饼突进用）。
    /// 风味 0=僵尸精灵弯腰团雪（手中雪球虚影渐大+雪尘聚拢）/ 1=姜饼人搓手蓄力（糖粉+渐急琥珀闪）。
    /// 全程跟随锚体、锚体死亡即消散（击杀=有效反制）；永不参与伤害，
    /// 客户端的前摇可见性全部由本实体承载（决策计时器是权威端私产）
    /// </summary>
    internal class FrmPoseTelegraphProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SnowBallHostile;

        internal const int StyleSnowGather = 0;
        internal const int StyleHandRub = 1;

        /// <summary>团雪：前摇帧（≥30 契约）/ 出手余痕帧</summary>
        internal const int SnowWindupFrames = 32;
        internal const int SnowLingerFrames = 8;

        /// <summary>搓手：前摇帧（≥24 体术契约）/ 突进伴随帧（=突进包络全长）/ 消散帧</summary>
        internal const int RubWindupFrames = 26;
        internal const int RubDashFrames = 42;
        internal const int RubFadeFrames = 8;

        private static readonly Color SnowWarn = new Color(216, 238, 255, 0);
        private static readonly Color AmberWarn = new Color(255, 186, 92, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private int Style => (int)Projectile.ai[1] / 1000;
        private int AnchorType => (int)Projectile.ai[1] % 1000;
        private bool AuthLocked => Projectile.ai[2] != 0f;
        private float LockedDir => Projectile.ai[2] - 10f;

        private int WindupFrames => Style == StyleHandRub ? RubWindupFrames : SnowWindupFrames;
        private int TotalLife => Style == StyleHandRub
            ? RubWindupFrames + RubDashFrames + RubFadeFrames
            : SnowWindupFrames + SnowLingerFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;

        /// <summary>团雪虚影目标体积：三型差异可视化（胡子重球大、女孩小球小）</summary>
        private float GhostScale => AnchorType == NPCID.ZombieElfBeard ? 1.45f
            : AnchorType == NPCID.ZombieElfGirl ? 0.78f : 1.05f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        /// <summary>纯姿态载体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.localAI[1] = Projectile.timeLeft;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.35f, Pitch = Style == StyleHandRub ? -0.2f : 0.3f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //跟随锚体（索引+类型双校验，防槽位复用）；锚体没了攻击不会发生，姿态随之消散
            if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives() || anchor.type != AnchorType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            int elapsed = Elapsed;
            int dir = anchor.spriteDirection;

            if (Style == StyleSnowGather) {
                //团雪期：雪尘向手中聚拢（≤2 粒/帧，实体承载=各端可见）
                if (elapsed < SnowWindupFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 hand = Projectile.Center + new Vector2(dir * 10f, -12f);
                    Vector2 from = hand + Main.rand.NextVector2CircularEdge(26f, 22f);
                    Dust dust = Dust.NewDustPerfect(from, DustID.Snow, (hand - from) * 0.09f, 130, default, 0.9f);
                    dust.noGravity = true;
                }
                if (elapsed == SnowWindupFrames && !Main.dedServ) {
                    //出手帧（各端本地播放）
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                }
            }
            else {
                if (elapsed < RubWindupFrames) {
                    //搓手期：糖粉四溢 + 偶发暖星（≤2 粒/帧）
                    if (!Main.dedServ && Main.rand.NextBool(2)) {
                        Vector2 hands = Projectile.Center + new Vector2(dir * 8f, -2f);
                        Dust dust = Dust.NewDustPerfect(hands + Main.rand.NextVector2Circular(8f, 6f),
                            Main.rand.NextBool(4) ? DustID.Torch : DustID.Snow,
                            new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.3f, 1f)), 130, default, 0.85f);
                        dust.noGravity = true;
                    }
                }
                else if (!Main.dedServ && Main.rand.NextBool(2)) {
                    //突进段：脚下糖屑尾迹（实体承载=联机同样可见）
                    Dust dust = Dust.NewDustPerfect(anchor.Bottom + new Vector2(-anchor.velocity.X * 1.5f, -4f),
                        Main.rand.NextBool(3) ? DustID.Torch : DustID.Snow,
                        new Vector2(-anchor.velocity.X * 0.15f, -Main.rand.NextFloat(0.8f, 2f)), 110, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = true;
                }
                if (elapsed == RubWindupFrames && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            Lighting.AddLight(Projectile.Center, (Style == StyleHandRub ? AmberWarn : SnowWarn).ToVector3() * 0.12f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fadeIn = MathHelper.Clamp(elapsed / 6f, 0f, 1f);
            Vector2 center = Projectile.Center - Main.screenPosition;
            int dir = AnchorIndex.TryGetNPC(out NPC anchor) && anchor.Alives() ? anchor.spriteDirection : 1;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            if (Style == StyleSnowGather) {
                if (elapsed >= SnowWindupFrames) {
                    return false;//球已出手，余痕期不再画虚影
                }
                float grow = MathHelper.Clamp(elapsed / (float)SnowWindupFrames, 0f, 1f);
                float urgency = grow * grow;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * (10f + 8f * urgency) + Projectile.identity);

                //手中雪球虚影渐大（原版雪球贴图，所见即将投之物）
                Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
                Texture2D ball = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
                int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
                Rectangle rect = ball.Frame(1, frames, 0, 0);
                Vector2 hand = center + new Vector2(dir * 10f, -12f);
                Color ghost = Color.Lerp(lightColor, new Color(232, 244, 255), 0.6f) * (0.85f * fadeIn * pulse);
                Main.EntitySpriteDraw(ball, hand, rect, ghost, grow * dir * 0.6f,
                    rect.Size() / 2f, GhostScale * (0.25f + 0.75f * grow), SpriteEffects.None, 0);
                //冷光衬底
                Main.EntitySpriteDraw(glow, hand, null, SnowWarn * (0.3f * fadeIn * pulse), 0f,
                    glow.Size() / 2f, 0.22f + 0.14f * grow, SpriteEffects.None, 0);
                return false;
            }

            //搓手风味
            if (elapsed < RubWindupFrames) {
                float grow = MathHelper.Clamp(elapsed / (float)RubWindupFrames, 0f, 1f);
                //渐急闪烁：频率随进度上升
                float flick = 0.6f + 0.4f * MathF.Sin(elapsed * (0.35f + 0.5f * grow) + Projectile.identity);
                Vector2 hands = center + new Vector2(dir * 8f, -2f);
                Main.EntitySpriteDraw(glow, hands, null, AmberWarn * (0.5f * fadeIn * flick), 0f,
                    glow.Size() / 2f, 0.2f + 0.12f * grow, SpriteEffects.None, 0);
                //锁定后：突进方向亮出琥珀楔（方向自此为承诺）
                if (AuthLocked) {
                    Vector2 ahead = center + LockedDir.ToRotationVector2() * 30f;
                    Main.EntitySpriteDraw(glow, ahead, null, AmberWarn * (0.45f * flick), LockedDir,
                        glow.Size() / 2f, new Vector2(1.9f, 0.4f), SpriteEffects.None, 0);
                }
                return false;
            }

            //突进段：身后残热（快速退淡）
            float dashT = MathHelper.Clamp((elapsed - RubWindupFrames) / (float)RubDashFrames, 0f, 1f);
            float tail = (1f - dashT) * 0.35f;
            if (tail > 0.02f) {
                Main.EntitySpriteDraw(glow, center, null, AmberWarn * tail, 0f,
                    glow.Size() / 2f, new Vector2(0.9f, 0.3f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

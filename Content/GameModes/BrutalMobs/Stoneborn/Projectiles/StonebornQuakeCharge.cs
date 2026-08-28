using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 花岗岩魔像·共振蓄能载体：ai[0]=锚NPC索引 ai[1]=锚NPC类型。
    /// 落地立定蓄能的全部可见性（体内电光渐亮+嗡鸣渐强）由本实体承载，
    /// 决策计时器是权威端私产、客户端画面只看这里（M8）。
    /// 锚体死亡或提前入壳（原版 ai[2]&lt;0）即消散——震地不会发生，预告随之取消（M3 失败方向=安全方向）。
    /// 永不参与伤害，共振波在提交帧由 NPC 侧生成
    /// </summary>
    internal class StonebornQuakeCharge : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>蓄能前摇帧（任务契约 ≥34，档位一律不缩短）</summary>
        internal const int WindupFrames = 34;
        /// <summary>提交后的余辉帧（覆盖震地一拍的可见反馈）</summary>
        internal const int LingerFrames = 6;

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1];
        private int Elapsed => WindupFrames + LingerFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindupFrames + LingerFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯蓄能载体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //锚定校验：索引+类型双校验防槽位复用；锚体没了或提前入壳，蓄能作废
            if (!AnchorIndex.TryGetNPC(out NPC anchor) || !anchor.Alives() || anchor.type != AnchorType) {
                Projectile.Kill();
                return;
            }
            if (Elapsed < WindupFrames && anchor.ai[2] < 0f) {
                //原版蹲缩打断蓄能（各端读同步的 ai[2]，结论一致；NPC 侧同一判据回冷却）
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            int elapsed = Elapsed;
            float charge = MathHelper.Clamp(elapsed / (float)WindupFrames, 0f, 1f);

            if (!Main.dedServ) {
                //嗡鸣渐强：低频 tick，音调与音量随蓄能爬升
                if (elapsed < WindupFrames && elapsed % 8 == 0) {
                    SoundEngine.PlaySound(SoundID.MaxMana with {
                        Volume = 0.25f + 0.3f * charge,
                        Pitch = -0.5f + 0.8f * charge,
                        MaxInstances = 5,
                    }, Projectile.Center);
                }
                //体内电光凝聚：电尘向躯干收拢（≤2 粒/帧）
                if (elapsed < WindupFrames && Main.rand.NextBool(2)) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(30f, 26f);
                    Dust dust = Dust.NewDustPerfect(from, DustID.Electric,
                        (Projectile.Center - from) * 0.10f, 90, default, 0.8f + 0.5f * charge);
                    dust.noGravity = true;
                }
                //提交帧：震地一拍（各端本地播放，M8 演出不走权威分支）
                if (elapsed == WindupFrames) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 4 }, Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust dust = Dust.NewDustPerfect(anchor.Bottom,
                            Main.rand.NextBool() ? DustID.Stone : DustID.Electric,
                            new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(0.5f, 2.4f)),
                            80, default, 1.2f);
                        dust.noGravity = Main.rand.NextBool();
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * (0.1f + 0.3f * charge));
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            //提交后快速退光；蓄能期强度=进度平方（临爆前最亮）
            float strength = elapsed >= WindupFrames
                ? MathHelper.Clamp(1f - (elapsed - WindupFrames) / (float)LingerFrames, 0f, 1f) * 0.8f
                : MathHelper.Clamp(elapsed / (float)WindupFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (9f + 14f * strength) + Projectile.identity);
            Color core = GraniteMarbleVFX.GraniteCore with { A = 0 };
            Color spark = GraniteMarbleVFX.GraniteSpark with { A = 0 };
            //体内电光渐亮：内亮芯 + 外扩晕（纯加色，蓄能载体不承担遮挡职责）
            Main.EntitySpriteDraw(glow, center, null, spark * (0.7f * strength * strength * pulse), 0f,
                glow.Size() / 2f, 0.28f + 0.22f * strength, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, core * (0.4f * strength * pulse), 0f,
                glow.Size() / 2f, 0.6f + 0.35f * strength, SpriteEffects.None, 0);
            return false;
        }
    }
}

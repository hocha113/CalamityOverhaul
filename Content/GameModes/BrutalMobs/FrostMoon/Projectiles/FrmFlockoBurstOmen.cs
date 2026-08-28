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
    /// 雪花怪自爆膨胀预告：ai[0]=锚NPC索引 ai[1]=放射环基准角（膨胀起始帧锁定，预告即承诺）
    /// ai[2]=锚NPC类型。膨胀期跟随锚体：霜壳渐大+闪烁渐急+蜂鸣渐促；
    /// 放射槽位虚影与真实发射共用 <see cref="SlotArmed"/> 同一判定（缺口槽所见即所空）。
    /// 膨胀期锚体死亡即取消（提前击杀=拆弹成功）；提交帧锚体按设计自亡，
    /// 爆点视觉在冻结的实体位置播放，各端从自身时间轴推得同一瞬间
    /// </summary>
    internal class FrmFlockoBurstOmen : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>膨胀预告帧数（契约 ≥40，各档位一律不缩短）</summary>
        internal const int SwellFrames = 44;
        /// <summary>爆闪余辉帧</summary>
        private const int FadeFrames = 12;

        //==== 公平阀门：具名槽位缺口（发射循环真正读取） ====
        /// <summary>放射环槽位总数（几何恒定，档位不改形状）</summary>
        internal const int BurstSlots = 8;
        /// <summary>恒定缺口槽位：发射循环跳过此槽=可学习的逃生方向（基准角=锁定时指向玩家）</summary>
        internal const int BurstGapSlot = 0;

        /// <summary>槽位是否装填（发射与虚影共用，缺口即所见）</summary>
        internal static bool SlotArmed(int slot) => slot != BurstGapSlot;

        /// <summary>第 slot 槽的放射角</summary>
        internal static float SlotAngle(float baseAngle, int slot)
            => baseAngle + MathHelper.TwoPi * slot / BurstSlots;

        private static readonly Color FrostWarn = new Color(170, 220, 255, 0);

        private int AnchorIndex => (int)Projectile.ai[0];
        private float BaseAngle => Projectile.ai[1];
        private int AnchorType => (int)Projectile.ai[2];
        private int TotalLife => SwellFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//纯预告体，杀伤经由冰晶
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>膨胀进度 0..1（闪烁与蜂鸣的加急曲线共用）</summary>
        private float Urgency => MathHelper.Clamp(Elapsed / (float)SwellFrames, 0f, 1f);

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;

            //来源检查：膨胀期锚体死亡则取消（提前击杀=拆弹成功）；类型比对防槽位复用。
            //提交帧起不再检查——锚体按设计在提交帧自亡，爆闪要在冻结位置照常播放
            if (!Cancelled && elapsed < SwellFrames) {
                if (AnchorIndex.TryGetNPC(out NPC anchor) && anchor.Alives() && anchor.type == AnchorType) {
                    Projectile.Center = anchor.Center;//跟随锚体，爆点=实体此刻位置
                }
                else {
                    Cancelled = true;
                }
            }

            if (Cancelled) {
                return;
            }

            if (elapsed < SwellFrames) {
                //蜂鸣渐促：间隔随进度收紧、音调走高（各端由 elapsed 确定性推得同一节拍）
                int beepGap = Math.Max(5, 15 - elapsed / 4);
                if (elapsed % beepGap == 0 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MaxMana with {
                        Volume = 0.4f + 0.25f * Urgency,
                        Pitch = -0.2f + 0.9f * Urgency,
                        MaxInstances = 6,
                    }, Projectile.Center);
                }
                //膨胀期环缘霜屑（≤2 粒/帧）
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * (14f + 26f * Urgency),
                        DustID.Frost, ang.ToRotationVector2() * 0.6f, 130, default, 0.9f);
                    dust.noGravity = true;
                }
            }
            else if (elapsed == SwellFrames && !Main.dedServ) {
                //爆帧（各端本地播放；冰晶实体另行同步抵达）
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with { Volume = 0.55f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Ice : DustID.Snow,
                        Main.rand.NextVector2Circular(4f, 4f), 90, default, Main.rand.NextFloat(1f, 1.6f));
                    dust.noGravity = Main.rand.NextBool();
                }
            }

            Lighting.AddLight(Projectile.Center, FrostWarn.ToVector3() * (0.2f + 0.35f * Urgency));
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D shell = CWRAsset.Extra_98.Value;

            if (Cancelled) {
                //拆弹成功：残壳快速退淡
                float ghost = 0.3f * MathHelper.Clamp(1f - elapsed / (float)SwellFrames, 0f, 1f);
                if (ghost > 0.02f) {
                    Main.EntitySpriteDraw(shell, center, null, new Color(150, 190, 220) * ghost, 0f,
                        shell.Size() / 2f, 0.3f, SpriteEffects.None, 0);
                }
                return false;
            }

            if (elapsed < SwellFrames) {
                float urgency = Urgency;
                float fadeIn = MathHelper.Clamp(elapsed / 8f, 0f, 1f);
                //高频闪烁：频率随进度加急（体积渐大由 swellScale 表达）
                float flick = 0.62f + 0.38f * MathF.Sin(elapsed * (0.4f + 0.75f * urgency) + Projectile.identity);
                float swellScale = 0.34f + 0.5f * urgency;

                //霜壳（真 alpha 实体层）渐大 + 加色冷芯
                Main.EntitySpriteDraw(shell, center, null, new Color(196, 226, 250) * (0.62f * fadeIn * flick),
                    Projectile.identity * 0.7f, shell.Size() / 2f, swellScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, center, null, FrostWarn * (0.55f * fadeIn * flick), 0f,
                    glow.Size() / 2f, 0.5f + 0.45f * urgency, SpriteEffects.None, 0);

                //放射槽位虚影：与发射循环共用 SlotArmed，缺口槽所见即所空
                Main.instance.LoadProjectile(ProjectileID.IceBolt);
                Texture2D bolt = TextureAssets.Projectile[ProjectileID.IceBolt].Value;
                Vector2 boltOrig = bolt.Size() / 2f;
                float ghostDist = 24f + 30f * urgency;
                float ghostAlpha = (0.3f + 0.4f * urgency) * fadeIn * flick;
                for (int slot = 0; slot < BurstSlots; slot++) {
                    if (!SlotArmed(slot)) {
                        continue;
                    }
                    float ang = SlotAngle(BaseAngle, slot);
                    Vector2 pos = center + ang.ToRotationVector2() * ghostDist;
                    Main.EntitySpriteDraw(bolt, pos, null, FrostWarn * ghostAlpha, ang + MathHelper.PiOver2,
                        boltOrig, 0.85f, SpriteEffects.None, 0);
                }

                //缺口亮巷（指示安全方向）
                float gapAng = SlotAngle(BaseAngle, BurstGapSlot);
                Vector2 lanePos = center + gapAng.ToRotationVector2() * (ghostDist + 26f);
                Main.EntitySpriteDraw(glow, lanePos, null, new Color(200, 255, 230, 0) * (0.5f * fadeIn), gapAng,
                    glow.Size() / 2f, new Vector2(2.4f, 0.42f), SpriteEffects.None, 0);
                return false;
            }

            //爆闪：白蓝闪芯 + 碎壳扩散（余辉期衰减）
            float vis = MathHelper.Clamp(1f - (elapsed - SwellFrames) / (float)FadeFrames, 0f, 1f);
            if (vis <= 0.01f) {
                return false;
            }
            float expand = 1f + 1.6f * (1f - vis);
            Main.EntitySpriteDraw(shell, center, null, new Color(180, 216, 244) * (0.5f * vis), Projectile.identity,
                shell.Size() / 2f, 0.8f * expand, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, FrostWarn * (0.85f * vis), 0f,
                glow.Size() / 2f, 1.5f * expand, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, Color.White with { A = 0 } * (0.6f * vis * vis), 0f,
                glow.Size() / 2f, 0.9f * expand, SpriteEffects.None, 0);
            return false;
        }
    }
}

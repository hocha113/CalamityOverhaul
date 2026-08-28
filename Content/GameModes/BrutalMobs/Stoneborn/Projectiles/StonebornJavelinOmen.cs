using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 豪杰骷髅·标枪三连预告：ai[0]=锁定瞄角（生成帧锁死，锁向即承诺）
    /// ai[1]=来源NPC+1|类型&lt;&lt;8 ai[2]=未用。
    /// 瞄准段 <see cref="AimFrames"/> 帧亮出短标线与三枚将掷标枪的幽灵位（所见即所射：
    /// 第 2 枪的幽灵按抬高角画出，节奏缺口在预告里就能读到）→
    /// 齐射段沿锁线每 <see cref="ShotIntervalFrames"/> 帧掷一枪。
    /// 瞄准/齐射期来源死亡则取消余下投掷（击杀施法者是有效反制）
    /// </summary>
    internal class StonebornJavelinOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>瞄准前摇帧（任务契约 ≥30，档位一律不缩短）</summary>
        internal const int AimFrames = 30;
        /// <summary>三连间隔帧</summary>
        internal const int ShotIntervalFrames = 10;
        /// <summary>三连总枪数</summary>
        internal const int ShotCount = 3;
        /// <summary>收尾淡出帧</summary>
        private const int FadeFrames = 8;

        //==== 公平阀门：具名节奏缺口（发射循环与幽灵预览同读） ====
        /// <summary>第 2 枪固定抬高的弧度：贴地走位可避（这一枪从锁线上方掠过，落点越过贴地目标）</summary>
        internal const float SecondShotLiftRad = 0.12f;
        /// <summary>第 1/3 枪的微散布（同锁线，读作同一次投掷动作的手抖）</summary>
        private const float MicroScatterRad = 0.025f;
        /// <summary>标枪出手速度</summary>
        private const float JavelinSpeed = 8.2f;
        /// <summary>标线可视长度</summary>
        private const float LaneLength = 250f;

        private float LockedAim => Projectile.ai[0];
        private int SourcePacked => (int)Projectile.ai[1];
        private int TotalLife => AimFrames + ShotIntervalFrames * (ShotCount - 1) + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 320;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AimFrames + ShotIntervalFrames * (ShotCount - 1) + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，伤害经由标枪实体</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        /// <summary>第 i 枪的实际出手角：第 2 枪抬高（具名缺口），其余同锁线微散布</summary>
        internal static float ShotAngle(float lockedAim, int shotIndex, float scatter) {
            if (shotIndex == 1) {
                //抬高方向与水平朝向绑定：无论朝左朝右都是「向上」抬（贴地走位可避）
                float upSign = MathF.Cos(lockedAim) >= 0f ? -1f : 1f;
                return lockedAim + upSign * SecondShotLiftRad;
            }
            return lockedAim + scatter;
        }

        public override void AI() {
            int elapsed = Elapsed;

            //来源检查：施法者死亡/槽位复用则取消余下投掷（各端读同步的 npc.active，结论一致）
            if (!Cancelled) {
                int src = (SourcePacked & 255) - 1;
                if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                    || Main.npc[src].type != SourcePacked >> 8) {
                    Cancelled = true;
                }
                else {
                    //跟随持枪手（枪位=原版投掷肩位：中心上方 14px）
                    Projectile.Center = Main.npc[src].Center + new Vector2(0f, -14f);
                }
            }

            if (!Main.dedServ) {
                if (elapsed == 0) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
                }
                //瞄准段：沿锁线的金尘（≤2 粒/帧）
                if (!Cancelled && elapsed < AimFrames && Main.rand.NextBool(2)) {
                    float along = Main.rand.NextFloat(20f, LaneLength * 0.6f);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + LockedAim.ToRotationVector2() * along,
                        DustID.GoldFlame, LockedAim.ToRotationVector2() * 0.5f, 120, default, 0.7f);
                    dust.noGravity = true;
                }
            }

            //齐射：提交帧起每 10 帧一枪（权威端生成，出手角随生成包带走；音效各端本地按拍播放）
            if (!Cancelled && elapsed >= AimFrames && (elapsed - AimFrames) % ShotIntervalFrames == 0) {
                int shotIndex = (elapsed - AimFrames) / ShotIntervalFrames;
                if (shotIndex < ShotCount) {
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        //散布在权威端掷骰，随标枪生成包同步（M8：随机不进判定分歧）
                        float scatter = Main.rand.NextFloat(-MicroScatterRad, MicroScatterRad);
                        Vector2 vel = ShotAngle(LockedAim, shotIndex, scatter).ToRotationVector2() * JavelinSpeed;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                            ModContent.ProjectileType<StonebornJavelinProj>(), Projectile.damage, 1f, Main.myPlayer);
                    }
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = 0.15f, MaxInstances = 5 }, Projectile.Center);
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.14f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float fade;
            if (Cancelled) {
                fade = 0.35f * MathHelper.Clamp(1f - elapsed / (float)AimFrames, 0f, 1f);
            }
            else {
                float tail = MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
                fade = Math.Min(MathHelper.Clamp(elapsed / 8f, 0f, 1f), tail);
            }
            if (fade <= 0.01f) {
                return false;
            }

            Texture2D lane = CWRAsset.MaskLaserLine.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 laneOrigin = new Vector2(0f, lane.Height / 2f);
            Color gold = GraniteMarbleVFX.MarbleGold with { A = 0 };
            float urgency = MathHelper.Clamp(elapsed / (float)AimFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            //短标线：锁向即承诺
            Main.EntitySpriteDraw(lane, drawPos, null, gold * (0.45f * fade * pulse), LockedAim,
                laneOrigin, new Vector2(LaneLength / lane.Width, 12f / lane.Height), SpriteEffects.None, 0);

            //三枚幽灵标枪：各画在实际出手角上（第 2 枚按抬高角画，所见即所射）；已掷出的枪位熄灭
            Main.instance.LoadProjectile(ProjectileID.JavelinHostile);
            Texture2D ghostTex = TextureAssets.Projectile[ProjectileID.JavelinHostile].Value;
            Vector2 ghostOrigin = ghostTex.Size() / 2f;
            int firedShots = elapsed < AimFrames ? 0 : (elapsed - AimFrames) / ShotIntervalFrames + 1;
            for (int i = 0; i < ShotCount; i++) {
                if (i < firedShots) {
                    continue;
                }
                float ang = ShotAngle(LockedAim, i, 0f);
                Vector2 pos = drawPos + ang.ToRotationVector2() * (26f + 20f * i + 8f * urgency);
                float ghostAlpha = (0.3f + 0.35f * urgency) * fade * pulse * (i == 1 ? 1f : 0.8f);
                //+PiOver2：原版标枪贴图朝上（aiStyle 1 默认旋转修正）
                Main.EntitySpriteDraw(ghostTex, pos, null,
                    GraniteMarbleVFX.MarbleCore * ghostAlpha, ang + MathHelper.PiOver2,
                    ghostOrigin, 0.9f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

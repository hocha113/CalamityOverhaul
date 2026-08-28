using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 近身招式姿态实体：苦工举盾肩撞 / 窃贼压低快扑 / 血鲨龇牙突进 / 小丑抬手掷弹的
    /// 前摇可见信号载体 + 突进窗镜像（受击端命中判窗读本实体，不读服务端私产计时器，
    /// 镜像 <see cref="NightPack.Projectiles.NightDiveOmen"/> 的判窗模式）。
    /// ai[0]=来源打包（whoAmI+1 | type&lt;&lt;8，施法者死亡或槽位复用即散） ai[1]=模式。
    /// 跟随锚定 NPC，永不造成伤害
    /// </summary>
    internal class LegionPoseOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModePeon = 0;
        internal const int ModeThief = 1;
        internal const int ModeShark = 2;
        internal const int ModeClown = 3;

        /// <summary>各模式前摇帧（近身体术姿态预告，公平底线 ≥24，档位一律不缩短）</summary>
        internal static readonly int[] WindupFrames = [26, 24, 30, 24];
        /// <summary>各模式突进/出手窗帧（NPC 相位机的执行段与此同长，命中特效按本窗判定）</summary>
        internal static readonly int[] StrikeFrames = [25, 19, 30, 14];

        private int Mode => Math.Clamp((int)Projectile.ai[1], 0, StrikeFrames.Length - 1);
        private int Total => WindupFrames[Mode] + StrikeFrames[Mode];
        private int Elapsed => Total - Projectile.timeLeft;
        /// <summary>是否处于突进/出手窗</summary>
        internal bool InStrike => Elapsed >= WindupFrames[Mode];
        /// <summary>前摇蓄势进度 0~1（苦工盾面微光按此渐亮）</summary>
        internal float WindupCharge => MathHelper.Clamp(Elapsed / (float)WindupFrames[Mode], 0f, 1f);

        /// <summary>找到某 NPC 某模式的在场姿态实体，无则 null（索引+类型+归属三重校验）</summary>
        internal static LegionPoseOmen FindFor(int npcIndex, int mode) {
            int type = ModContent.ProjectileType<LegionPoseOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == mode
                    && ((int)proj.ai[0] & 255) - 1 == npcIndex
                    && proj.ModProjectile is LegionPoseOmen omen) {
                    return omen;
                }
            }
            return null;
        }

        /// <summary>受击端判窗：该 NPC 该模式当前是否处于突进/出手窗</summary>
        internal static bool IsStrikeWindowFor(int npcIndex, int mode)
            => FindFor(npcIndex, mode) is { InStrike: true };

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>纯姿态载体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            //首帧按模式套定总时长，各端由同步的 ai[1] 确定性得到相同值
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Total;
            }

            //来源校验：施法者死亡或槽位被新怪复用即散（击杀施法者=有效反制）
            int packed = (int)Projectile.ai[0];
            int src = (packed & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != packed >> 8) {
                Projectile.Kill();
                return;
            }
            NPC anchor = Main.npc[src];
            Projectile.Center = anchor.Center;

            //出手瞬间的挥击风声（各端本地，锚在实体相位上）
            if (Elapsed == WindupFrames[Mode] && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.5f,
                    Pitch = Mode == ModeShark ? -0.4f : 0.2f,
                    MaxInstances = 5,
                }, Projectile.Center);
            }

            if (Main.dedServ || InStrike) {
                return;
            }
            //前摇可见信号（客户端尘，≤2 粒/帧），各模式姿态不同
            float charge = WindupCharge;
            switch (Mode) {
                case ModePeon:
                    //盾面铁星：微光渐亮由 LegionNPC.PostDraw 的实绘盾承担，这里补金属屑
                    if (Main.rand.NextBool(3)) {
                        Vector2 shieldPos = anchor.Center + new Vector2(anchor.direction * 15f, -3f);
                        Dust spark = Dust.NewDustPerfect(shieldPos, DustID.Iron,
                            new Vector2(anchor.direction * 0.6f, -Main.rand.NextFloat(0.4f, 1.2f)),
                            90, default, 0.7f + 0.4f * charge);
                        spark.noGravity = true;
                    }
                    break;
                case ModeThief:
                    //压低蓄势：足底烟尘 + 偶发金币贼光（提示偷窃意图）
                    if (Main.rand.NextBool(2)) {
                        Dust dust = Dust.NewDustPerfect(anchor.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), -2f),
                            DustID.Smoke, new Vector2(-anchor.direction * 0.8f, -0.3f), 140, default, 0.8f);
                        dust.noGravity = true;
                    }
                    if (Main.rand.NextBool(8)) {
                        Dust glint = Dust.NewDustPerfect(anchor.Center + new Vector2(anchor.direction * 8f, -6f),
                            DustID.GoldCoin, new Vector2(0f, -0.6f), 100, default, 0.8f);
                        glint.noGravity = true;
                    }
                    break;
                case ModeShark:
                    //龇牙：口部猩红炬 + 血沫，蓄满前逐渐密集
                    if (Main.rand.NextBool(2)) {
                        Vector2 mouth = anchor.Center + new Vector2(anchor.direction * 22f, -2f);
                        Dust rage = Dust.NewDustPerfect(mouth, DustID.CrimsonTorch,
                            new Vector2(anchor.direction * Main.rand.NextFloat(0.5f, 1.5f), -0.4f),
                            100, default, 0.9f + 0.6f * charge);
                        rage.noGravity = true;
                    }
                    if (Main.rand.NextBool(5)) {
                        Dust.NewDustDirect(anchor.position, anchor.width, anchor.height,
                            DustID.Blood, anchor.direction * 0.5f, 0.2f, 120, default, 0.9f);
                    }
                    break;
                case ModeClown:
                    //抬手引信预热：头顶火星打旋
                    if (Main.rand.NextBool(2)) {
                        float swirl = Main.GlobalTimeWrappedHourly * 9f + Projectile.identity;
                        Vector2 hand = anchor.Top + new Vector2(MathF.Cos(swirl) * 12f, -8f + MathF.Sin(swirl) * 4f);
                        Dust fuse = Dust.NewDustPerfect(hand, DustID.Torch,
                            new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)), 90, default, 0.8f + 0.5f * charge);
                        fuse.noGravity = true;
                    }
                    break;
            }
            Lighting.AddLight(anchor.Center, 0.14f * charge, 0.09f * charge, 0.05f * charge);
        }

        /// <summary>不自绘：姿态信号全在锚定 NPC 身上（尘 + 压速 + 苦工的实绘盾）</summary>
        public override bool PreDraw(ref Color lightColor) => false;
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// 军列冲锋 / BoneLee 二连段的预告与判定窗实体。
    /// ai[0]=来源打包（槽位+1|类型&lt;&lt;8） ai[1]=时间线打包（系|前摇&lt;&lt;3|冲锋&lt;&lt;9；BoneLee 系走固定时间表）
    /// ai[2]=锁定方向+10（BoneLee 回旋踢重锁一次时被权威端改写并同步）。
    /// 前摇期贴怪画方向标线+跺骨尘（骨响可听），冲锋期保留为淡出余痕兼攻击窗载体：
    /// 狱甲系点燃与蓝甲系横扫都由本实体的窗口判定，永不造成伤害。
    /// 来源死亡/槽位复用即消散（击杀冲锋者=有效反制）
    /// </summary>
    internal class DdChargeOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        //==== 系旗味 ====
        internal const byte FlavorAngry = 0;
        internal const byte FlavorBlue = 1;
        internal const byte FlavorRusty = 2;
        internal const byte FlavorHell = 3;
        internal const byte FlavorBoneLee = 4;

        //==== BoneLee 固定时间表（压身 24 → 突进拳 → 顿 8 → 回旋踢 → 长力竭由 NPC 侧承担） ====
        internal const int BLWindupFrames = 24;
        internal const int BLDash1Frames = 19;
        internal const int BLPauseFrames = 8;
        internal const int BLDash2Frames = 18;
        /// <summary>二连段收尾的长力竭（惩罚窗，NPC 侧使用）</summary>
        internal const int BLRecoverFrames = 30;

        /// <summary>蓝甲系收尾横扫窗：冲锋最后此帧数内命中触发小击退</summary>
        internal const int SweepWindowFrames = 10;
        private const int FadeFrames = 10;

        /// <summary>标线长度（冲锋 / BoneLee 短段）</summary>
        private const float LaneLenCharge = 240f;
        private const float LaneLenBoneLee = 160f;

        private static readonly Color[] FlavorColor = [
            new Color(214, 202, 170),//怒骨:骨白
            new Color(120, 150, 255),//蓝甲
            new Color(255, 170, 90), //锈甲
            new Color(255, 110, 60), //狱甲
            new Color(255, 228, 150),//BoneLee:武僧金
        ];

        /// <summary>时间线打包：系 3 位 | 前摇 6 位 | 冲锋帧 其余位</summary>
        internal static int PackTimeline(byte flavor, int windup, int strike)
            => flavor | (Math.Clamp(windup, 0, 63) << 3) | (Math.Clamp(strike, 0, 16383) << 9);

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private int Packed => (int)Projectile.ai[1];
        private int Flavor => Packed & 7;
        private bool IsBoneLee => Flavor == FlavorBoneLee;
        private int Windup => IsBoneLee ? BLWindupFrames : (Packed >> 3) & 63;
        private int Strike => IsBoneLee ? BLDash1Frames + BLPauseFrames + BLDash2Frames : Packed >> 9;
        private int TotalLife => Windup + Strike + FadeFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;

        /// <summary>攻击窗：冲锋期（BoneLee 为两段突进，顿帧不算窗）</summary>
        private bool InStrike {
            get {
                int t = Elapsed - Windup;
                if (t < 0) {
                    return false;
                }
                if (!IsBoneLee) {
                    return t < Strike;
                }
                return t < BLDash1Frames
                    || (t >= BLDash1Frames + BLPauseFrames && t < BLDash1Frames + BLPauseFrames + BLDash2Frames);
            }
        }

        /// <summary>蓝甲系横扫窗：冲锋收尾段</summary>
        private bool InSweep {
            get {
                if (IsBoneLee) {
                    return false;
                }
                int t = Elapsed - Windup;
                return t >= Strike - SweepWindowFrames && t < Strike;
            }
        }

        /// <summary>受害端判定：该怒骨当前是否处于冲锋攻击窗（狱甲点燃据此挂）</summary>
        internal static bool IsStrikeWindowFor(int npcIndex) {
            int type = ModContent.ProjectileType<DdChargeOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && ((int)proj.ai[0] & 255) == npcIndex + 1
                    && proj.ModProjectile is DdChargeOmen omen && omen.InStrike) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>受害端判定：该蓝甲当前是否处于收尾横扫窗</summary>
        internal static bool IsSweepWindowFor(int npcIndex) {
            int type = ModContent.ProjectileType<DdChargeOmen>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && ((int)proj.ai[0] & 255) == npcIndex + 1
                    && proj.ModProjectile is DdChargeOmen omen && omen.InSweep) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>军列并发计数：数活着的冲锋预告（boneLee=true 时数二连段），自愈无漂移</summary>
        internal static int CountActiveCharges(bool boneLee) {
            int type = ModContent.ProjectileType<DdChargeOmen>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (((int)proj.ai[1] & 7) == FlavorBoneLee) == boneLee) {
                    count++;
                }
            }
            return count;
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                Projectile.localAI[1] = TotalLife;
                if (!Main.dedServ) {
                    //跺骨骨响：前摇开始的可听信号
                    SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //来源校验：冲锋者死亡/槽位复用则预告随之消散（击杀=有效反制）
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != SourcePacked >> 8) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;
            Projectile.rotation = Projectile.ai[2] - 10f;

            int elapsed = Elapsed;
            if (elapsed < Windup) {
                //跺骨尘：脚下扬起骨屑（≤2 粒/帧）
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(anchor.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                        DustID.Bone, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.8f, 1.8f)),
                        90, default, 1.0f);
                    dust.noGravity = false;
                }
            }
            else if (InStrike) {
                if (Flavor == FlavorHell && !Main.dedServ && Main.rand.NextBool()) {
                    //狱甲系冲锋躯干带火尘（点燃窗的可见证据）
                    Dust fire = Dust.NewDustDirect(anchor.position, anchor.width, anchor.height,
                        DustID.Torch, anchor.velocity.X * 0.2f, -1.2f, 100, default, 1.3f);
                    fire.noGravity = true;
                }
            }

            if (elapsed == Windup && !Main.dedServ) {
                //起步爆发音
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
            }
            if (IsBoneLee && elapsed == Windup + BLDash1Frames + BLPauseFrames && !Main.dedServ) {
                //回旋踢重锁提示音
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            }

            Color warn = FlavorColor[Math.Clamp(Flavor, 0, FlavorColor.Length - 1)];
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.12f, warn.G / 255f * 0.12f, warn.B / 255f * 0.12f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            int windup = Windup;
            float strength;
            if (elapsed < windup) {
                float t = elapsed / (float)windup;
                //前摇渐亮，末 8 帧白热提示即将起步
                strength = 0.25f + 0.5f * t + (windup - elapsed <= 8 ? 0.25f : 0f);
            }
            else {
                //冲锋期余痕：可见窗与判定窗同一实体
                strength = MathHelper.Clamp(1f - (elapsed - windup) / (float)(Strike + FadeFrames), 0f, 1f) * 0.30f;
            }
            if (strength <= 0.02f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float laneLen = IsBoneLee ? LaneLenBoneLee : LaneLenCharge;
            float scaleX = laneLen / tex.Width;
            Color warn = FlavorColor[Math.Clamp(Flavor, 0, FlavorColor.Length - 1)] with { A = 0 };
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            //方向标线：细芯 + 宽柔光
            Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.55f * strength * pulse), Projectile.rotation,
                origin, new Vector2(scaleX, 20f / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.3f * strength), Projectile.rotation,
                origin, new Vector2(scaleX, 46f / tex.Height), SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}

using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Temple.Projectiles
{
    /// <summary>
    /// 飞蛇三连俯冲的标线预兆：ai[0]=来源NPC槽+1|类型&lt;&lt;8 ai[1]=段号0..2 ai[2]=锁定方向+10（0=未锁定）。
    /// 追踪期直读目标方向，锁定帧后冻结（预告即承诺），权威端在锁定帧写 ai[2] 做各端纠偏；
    /// 俯冲期保留为淡出余痕（可见窗=突进窗）。三段递进节奏走 ByStage 常量表：预告逐段加长、峰速逐段加快。
    /// 来源死亡或槽位被复用即消散（击杀施法者是有效反制），永不参与伤害
    /// </summary>
    internal class TempleDiveOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>各段预告帧数（≥30 契约、档位不缩短；第三段最快所以预告最长）</summary>
        internal static readonly int[] TelegraphByStage = [30, 34, 40];
        /// <summary>各段名义峰速（未含提速补偿，NPC 注入时除回 MoveGain）</summary>
        internal static readonly float[] DivePeakByStage = [10f, 11.5f, 13f];
        /// <summary>各段预告线长（略长于实际突进行程，宁可多警一分）</summary>
        internal static readonly float[] LaneLengthByStage = [320f, 360f, 420f];
        /// <summary>预告末段锁定冻结帧（追踪→锁定→冻结的后两拍）</summary>
        internal const int LockFreezeFrames = 12;
        /// <summary>俯冲包络三段：爬升/保持/衰减帧（NPC 侧塑形同用这组，余痕窗与总和一致）</summary>
        internal const int DiveRise = 6;
        internal const int DiveHold = 20;
        internal const int DiveDecay = 10;
        /// <summary>俯冲窗总帧数（余痕可见窗=突进判定窗）</summary>
        internal const int DiveWindowFrames = DiveRise + DiveHold + DiveDecay;

        /// <summary>线芯宽与柔光宽：画宽于蛇体，把突进期原版残余转向也包进警示带</summary>
        private const float LaneCoreWidth = 22f;
        private const float LaneGlowWidth = 56f;

        /// <summary>各段警示色：金→琥珀→炽红，段位递进一眼可读（加色层，A=0）</summary>
        private static readonly Color[] WarnByStage = [
            new Color(255, 214, 96, 0),
            new Color(255, 178, 72, 0),
            new Color(255, 122, 58, 0),
        ];

        private int SrcPacked => (int)Projectile.ai[0];
        private int Stage => Math.Clamp((int)Projectile.ai[1], 0, 2);
        private int TelegraphFrames => TelegraphByStage[Stage];
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool Locked => Elapsed >= TelegraphFrames - LockFreezeFrames;
        private bool InStrike => Elapsed >= TelegraphFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 640;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphByStage[2] + DiveWindowFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧按段号套定总时长，各端由随生成包同步的 ai[1] 确定性得到相同值
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TelegraphFrames + DiveWindowFrames;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入玩家：首帧 ai[2] 已非零=权威端早过锁定帧，本地相位快进到锁定起点，不重放追踪期
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = DiveWindowFrames + LockFreezeFrames;
                }
            }

            //来源校验：施法者死亡或槽位被新怪复用即消散（类型比对防复用欺骗）
            int src = (SrcPacked & 255) - 1;
            if (src < 0 || src >= Main.maxNPCs || !Main.npc[src].active
                || Main.npc[src].type != SrcPacked >> 8) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = Main.npc[src].Center;

            if (Projectile.ai[2] != 0f) {
                //权威端已写入的锁定方向（各端一致的承诺值）
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //追踪期：直读目标方向（各端从同步数据确定性推得）
                int target = Main.npc[src].target;
                if (target >= 0 && target < Main.maxPlayers && Main.player[target].Alives()) {
                    Projectile.rotation = (Main.player[target].Center - Projectile.Center).ToRotation();
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            //方向写进 velocity：不推位移（ShouldUpdatePosition=false），只为同步快照多带一份朝向证据
            Projectile.velocity = Projectile.rotation.ToRotationVector2();

            if (Elapsed == TelegraphFrames - LockFreezeFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
            }
            if (Elapsed == TelegraphFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.2f + 0.15f * Stage, MaxInstances = 4 }, Projectile.Center);
            }

            //凝势金焰尘（≤2/帧，纯表现允许各端散度）
            if (!InStrike && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(12f, 40f),
                    DustID.GoldFlame, dir * Main.rand.NextFloat(0.5f, 1.6f), 130, default, 0.9f);
                dust.noGravity = true;
            }

            Color warn = WarnByStage[Stage];
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.18f, warn.G / 255f * 0.18f, warn.B / 255f * 0.18f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InStrike) {
                //突进余痕：可见窗与突进窗同一实体
                strength = MathHelper.Clamp(1f - (Elapsed - TelegraphFrames) / (float)DiveWindowFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.55f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = LaneLengthByStage[Stage] / tex.Width;
            Color warn = WarnByStage[Stage];
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            if (!Locked || InStrike) {
                //追踪期/余痕期：细芯+宽柔光
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.3f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪宣告轨迹已承诺
                float lockT = MathHelper.Clamp((Elapsed - (TelegraphFrames - LockFreezeFrames)) / (float)LockFreezeFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color core = new Color(255, 244, 224, 0) * (0.85f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.65f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 18f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 8f) / tex.Height), SpriteEffects.None, 0);
            }

            //段位记号：第 N 段点亮 N+1 颗小珠，玩家可读「三连中的第几冲」
            Texture2D dot = CWRAsset.SoftGlow.Value;
            Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            for (int i = 0; i <= Stage; i++) {
                Vector2 p = drawPos + side * (16f + 12f * i);
                Main.EntitySpriteDraw(dot, p, null, warn * (0.5f * strength * pulse), 0f,
                    dot.Size() / 2f, 0.05f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI)
            => behindNPCsAndTiles.Add(index);
    }
}

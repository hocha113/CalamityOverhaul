using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Graveyard.Projectiles
{
    /// <summary>
    /// 鬼魂"点名"幽光丝线：ai[0]=(来源槽+1)|(来源类型&lt;&lt;8) ai[1]=档位 ai[2]=锁定方向+10（0=未锁定）。
    /// 点名期丝线由鬼魂指向目标并逐帧渐亮、哀鸣分级渐响；锁定帧起方向冻结（锁定即承诺）；
    /// 幽冲期保留为淡出余痕（余痕可见窗=幽冲窗）；力竭期不再绘线，只向宿主盖疲劳戳驱动半透明。
    /// 来源死亡/槽位复用即消散（击杀施法者是有效反制），本体永不参与伤害
    /// </summary>
    internal class GyGhostNameThread : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>点名帧数（任务口径 ≥40，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 44;
        /// <summary>末段锁定帧：方向冻结并白热闪烁</summary>
        internal const int LockFrames = 14;
        /// <summary>幽冲窗帧数（与 NPC 侧包络 rise+hold+decay 严格对齐）</summary>
        internal const int StrikeFrames = 26;
        /// <summary>力竭帧数（惩罚窗，期间鬼魂被压速且半透明）</summary>
        internal const int FatigueFrames = 30;

        /// <summary>丝线芯宽与柔光宽：画宽于鬼体判定，把幽冲期原版残余漂移包进警示范围</summary>
        private const float ThreadCoreWidth = 6f;
        private const float ThreadGlowWidth = 20f;
        /// <summary>丝线表现长度上下限（纯表现量，判定不读它）</summary>
        private const float ThreadMinLen = 90f;
        private const float ThreadMaxLen = 520f;

        //豁免声明：幽光属光——丝线为纯加色发光体（A=0），按 M5 光类豁免不带遮挡外壳
        private static readonly Color ThreadGlow = new Color(150, 216, 255, 0);
        private static readonly Color ThreadCore = new Color(236, 248, 255, 0);

        private int SrcPacked => (int)Projectile.ai[0];
        private int SrcIndex => (SrcPacked & 255) - 1;
        private int SrcType => SrcPacked >> 8;
        private int TotalLife => TelegraphFrames + StrikeFrames + FatigueFrames;
        private int Elapsed => (int)Projectile.localAI[0] - Projectile.timeLeft;
        private bool Locked => Elapsed >= TelegraphFrames - LockFrames;
        private bool InStrike => Elapsed >= TelegraphFrames && Elapsed < TelegraphFrames + StrikeFrames;
        private bool InFatigue => Elapsed >= TelegraphFrames + StrikeFrames;

        /// <summary>丝线表现长度（各端本地缓存，锁定后冻结）</summary>
        private float threadLen = 220f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 640;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 100;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = TotalLife;
                Projectile.timeLeft = TotalLife;
                //迟入玩家：首帧 ai[2] 已非零=服务端早过锁定帧，本地相位快进到锁定起点
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = LockFrames + StrikeFrames + FatigueFrames;
                }
            }

            //来源校验：索引+类型双检，施法者死亡或槽位复用即消散（点名承诺随之作废）
            if (SrcIndex < 0 || SrcIndex >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC anchor = Main.npc[SrcIndex];
            if (!anchor.active || anchor.type != SrcType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                //服务端已写入权威锁定方向
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //点名追踪期：直读目标方向（各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
                        threadLen = MathHelper.Clamp(Vector2.Distance(player.Center, Projectile.Center),
                            ThreadMinLen, ThreadMaxLen);
                    }
                }
            }

            int elapsed = Elapsed;
            //哀鸣分级渐响：起手轻吟→中段转清→锁定高鸣→幽冲嘶声（配合渐亮丝线构成点名仪式）
            if (!VaultUtils.isServer) {
                if (elapsed == 2) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.2f, Pitch = -0.62f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames / 2) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.34f, Pitch = -0.38f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames - LockFrames) {
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 4 }, Projectile.Center);
                }
                else if (elapsed == TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            //点名期凝魂尘：沿丝线向鬼魂聚拢（≤1 粒/2 帧）
            if (elapsed < TelegraphFrames && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(20f, threadLen * 0.8f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Smoke, -dir * Main.rand.NextFloat(0.6f, 1.6f), 170, default, 0.8f);
                dust.noGravity = true;
            }

            //力竭期：向宿主盖疲劳戳（所有端各自执行，宿主 PostAI 读戳抬半透明；镜像 EliteMove Stamp 模式）
            if (InFatigue && anchor.TryGetGlobalNPC(out GraveyardBrutalNPC pack)) {
                pack.StampGhostFatigue();
            }

            Lighting.AddLight(Projectile.Center, ThreadGlow.R / 255f * 0.14f,
                ThreadGlow.G / 255f * 0.14f, ThreadGlow.B / 255f * 0.14f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            if (InFatigue) {
                return false;//力竭期无线可画，疲劳表现走宿主半透明
            }

            float strength;
            if (InStrike) {
                //幽冲余痕：可见窗与幽冲窗同一实体
                strength = MathHelper.Clamp(1f - (elapsed - TelegraphFrames) / (float)StrikeFrames, 0f, 1f) * 0.24f;
            }
            else {
                //渐亮：点名进度即亮度，读秒可视
                strength = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f) * (Locked ? 1f : 0.7f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = threadLen / tex.Width;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);

            if (!Locked || InStrike) {
                Main.EntitySpriteDraw(tex, drawPos, null, ThreadGlow * (0.42f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, ThreadGlowWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, ThreadCore * (0.6f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, ThreadCoreWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告丝线已成承诺
                float lockT = MathHelper.Clamp((elapsed - (TelegraphFrames - LockFrames)) / (float)LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Main.EntitySpriteDraw(tex, drawPos, null, ThreadGlow * (0.75f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (ThreadGlowWidth + 10f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, ThreadCore * (0.9f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (ThreadCoreWidth - 2f) / tex.Height), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}

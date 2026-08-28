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
    /// 诅咒颅咒锁俯冲预告体：绕旋 30 帧期间沿轨迹留咒光点，锁向后亮标线（方向冻结，不追踪）。
    /// ai[0]=来源打包（槽位+1|类型&lt;&lt;8） ai[1]=变体（0 小颅 / 1 大颅） ai[2]=锁定方向+10（0=绕旋中）。
    /// 大颅在锁向期额外画四向十字幽灵（冲刺尾帧的四向咒火预告，固定向=非追踪保证）。
    /// 颅骨死亡/槽位复用即消散（击杀=有效反制），永不造成伤害
    /// </summary>
    internal class DdSkullOmen : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>绕旋预告帧（≥30 契约的近战体术前摇，轨迹咒光点即可见信号）</summary>
        internal const int OrbitFrames = 30;
        /// <summary>俯冲窗帧（NPC 侧包络冲刺的时长）</summary>
        internal const int StrikeFrames = 26;
        private const int FadeFrames = 10;

        /// <summary>标线长度（小颅/大颅）</summary>
        private const float LaneLenSmall = 300f;
        private const float LaneLenGiant = 340f;
        /// <summary>轨迹咒光点采样间隔与容量</summary>
        private const int TrailSampleGap = 3;
        private const int TrailCapacity = 8;

        private static readonly Color CurseGlow = new Color(150, 120, 255, 0);
        private static readonly Color CurseDark = new Color(28, 18, 46, 215);

        private int SourcePacked => (int)Projectile.ai[0];
        private int AnchorIndex => (SourcePacked & 255) - 1;
        private bool Giant => Projectile.ai[1] == 1f;
        private bool Locked => Projectile.ai[2] != 0f;
        private float LockDir => Projectile.ai[2] - 10f;
        private int TotalLife => OrbitFrames + StrikeFrames + FadeFrames;
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;

        /// <summary>轨迹咒光点缓存（本端绘制用，非同步状态）</summary>
        private readonly Vector2[] trail = new Vector2[TrailCapacity];
        private int trailHead;
        private bool lockChimed;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OrbitFrames + StrikeFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = Projectile.timeLeft;
                //迟入玩家：首帧已锁向=权威端早过绕旋段，本地相位快进到锁定起点
                if (Locked) {
                    Projectile.timeLeft = StrikeFrames + FadeFrames;
                    Projectile.localAI[1] = TotalLife;
                }
                for (int i = 0; i < trail.Length; i++) {
                    trail[i] = Projectile.Center;
                }
            }

            //来源校验：颅骨没了俯冲不会来（或已中断），预告随之消散
            NPC anchor = AnchorIndex.TryGetNPC(out NPC a) ? a : null;
            if (!anchor.Alives() || anchor.type != SourcePacked >> 8) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;
            if (Locked) {
                Projectile.rotation = LockDir;
                if (!lockChimed) {
                    lockChimed = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 5 }, Projectile.Center);
                    }
                }
            }

            //绕旋轨迹采样：咒光点跟着颅骨走过的弧线
            if (Elapsed % TrailSampleGap == 0) {
                trail[trailHead] = anchor.Center;
                trailHead = (trailHead + 1) % trail.Length;
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(anchor.Center + Main.rand.NextVector2Circular(anchor.width * 0.5f, anchor.height * 0.5f),
                    DustID.Shadowflame, -anchor.velocity * 0.1f, 130, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.18f, 0.12f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float strength;
            if (elapsed >= OrbitFrames) {
                //冲刺余痕：可见窗随冲刺衰减
                strength = MathHelper.Clamp(1f - (elapsed - OrbitFrames) / (float)(StrikeFrames + FadeFrames), 0f, 1f) * 0.4f;
            }
            else {
                strength = MathHelper.Clamp(elapsed / 8f, 0f, 1f) * (Locked ? 1f : 0.6f);
            }
            if (strength <= 0.02f) {
                return false;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D dark = CWRAsset.Extra_98.Value;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);

            //轨迹咒光点：暗纹垫底 + 加色咒光（越旧越淡）
            for (int i = 0; i < trail.Length; i++) {
                int age = (trailHead - 1 - i + trail.Length) % trail.Length;
                float t = 1f - age / (float)trail.Length;
                Vector2 dotPos = trail[i] - Main.screenPosition;
                Main.EntitySpriteDraw(dark, dotPos, null, CurseDark * (0.65f * t * strength), 0f,
                    dark.Size() / 2f, 0.10f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, dotPos, null, CurseGlow * (0.5f * t * strength * pulse), 0f,
                    glow.Size() / 2f, 0.05f, SpriteEffects.None, 0);
            }

            if (Locked) {
                //锁向标线：白热窄闪宣告轨迹已承诺（此后不追踪）
                Texture2D lane = TextureAssets.Projectile[Type].Value;
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Vector2 origin = new Vector2(0f, lane.Height / 2f);
                float laneLen = Giant ? LaneLenGiant : LaneLenSmall;
                float scaleX = laneLen / lane.Width;
                Main.EntitySpriteDraw(lane, drawPos, null, CurseGlow * (0.6f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, 22f / lane.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(lane, drawPos, null, new Color(240, 230, 255, 0) * (0.4f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, 10f / lane.Height), SpriteEffects.None, 0);

                if (Giant && elapsed < OrbitFrames + StrikeFrames) {
                    //大颅四向十字幽灵：冲刺尾帧要放的四向咒火，固定上下左右（非追踪保证）
                    int donor = ProjectileID.CursedFlameHostile;
                    Main.instance.LoadProjectile(donor);
                    Texture2D ghost = TextureAssets.Projectile[donor].Value;
                    int donorFrames = Math.Max(1, Main.projFrames[donor]);
                    Rectangle frameRect = new(0, 0, ghost.Width, ghost.Height / donorFrames);
                    for (int k = 0; k < 4; k++) {
                        float ang = MathHelper.PiOver2 * k;
                        Vector2 ghostPos = drawPos + ang.ToRotationVector2() * 30f;
                        Main.EntitySpriteDraw(ghost, ghostPos, frameRect,
                            new Color(170, 255, 80, 60) * (0.4f * strength * pulse), ang,
                            frameRect.Size() / 2f, 0.8f, SpriteEffects.None, 0);
                    }
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}

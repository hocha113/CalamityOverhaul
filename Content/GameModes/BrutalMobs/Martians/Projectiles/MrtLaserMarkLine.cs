using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>标线打击参数（每风味一行；NPC 决策端与标线实体读同一份，单一事实源）</summary>
    internal readonly struct MrtStrikeProfile(int telegraphFrames, int lockFrames, float minRange, float maxRange,
        float boltSpeed, int shots, float damageFrac, float lineLength, int cd1, int cd2, int cd3)
    {
        /// <summary>预告总帧（公平契约 ≥30，各档位一律不缩短）</summary>
        public readonly int TelegraphFrames = telegraphFrames;
        /// <summary>末段锁定帧：进入后方向冻结（预告即承诺）</summary>
        public readonly int LockFrames = lockFrames;
        public readonly float MinRange = minRange;
        public readonly float MaxRange = maxRange;
        public readonly float BoltSpeed = boltSpeed;
        /// <summary>沿同一锁定方向的发数（&gt;1 时后续弹不重瞄）</summary>
        public readonly int Shots = shots;
        /// <summary>弹体伤害 = npc.damage（已缩放值）× 此比例</summary>
        public readonly float DamageFrac = damageFrac;
        public readonly float LineLength = lineLength;
        private readonly int cd1 = cd1, cd2 = cd2, cd3 = cd3;
        /// <summary>攻击冷却（档位只缩冷却，不碰预告时长）</summary>
        public int Cooldown(int tier) => tier >= 3 ? cd3 : tier == 2 ? cd2 : cd1;
    }

    /// <summary>
    /// 火星激光标线（直线科技标线）：ai[0]=锚 NPC 索引，ai[1]=锚 NPC 类型*10+风味（索引+类型双校验），
    /// ai[2]=锁定方向+10（0=未锁定，权威端在锁定帧写入作各端纠偏）。
    /// 追踪期直读目标方向 → 锁定期方向冻结白闪（预告即承诺）→ 开火后保留余痕。
    /// 弹体沿标线当前所示方向出膛，线即承诺；本实体永不造成伤害
    /// </summary>
    internal class MrtLaserMarkLine : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        /// <summary>开火后余痕帧（覆盖多连发窗口）</summary>
        internal const int RemnantFrames = 20;
        /// <summary>多连发的次发间隔帧（NPC 侧收势计时读取；须 &lt; RemnantFrames）</summary>
        internal const int SecondShotGapFrames = 8;

        /// <summary>风味索引：0 无人机 / 1 行走者 / 2 射线枪手 / 3 脑波扰乱者 / 4 军官 / 5 Scutlix 骑手</summary>
        internal static readonly MrtStrikeProfile[] Profiles = [
            new(34, 12,  80f, 520f, 14f, 1, 0.55f, 640f, 420, 360, 300),
            new(40, 14, 140f, 700f, 12f, 2, 0.50f, 820f, 520, 440, 370),
            new(36, 12, 180f, 780f, 17f, 1, 0.60f, 900f, 480, 410, 340),
            new(38, 14, 160f, 700f, 12f, 1, 0.50f, 820f, 540, 460, 380),
            new(44, 16, 160f, 700f, 11f, 1, 0.65f, 820f, 600, 510, 430),
            new(46, 16, 140f, 650f,  9f, 1, 0.75f, 780f, 560, 480, 400),
        ];

        /// <summary>风味警示色（A=0 加色标线；弹体不透明配色在弹体类里）</summary>
        internal static readonly Color[] FlavorColors = [
            new(110, 225, 255, 0),
            new(255, 120, 205, 0),
            new(130, 255, 150, 0),
            new(200, 130, 255, 0),
            new(255, 205, 110, 0),
            new(255, 90, 90, 0),
        ];

        /// <summary>标线芯宽（宽于弹体判定宽 14px，警示范围不缩水）</summary>
        private const float LaneCoreWidth = 18f;
        private const float LaneGlowWidth = 46f;

        private int AnchorIndex => (int)Projectile.ai[0];
        private int AnchorType => (int)Projectile.ai[1] / 10;
        private int Flavor => (int)Projectile.ai[1] % 10;
        private MrtStrikeProfile Profile => Profiles[Flavor];
        private int Elapsed => (int)Projectile.localAI[1] - Projectile.timeLeft;
        private bool Locked => Elapsed >= Profile.TelegraphFrames - Profile.LockFrames;
        private bool InRemnant => Elapsed >= Profile.TelegraphFrames;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 960;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                int total = Profile.TelegraphFrames + RemnantFrames;
                Projectile.timeLeft = total;
                Projectile.localAI[1] = total;
                //迟入端：首帧 ai[2] 已非零 = 权威端早过锁定帧的同步证据，本地相位快进到锁定段起点，
                //不重放整段追踪期（方向本就走 ai[2] 权威分支，此处只对齐判定窗相位）
                if (Projectile.ai[2] != 0f) {
                    Projectile.timeLeft = Profile.LockFrames + RemnantFrames;
                }
            }

            //锚定怪索引+类型双校验：怪没了/换人了 → 打击不会发生，标线随之消散
            NPC anchor = AnchorIndex >= 0 && AnchorIndex < Main.maxNPCs ? Main.npc[AnchorIndex] : null;
            if (anchor == null || !anchor.active || anchor.type != AnchorType) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = anchor.Center;

            if (Projectile.ai[2] != 0f) {
                //权威端已写入锁定方向
                Projectile.rotation = Projectile.ai[2] - 10f;
            }
            else if (!Locked) {
                //追踪期：直读目标方向（各端从同步数据确定性推得）
                int target = anchor.target;
                if (target >= 0 && target < Main.maxPlayers) {
                    Player player = Main.player[target];
                    if (player.Alives()) {
                        Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
                    }
                }
            }
            //锁定后 rotation 冻结在最后追踪值，等待/无需 ai[2] 纠偏

            if (Elapsed == Profile.TelegraphFrames - Profile.LockFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
            }

            Projectile.velocity = Projectile.rotation.ToRotationVector2();
            Color warn = FlavorColors[Flavor];
            Lighting.AddLight(Projectile.Center, warn.R / 255f * 0.14f, warn.G / 255f * 0.14f, warn.B / 255f * 0.14f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fadeIn = MathHelper.Clamp(Elapsed / 8f, 0f, 1f);
            float strength;
            if (InRemnant) {
                //余痕期：淡出残线，标注弹体正沿此线飞行
                strength = MathHelper.Clamp(1f - (Elapsed - Profile.TelegraphFrames) / (float)RemnantFrames, 0f, 1f) * 0.22f;
            }
            else {
                strength = fadeIn * (Locked ? 1f : 0.5f);
            }
            if (strength <= 0.01f) {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            //NPC 锚定绘制补 gfxOffY（上坡步进补偿）
            NPC anchor = AnchorIndex >= 0 && AnchorIndex < Main.maxNPCs ? Main.npc[AnchorIndex] : null;
            float gfxOff = anchor != null && anchor.active ? anchor.gfxOffY : 0f;
            Vector2 drawPos = Projectile.Center + new Vector2(0f, gfxOff) - Main.screenPosition;
            Vector2 origin = new Vector2(0f, tex.Height / 2f);
            float scaleX = Profile.LineLength / tex.Width;
            Color warn = FlavorColors[Flavor];
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            if (!Locked || InRemnant) {
                //追踪期/余痕期：细芯 + 宽柔光（直线科技标线）
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.5f * strength * pulse), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneCoreWidth / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.28f * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, LaneGlowWidth / tex.Height), SpriteEffects.None, 0);
            }
            else {
                //锁定期：白热窄闪，宣告方向已承诺
                float lockT = MathHelper.Clamp((Elapsed - (Profile.TelegraphFrames - Profile.LockFrames)) / (float)Profile.LockFrames, 0f, 1f);
                float flash = 0.7f + 0.3f * MathF.Sin(lockT * MathHelper.Pi * 5f);
                Color core = new Color(255, 250, 235, 0) * (0.8f * flash * strength);
                Main.EntitySpriteDraw(tex, drawPos, null, warn * (0.6f * flash * strength), Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneGlowWidth + 16f) / tex.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation,
                    origin, new Vector2(scaleX, (LaneCoreWidth - 6f) / tex.Height), SpriteEffects.None, 0);
            }

            //枪口节点：Scutlix 骑手风味外加蓄能球（充能进度=预告进度，视觉即计时）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float nodeScale = 0.28f;
            if (Flavor == 5 && !InRemnant) {
                float charge = MathHelper.Clamp(Elapsed / (float)Profile.TelegraphFrames, 0f, 1f);
                nodeScale = 0.3f + 0.55f * charge;
                Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 255, 255, 0) * (0.5f * charge * strength),
                    0f, glow.Size() / 2f, nodeScale * 0.4f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(glow, drawPos, null, warn * (0.9f * strength * pulse),
                0f, glow.Size() / 2f, nodeScale, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCsAndTiles.Add(index);
    }
}

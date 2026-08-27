using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 灰皮步兵掷扳手：ai[0]=落点X ai[1]=落点Y ai[2]=步兵索引（类型按 GrayGrunt 复验）。
    /// 预告期悬浮在步兵头顶自旋加速（抬臂前摇可见）并在落点亮标记（落点自生成帧锁死，预告即承诺）；
    /// 掷出帧从当前位置向锁定落点做抛物线解算，各端确定性同解、权威端补一次同步纠偏。
    /// 伤害窗=飞行期，与可见状态同门控。
    /// 贴图用原版 SaucerScrap（灰蓝金属废件，tModLoader.xml 证实存在）+ 灰蓝染色
    /// </summary>
    internal class MrtWrenchProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SaucerScrap;

        /// <summary>抬臂前摇帧（任务口径 ≥24，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 26;
        /// <summary>掷出后到锁定落点的飞行帧</summary>
        private const int FlightFrames = 46;
        /// <summary>落点未及（悬崖豁口等）时继续下坠的宽限帧</summary>
        private const int FallBufferFrames = 90;
        /// <summary>自施重力与坠速上限，与弹道解算使用同一常数</summary>
        private const float Gravity = 0.3f;
        private const float MaxFall = 16f;
        /// <summary>落点标记尺寸</summary>
        private const float MarkerWidth = 84f;
        private const float MarkerHeight = 28f;

        private static readonly Color SteelBlue = new(150, 175, 205);
        private static readonly Color RimDark = new(40, 48, 62);
        private static readonly Color MarkerCore = new(140, 215, 255, 0);

        private Vector2 LockPoint => new Vector2(Projectile.ai[0], Projectile.ai[1]);
        private int AnchorIndex => (int)Projectile.ai[2];

        private ref float Age => ref Projectile.localAI[0];
        private bool Flying => Age > TelegraphFrames;
        private int SpinDir => Projectile.identity % 2 == 0 ? 1 : -1;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + FlightFrames + FallBufferFrames;
            Projectile.netImportant = true;
        }

        /// <summary>预告期锚定悬浮，掷出后才交给引擎位移</summary>
        public override bool ShouldUpdatePosition() => Flying;

        /// <summary>伤害窗=飞行期（公平阀：预告期绝无判定）</summary>
        public override bool? CanDamage() => Flying ? null : false;

        public override void AI() {
            Age++;

            //迟入玩家/权威掷出包先到：预告期速度只可能为零，非零即已掷出的同步证据，本地快进到飞行期
            if (Age <= TelegraphFrames && Projectile.velocity.LengthSquared() > 1f) {
                Age = TelegraphFrames + 1;
            }

            if (Age <= TelegraphFrames) {
                Projectile.hostile = false;
                Projectile.tileCollide = false;

                //步兵索引+类型双校验：掷者已倒则这次投掷不会发生（击杀施法者是有效反制）
                NPC anchor = AnchorIndex >= 0 && AnchorIndex < Main.maxNPCs ? Main.npc[AnchorIndex] : null;
                if (anchor == null || !anchor.active || anchor.type != NPCID.GrayGrunt) {
                    Projectile.Kill();
                    return;
                }
                //悬浮头顶：抬臂举械的可见前摇
                Projectile.Center = anchor.Top + new Vector2(
                    anchor.direction * 6f + MathF.Sin(Age * 0.15f + Projectile.identity) * 3f, -16f);

                //自旋加速：蓄力可读
                float windup = Age / (float)TelegraphFrames;
                Projectile.rotation += (0.06f + 0.3f * windup * windup) * SpinDir;

                if ((int)Age == TelegraphFrames) {
                    //掷出：从当前位置向锁定落点抛物线解算（不重瞄）
                    Vector2 to = LockPoint - Projectile.Center;
                    float t = FlightFrames;
                    Projectile.velocity = new Vector2(
                        MathHelper.Clamp(to.X / t, -13f, 13f),
                        MathHelper.Clamp(to.Y / t - Gravity * t * 0.5f, -17f, 8f));
                    //权威端纠偏；客户端置位会被所有权门拦下，无副作用
                    Projectile.netUpdate = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.15f }, Projectile.Center);
                    }
                }
                return;
            }

            //飞行期：与可见状态同门控的判定窗，旋转扳手抛物线坠落
            Projectile.hostile = true;
            Projectile.tileCollide = true;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFall);
            Projectile.rotation += 0.38f * SpinDir;
            Lighting.AddLight(Projectile.Center, SteelBlue.ToVector3() * 0.12f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = 0.15f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MartianSaucerSpark,
                    Main.rand.NextVector2Circular(2.6f, 2.6f) - Vector2.UnitY * 1.1f, 0, default,
                    Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //灰蓝染色：本体保留原版贴图遮挡像素
            Color body = Color.Lerp(lightColor, SteelBlue, 0.5f);

            if (!Flying) {
                float windup = MathHelper.Clamp(Age / TelegraphFrames, 0f, 1f);
                float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);

                //落点标记：暗色实底外圈 + 加色芯，随蓄力增强
                Texture2D rim = CWRAsset.Extra_98.Value;
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                Vector2 markerPos = LockPoint - Main.screenPosition;
                float markerT = windup * (0.5f + 0.5f * pulse);
                Main.EntitySpriteDraw(rim, markerPos, null, RimDark * (0.75f * markerT), 0f, rim.Size() / 2f,
                    new Vector2(MarkerWidth / rim.Width, MarkerHeight / rim.Height) * 1.15f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glowTex, markerPos, null, MarkerCore * markerT, 0f, glowTex.Size() / 2f,
                    new Vector2(MarkerWidth / glowTex.Width, MarkerHeight / glowTex.Height), SpriteEffects.None, 0);

                //悬浮扳手：冷光衬底 + 本体渐显
                Main.EntitySpriteDraw(tex, drawPos, null, MarkerCore * (0.3f * windup * pulse),
                    Projectile.rotation, orig, Projectile.scale * 1.2f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, body * (0.45f + 0.55f * windup),
                    Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
                return false;
            }

            //飞行期拖尾：同材质降比重画，只画掷出之后的位置
            int flightAge = (int)Age - TelegraphFrames;
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (i >= flightAge || Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null, body * (0.4f * t),
                    Projectile.oldRot[i], orig, Projectile.scale * 0.78f, SpriteEffects.None, 0);
            }

            //冷光衬底 + 本体
            Main.EntitySpriteDraw(tex, drawPos - Projectile.velocity * 0.5f, null, MarkerCore * 0.3f,
                Projectile.rotation - 0.2f * SpinDir, orig, Projectile.scale * 1.12f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, body,
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

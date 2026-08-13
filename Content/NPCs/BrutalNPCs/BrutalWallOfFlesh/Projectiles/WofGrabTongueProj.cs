using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 抓取舌：舌卷回吞投技的舌体。ai[0]=墙whoAmI ai[1]=出生即缠住(绕后路径)；
    /// spawn时velocity=单位方向(锁线数据槽，不积分位移)。
    /// 甩出段有小接触伤害；缠住后伤害归零(伤害节拍由状态编排)，
    /// 舌尖全程跟随受害者(各端读同步位置本地绘制)，状态结束或落空则回吞消失
    /// </summary>
    internal class WofGrabTongueProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>回吞速度 px/f</summary>
        private const float SnapBackSpeed = 55f;

        private NPC Wall => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private bool SpawnAttached => Projectile.ai[1] > 0.5f;

        /// <summary>锁定方向常驻velocity槽</summary>
        private Vector2 LashDir => Projectile.velocity.SafeNormalize(Vector2.UnitX);

        /// <summary>当前舌长(各端本地推进)</summary>
        private float reach;
        /// <summary>正在回吞(落空/吐出/断投)</summary>
        private bool snappingBack;
        /// <summary>回吞时保持的方向(缠住期舌尖跟人，回吞瞬间定格)</summary>
        private Vector2 snapDir;

        //方向存velocity只作数据槽，不做位移积分
        public override bool ShouldUpdatePosition() => false;

        //链体自口器铺到舌尖，给足绘制余量防边缘裁切
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>找到属于指定墙的活跃抓取舌</summary>
        internal static Projectile FindForWall(int wallWhoAmI) {
            int type = ModContent.ProjectileType<WofGrabTongueProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == type && (int)p.ai[0] == wallWhoAmI) {
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            NPC wall = Wall;
            if (!wall.Alives()) {
                Projectile.Kill();
                return;
            }

            bool grabActive = WofTongueGrabState.TryGetActiveGrab(out _, out WofTongueGrabState state);
            int victim = grabActive ? WofTongueGrabState.VictimIndex(wall) : -1;
            Vector2 mouth = wall.Center;

            if (snappingBack) {
                UpdateSnapBack();
                return;
            }

            //状态提前结束(墙死亡/断投等) → 回吞
            if (!grabActive) {
                BeginSnapBack();
                return;
            }

            int timer = state.GrabTimer;

            if (victim >= 0 && victim < Main.maxPlayers && Main.player[victim].Alives()
                && timer < WofTongueGrabState.SpitTick) {
                //缠住：舌尖锁定受害者(各端读本地视角的同步位置)，伤害归零
                Projectile.damage = 0;
                Vector2 toVictim = Main.player[victim].Center - mouth;
                reach = toVictim.Length();
                Projectile.Center = Main.player[victim].Center;
                Projectile.rotation = toVictim.SafeNormalize(Vector2.UnitX).ToRotation();
                Projectile.timeLeft = 120;

                //缠体渗涎
                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                        Main.rand.NextVector2Circular(1.5f, 1.5f) + Vector2.UnitY * 1.5f,
                        WofMotionFX.BloodMid, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(12, 22), 0.3f);
                }
            }
            else if (victim < 0 && timer >= WofTongueGrabState.LashStartTick && timer < WofTongueGrabState.ReelStartTick) {
                //甩出段：沿锁定线暴射，此段保留接触伤害
                reach = Math.Min((timer - WofTongueGrabState.LashStartTick) * Core.WofDirector.GrabExtendSpeed,
                    Core.WofDirector.GrabMaxReach);
                Projectile.Center = mouth + LashDir * reach;
                Projectile.rotation = LashDir.ToRotation();
                Projectile.timeLeft = 120;

                //舌尖甩涎
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                        LashDir.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 5f),
                        WofMotionFX.BloodMid, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(14, 26), 0.32f);
                }
            }
            else {
                //落空窗口结束或已吐出 → 回吞
                BeginSnapBack();
                return;
            }

            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center, WofMotionFX.BloodHot.ToVector3() * 0.45f);
            }
        }

        /// <summary>进入回吞：方向定格，伤害归零</summary>
        private void BeginSnapBack() {
            snappingBack = true;
            Projectile.damage = 0;
            NPC wall = Wall;
            Vector2 mouth = wall.Alives() ? wall.Center : Projectile.Center;
            snapDir = (Projectile.Center - mouth).SafeNormalize(LashDir);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
            }
        }

        private void UpdateSnapBack() {
            NPC wall = Wall;
            if (!wall.Alives()) {
                Projectile.Kill();
                return;
            }
            reach -= SnapBackSpeed;
            if (reach <= 12f) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = wall.Center + snapDir * reach;
            Projectile.rotation = snapDir.ToRotation();
        }

        /// <summary>线体碰撞：只在甩出段有效(缠住后伤害由状态节拍编排)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC wall = Wall;
            if (!wall.Alives() || snappingBack || Projectile.damage <= 0 || reach < 8f) {
                return false;
            }
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                wall.Center, wall.Center + LashDir * reach, 30f, ref point);
        }

        /// <summary>命中反馈：缠上的湿响(抓取判定由服务端沿线扫描权威决定)</summary>
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.35f, Volume = 1f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC wall = Wall;
            if (!wall.Alives() || reach < 8f) {
                return false;
            }

            Texture2D chainTex = TextureAssets.Chain12.Value;
            Texture2D drop = CWRAsset.Extra_98.Value;
            Vector2 mouth = wall.Center;
            Vector2 dir = (Projectile.Center - mouth).SafeNormalize(LashDir);
            float len = Vector2.Distance(mouth, Projectile.Center);
            float segLen = chainTex.Height;
            int segments = (int)(len / segLen) + 1;
            float chainRot = dir.ToRotation() + MathHelper.PiOver2;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            bool attached = WofTongueGrabState.VictimIndex(wall) >= 0 && !snappingBack;
            //缠住期舌体紧绷微颤，其余松弛
            float slack = attached ? 3f : (snappingBack ? 16f : 6f);

            //根部内收：首节埋进口器暗喉
            float rootInset = MathHelper.Min(30f, len * 0.22f);
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float rootT = MathHelper.Clamp(t * 4f, 0f, 1f);
                float sag = (float)Math.Sin(t * MathHelper.Pi) * slack
                    + (float)Math.Sin(t * 11f + Main.GlobalTimeWrappedHourly * 16f) * (attached ? 3.5f : 2f);
                Vector2 pos = mouth + dir * (rootInset + t * (len - rootInset)) + perp * sag;
                Color light = Lighting.GetColor((int)pos.X / 16, (int)(pos.Y / 16f));
                //抓取舌比普通舌鞭更粗更暗，节间交替鼓起读作倒刺肌节
                float barb = i % 2 == 0 ? 1.18f : 0.92f;
                Color tint = Color.Lerp(light, WofMotionFX.BloodDark, 0.45f) * MathHelper.Lerp(0.5f, 1f, rootT);
                spriteBatchDraw(chainTex, pos, tint, chainRot, MathHelper.Lerp(0.7f, 1.25f, rootT) * barb);
            }

            //缠体环：受害者身上绕两圈舌肉(读作缠住而非贴着)
            if (attached) {
                int victim = WofTongueGrabState.VictimIndex(wall);
                if (victim >= 0 && victim < Main.maxPlayers && Main.player[victim].Alives()) {
                    Vector2 center = Main.player[victim].Center;
                    for (int ring = 0; ring < 2; ring++) {
                        float radius = 22f + ring * 11f;
                        float spin = Main.GlobalTimeWrappedHourly * (ring == 0 ? 1.6f : -1.2f);
                        const int RingSegs = 9;
                        for (int i = 0; i < RingSegs; i++) {
                            float ang = MathHelper.TwoPi * i / RingSegs + spin;
                            Vector2 pos = center + ang.ToRotationVector2() * new Vector2(radius, radius * 0.62f);
                            Color light = Lighting.GetColor((int)pos.X / 16, (int)(pos.Y / 16f));
                            spriteBatchDraw(chainTex, pos, Color.Lerp(light, WofMotionFX.BloodDark, 0.5f),
                                ang + MathHelper.PiOver2, 0.72f);
                        }
                    }
                    //勒痕血光(加色层，A=0)
                    Texture2D glow = CWRAsset.SoftGlow.Value;
                    Main.EntitySpriteDraw(glow, center - Main.screenPosition, null,
                        new Color(255, 55, 40, 0) * 0.35f, 0f, glow.Size() / 2f, 0.75f, SpriteEffects.None, 0);
                }
            }

            //舌尖肉锤：暗核+湿高光
            Vector2 tipScreen = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(drop, tipScreen, null, WofMotionFX.BloodDark,
                Projectile.rotation + MathHelper.PiOver2, drop.Size() / 2f,
                new Vector2(0.72f, 0.85f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(drop, tipScreen - new Vector2(2f, 3f), null, WofMotionFX.BloodHot * 0.7f,
                Projectile.rotation + MathHelper.PiOver2, drop.Size() / 2f,
                new Vector2(0.42f, 0.55f), SpriteEffects.None, 0);
            return false;
        }

        private static void spriteBatchDraw(Texture2D tex, Vector2 worldPos, Color color, float rotation, float scale = 1f) {
            Main.EntitySpriteDraw(tex, worldPos - Main.screenPosition, null, color, rotation,
                tex.Size() / 2f, scale, SpriteEffects.None, 0);
        }
    }
}

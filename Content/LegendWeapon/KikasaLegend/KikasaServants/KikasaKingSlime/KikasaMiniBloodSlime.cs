using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaKingSlime
{
    /// <summary>
    /// 压砸分裂出的小血史莱姆：从本体撕下来的一小团活凝胶。
    /// 先朝猎物蹦两跳（落地蹲一拍再弹，凝胶节奏），跳完掉头回流
    /// 追着本体跳最后一程，贴身即被吞并合体，不留常驻单位。
    /// ai0=回弹地板（湖面 Y，spawn 一次带齐），ai1=已完成跳数（owner 盖章纠偏）。
    /// 接触伤害全程有效，命中在 owner 端结算，落点视觉允许端间微漂
    /// </summary>
    internal class KikasaMiniBloodSlime : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>回流前的追猎跳数</summary>
        private const int ChaseHops = 2;

        /// <summary>落地蹲底帧数：小凝胶也得先压扁再弹</summary>
        private const int SquatFrames = 7;

        private const float Gravity = 0.5f;

        private ref float FloorY => ref Projectile.ai[0];
        private ref float HopsDone => ref Projectile.ai[1];

        //本地表现量
        private int squatTimer;
        private int frameTick;
        private int frameIndex;
        private float visSx = 1f;
        private float visSy = 1f;

        private static Color GelMain => KikasaDomain.CoolTint(new(224, 66, 62), new(122, 154, 160));
        private static Color GelDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color GelBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        private float Seed => Projectile.identity * 0.7391f % 4.13f;

        private bool Grounded => squatTimer > 0;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = 360;
        }

        /// <summary>找回本体：同主人场上至多一只，扫到即认</summary>
        internal static Projectile FindParent(int owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == owner
                    && proj.type == ModContent.ProjectileType<KikasaKingSlimeServant>()) {
                    return proj;
                }
            }
            return null;
        }

        public override void AI() {
            bool authority = Main.myPlayer == Projectile.owner;
            bool returning = (int)HopsDone >= ChaseHops;
            Projectile parent = FindParent(Projectile.owner);

            //回流途中贴上本体：吞并合体，owner 收场、远端等 kill 包
            if (returning && parent != null
                && Vector2.Distance(parent.Center, Projectile.Center) < 60f) {
                if (authority) {
                    Projectile.Kill();
                }
                return;
            }
            //本体没了就没有归处：owner 直接放它化掉
            if (returning && parent == null && authority) {
                Projectile.Kill();
                return;
            }

            if (Grounded) {
                //蹲底：压扁蓄力一拍，残留的空中倾角回正
                Projectile.velocity = Vector2.Zero;
                squatTimer--;
                Projectile.rotation *= 0.7f;
                visSy = MathHelper.Lerp(visSy, 0.55f, 0.5f);
                visSx = 1f + (1f - visSy) * 0.9f;
                if (squatTimer <= 0) {
                    LaunchHop(parent, returning, authority);
                }
                return;
            }

            //腾空：重力弹道，沿速度拉伸
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + Gravity, 14f);
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.55f);
            visSy = 1f + stretch;
            visSx = 1f / MathF.Sqrt(visSy);
            Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.03f, -0.3f, 0.3f);

            //失稳甩珠
            if (!Main.dedServ && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    GelMain * 0.5f, Main.rand.NextFloat(0.25f, 0.42f))?.Configure(Main.rand.Next(10, 18));
            }

            //落回弹床：湖面即地板
            if (Projectile.velocity.Y > 0f && Projectile.Bottom.Y >= FloorY) {
                Projectile.Bottom = new Vector2(Projectile.Bottom.X, FloorY);
                Projectile.velocity = Vector2.Zero;
                squatTimer = SquatFrames;
                LandBeat();
            }

            Lighting.AddLight(Projectile.Center, 0.16f, 0.04f, 0.04f);
        }

        /// <summary>起跳一帧定弹道：追猎跳奔猎物，回流跳奔本体</summary>
        private void LaunchHop(Projectile parent, bool returning, bool authority) {
            Vector2 aim;
            if (!returning) {
                int target = FindTarget();
                if (target >= 0) {
                    NPC npc = Main.npc[target];
                    aim = npc.Center + npc.velocity * 8f;
                }
                else {
                    //没猎物就直接掉头回家
                    HopsDone = ChaseHops;
                    returning = true;
                    aim = parent?.Center ?? Projectile.Center;
                }
            }
            else {
                aim = parent?.Center ?? Projectile.Center;
            }

            float dx = aim.X - Projectile.Center.X;
            float upBias = MathHelper.Clamp((Projectile.Center.Y - aim.Y) * 0.02f, 0f, 2.6f);
            Projectile.velocity = new Vector2(
                MathHelper.Clamp(dx / 24f, -8.5f, 8.5f),
                -(returning ? 8.8f : 8.2f) - upBias);
            HopsDone++;
            Projectile.netUpdate = authority;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.3f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
            if (!Main.dedServ) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f),
                        new Vector2(-MathF.Sign(Projectile.velocity.X) * Main.rand.NextFloat(0.5f, 1.5f),
                            -Main.rand.NextFloat(1f, 2.2f)),
                        GelMain * 0.5f, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(12, 20), 0f);
                }
            }
        }

        /// <summary>落水拍：小圈涟漪 + 碎珠，量级只有本体的零头</summary>
        private void LandBeat() {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.28f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.25f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            if (Main.dedServ || KikasaDomain.Viewed == null
                || KikasaDomain.Viewed.Player.whoAmI != Projectile.owner) {
                return;
            }
            KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, FloorY), 0.4f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-10f, 10f), FloorY - 2f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.4f, 2.8f)),
                    GelMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22), 0f);
            }
        }

        private int FindTarget() {
            int best = -1;
            float bestDist = 900f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            Projectile parent = FindParent(Projectile.owner);
            bool merged = parent != null && Vector2.Distance(parent.Center, Projectile.Center) < 130f;
            if (merged) {
                //吞并：血珠成束涌进本体
                Vector2 into = (parent.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 8f),
                        into * Main.rand.NextFloat(3f, 6f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        GelMain * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 16), 0f, 0.99f);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                return;
            }
            //其他死法：原地散珠
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-0.5f, 1.6f)),
                    GelMain * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制 ====================

        /// <summary>小凝胶三层 CPU 血染：暗缘压边、半透主体、A=0 湿亮芯，小到不值一次批切换</summary>
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.BlueSlime);
            Texture2D tex = TextureAssets.Npc[NPCID.BlueSlime]?.Value;
            if (tex == null) {
                return false;
            }
            int frameCount = Main.npcFrameCount[NPCID.BlueSlime];
            if (++frameTick >= 8) {
                frameTick = 0;
                frameIndex = (frameIndex + 1) % frameCount;
            }
            int frameH = tex.Height / frameCount;
            Rectangle frame = new(0, frameH * frameIndex, tex.Width, frameH);

            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = new(frame.Width * 0.5f, frame.Height - 4f);
            Vector2 pos = Projectile.Bottom - Main.screenPosition;
            Vector2 scale = new Vector2(visSx, visSy) * 0.92f;

            //暗血压边略宽一圈给体积
            sb.Draw(tex, pos, frame, GelDark * 0.75f, Projectile.rotation, origin,
                scale * 1.08f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, frame, GelMain * 0.85f, Projectile.rotation, origin,
                scale, SpriteEffects.None, 0f);
            //湿面亮芯：A=0 在预乘混合下即加色
            float sheenPulse = 0.35f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Seed);
            sb.Draw(tex, pos + new Vector2(-2f * visSx, -4f * visSy), frame,
                (GelBright with { A = 0 }) * sheenPulse, Projectile.rotation, origin,
                scale * new Vector2(0.5f, 0.42f), SpriteEffects.None, 0f);

            return false;
        }
    }
}

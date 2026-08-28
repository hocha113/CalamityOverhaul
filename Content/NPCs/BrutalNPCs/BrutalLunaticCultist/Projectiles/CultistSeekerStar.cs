using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 追星矢:司祭头顶凝成的奥术星,按槽位错拍逐颗锁向掷出<br/>
    /// ai[0]=宿主npc ai[1]=槽位(定悬停位与出手拍) ai[2]=段 0凝聚悬停 1掷出(权威端翻转)<br/>
    /// 公平阀:出手拍锁向后纯直线(无追踪);末 AimFreezeFrames 帧预瞄线冻结=预告即承诺;<br/>
    /// 悬停段无伤,掷出起步 4 帧无伤
    /// </summary>
    internal class CultistSeekerStar : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>首颗出手拍(出生龄)</summary>
        internal const int FirstBeat = 26;
        /// <summary>相邻槽位出手错拍</summary>
        internal const int BeatGap = 9;
        /// <summary>预瞄线显示窗(出手前),末段冻结</summary>
        private const int AimShowFrames = 14;
        /// <summary>预瞄冻结窗:此后线不再跟人=承诺</summary>
        private const int AimFreezeFrames = 6;
        private const float DartSpeed = 17f;
        private const float MaxSpeed = 23f;
        private const int DartLifetime = 150;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Slot => (int)Projectile.ai[1];
        private int Stage => (int)Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];
        private ref float StageAge => ref Projectile.localAI[1];

        /// <summary>本槽出手拍(出生龄)</summary>
        private int LaunchBeat => FirstBeat + Slot * BeatGap;

        private int lastSeenStage = -1;
        /// <summary>预瞄方向(本地缓存,冻结窗内不再更新)</summary>
        private Vector2 aimDir = Vector2.UnitY;
        /// <summary>本地已按拍预掷(客户端先走本地预估,权威 ai[2]+速度包到达后回正)</summary>
        private bool provisionalLaunched;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //换段看门:远端由 ai[2] 同步得知换段,本地段龄归零
            if (Stage != lastSeenStage) {
                lastSeenStage = Stage;
                StageAge = 0f;
            }
            Age++;
            StageAge++;

            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(0), 4, 4f);
                Projectile.Kill();
                return;
            }
            int palette = (int)owner.ai[0];
            Player target = owner.target >= 0 && owner.target < 255 ? Main.player[owner.target] : null;

            if (Stage == 0 && Age < LaunchBeat) {
                //凝聚悬停:头顶扇形冠位,呼吸浮动;出手前缓抬(拉弓)
                float crownAngle = -MathHelper.PiOver2 + (Slot - 2f) * 0.42f;
                float lift = MathHelper.Clamp((Age - (LaunchBeat - 12f)) / 12f, 0f, 1f);
                Vector2 anchor = owner.Center + crownAngle.ToRotationVector2() * (96f + lift * 26f)
                    + new Vector2(0f, (float)Math.Sin(Age * 0.11f + Slot * 1.7f) * 7f);
                Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.25f);
                Projectile.velocity = Vector2.Zero;

                //预瞄:显示窗内跟人,冻结窗锁死(各端本地缓存,权威速度回正)
                if (target.Alives() && Age < LaunchBeat - AimFreezeFrames) {
                    aimDir = (CultistMotion.PredictTarget(target, Projectile.Center, DartSpeed, 0.5f)
                        - Projectile.Center).SafeNormalize(Vector2.UnitY);
                }
            }
            else {
                //出手拍(各端同拍到点:权威翻 ai[2] 定案,客户端先按本地预瞄走,速度包到达即回正)
                if (!provisionalLaunched) {
                    provisionalLaunched = true;
                    //只在还没收到权威定案时用本地预瞄铺垫;ai[2] 已是 1(含晚入场)则速度以同步值为准
                    if (Stage == 0) {
                        Projectile.velocity = aimDir * DartSpeed;
                        if (!VaultUtils.isClient) {
                            Projectile.ai[2] = 1;
                            lastSeenStage = 1;
                            StageAge = 0f;
                            Projectile.netUpdate = true;
                        }
                    }
                    CultistMotion.CastFlash(Projectile.Center, CultistMotion.PhaseCore(palette), 0.9f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item125 with { Volume = 0.7f, Pitch = 0.1f + Slot * 0.08f }, Projectile.Center);
                    }
                }
                //掷出:纯直线复利续力(锁向即承诺,不追踪);权威速度包到达前沿本地预估飞
                if (Projectile.velocity.Length() < MaxSpeed) {
                    Projectile.velocity *= 1.013f;
                }
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (StageAge > DartLifetime && Stage == 1) {
                    Projectile.Kill();
                    return;
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(palette).ToVector3() * 0.4f);
        }

        /// <summary>伤害窗=掷出起速后(悬停凝聚只是光)</summary>
        public override bool CanHitPlayer(Player target) {
            return Stage == 1 && StageAge > 4f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Stage != 1 || StageAge <= 4f) {
                return false;
            }
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - Projectile.velocity, Projectile.Center + Projectile.velocity * 0.4f,
                18f, ref point);
        }

        public override void OnKill(int timeLeft) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int palette = owner != null && owner.active ? (int)owner.ai[0] : 0;
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(palette), 0.55f, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int palette = owner != null && owner.active ? (int)owner.ai[0] : 0;
            Color mid = CultistMotion.PhaseCore(palette);
            Color edge = CultistMotion.PhaseEdge(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(edge, Color.Black, 0.45f);
            float seed = Projectile.identity % 100 * 0.071f;
            SpriteBatch sb = Main.spriteBatch;

            //凝聚度:出生渐凝,出手前顶亮
            float form = MathHelper.Clamp(Age / 14f, 0f, 1f);
            float nearLaunch = Stage == 0
                ? MathHelper.Clamp((Age - (LaunchBeat - AimShowFrames)) / (float)AimShowFrames, 0f, 1f) : 1f;

            //预瞄线:出手前 AimShowFrames 帧渐显,冻结窗白热(线指哪,矢飞哪)
            if (Stage == 0 && nearLaunch > 0.01f) {
                bool frozen = Age >= LaunchBeat - AimFreezeFrames;
                Vector2 root = Projectile.Center - Main.screenPosition;
                Vector2[] pts = [root, root + aimDir * 1500f];
                float[] widths = [6f + nearLaunch * 3f, 4f];
                float[] alphas = [0.5f * nearLaunch, 0.2f * nearLaunch];
                sb.End();
                CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                    deep, mid, bright, 1f, frozen ? 0f : 13f, frozen ? 0.9f : 0.2f, seed, nearLaunch);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //掷出拖尾:同料星芒回溯
            if (Stage == 1) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    CultistOrreryRenderer.DrawStarBead(sb, ghostPos, mid, edge,
                        0.30f * (0.4f + 0.5f * t), 0.38f * t, Projectile.rotation - i * 0.07f);
                }
            }

            //星体:凝聚渐大,出手前脉动加剧
            float pulse = Stage == 0
                ? 0.16f + form * 0.12f + nearLaunch * 0.08f
                    + (float)Math.Sin(Age * 0.4f + Slot) * 0.02f * (1f + nearLaunch * 2f)
                : 0.32f;
            CultistOrreryRenderer.DrawStarBead(sb, Projectile.Center - Main.screenPosition, mid, edge,
                pulse, MathHelper.Max(form, 0.3f), Main.GlobalTimeWrappedHourly * 2.6f + Slot * 0.9f);
            return false;
        }
    }
}

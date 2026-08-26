using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles
{
    /// <summary>
    /// 凝形火种：小鬼传送开窗预告。传送落点处凝形 45 帧才许开火；
    /// 第 30 帧锁定瞄向（此后不再追踪，预告即承诺），窗口结束发射锁向焰弹束。
    /// 小鬼中途死亡或再次传送则预告作废不开火。<br/>
    /// ai[0]=小鬼NPC索引+档位*1000 ai[1]=目标玩家索引 ai[2]=锁定角（未锁定时为哨兵值）
    /// </summary>
    internal class JhAmbushOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>开窗总帧数（任务契约 ≥45）</summary>
        private const int WindowFrames = 45;
        /// <summary>锁定瞄向的帧：此后方向冻结</summary>
        private const int LockFrame = 30;
        /// <summary>基础弹数，每档位+1</summary>
        private const int BurstBase = 3;
        /// <summary>束内相邻弹的固定夹角（弧度）</summary>
        private const float BurstStepAngle = 0.09f;
        private const float BoltSpeed = 6.5f;
        /// <summary>再传送判定距离</summary>
        private const float RebounceDist = 128f;
        /// <summary>ai[2] 的"未锁定"哨兵值（合法角度域为 [-π,π]）</summary>
        internal const float UnlockedAngle = -10f;

        private int NpcIndex => (int)Projectile.ai[0] % 1000;
        private int Tier => Math.Max(1, (int)Projectile.ai[0] / 1000);
        private int TargetIdx => (int)Projectile.ai[1];
        private bool Locked => Projectile.ai[2] > UnlockedAngle + 1f;
        private int Age => WindowFrames - Projectile.timeLeft;

        /// <summary>再传送检测的上一帧小鬼位置（各端本地，仅服务端据此裁决）</summary>
        private Vector2 prevImpPos;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WindowFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体，永不参与伤害</summary>
        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //绑定校验：小鬼没了预告就没了（各端读同步NPC态，同判定）
            if (!NpcIndex.TryGetNPC(out NPC imp) || !imp.Alives() || imp.type != NPCID.FireImp) {
                Projectile.Kill();
                return;
            }

            //再次传送→预告作废，不开火（服务端裁决，客户端等同步）
            if (prevImpPos != Vector2.Zero
                && Vector2.DistanceSquared(prevImpPos, imp.position) > RebounceDist * RebounceDist) {
                if (!VaultUtils.isClient) {
                    Projectile.Kill();
                }
                return;
            }
            prevImpPos = imp.position;
            Projectile.Center = imp.Center + new Vector2(0f, -8f);

            int age = Age;
            if (!VaultUtils.isClient) {
                if (age == LockFrame) {
                    if (!LockAim()) {
                        Projectile.Kill();
                        return;
                    }
                }
                if (age >= WindowFrames - 1) {
                    if (Locked) {
                        FireBurst();
                    }
                    Projectile.Kill();
                    return;
                }
            }

            //凝形吸聚尘（≤3/帧）
            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(26f, 26f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Torch,
                    -offset * 0.06f, 120, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = true;
            }
            float grow = age / (float)WindowFrames;
            Lighting.AddLight(Projectile.Center, 0.5f * grow, 0.25f * grow, 0.08f * grow);
        }

        /// <summary>锁定瞄向并一次性同步（此后不再追踪目标）</summary>
        private bool LockAim() {
            Player target = TargetIdx.TryGetPlayer(out Player p) && p.Alives() ? p : null;
            if (target == null) {
                return false;
            }
            Projectile.ai[2] = (target.Center - Projectile.Center).ToRotation();
            Projectile.netUpdate = true;
            return true;
        }

        /// <summary>沿锁定角发射固定夹角焰弹束（不重瞄）</summary>
        private void FireBurst() {
            int count = BurstBase + (Tier - 1);
            float aim = Projectile.ai[2];
            for (int i = 0; i < count; i++) {
                float angle = aim + (i - (count - 1) * 0.5f) * BurstStepAngle;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                    angle.ToRotationVector2() * BoltSpeed, ModContent.ProjectileType<JhImpFireBolt>(),
                    Projectile.damage, 0f, Main.myPlayer, Tier);
            }
        }

        /// <summary>
        /// 发射音效在死亡帧各端本地播放（发射走服务端路径，音效留在那里则联机无人听见）。
        /// 只有锁定完成且走满窗口的死亡才算真开火；提前作废（小鬼死亡/再传送）不播
        /// </summary>
        public override void OnKill(int timeLeft) {
            if (Main.dedServ || !Locked || timeLeft > 4) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.85f, MaxInstances = 4 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.BallofFire);
            Texture2D fire = TextureAssets.Projectile[ProjectileID.BallofFire].Value;
            Texture2D core = CWRAsset.Extra_98.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float grow = MathHelper.Clamp(Age / (float)WindowFrames, 0f, 1f);
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            //锁定后画瞄准线：可见的承诺方向
            if (Locked) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float flash = 0.55f + 0.35f * pulse;
                Vector2 lineScale = new Vector2(560f / pixel.Width, 3.2f / pixel.Height);
                Main.EntitySpriteDraw(pixel, drawPos, null, new Color(255, 150, 50, 0) * (0.5f * flash),
                    Projectile.ai[2], new Vector2(0f, pixel.Height / 2f), lineScale, SpriteEffects.None, 0);
            }

            //凝形火球：暗芯打底+火球贴图幽灵渐显
            Main.EntitySpriteDraw(core, drawPos, null, new Color(140, 50, 20) * (0.6f * grow),
                0f, core.Size() / 2f, 0.3f + 0.18f * grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(fire, drawPos, null, Color.White * (0.25f + 0.65f * grow),
                Projectile.timeLeft * 0.1f, fire.Size() / 2f, 0.35f + 0.55f * grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(fire, drawPos, null, new Color(255, 160, 60, 0) * (0.5f * grow * pulse),
                -Projectile.timeLeft * 0.07f, fire.Size() / 2f, (0.35f + 0.55f * grow) * 1.25f, SpriteEffects.None, 0);
            return false;
        }
    }
}

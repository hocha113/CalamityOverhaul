using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// 哨戒炮塔 ICE（M3 威胁扩容）：地下机房的定点岗哨。悬锚不动，
    /// 半径内通视充能锁定（可读性=扫描线收拢），锁定完成点亮玩家并周期射击；
    /// 弹丸伤 RAM 为主（ICE 家族的牙）。可击毁，击毁有噪音代价
    /// 潜行绕行 or 拔哨，仍是决策不是清怪
    /// </summary>
    internal class OldNetTurretICE : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //ai[0]：锁定充能；ai[1]：射击倒数；ai[2]：已警报旗标
        private ref float LockCharge => ref NPC.ai[0];
        private ref float FireTimer => ref NPC.ai[1];
        private ref float Alerted => ref NPC.ai[2];

        private float Seed => NPC.whoAmI * 0.771f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 26;
            NPC.height = 26;
            //无接触伤害：威胁全在弹丸
            NPC.damage = 0;
            NPC.defense = OldNetMetrics.TurretDefense;
            NPC.lifeMax = OldNetMetrics.TurretLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 0.3f;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
        }

        public override void AI() {
            if (!OldNetWorld.Active) {
                NPC.active = false;
                return;
            }
            //定点岗哨：钉在出生位
            NPC.velocity = Vector2.Zero;

            NPC.TargetClosest(faceTarget: false);
            Player player = Main.player[NPC.target];
            bool hasTarget = player != null && player.active && !player.dead;

            float radius = OldNetMetrics.TurretScanRadius;
            bool inSight = hasTarget
                && Vector2.Distance(player.Center, NPC.Center) < radius
                && Collision.CanHitLine(NPC.position, NPC.width, NPC.height,
                    player.position, player.width, player.height);

            if (!inSight) {
                //脱出视界：充能缓释、警报保持（哨塔记仇，不重置锁定）
                LockCharge = MathF.Max(0f, LockCharge - 2f);
                return;
            }

            if (LockCharge < OldNetMetrics.TurretLockChargeTicks) {
                LockCharge++;
                if ((int)LockCharge % 14 == 1 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.8f }, NPC.Center);
                }
                if (LockCharge >= OldNetMetrics.TurretLockChargeTicks && Alerted < 1f) {
                    //首次锁定完成：点亮玩家
                    Alerted = 1f;
                    OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseTurretSpotted);
                    NPC.netUpdate = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.45f, Pitch = 0.4f }, NPC.Center);
                    }
                }
                return;
            }

            //锁定态：周期射击
            if (--FireTimer > 0f) {
                return;
            }
            FireTimer = OldNetMetrics.TurretFireInterval;
            if (VaultUtils.isClient) {
                return;
            }
            Vector2 vel = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY)
                * OldNetMetrics.TurretBoltSpeed;
            int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel,
                ModContent.ProjectileType<OldNetTurretBolt>(), OldNetMetrics.TurretBoltDamage, 1f);
            if (proj >= 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.45f, Pitch = -0.35f }, NPC.Center);
            }
        }

        public override void OnKill() {
            //拔哨有代价：击毁者噪音上扬
            int idx = NPC.lastInteraction;
            Player killer = idx >= 0 && idx < Main.maxPlayers ? Main.player[idx] : null;
            if (killer?.active != true) {
                killer = Main.LocalPlayer;
            }
            if (killer?.active == true) {
                OldNetPlayer.Get(killer).AddNoise(OldNetMetrics.NoiseTurretKill);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (NPC.life <= 0 ? 14 : 3); i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Electric, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.5f, 1f);
            }
        }

        //──── 程序化绘制：吊装座 + 转向炮头 + 充能环 ────

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = NPC.Center - screenPos;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;

            float chargeFrac = MathHelper.Clamp(
                NPC.ai[0] / OldNetMetrics.TurretLockChargeTicks, 0f, 1f);
            bool locked = chargeFrac >= 1f;
            Color shell = new(20, 44, 50);
            Color accent = locked ? new Color(235, 64, 44)
                : Color.Lerp(new Color(0, 220, 255), new Color(255, 170, 60), chargeFrac);

            //炮头朝向：有目标时指向目标，否则慢摆
            Player target = Main.player[NPC.target];
            float aim = target?.active == true && !target.dead && chargeFrac > 0.01f
                ? (target.Center - NPC.Center).ToRotation()
                : MathF.Sin(t * 0.8f + Seed) * 0.6f + MathHelper.PiOver2;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //吊装座：贴锚方块 + 短臂
            spriteBatch.Draw(px, center, null, shell, MathHelper.PiOver4,
                origin, Size(14f, 14f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center, null, shell * 0.9f, 0f,
                origin, Size(18f, 5f), SpriteEffects.None, 0f);
            //炮管：指向目标的短杆
            Vector2 muzzle = center + aim.ToRotationVector2() * 9f;
            spriteBatch.Draw(px, center + aim.ToRotationVector2() * 5f, null, accent * 0.85f,
                aim, origin, Size(14f, 3f), SpriteEffects.None, 0f);
            //炮口芯
            spriteBatch.Draw(px, muzzle, null, Color.White * (0.5f + chargeFrac * 0.4f),
                MathHelper.PiOver4, origin, Size(3f, 3f), SpriteEffects.None, 0f);

            //充能环：锁定进度的可读化（斜置方形描边收拢）
            if (chargeFrac > 0.02f && !locked) {
                float ringR = 20f - chargeFrac * 10f;
                for (int k = 0; k < 4; k++) {
                    float ang = MathHelper.PiOver2 * k + MathHelper.PiOver4 + t * 1.5f;
                    Vector2 a = center + ang.ToRotationVector2() * ringR;
                    Vector2 b = center + (ang + MathHelper.PiOver2).ToRotationVector2() * ringR;
                    Vector2 diff = b - a;
                    spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), accent * (0.5f * chargeFrac),
                        diff.ToRotation(), new Vector2(0f, 0.5f),
                        new Vector2(diff.Length(), 1.1f), SpriteEffects.None, 0f);
                }
            }

            //警报眼芯辉光（A=0 亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                float pulse = locked ? 0.75f + 0.25f * MathF.Sin(t * 9f) : 0.4f + chargeFrac * 0.3f;
                Color glowCol = accent * (0.5f * pulse);
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, center, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>哨戒弹：直线追踪弹，命中小额 HP + RAM 扣减；撞地即灭</summary>
    internal class OldNetTurretBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            if (!OldNetWorld.Active) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.22f, 0.05f, 0.04f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //TODO(MP)：本钩子只在被打端跑，RAM 扣减 MP 客户端直调必失败，联机化走请求包
            RamSystem.TryConsume(target, OldNetMetrics.TurretBoltRam, out _);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Electric, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f));
                dust.noGravity = true;
                dust.scale = Main.rand.NextFloat(0.4f, 0.8f);
            }
        }

        //速度拉伸的曳光弹体：红缘黑芯 + 白热头
        public override bool PreDraw(ref Color lightColor) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float rot = Projectile.rotation;
            Color edge = new(235, 64, 44);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //拖尾三段渐隐
            for (int i = 1; i <= 3; i++) {
                Vector2 back = center - Projectile.velocity * (i * 1.6f);
                Main.EntitySpriteDraw(px, back, null, edge * (0.35f - i * 0.09f), rot,
                    origin, Size(9f - i * 2f, 2.2f), SpriteEffects.None, 0);
            }
            //弹体：红缘 + 白热头
            Main.EntitySpriteDraw(px, center, null, edge * 0.9f, rot,
                origin, Size(12f, 3f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(px, center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 4f,
                null, Color.White * 0.8f, rot, origin, Size(4f, 2f), SpriteEffects.None, 0);
            return false;
        }
    }
}

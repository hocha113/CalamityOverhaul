using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【甲虫鳞甲·战争圣甲虫 ★A】输出向甲虫胸甲的独立神赋：①连击维持虫势（1.5 秒不命中即断）
    /// ②虫势攀至 5/10/15 节点，背甲放出一只战争圣甲虫撞向当前目标，节点越高甲虫越大力道越沉
    /// ③第 15 击的巨甲虫撞后炸开甲壳并清空虫势重新蓄力。原版套装奖励（甲虫之力）保留，神赋叠加
    /// </summary>
    internal class GsBeetleScaleArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsB";

        public override int[] HeadIDs => [ItemID.BeetleHelmet];

        public override int BodyID => ItemID.BeetleScaleMail;

        public override int LegsID => ItemID.BeetleLeggings;

        protected override string EndowLineFallback =>
            "War Scarab: chain strikes to build momentum (breaks after 1.5s idle); at 5, 10 and 15 hits a war scarab charges from your back, each rank larger and heavier";

        //甲虫铁青色板
        internal static readonly Color BeetleShine = new(150, 255, 214);
        internal static readonly Color BeetleGreen = new(58, 160, 110);
        internal static readonly Color BeetleDark = new(24, 52, 46);

        /// <summary>连击维持窗口（帧）</summary>
        private const int ComboWindow = 90;

        private bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsBeetleWarScarabProj>();

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //断连：窗口外清零虫势
            if (state.EndowCharge > 0 && Main.GameUpdateCount > state.EndowTimer + ComboWindow) {
                state.EndowCharge = 0;
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 4; i++) {
                        Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                            DustID.Smoke, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.5f)),
                            140, BeetleGreen, 1f);
                        d.noGravity = false;
                    }
                }
            }
            //高虫势的贴身虫鸣微光（个人读数）
            if (!VaultUtils.isServer && state.EndowCharge >= 10 && Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(16f, 22f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    BeetleShine, 0.3f)?.Configure(false, 12);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            state.EndowTimer = Main.GameUpdateCount;
            state.EndowCharge++;

            //节点：5/10/15 各放一只，档位递增
            int rank = state.EndowCharge switch {
                5 => 1,
                10 => 2,
                15 => 3,
                _ => 0,
            };
            if (rank == 0) {
                return;
            }
            if (state.EndowCharge >= 15) {
                state.EndowCharge = 0;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Volume = 0.25f + 0.15f * rank, Pitch = 0.5f - 0.35f * rank, MaxInstances = 3
                }, player.Center);
                //背甲开壳喷息
                for (int i = 0; i < 3 + rank * 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + new Vector2(-player.direction * 12f, -6f),
                        new Vector2(-player.direction * Main.rand.NextFloat(1f, 2.5f), Main.rand.NextFloat(-1.5f, 0.5f)),
                        i % 2 == 0 ? BeetleShine : BeetleGreen, Main.rand.NextFloat(0.3f, 0.45f))
                        ?.Configure(false, Main.rand.Next(12, 18));
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            float rankMul = rank switch { 1 => 0.5f, 2 => 0.8f, _ => 1.2f };
            int scarabDamage = Math.Clamp((int)(damageDone * rankMul), 15, 90 + rank * 90);
            Vector2 from = player.Center + new Vector2(-player.direction * 26f, -10f);
            Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX) * 7f - Vector2.UnitY * 3f;
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithBeetleScaleEndow"),
                from, vel, ModContent.ProjectileType<GsBeetleWarScarabProj>(),
                scarabDamage, 4f + rank, player.whoAmI, 0f, target.whoAmI, rank);
        }
    }

    /// <summary>
    /// 战争圣甲虫：自背甲冲出的铁青圣甲虫，先扬升振翅、再俯冲撞击点名目标；
    /// 虫体三层甲光 + 双侧鞘翅高频振颤 + 前角，撞击炸开甲壳迸屑；档位（1~3）决定体型与力道
    /// </summary>
    internal class GsBeetleWarScarabProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private ref float TargetIndex => ref Projectile.ai[1];

        /// <summary>档位 1~3</summary>
        private ref float Rank => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.7307f % 3.37f;

        /// <summary>扬升振翅帧数</summary>
        private const int LiftFrames = 12;

        private float RankScale => 0.62f + Rank * 0.26f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            if (Life <= LiftFrames) {
                //扬升：减速上飘 + 振翅蓄势
                Projectile.velocity *= 0.9f;
                Projectile.velocity.Y -= 0.35f;
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (!Main.dedServ && Life % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                        new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)),
                        GsBeetleScaleArmor.BeetleShine, 0.25f)?.Configure(false, 10);
                }
                return;
            }

            //俯冲撞击：咬向点名目标，重加速
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            if (target != null && target.active && target.CanBeChasedBy(Projectile)) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * (13f + Rank * 2.5f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.14f);
            }
            else {
                //目标没了：找最近的替补
                NPC fallback = FindTarget();
                if (fallback != null) {
                    TargetIndex = fallback.whoAmI;
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Life % 2 == 0) {
                //虫翼嗡息
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? GsBeetleScaleArmor.BeetleShine : GsBeetleScaleArmor.BeetleGreen,
                    Main.rand.NextFloat(0.2f, 0.3f) * RankScale)?.Configure(false, Main.rand.Next(7, 12));
            }
            Lighting.AddLight(Projectile.Center, GsBeetleScaleArmor.BeetleGreen.ToVector3() * (0.24f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 500f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //撞击：甲壳崩裂
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.35f + 0.1f * Rank, Pitch = -0.2f - 0.15f * Rank, MaxInstances = 3
            }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsBeetleScaleArmor.BeetleShine, 0.1f + 0.05f * Rank)?.Configure(8, 0.8f);
            int shards = 4 + (int)Rank * 3;
            for (int i = 0; i < shards; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f + Rank),
                    Main.rand.NextBool() ? GsBeetleScaleArmor.BeetleGreen : GsBeetleScaleArmor.BeetleDark,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(16, 26));
            }
        }

        //==================== 绘制：三层甲光虫体 + 振颤鞘翅 + 前角 + 冲撞残影 ====================

        private void DrawScarab(Vector2 pos, float rotation, float alpha, float scaleMul) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (core == null || crescent == null || shot == null) {
                return;
            }
            float s = RankScale * scaleMul;
            Vector2 dir = rotation.ToRotationVector2();
            //鞘翅双侧振颤（高频小幅）
            float buzz = MathF.Sin(Life * 1.6f + Seed * 5f) * 0.16f;
            for (int i = -1; i <= 1; i += 2) {
                Vector2 wing = pos - dir * 4f * s + dir.RotatedBy(MathHelper.PiOver2) * i * 8f * s;
                Main.EntitySpriteDraw(crescent, wing, null,
                    (GsBeetleScaleArmor.BeetleShine with { A = 0 }) * (0.4f * alpha), rotation + i * (0.7f + buzz), crescent.Size() * 0.5f,
                    new Vector2(0.09f, 0.05f) * s, SpriteEffects.None, 0);
            }
            //甲壳暗底（真 alpha 占体积）
            Main.EntitySpriteDraw(core, pos, null,
                GsBeetleScaleArmor.BeetleDark * (0.95f * alpha), rotation, core.Size() * 0.5f,
                new Vector2(0.20f, 0.15f) * s, SpriteEffects.None, 0);
            //铁青甲光
            Main.EntitySpriteDraw(core, pos, null,
                (GsBeetleScaleArmor.BeetleGreen with { A = 0 }) * (0.85f * alpha), rotation, core.Size() * 0.5f,
                new Vector2(0.16f, 0.115f) * s, SpriteEffects.None, 0);
            //背甲高光条
            Main.EntitySpriteDraw(core, pos - dir.RotatedBy(MathHelper.PiOver2) * 2.5f * s, null,
                (GsBeetleScaleArmor.BeetleShine with { A = 0 }) * (0.55f * alpha), rotation, core.Size() * 0.5f,
                new Vector2(0.11f, 0.045f) * s, SpriteEffects.None, 0);
            //前角
            Main.EntitySpriteDraw(shot, pos + dir * 12f * s, null,
                (GsBeetleScaleArmor.BeetleShine with { A = 0 }) * (0.8f * alpha), rotation, shot.Size() * 0.5f,
                new Vector2(0.10f, 0.025f) * s, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = VisualFade;
            //冲撞残影
            if (Life > LiftFrames) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.3f * fade;
                    DrawScarab(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                        Projectile.rotation, ghost, 1f - i * 0.05f);
                }
            }
            DrawScarab(Projectile.Center - Main.screenPosition, Projectile.rotation, fade, 1f);
            return false;
        }
    }
}

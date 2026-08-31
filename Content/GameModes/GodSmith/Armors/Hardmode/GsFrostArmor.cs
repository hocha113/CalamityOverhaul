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
    /// 【霜冻套·寒潮领域】冰核淬霜之甲：①命中积攒霜寒，满八层后下一击唤来随身寒潮
    /// ②四秒领域内，风雪自动在域内敌人头顶凝出冰晶矢坠落点名 ③冰矢命中挂霜火并炸开冰屑。
    /// 原版套装奖励（近战远程命中挂霜火）保留，神赋叠加
    /// </summary>
    internal class GsFrostArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.FrostHelmet];

        public override int BodyID => ItemID.FrostBreastplate;

        public override int LegsID => ItemID.FrostLeggings;

        protected override string EndowLineFallback =>
            "Coldsnap Dominion: strikes build rime; at 8 stacks the next strike raises a 4s blizzard that rains crystal darts on foes inside";

        //霜冻冰蓝色板
        internal static readonly Color FrostBright = new(224, 246, 255);
        internal static readonly Color FrostMain = new(122, 192, 250);
        internal static readonly Color FrostDeep = new(44, 92, 164);

        protected override int FullCharge => 8;

        protected override Color ThemeMain => FrostMain;

        protected override Color ThemeBright => FrostBright;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsFrostSquallDomainProj>()
            || proj.type == ModContent.ProjectileType<GsFrostSquallShardProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
                //寒潮起势：环身冰雾炸开
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_Smoke>(player.Center + ang.ToRotationVector2() * 14f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f),
                        FrostBright, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(22, 0.4f, 0.03f);
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int shardDamage = Math.Clamp((int)(damageDone * 0.30f), 8, 110);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithFrostEndow"),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<GsFrostSquallDomainProj>(),
                shardDamage, 0f, player.whoAmI);
        }
    }

    /// <summary>
    /// 寒潮领域：随佩戴者而行的一场私有风雪，双层逆旋气环 + 缘雾 + 域内落雪；
    /// 每 12 帧点名至多两名域内敌人，自其头顶凝冰晶矢坠击（佩戴者端裁定）
    /// </summary>
    internal class GsFrostSquallDomainProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Airflow";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.5903f % 2.87f;

        /// <summary>领域半径</summary>
        private const float Radius = 230f;

        /// <summary>领域时长</summary>
        private const int Duration = 240;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 18f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>领域本体不判定，冰晶矢才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //随主而行
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.2f);
            Projectile.velocity = Vector2.Zero;

            //点名坠矢（佩戴者端裁定）
            if (Projectile.owner == Main.myPlayer && Life % 12 == 0 && Life < Duration - 30) {
                int called = 0;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (called >= 2) {
                        break;
                    }
                    if (!npc.CanBeChasedBy(Projectile) || npc.Center.Distance(Projectile.Center) > Radius) {
                        continue;
                    }
                    called++;
                    //生成前探顶棚收缩高度，矢带标的线（ai[2]）免碰撞降到线再恢复，隧道里领域不再空转
                    Vector2 from = GsArmorTerrainProbe.SkySpawnAbove(npc.Center,
                        Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(220f, 280f));
                    Vector2 vel = (npc.Center - from).SafeNormalize(Vector2.UnitY) * 14f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        from, vel, ModContent.ProjectileType<GsFrostSquallShardProj>(),
                        Projectile.damage, 1f, Projectile.owner, 0f, npc.whoAmI, npc.Center.Y);
                }
            }

            //域内落雪与缘雾（客户端装饰）
            if (!Main.dedServ) {
                if (Main.rand.NextBool(2)) {
                    Vector2 at = Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.9f, Radius * 0.6f) - new Vector2(0f, 60f);
                    PRTLoader.NewParticle<PRT_Light>(at,
                        new Vector2(MathF.Sin(Life * 0.05f + at.X * 0.01f) * 0.6f, Main.rand.NextFloat(1f, 2f)),
                        GsFrostArmor.FrostBright, Main.rand.NextFloat(0.05f, 0.1f))?.Configure(26, 0.55f);
                }
                if (Main.rand.NextBool(6)) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + ang.ToRotationVector2() * Radius * Main.rand.NextFloat(0.85f, 1f),
                        (ang + MathHelper.PiOver2).ToRotationVector2() * 1.4f,
                        GsFrostArmor.FrostMain, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(26, 0.22f, 0.02f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsFrostArmor.FrostMain.ToVector3() * (0.3f * VisualFade));
        }

        //==================== 绘制：双层逆旋风环 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flow = CWRAsset.Airflow?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (flow == null || ring == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float scale = Radius * 2f / flow.Width;

            //外环顺旋
            Main.EntitySpriteDraw(flow, pos, null,
                (GsFrostArmor.FrostMain with { A = 0 }) * (0.34f * fade), Life * 0.014f + Seed, flow.Size() * 0.5f,
                scale, SpriteEffects.None, 0);
            //内环逆旋（略小、更亮）
            Main.EntitySpriteDraw(flow, pos, null,
                (GsFrostArmor.FrostBright with { A = 0 }) * (0.24f * fade), -Life * 0.02f - Seed, flow.Size() * 0.5f,
                scale * 0.72f, SpriteEffects.None, 0);
            //域界淡环，呼吸起伏
            float breathe = 1f + MathF.Sin(Life * 0.06f + Seed * 3f) * 0.02f;
            Main.EntitySpriteDraw(ring, pos, null,
                (GsFrostArmor.FrostDeep with { A = 0 }) * (0.30f * fade), 0f, ring.Size() * 0.5f,
                Radius * 2f * breathe / ring.Width, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 冰晶矢：风雪凝成的坠落冰锥，落向点名目标微调弹道；出生免地形碰撞、
    /// 越过标的线（ai[2]）才恢复（Stardust 式高度门，低顶棚下照常坠击）；
    /// 矢身冰蓝三层 + 晶尖闪光，命中挂霜火并炸开冰屑
    /// </summary>
    internal class GsFrostSquallShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        private ref float TargetIndex => ref Projectile.ai[1];

        /// <summary>标的高度线：低于此线才恢复地形碰撞</summary>
        private ref float TargetLineY => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.8447f % 3.91f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            //出生免碰撞，越过标的线由高度门恢复（见 AI）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //高度门：越过标的线才恢复地形碰撞
            GsArmorTerrainProbe.UpdateFallGate(Projectile, TargetLineY);
            //坠中微调横向，咬准点名目标
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            if (target != null && target.active) {
                float wantX = MathHelper.Clamp((target.Center.X - Projectile.Center.X) * 0.04f, -1.2f, 1.2f);
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, Projectile.velocity.X + wantX, 0.4f);
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y * 1.02f, 19f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.04f, GsFrostArmor.FrostBright,
                    Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, Main.rand.Next(6, 10));
            }
            Lighting.AddLight(Projectile.Center, GsFrostArmor.FrostMain.ToVector3() * 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 120);

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.7f, MaxInstances = 4 }, Projectile.Center);
            //冰屑迸裂
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsFrostArmor.FrostBright : GsFrostArmor.FrostMain,
                    Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, -Projectile.velocity * 0.05f,
                GsFrostArmor.FrostBright, 0.35f)?.Configure(16, 0.3f, 0.02f);
        }

        //==================== 绘制：三层冰锥 + 晶尖闪光 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (shot == null || star == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = shot.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.4f);

            //寒衬
            Main.EntitySpriteDraw(shot, pos, null,
                (GsFrostArmor.FrostDeep with { A = 0 }) * (0.85f * fade), rotation, origin,
                new Vector2(0.30f + stretch, 0.10f), SpriteEffects.None, 0);
            //冰锥主体
            Main.EntitySpriteDraw(shot, pos, null,
                (GsFrostArmor.FrostMain with { A = 0 }) * fade, rotation, origin,
                new Vector2(0.24f + stretch * 0.8f, 0.065f), SpriteEffects.None, 0);
            //亮晶芯
            Main.EntitySpriteDraw(shot, pos, null,
                (GsFrostArmor.FrostBright with { A = 0 }) * (0.85f * fade), rotation, origin,
                new Vector2(0.18f + stretch * 0.5f, 0.03f), SpriteEffects.None, 0);
            //晶尖闪光（identity 相位闪烁）
            float glint = MathF.Sin(Life * 0.6f + Seed * 5f) * 0.5f + 0.5f;
            Vector2 tip = pos + rotation.ToRotationVector2() * (14f + stretch * 20f);
            Main.EntitySpriteDraw(star, tip, null,
                (GsFrostArmor.FrostBright with { A = 0 }) * (0.6f * glint * fade), 0f, star.Size() * 0.5f,
                0.16f, SpriteEffects.None, 0);
            return false;
        }
    }
}

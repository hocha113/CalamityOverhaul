using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Yoyos
{
    //================================================================ T1：驻场教学 ================================================================

    /// <summary>Y1 木悠悠球：纯基准行教驻场，驻场自旋带木屑微粒</summary>
    internal sealed class GsWoodYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.WoodYoyo;
        internal override int Tier => 1;
        internal override float DamageMul => 1.15f;
        internal override Color GlowColor => new(205, 160, 90);
        protected override string GsDescFallback =>
            "Reforged: right click to anchor the yoyo at your cursor, patrolling in place\nRepeated hits on the same target build Heat, up to +40% damage";

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (VaultUtils.isServer || !Main.rand.NextBool(6)) {
                return;
            }
            //驻场自旋抛洒木屑
            Dust d = Dust.NewDustPerfect(proj.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.WoodFurniture, proj.velocity * 0.2f + Main.rand.NextVector2Circular(1.2f, 1.2f));
            d.noGravity = false;
            d.scale = Main.rand.NextFloat(0.7f, 1.05f);
        }
    }

    /// <summary>Y2 集结：驻场每 5 hit 提一档连击节奏（缩短命中免疫间隔），至三档</summary>
    internal sealed class GsRallyYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Rally;
        internal override int Tier => 1;
        internal override float DamageMul => 1.12f;
        internal override Color GlowColor => new(235, 190, 90);
        protected override string GsDescFallback =>
            "Reforged: right click to anchor; every 5 anchored hits quicken the strike tempo, up to 3 ranks\nRanks reset when the yoyo returns";

        /// <summary>连击档位 → 免疫帧上限（原版基准 10）</summary>
        private static readonly int[] immuneByRank = [10, 9, 8, 8];

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode == GsYoyoMode.Anchor) {
                st.SigCount++;
            }
        }

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            //持续压热度目标的免疫帧上限，绕开命中流程里的赋值顺序问题
            if (!proj.IsOwnedByLocalPlayer() || st.HeatTarget < 0) {
                return;
            }
            int rank = Math.Min(st.SigCount / 5, 3);
            if (rank <= 0) {
                return;
            }
            NPC npc = Main.npc[st.HeatTarget];
            if (npc.active && npc.type == st.HeatTargetType && npc.immune[proj.owner] > immuneByRank[rank]) {
                npc.immune[proj.owner] = immuneByRank[rank];
            }
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            //集结号档位可视：球体金光随档增亮
            int rank = Math.Min(st.SigCount / 5, 3);
            if (rank <= 0 || effMode != GsYoyoMode.Anchor) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color c = GlowColor * (0.2f * rank);
            c.A = 0;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null, c, 0f,
                glow.Size() / 2f, 0.5f + 0.1f * rank, SpriteEffects.None, 0);
        }
    }

    /// <summary>Y3 萎靡：驻场点周围 60px 萎靡场，敌人移速折减 8%</summary>
    internal sealed class GsMalaiseYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.CorruptYoyo;
        internal override int Tier => 1;
        internal override float DamageMul => 1.10f;
        internal override Color GlowColor => new(150, 90, 200);
        protected override string GsDescFallback =>
            "Reforged: right click to anchor; a field of malaise around the anchor slows enemies by 8%";

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            //减速是权威量：服务器/单人施加，多人客户端只看同步结果
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.friendly || !npc.CanBeChasedBy(proj)) {
                        continue;
                    }
                    if (npc.DistanceSQ(proj.Center) < 60f * 60f) {
                        npc.position -= npc.velocity * 0.08f;
                    }
                }
            }
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (effMode != GsYoyoMode.Anchor) {
                return;
            }
            //暗紫雾环：真 alpha 暗层贴图缓旋（加色画不了暗）
            Texture2D veil = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Extra_98")?.Value;
            if (veil == null) {
                return;
            }
            float pulse = 0.26f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + proj.identity);
            Main.EntitySpriteDraw(veil, proj.Center - Main.screenPosition, null,
                new Color(60, 25, 90) * pulse, Main.GlobalTimeWrappedHourly * 0.6f,
                veil.Size() / 2f, 0.72f, SpriteEffects.None, 0);
        }
    }

    /// <summary>Y4 动脉：驻场命中两成几率溅出血珠小弹（15% 伤害抛物线）</summary>
    internal sealed class GsArteryYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.CrimsonYoyo;
        internal override int Tier => 1;
        internal override float DamageMul => 1.10f;
        internal override Color GlowColor => new(220, 60, 60);
        protected override string GsDescFallback =>
            "Reforged: right click to anchor; anchored hits have a 20% chance to fling a blood bead dealing 15% damage";

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            //命中钩子只在攻击方端执行，直接掷随机并生成
            if (mode != GsYoyoMode.Anchor || !Main.rand.NextBool(5)) {
                return;
            }
            int pellet = ModContent.ProjectileType<GsYoyoPelletProj>();
            if (Main.player[proj.owner].ownedProjectileCounts[pellet] >= 4) {
                return;
            }
            Vector2 vel = new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 5.5f));
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, vel, pellet,
                Math.Max(1, (int)(proj.damage * 0.15f)), 0f, proj.owner, GsYoyoPelletProj.StyleBloodBead);
        }
    }

    /// <summary>Y5 亚马逊：驻场命中叠中毒，热度满层后中毒时长翻倍</summary>
    internal sealed class GsAmazonYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.JungleYoyo;
        internal override int Tier => 1;
        internal override float DamageMul => 1.10f;
        internal override Color GlowColor => new(120, 220, 100);
        protected override string GsDescFallback =>
            "Reforged: right click to anchor; anchored hits inflict Poisoned\nAt full Heat the poison lasts twice as long";

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode != GsYoyoMode.Anchor && mode != GsYoyoMode.Path) {
                return;
            }
            target.AddBuff(BuffID.Poisoned, st.HeatLayers >= HeatCapLayers ? 240 : 120);
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (effMode != GsYoyoMode.Anchor || heatRatio < 1f) {
                return;
            }
            //热度满层：毒藤缠球的绿色加色环
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color c = GlowColor * 0.5f;
            c.A = 0;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null, c,
                0f, glow.Size() / 2f, 0.8f, SpriteEffects.None, 0);
        }
    }

    //================================================================ T2：+折返 ================================================================

    /// <summary>Y6 代码1：折返路径显示为数据流，折返命中迸数据方块</summary>
    internal sealed class GsCode1Yoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Code1;
        internal override int Tier => 2;
        internal override float DamageMul => 1.08f;
        internal override Color GlowColor => new(90, 220, 235);
        protected override string GsDescFallback =>
            "Reforged: right click to anchor, double click to lash back through foes at 2.2x speed for 135% damage\nThe lash trail streams with data";

        internal override void OnLashTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (VaultUtils.isServer || st.LashTimer % 2 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_CyberSquare>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                -proj.velocity * 0.05f, GlowColor, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(GlowColor, Main.rand.Next(10, 16));
        }

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode != GsYoyoMode.Lash || VaultUtils.isServer) {
                return;
            }
            //故障色偏：命中处一撮错位数据块
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(2f, 2f), GlowColor, 0.7f)?.Configure(GlowColor, 14);
            }
        }
    }

    /// <summary>Y7 勇气：折返击退翻倍；驻场点近旁敌数不小于 3 时驻场伤害 +15%</summary>
    internal sealed class GsValorYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Valor;
        internal override int Tier => 2;
        internal override float DamageMul => 1.08f;
        internal override Color GlowColor => new(240, 200, 80);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; lash knockback is doubled\nWhile 3 or more enemies crowd the anchor, anchored damage gains 15%";

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            //围攻计数 15f 一算，缓存进寄存器（判定端读 owner 的值）
            if (++st.SigTimer < 15) {
                return;
            }
            st.SigTimer = 0;
            int count = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.friendly && npc.CanBeChasedBy(proj) && npc.DistanceSQ(proj.Center) < 90f * 90f) {
                    count++;
                }
            }
            st.SigCount = count;
        }

        internal override void ModifyCommandHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GsYoyoState st, int mode) {
            if (mode == GsYoyoMode.Lash) {
                modifiers.Knockback *= 2f;
            }
            if (mode == GsYoyoMode.Anchor && st.SigCount >= 3) {
                modifiers.FinalDamage *= 1.15f;
            }
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (effMode != GsYoyoMode.Anchor || st.SigCount < 3) {
                return;
            }
            //越围越勇：球体泛金
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.45f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f);
            Color c = GlowColor * pulse;
            c.A = 0;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null, c, 0f,
                glow.Size() / 2f, 0.75f, SpriteEffects.None, 0);
        }
    }

    /// <summary>Y8 小瀑布：命中溅火花瀑，折返路径留 0.5s 火线段</summary>
    internal sealed class GsCascadeYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Cascade;
        internal override int Tier => 2;
        internal override float DamageMul => 1.08f;
        internal override Color GlowColor => new(255, 140, 50);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; hits shed a cascade of falling sparks\nThe lash path leaves burning lines behind";

        internal override void OnLashBeginOwner(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            st.SigPoint = proj.Center;
        }

        internal override void OnLashTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //每 100px 铺一段火线，在场至多 5 段
            if (Vector2.DistanceSquared(proj.Center, st.SigPoint) < 100f * 100f) {
                return;
            }
            st.SigPoint = proj.Center;
            int zone = ModContent.ProjectileType<GsYoyoBurnZoneProj>();
            if (Main.player[proj.owner].ownedProjectileCounts[zone] >= 5) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, Vector2.Zero, zone,
                Math.Max(1, (int)(proj.damage * 0.15f)), 0f, proj.owner, GsYoyoBurnZoneProj.StyleFireLine);
        }

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1.5f, 3.5f)),
                    GlowColor, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>Y9 蜂巢：驻场化蜂巢，每 0.8s 放一只原版蜂（吃蜂蜜手套等加成）</summary>
    internal sealed class GsHiveFiveYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.HiveFive;
        internal override int Tier => 2;
        internal override float DamageMul => 1.06f;
        internal override Color GlowColor => new(250, 190, 60);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; while anchored the yoyo becomes a hive, releasing a bee every 0.8s\nBees benefit from your bee equipment";

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (!VaultUtils.isServer && Main.rand.NextBool(30)) {
                //巢口滴蜜
                PRTLoader.NewParticle<PRT_SHPCHoneyDrop>(proj.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), GlowColor, Main.rand.NextFloat(0.5f, 0.8f))?
                    .Configure(Main.rand.Next(18, 28));
            }
            if (!proj.IsOwnedByLocalPlayer() || ++st.SigTimer < 48) {
                return;
            }
            st.SigTimer = 0;
            Player owner = Main.player[proj.owner];
            //产率封顶：自家蜂在场不超过 6 只
            int bees = owner.ownedProjectileCounts[ProjectileID.Bee] + owner.ownedProjectileCounts[ProjectileID.GiantBee];
            if (bees >= 6) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center,
                Main.rand.NextVector2Circular(3f, 3f), owner.beeType(),
                owner.beeDamage(Math.Max(1, (int)(proj.damage * 0.30f))), owner.beeKB(0f), proj.owner);
        }
    }

    /// <summary>Y10 格式化:C：折返命中把热度直接重写为满层，冷却 6s</summary>
    internal sealed class GsFormatCYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.FormatC;
        internal override int Tier => 2;
        internal override float DamageMul => 1.06f;
        internal override Color GlowColor => new(200, 230, 255);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; a lash hit formats Heat straight to full on that target\n6s cooldown, a white star marks it ready";

        internal override void OnGlobalTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode) {
            if (st.SigTimer > 0) {
                st.SigTimer--;
            }
        }

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode != GsYoyoMode.Lash || st.SigTimer > 0) {
                return;
            }
            //格式化：热度目标改写为本目标并拉满
            st.SigTimer = 360;
            st.HeatTarget = target.whoAmI;
            st.HeatTargetType = target.type;
            st.HeatLayers = HeatCapLayers;
            router.MarkData2 = st.HeatLayers;
            proj.netUpdate = true;
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Vector2.Zero, Color.White, 0.6f)?
                    .Configure(Color.White, 18, 0.3f);
            }
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (st.SigTimer > 0) {
                return;
            }
            //就绪指示：球上淡白星脉动
            Texture2D star = CWRAsset.StarGlow01.Value;
            float pulse = 0.35f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + proj.identity);
            Color c = Color.White * pulse;
            c.A = 0;
            Main.EntitySpriteDraw(star, proj.Center - Main.screenPosition, null, c,
                Main.GlobalTimeWrappedHourly * 2f, star.Size() / 2f, 0.22f, SpriteEffects.None, 0);
        }
    }

    /// <summary>Y11 梯度：热度上限提至 +60%，球体色相随热度金→橙→红渐变</summary>
    internal sealed class GsGradientYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Gradient;
        internal override int Tier => 2;
        internal override float DamageMul => 1.06f;
        internal override float HeatCap => 0.60f;
        internal override Color GlowColor => new(255, 180, 70);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; Heat cap raised to +60%\nThe yoyo shifts gold, orange then red as Heat builds";

        private static readonly Color GradGold = new(255, 215, 120);
        private static readonly Color GradRed = new(255, 60, 40);

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (heatRatio <= 0f) {
                return;
            }
            //梯度身份：热度即色相
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color c = Color.Lerp(GradGold, GradRed, heatRatio) * (0.3f + 0.45f * heatRatio);
            c.A = 0;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null, c, 0f,
                glow.Size() / 2f, 0.6f + 0.25f * heatRatio, SpriteEffects.None, 0);
        }
    }

    /// <summary>Y12 奇克：全族最快折返（2.6 倍球速），折返带音爆环</summary>
    internal sealed class GsChikYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Chik;
        internal override int Tier => 2;
        internal override float DamageMul => 1.06f;
        internal override float LashSpeedMul => 2.6f;
        internal override Color GlowColor => new(190, 240, 250);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; the lash flies at 2.6x speed with a sonic ring on launch";

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            //音爆环：折返切入帧起 12f 扩张（各端由切换闪光计时驱动）
            if (effMode != GsYoyoMode.Lash || st.SwitchFlash <= 0) {
                return;
            }
            float k = 1f - st.SwitchFlash / 12f;
            ShockRingDraw.Draw(Main.spriteBatch, proj.Center, 12f + 58f * k, 7f,
                Color.White, GlowColor, new Color(90, 150, 190), (1f - k) * 0.8f);
        }
    }

    /// <summary>Y13 冥火：驻场每 2s 在正下方地表点起火环，驻场变阵地烧烤</summary>
    internal sealed class GsHelFireYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.HelFire;
        internal override int Tier => 2;
        internal override float DamageMul => 1.05f;
        internal override Color GlowColor => new(255, 90, 30);
        protected override string GsDescFallback =>
            "Reforged: anchor and lash commands unlocked; every 2s the anchor ignites the ground below with a ring of hellfire";

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (!proj.IsOwnedByLocalPlayer() || ++st.SigTimer < 120) {
                return;
            }
            st.SigTimer = 0;
            int zone = ModContent.ProjectileType<GsYoyoBurnZoneProj>();
            if (Main.player[proj.owner].ownedProjectileCounts[zone] >= 3) {
                return;
            }
            //向下探地表（至多 40 格）
            int tx = (int)(proj.Center.X / 16f);
            int ty = (int)(proj.Center.Y / 16f);
            for (int dy = 0; dy < 40; dy++) {
                int y = ty + dy;
                if (!WorldGen.InWorld(tx, y, 8)) {
                    break;
                }
                Terraria.Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    Vector2 pos = new(proj.Center.X, y * 16f - 16f);
                    Projectile.NewProjectile(proj.GetSource_FromAI(), pos, Vector2.Zero, zone,
                        Math.Max(1, (int)(proj.damage * 0.20f)), 0f, proj.owner, GsYoyoBurnZoneProj.StyleGroundRing);
                    break;
                }
            }
        }
    }

    //================================================================ T3：+环绕 ================================================================

    /// <summary>Y14 阿马洛克：环绕轨道成寒霜领域，冰雾带减速 12%</summary>
    internal sealed class GsAmarokYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Amarok;
        internal override int Tier => 3;
        internal override float DamageMul => 1.05f;
        internal override Color GlowColor => new(150, 220, 255);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; the orbit trails a ring of frost mist that slows enemies inside by 12%";

        internal override void OnOrbitTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            Player owner = Main.player[proj.owner];
            float r = GsYoyoCommandLayer.OrbitRadius(this, proj);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                //环带内敌减速（服务器权威）
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.friendly || !npc.CanBeChasedBy(proj)) {
                        continue;
                    }
                    float dist = npc.Distance(owner.Center);
                    if (dist < r + 46f) {
                        npc.position -= npc.velocity * 0.12f;
                    }
                }
            }
            if (!VaultUtils.isServer && st.SigTimer++ % 8 == 0) {
                //轨道冰雾（该粒子原生支持轨道运动）
                PRTLoader.NewParticle<PRT_DefCryoMist>(proj.Center, Vector2.Zero, GlowColor,
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(24, 36), owner.Center, r);
            }
        }
    }

    /// <summary>Y15 代码2：折返分裂平行镜像轨迹（40px 错位实体残影，50% 伤害）</summary>
    internal sealed class GsCode2Yoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Code2;
        internal override int Tier => 3;
        internal override float DamageMul => 1.05f;
        internal override Color GlowColor => new(90, 220, 235);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; the lash splits a parallel mirror track 40px aside, dealing 50% damage";

        internal override void OnLashBeginOwner(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            Player owner = Main.player[proj.owner];
            Vector2 to = owner.Center - proj.Center;
            float dist = to.Length();
            if (dist < 60f) {
                return;
            }
            Vector2 dir = to / dist;
            float speed = MathF.Min(MathF.Max(ProjectileID.Sets.YoyosTopSpeed[proj.type], 8f) * LashSpeedMul, 40f);
            int life = (int)(dist / speed) + 8;
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2) * 40f;
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center + normal, dir * speed,
                ModContent.ProjectileType<GsYoyoMirrorProj>(), Math.Max(1, (int)(proj.damage * 0.50f)),
                proj.knockBack * 0.5f, proj.owner, proj.type, life);
        }
    }

    /// <summary>Y16 叶列茨：驻场每 6 hit 甩出一圈 8 粒孢子小弹（各 40%）</summary>
    internal sealed class GsYeletsYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Yelets;
        internal override int Tier => 3;
        internal override float DamageMul => 1.05f;
        internal override Color GlowColor => new(150, 230, 90);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; every 6 anchored hits fling a ring of 8 spores, each dealing 40% damage";

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode != GsYoyoMode.Anchor && mode != GsYoyoMode.Path) {
                return;
            }
            if (++st.SigCount < 6) {
                return;
            }
            st.SigCount = 0;
            int pellet = ModContent.ProjectileType<GsYoyoPelletProj>();
            //上一波清完再放下一波，在场恒不超 8
            if (Main.player[proj.owner].ownedProjectileCounts[pellet] > 0) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 8f).ToRotationVector2();
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, dir * 6f, pellet,
                    Math.Max(1, (int)(proj.damage * 0.40f)), 0f, proj.owner, GsYoyoPelletProj.StyleSpore);
            }
        }
    }

    /// <summary>Y17 红的投掷：T3 基准数值，折返命中爆红色像素方块（开发者彩蛋）</summary>
    internal sealed class GsRedsThrowYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.RedsYoyo;
        internal override int Tier => 3;
        internal override float DamageMul => 1.04f;
        internal override Color GlowColor => new(235, 60, 60);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; lash hits burst into red pixels";

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode != GsYoyoMode.Lash || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f), GlowColor, Main.rand.NextFloat(0.5f, 0.8f))?
                    .Configure(GlowColor, Main.rand.Next(12, 20));
            }
        }
    }

    /// <summary>Y18 女武神悠悠球：环绕轨道假 3D 倾斜化（纯绘制层透视，判定仍平面圆）</summary>
    internal sealed class GsValkyrieYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.ValkyrieYoyo;
        internal override int Tier => 3;
        internal override float DamageMul => 1.04f;
        internal override Color GlowColor => new(245, 235, 200);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; the orbit tilts into a valkyrie's gyre, shedding feather light on hits";

        internal override void OnCommandHit(Projectile proj, NPC target, in NPC.HitInfo hit, GsYoyoState st, int mode, GodSmithProjRouter router) {
            if (mode != GsYoyoMode.Orbit || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f), GlowColor, 0.14f)?.Configure(10, 0.7f);
            }
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (effMode != GsYoyoMode.Orbit) {
                return;
            }
            //透视暗示：椭圆压扁的轨道残像（近下大而亮，远上小而暗），球体位置不动保证线绳与判定一致
            Player owner = Main.player[proj.owner];
            float r = GsYoyoCommandLayer.OrbitRadius(this, proj);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            for (int i = 1; i <= 6; i++) {
                float phase = st.SpinPhase - i * 0.22f;
                float depth = 0.5f + 0.5f * MathF.Sin(phase);   //0 远 1 近
                Vector2 pos = owner.Center + new Vector2(MathF.Cos(phase) * r, MathF.Sin(phase) * r * 0.5f);
                Color c = GlowColor * ((0.28f - i * 0.035f) * (0.45f + 0.55f * depth));
                c.A = 0;
                Main.EntitySpriteDraw(glow, pos - Main.screenPosition, null, c, 0f,
                    glow.Size() / 2f, 0.2f + 0.16f * depth, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>Y19 挪威海妖：驻场「触渊」，向最近两敌各伸一条墨色触须持续撕扯</summary>
    internal sealed class GsKrakenYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Kraken;
        internal override int Tier => 3;
        internal override float DamageMul => 1.03f;
        internal override Color GlowColor => new(60, 140, 110);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; while anchored, inky tendrils lash the two nearest foes for 30% damage per pulse\nInfinite spin time makes it a true siege core";

        /// <summary>触须打击半径</summary>
        private const float TendrilRange = 340f;

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            //30f 一拍：重选最近两敌并结算（选择规则各端一致，判定只在权威端）
            if (++st.SigTimer % 30 != 0) {
                return;
            }
            st.SigTarget = st.SigTarget2 = -1;
            float best = TendrilRange, best2 = TendrilRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || !npc.CanBeChasedBy(proj)) {
                    continue;
                }
                float dist = npc.Distance(proj.Center);
                if (dist < best) {
                    best2 = best;
                    st.SigTarget2 = st.SigTarget;
                    best = dist;
                    st.SigTarget = npc.whoAmI;
                }
                else if (dist < best2) {
                    best2 = dist;
                    st.SigTarget2 = npc.whoAmI;
                }
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            //权威端结算触须撕扯（SimpleStrikeNPC 服务器调用自带全端广播）
            StrikeTendril(proj, st.SigTarget);
            StrikeTendril(proj, st.SigTarget2);
        }

        private static void StrikeTendril(Projectile proj, int whoAmI) {
            if (whoAmI < 0) {
                return;
            }
            NPC npc = Main.npc[whoAmI];
            if (!npc.active || npc.friendly) {
                return;
            }
            int dir = npc.Center.X >= proj.Center.X ? 1 : -1;
            npc.SimpleStrikeNPC(Math.Max(1, (int)(proj.damage * 0.30f)), dir, false, 0f, null, true, 0f, true);
        }

        internal override void OnCommandDraw(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode, float heatRatio) {
            if (effMode != GsYoyoMode.Anchor) {
                return;
            }
            DrawTendril(proj, st.SigTarget, 0f);
            DrawTendril(proj, st.SigTarget2, 2.6f);
        }

        /// <summary>墨色触须：八段折线蠕动（identity 定相），暗体线 + 微绿加色缘</summary>
        private static void DrawTendril(Projectile proj, int whoAmI, float phaseSeed) {
            if (whoAmI < 0) {
                return;
            }
            NPC npc = Main.npc[whoAmI];
            if (!npc.active || npc.Distance(proj.Center) > TendrilRange + 60f) {
                return;
            }
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 from = proj.Center;
            Vector2 to = npc.Center;
            Vector2 axis = to - from;
            Vector2 normal = axis.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            const int segs = 8;
            Vector2 prev = from;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                //端点钉死，中段蠕动
                float wave = MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f + i * 0.9f + proj.identity * 0.7f + phaseSeed)
                    * 9f * MathF.Sin(t * MathHelper.Pi);
                Vector2 cur = from + axis * t + normal * wave;
                Vector2 mid = (prev + cur) / 2f;
                float rot = (cur - prev).ToRotation();
                float lenScale = Vector2.Distance(prev, cur) / line.Width;
                Main.EntitySpriteDraw(line, mid - Main.screenPosition, null, new Color(16, 42, 34) * 0.85f,
                    rot, line.Size() / 2f, new Vector2(lenScale, 0.24f), SpriteEffects.None, 0);
                Color edge = new Color(70, 170, 130) * 0.30f;
                edge.A = 0;
                Main.EntitySpriteDraw(line, mid - Main.screenPosition, null, edge,
                    rot, line.Size() / 2f, new Vector2(lenScale, 0.34f), SpriteEffects.None, 0);
                prev = cur;
            }
        }
    }

    /// <summary>Y20 克苏鲁之眼：驻场盯防喷血泪，折返化作放大冲撞（1.6 倍）</summary>
    internal sealed class GsEyeOfCthulhuYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.TheEyeOfCthulhu;
        internal override int Tier => 3;
        internal override float DamageMul => 1.03f;
        internal override float LashDamageMul => 1.6f;
        internal override Color GlowColor => new(200, 40, 40);
        protected override string GsDescFallback =>
            "Reforged: all three commands unlocked; the anchor stares down the nearest foe, spitting 3 blood tears every 2s\nThe lash becomes a ramming charge dealing 160% damage";

        internal override void OnGlobalTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st, int effMode) {
            //冲撞形态：折返期球体放大（各端由同步模式驱动，纯绘制量）
            proj.scale = effMode == GsYoyoMode.Lash ? 1.3f : 1f;
        }

        internal override void OnAnchorTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (!proj.IsOwnedByLocalPlayer() || ++st.SigTimer < 120) {
                return;
            }
            st.SigTimer = 0;
            NPC target = proj.Center.FindClosestNPC(600f);
            if (target == null) {
                return;
            }
            int pellet = ModContent.ProjectileType<GsYoyoPelletProj>();
            if (Main.player[proj.owner].ownedProjectileCounts[pellet] >= 6) {
                return;
            }
            Vector2 dir = (target.Center - proj.Center).SafeNormalize(Vector2.UnitX);
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = dir.RotatedBy(i * MathHelper.ToRadians(7.5f)) * 9f;
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, vel, pellet,
                    Math.Max(1, (int)(proj.damage * 0.25f)), 0f, proj.owner, GsYoyoPelletProj.StyleBloodTear);
            }
        }

        internal override void OnLashTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (VaultUtils.isServer || st.LashTimer % 2 != 0) {
                return;
            }
            //冲撞血雾尾
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(proj.Center + Main.rand.NextVector2Circular(8f, 8f),
                -proj.velocity * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f), GlowColor,
                Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 16));
        }
    }

    //================================================================ T4：路径编程 ================================================================

    /// <summary>
    /// Y21 泰拉悠悠球：全指令 + 路径编程。右键连点标记至多 3 个路径点，
    /// 球按序贝塞尔巡回；巡回中绿珠发射率翻倍（原版绿珠照发，巡回补射同款等效实现）；
    /// 招牌绿珠与 ∞ 时限完整保留
    /// </summary>
    internal sealed class GsTerrarianYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Terrarian;
        internal override int Tier => 4;
        internal override float DamageMul => 1.0f;
        internal override int PathPoints => 3;
        internal override Color GlowColor => new(150, 255, 120);
        protected override string GsDescFallback =>
            "Reforged: all commands unlocked, plus Path Programming\nRight click to chart up to 3 waypoints; the yoyo patrols the bezier route and its beam rate doubles while patrolling\nClick again when full to clear the route";

        /// <summary>巡回补射间隔（帧），对齐原版绿珠观测率的等效翻倍</summary>
        private const int BeamInterval = 20;

        internal override void OnPathTick(Projectile proj, GodSmithProjRouter router, GsYoyoState st) {
            if (!proj.IsOwnedByLocalPlayer() || ++st.SigTimer < BeamInterval) {
                return;
            }
            st.SigTimer = 0;
            NPC target = proj.Center.FindClosestNPC(600f);
            if (target == null) {
                return;
            }
            //补射同款绿珠：原版 AI 自身的发射照常跑，两者叠加即发射率翻倍
            Vector2 vel = (target.Center - proj.Center).SafeNormalize(Vector2.UnitX) * 14f;
            Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, vel, ProjectileID.TerrarianBeam,
                proj.damage, proj.knockBack, proj.owner);
        }
    }
}

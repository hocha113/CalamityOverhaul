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
    /// 蘑菇矿三盔共用的子族骨架：只吃远程弹幕命中（材质=荧光菌蓝的军工菌械）；
    /// 三顶头盔各绑一条签名神赋（箭=孢子箭塔 / 弹=真菌链爆 / 火箭=菌毯空投），同胸同腿按头盔分流
    /// </summary>
    internal abstract class GsShroomiteArmorScheme : GsArmorsBChargeScheme
    {
        public override int BodyID => ItemID.ShroomiteBreastplate;

        public override int LegsID => ItemID.ShroomiteLeggings;

        //荧光菌蓝色板
        internal static readonly Color ShroomBright = new(172, 236, 255);
        internal static readonly Color ShroomBlue = new(82, 172, 255);
        internal static readonly Color ShroomDeep = new(30, 72, 152);

        protected override Color ThemeMain => ShroomBlue;

        protected override Color ThemeBright => ShroomBright;

        protected sealed override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsShroomiteSporeTurretProj>()
            || proj.type == ModContent.ProjectileType<GsShroomiteSporeArrowProj>()
            || proj.type == ModContent.ProjectileType<GsShroomiteChainBloomProj>()
            || proj.type == ModContent.ProjectileType<GsShroomiteMortarProj>();

        public sealed override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //菌械只认远程弹药：远程弹幕命中才积攒/触发
            if (sourceProj == null || !sourceProj.CountsAsClass(DamageClass.Ranged)) {
                return;
            }
            base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
        }
    }

    /// <summary>
    /// 【蘑菇矿·孢箭盔（箭）★A】荧菌军工的苗圃战术：①远程命中积攒孢种，满六层后下一箭在命中处
    /// 种下孢子箭塔 ②塔驻五秒，向最近敌自动连射孢子箭 ③塔谢时菌盖凋散。
    /// 原版套装奖励（潜伏隐身与箭矢强化）保留，神赋叠加
    /// </summary>
    internal class GsShroomiteArrowArmor : GsShroomiteArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShroomiteHeadgear];

        protected override string EndowLineFallback =>
            "Spore Nursery: ranged hits build spores; at 6 stacks the next hit plants a spore turret that snipes the nearest foe with fungal arrows for 5s";

        protected override int FullCharge => 6;

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.2f }, target.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int arrowDamage = Math.Clamp((int)(damageDone * 0.35f), 8, 140);
            //落点探地：命中点向下最多 8 格找立足，找不到就悬浮
            Vector2 plant = target.Center;
            Point tile = plant.ToTileCoordinates();
            for (int dy = 0; dy < 8; dy++) {
                Point at = new(tile.X, tile.Y + dy);
                if (!WorldGen.InWorld(at.X, at.Y, 10)) {
                    break;
                }
                Tile t = Framing.GetTileSafely(at.X, at.Y);
                if (t.HasTile && Main.tileSolid[t.TileType]) {
                    plant = new Vector2(at.X * 16f + 8f, at.Y * 16f - 20f);
                    break;
                }
            }
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithShroomiteEndow"),
                plant, Vector2.Zero,
                ModContent.ProjectileType<GsShroomiteSporeTurretProj>(),
                arrowDamage, 1f, player.whoAmI);
        }
    }

    /// <summary>
    /// 【蘑菇矿·菌爆盔（弹）★A】荧菌军工的链爆战术：①远程命中积攒菌丝，满八层立即自最后命中点
    /// 引爆真菌链爆 ②爆心逐跳蔓延至多三段，一段比一段大 ③每段爆后余留荧孢飘散。
    /// 原版套装奖励保留，神赋叠加
    /// </summary>
    internal class GsShroomiteBulletArmor : GsShroomiteArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShroomiteMask];

        protected override string EndowLineFallback =>
            "Mycelium Chain: ranged hits build mycelium; at 8 stacks the strike point erupts into a fungal blast that chain-jumps to nearby foes up to 3 times, growing each jump";

        protected override int FullCharge => 8;

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int blastDamage = Math.Clamp((int)(damageDone * 0.45f), 10, 150);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithShroomiteEndow"),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsShroomiteChainBloomProj>(),
                blastDamage, 4f, player.whoAmI, 0f, 0f, 0f);
        }
    }

    /// <summary>
    /// 【蘑菇矿·菌轰盔（火箭）★A】荧菌军工的空投战术：①远程命中积攒菌雷，满五层后下一击
    /// 呼叫三枚孢子迫击弹自天而降错拍轰击目标 ②着弹炸开菌云 ③弹体坠落拖荧菌烟。
    /// 原版套装奖励保留，神赋叠加
    /// </summary>
    internal class GsShroomiteRocketArmor : GsShroomiteArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShroomiteHelmet];

        protected override string EndowLineFallback =>
            "Sporefall Barrage: ranged hits build charges; at 5 stacks the next hit calls three spore mortars crashing down on the target in a staggered barrage";

        protected override int FullCharge => 5;

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.55f, Pitch = -0.5f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int mortarDamage = Math.Clamp((int)(damageDone * 0.50f), 12, 170);
            for (int i = 0; i < 3; i++) {
                Vector2 from = target.Center + new Vector2(Main.rand.NextFloat(-70f, 70f), -Main.rand.NextFloat(300f, 360f));
                Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitY) * 3f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithShroomiteEndow"),
                    from, vel, ModContent.ProjectileType<GsShroomiteMortarProj>(),
                    mortarDamage, 5f, player.whoAmI, 0f, target.whoAmI, i * 8f);
            }
        }
    }

    /// <summary>
    /// 孢子箭塔：命中处拔地而生的荧菌箭塔，菌柄托举发光菌盖；
    /// 生长十二帧后开火，向最近敌每 30 帧射一支孢子箭，谢幕时菌盖凋散
    /// </summary>
    internal class GsShroomiteSporeTurretProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.7873f % 3.61f;

        /// <summary>生长帧数</summary>
        private const int GrowFrames = 12;

        /// <summary>射击周期</summary>
        private const int FireInterval = 30;

        private float VisualFade => MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>塔体不撞人，孢子箭才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //开火（佩戴者端裁定）
            if (Projectile.owner == Main.myPlayer && Life > GrowFrames && Life % FireInterval == 0) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 muzzle = Projectile.Center - new Vector2(0f, 14f);
                    Vector2 vel = (target.Center + target.velocity * 6f - muzzle).SafeNormalize(Vector2.UnitX) * 15f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        muzzle, vel, ModContent.ProjectileType<GsShroomiteSporeArrowProj>(),
                        Projectile.damage, 1f, Projectile.owner);
                    Projectile.netUpdate = true;
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, muzzle);
                    }
                }
            }

            //菌盖孢尘（客户端装饰）
            if (!Main.dedServ && Life > GrowFrames && Main.rand.NextBool(8)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), -16f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                    GsShroomiteArmorScheme.ShroomBright, Main.rand.NextFloat(0.18f, 0.3f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, GsShroomiteArmorScheme.ShroomBlue.ToVector3() * (0.3f * VisualFade));
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

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //菌盖凋散
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(0f, -12f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    Main.rand.NextBool() ? GsShroomiteArmorScheme.ShroomBright : GsShroomiteArmorScheme.ShroomBlue,
                    Main.rand.NextFloat(0.25f, 0.42f))?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        //==================== 绘制：菌柄 + 发光菌盖 + 生长动画 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (core == null || crescent == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            //生长：自地拔起 + 纵向舒展
            float grow = MathHelper.Clamp(Life / GrowFrames, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            Vector2 basePos = Projectile.Center + new Vector2(0f, 20f) - Main.screenPosition;
            //开火脉冲：临近射击拍菌盖增亮
            float firePulse = Life % FireInterval > FireInterval - 6 ? (Life % FireInterval - (FireInterval - 6)) / 6f : 0f;
            float breathe = 1f + MathF.Sin(Life * 0.08f + Seed * 3f) * 0.05f;

            //菌柄（真 alpha 淡蓝柱）
            Main.EntitySpriteDraw(core, basePos - new Vector2(0f, 14f * grow), null,
                GsShroomiteArmorScheme.ShroomDeep * (0.85f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.07f, 0.20f * grow), SpriteEffects.None, 0);
            //菌盖（月牙拱面，发光）
            Vector2 capPos = basePos - new Vector2(0f, 30f * grow);
            Main.EntitySpriteDraw(crescent, capPos, null,
                (GsShroomiteArmorScheme.ShroomBlue with { A = 0 }) * (0.9f * fade), -MathHelper.PiOver2, crescent.Size() * 0.5f,
                new Vector2(0.14f, 0.10f) * grow * breathe, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, capPos, null,
                (GsShroomiteArmorScheme.ShroomBright with { A = 0 }) * ((0.55f + firePulse * 0.4f) * fade), -MathHelper.PiOver2, crescent.Size() * 0.5f,
                new Vector2(0.10f, 0.06f) * grow * breathe, SpriteEffects.None, 0);
            //盖下荧光
            Main.EntitySpriteDraw(glow, capPos + new Vector2(0f, 6f * grow), null,
                (GsShroomiteArmorScheme.ShroomBlue with { A = 0 }) * ((0.4f + firePulse * 0.3f) * fade), 0f, glow.Size() * 0.5f,
                0.4f * grow, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 孢子箭：箭塔射出的荧菌之箭，箭体蓝芒三层 + 孢尾，命中迸开荧孢
    /// </summary>
    internal class GsShroomiteSporeArrowProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //轻微坠弧
            Projectile.velocity.Y += 0.08f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f, GsShroomiteArmorScheme.ShroomBright,
                    Main.rand.NextFloat(0.16f, 0.28f))?.Configure(false, Main.rand.Next(6, 11));
            }
            Lighting.AddLight(Projectile.Center, GsShroomiteArmorScheme.ShroomBlue.ToVector3() * 0.18f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    Main.rand.NextBool() ? GsShroomiteArmorScheme.ShroomBright : GsShroomiteArmorScheme.ShroomBlue,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        //==================== 绘制：蓝芒箭体三层 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = shot.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.4f);

            Main.EntitySpriteDraw(shot, pos, null,
                (GsShroomiteArmorScheme.ShroomDeep with { A = 0 }) * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.26f + stretch, 0.08f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, pos, null,
                (GsShroomiteArmorScheme.ShroomBlue with { A = 0 }) * fade, Projectile.rotation, origin,
                new Vector2(0.21f + stretch * 0.8f, 0.05f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, pos, null,
                (GsShroomiteArmorScheme.ShroomBright with { A = 0 }) * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.16f + stretch * 0.5f, 0.025f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 真菌链爆：一次逐跳蔓延的荧菌爆裂，涨-顶-散三相；
    /// 爆后向最近敌再跳一段（至多三段，一段比一段大），余留荧孢飘散
    /// </summary>
    internal class GsShroomiteChainBloomProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>跳段序号 0~2</summary>
        private ref float JumpIndex => ref Projectile.ai[2];

        /// <summary>下一跳是否已派发</summary>
        private ref float NextSpawned => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.6491f % 3.07f;

        /// <summary>涨爆帧数</summary>
        private const int SwellFrames = 10;

        /// <summary>总时长</summary>
        private const int TotalFrames = 26;

        private float JumpScale => 1f + JumpIndex * 0.3f;

        private float BlastRadius => 74f * JumpScale;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>顶相判定窗</summary>
        public override bool? CanDamage() => Life >= SwellFrames && Life < SwellFrames + 6;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2())
                < BlastRadius + targetHitbox.Width * 0.25f;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            if (Life == SwellFrames && !Main.dedServ) {
                //顶相：爆响 + 荧孢四散
                SoundEngine.PlaySound(SoundID.Item14 with {
                    Volume = 0.4f + 0.12f * JumpIndex,
                    Pitch = 0.4f - 0.25f * JumpIndex,
                    MaxInstances = 3
                }, Projectile.Center);
                for (int i = 0; i < 8 + (int)JumpIndex * 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f) * JumpScale,
                        Main.rand.NextBool() ? GsShroomiteArmorScheme.ShroomBright : GsShroomiteArmorScheme.ShroomBlue,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(16, 28));
                }
            }

            //派发下一跳（佩戴者端）
            if (NextSpawned == 0f && Life >= SwellFrames + 6 && JumpIndex < 2f) {
                NextSpawned = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    NPC next = FindNext();
                    if (next != null) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            next.Center, Vector2.Zero, Projectile.type,
                            (int)(Projectile.damage * 1.15f), Projectile.knockBack, Projectile.owner,
                            0f, 0f, JumpIndex + 1f);
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsShroomiteArmorScheme.ShroomBlue.ToVector3() * (0.5f * JumpScale));
        }

        private NPC FindNext() {
            NPC best = null;
            float bestDist = 280f * JumpScale;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                //跳出当前爆心，别在原地重爆
                if (dist > BlastRadius * 0.5f && dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        //==================== 绘制：涨-顶-散菌爆 + 迸开的迷你菌盖 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (core == null || ring == null || crescent == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //三相包络：涨（0~10）顶（10~16）散（16~26）
            float swell = MathHelper.Clamp(Life / SwellFrames, 0f, 1f);
            float burst = MathHelper.Clamp((Life - SwellFrames) / 6f, 0f, 1f);
            float decay = 1f - MathHelper.Clamp((Life - SwellFrames - 6f) / 10f, 0f, 1f);
            float radius = BlastRadius * (0.4f + 0.6f * swell + 0.25f * burst);

            //菌云本体
            Main.EntitySpriteDraw(core, pos, null,
                (GsShroomiteArmorScheme.ShroomDeep with { A = 0 }) * (0.7f * decay), Seed, core.Size() * 0.5f,
                radius * 2.6f / core.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                (GsShroomiteArmorScheme.ShroomBlue with { A = 0 }) * (0.85f * decay), -Seed, core.Size() * 0.5f,
                radius * 2.0f / core.Width, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                (GsShroomiteArmorScheme.ShroomBright with { A = 0 }) * ((0.6f + burst * 0.4f) * decay), 0f, core.Size() * 0.5f,
                radius * 1.2f / core.Width, SpriteEffects.None, 0);
            //爆环
            if (burst > 0f) {
                Main.EntitySpriteDraw(ring, pos, null,
                    (GsShroomiteArmorScheme.ShroomBright with { A = 0 }) * ((1f - burst) * 0.8f), 0f, ring.Size() * 0.5f,
                    radius * 2.4f * (0.6f + burst * 0.6f) / ring.Width, SpriteEffects.None, 0);
                //迸开的迷你菌盖（三顶，随爆散射）
                for (int i = 0; i < 3; i++) {
                    float ang = Seed * 4f + MathHelper.TwoPi * i / 3f;
                    Vector2 capAt = pos + ang.ToRotationVector2() * radius * burst * 0.8f;
                    Main.EntitySpriteDraw(crescent, capAt, null,
                        (GsShroomiteArmorScheme.ShroomBlue with { A = 0 }) * ((1f - burst * 0.6f) * decay),
                        -MathHelper.PiOver2 + ang * 0.2f, crescent.Size() * 0.5f,
                        new Vector2(0.06f, 0.04f) * JumpScale, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 孢子迫击弹：自天而降的荧菌炮弹，弹头蓝芒 + 坠落菌烟，触地/触敌涨爆菌云
    /// </summary>
    internal class GsShroomiteMortarProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private ref float TargetIndex => ref Projectile.ai[1];

        /// <summary>错拍延迟帧（悬停蓄势后再坠）</summary>
        private ref float HoldFrames => ref Projectile.ai[2];

        /// <summary>1=已入爆炸态</summary>
        private ref float Exploding => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.9203f % 4.13f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            if (Exploding == 1f) {
                Projectile.velocity = Vector2.Zero;
                return;
            }

            //错拍：悬停蓄势
            if (Life < HoldFrames) {
                Projectile.position -= Projectile.velocity;
                return;
            }

            //坠落：加速 + 微调横向咬准
            NPC target = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
            if (target != null && target.active) {
                float wantX = MathHelper.Clamp((target.Center.X - Projectile.Center.X) * 0.03f, -1f, 1f);
                Projectile.velocity.X += wantX * 0.3f;
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.55f, 18f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Life % 2 == 0) {
                //坠落菌烟
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.04f, GsShroomiteArmorScheme.ShroomDeep,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(16, 0.3f, 0.03f);
            }
            Lighting.AddLight(Projectile.Center, GsShroomiteArmorScheme.ShroomBlue.ToVector3() * (0.25f * VisualFade));
        }

        private void StartExplosion() {
            if (Exploding == 1f) {
                return;
            }
            Exploding = 1f;
            Projectile.velocity = Vector2.Zero;
            //爆窗：扩容命中盒吃满爆心
            Projectile.Resize(130, 130);
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 5);
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool() ? GsShroomiteArmorScheme.ShroomBright : GsShroomiteArmorScheme.ShroomBlue,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f),
                    GsShroomiteArmorScheme.ShroomBlue, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(22, 0.4f, 0.04f);
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsShroomiteArmorScheme.ShroomBright, 0.2f)?.Configure(10, 0.8f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            StartExplosion();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => StartExplosion();

        //==================== 绘制：弹头蓝芒 + 悬停蓄势闪 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (core == null || shot == null) {
                return false;
            }
            if (Exploding == 1f) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            bool holding = Life < HoldFrames;
            //蓄势闪烁（identity 相位）
            float pulse = holding ? 0.6f + MathF.Sin(Life * 0.9f + Seed * 5f) * 0.4f : 1f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.5f);
            float rot = holding ? MathHelper.PiOver2 : Projectile.rotation;

            //弹体
            Main.EntitySpriteDraw(shot, pos, null,
                (GsShroomiteArmorScheme.ShroomDeep with { A = 0 }) * (0.9f * fade * pulse), rot, shot.Size() * 0.5f,
                new Vector2(0.20f + stretch, 0.09f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, pos, null,
                (GsShroomiteArmorScheme.ShroomBlue with { A = 0 }) * (fade * pulse), rot, shot.Size() * 0.5f,
                new Vector2(0.16f + stretch * 0.7f, 0.055f), SpriteEffects.None, 0);
            //弹头荧核
            Main.EntitySpriteDraw(core, pos + rot.ToRotationVector2() * 8f, null,
                (GsShroomiteArmorScheme.ShroomBright with { A = 0 }) * (0.85f * fade * pulse), 0f, core.Size() * 0.5f,
                0.06f, SpriteEffects.None, 0);
            return false;
        }
    }
}

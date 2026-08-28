using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.TideReapers
{
    /// <summary>
    /// 镰渊，深渊飞镰。左键掷出自旋镰刀回旋归手，飞行途中甩出追猎的镰渊新月；
    /// 右键掷出漩涡锚，镰刀钉在前方旋成漩涡，拖拽敌人并加速放波，随后返回。
    /// 飞行体在 <see cref="TideReaperThrown"/>，新月在 <see cref="TideReaperWave"/>
    /// </summary>
    internal class TideReaper : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "TideReaper";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 54;
            Item.height = 66;
            Item.damage = 76;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 26;
            Item.shootSpeed = 14.5f;
            Item.knockBack = 6f;
            Item.crit = 6;
            Item.value = Item.sellPrice(0, 11);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<TideReaperThrown>();
        }

        public override bool MeleePrefix() => true;

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                //漩涡锚要求场上无镰
                return player.ownedProjectileCounts[Item.shoot] == 0;
            }
            //常规掷最多两把在空中
            return player.ownedProjectileCounts[Item.shoot] <= 1;
        }

        public override float UseSpeedMultiplier(Player player)
            => player.altFunctionUse == 2 ? 0.6f : 1f;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool anchor = player.altFunctionUse == 2;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = anchor ? -0.2f : 0.15f, Volume = 0.7f }, position);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.3f, Volume = 0.5f }, position);
            Projectile.NewProjectile(source, position, velocity * (anchor ? 1.15f : 1f), type
                , damage, knockback, player.whoAmI, ai2: anchor ? 1f : 0f);
            return false;
        }

        public override void AddRecipes() {
            if (CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0 && CWRID.Item_DepthCells > 0) {
                CreateRecipe().
                    AddIngredient<SeaShrimpShell>(8).
                    AddIngredient(CWRID.Item_Lumenyl, 9).
                    AddIngredient(CWRID.Item_DepthCells, 11).
                    AddIngredient(CWRID.Item_Voidstone, 15).
                    AddIngredient(ItemID.ChlorophyteBar, 7).
                    AddTile(TileID.MythrilAnvil).
                    Register();
                return;
            }
            CreateRecipe().
                AddIngredient<SeaShrimpShell>(8).
                AddIngredient(ItemID.ChlorophyteBar, 12).
                AddIngredient(ItemID.SharkFin, 7).
                AddIngredient(ItemID.SoulofFright, 8).
                AddTile(TileID.MythrilAnvil).
                Register();
        }
    }

    /// <summary>
    /// 镰渊飞行体。ai[2] 0 普通回旋 / 1 漩涡锚，ai[0] 计时。
    /// 普通：抛出减速+末段轻追踪 → 高速回收，途中定期甩新月；
    /// 锚：短抛后钉住自旋 <see cref="AnchorTime"/> 帧，拖拽敌人并密集放波，再回收。
    /// 锚点由固定飞行时长推出，不依赖 owner 本地光标，各端一致
    /// </summary>
    internal class TideReaperThrown : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "TideReaper";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<TideReaper>();

        private bool IsAnchor => Projectile.ai[2] > 0.5f;
        private float Timer { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }

        private const int OutboundTime = 55;
        private const int AnchorFlight = 24;
        private const int AnchorTime = 70;
        private const int WaveInterval = 14;
        private const int AnchorWaveInterval = 8;
        private const float ReturnSpeed = 26f;
        private const float MaxStraySqr = 1500f * 1500f;

        private bool playedReturnSound;

        /// <summary>普通掷已进入回收</summary>
        private bool Returning => IsAnchor
            ? Timer >= AnchorFlight + AnchorTime
            : Timer >= OutboundTime;
        /// <summary>锚定中</summary>
        private bool Anchored => IsAnchor && Timer >= AnchorFlight && Timer < AnchorFlight + AnchorTime;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            if (IsAnchor) {
                Projectile.scale = 1.15f;
            }
            if (!Main.dedServ) {
                Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center
                        , forward.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2.5f, 6f)
                        , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.3f, 0.5f))
                        .Configure(12, 1.4f);
                }
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Timer++;

            //自旋:飞行随速度,锚定恒速快旋
            float spinSign = Projectile.velocity.X >= 0 || Anchored ? 1f : -1f;
            float spin = Anchored ? 0.45f : Projectile.velocity.Length() * 0.032f;
            Projectile.rotation += spin * spinSign;

            if (Returning || Projectile.DistanceSQ(Owner.Center) > MaxStraySqr) {
                ReturnAI();
            }
            else if (Anchored) {
                AnchorAI();
            }
            else {
                OutboundAI();
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.32f, 0.42f);

            if (!Main.dedServ && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , AbyssrendFX.Body, Main.rand.NextFloat(0.22f, 0.4f))
                    .Configure(10, 1.3f);
            }
        }

        /// <summary>外抛:减速,普通掷后半段轻追踪并按节奏放波</summary>
        private void OutboundAI() {
            Projectile.velocity *= IsAnchor ? 0.97f : 0.985f;

            if (!IsAnchor) {
                if (Timer > OutboundTime * 0.5f) {
                    NPC target = Projectile.Center.FindClosestNPC(420f);
                    if (target != null) {
                        Projectile.SmoothHomingBehavior(target.Center, 1.03f, 0.07f);
                    }
                }
                if (Timer % WaveInterval == 0) {
                    FlingWave(0.45f);
                }
            }
        }

        /// <summary>锚定:钉住快旋,拖拽敌人,密集放波</summary>
        private void AnchorAI() {
            Projectile.velocity *= 0.72f;

            if ((int)Timer == AnchorFlight + 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item96 with { Pitch = 0.4f, Volume = 0.55f }, Projectile.Center);
            }

            //向心拖拽,NPC 权威端结算
            if (!VaultUtils.isClient) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.friendly || npc.knockBackResist <= 0f || npc.immortal) {
                        continue;
                    }
                    float dist = npc.Distance(Projectile.Center);
                    if (dist > 170f || dist < 14f) {
                        continue;
                    }
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 0.45f * npc.knockBackResist;
                }
            }

            if (Timer % AnchorWaveInterval == 0) {
                FlingWave(0.4f);
            }
        }

        /// <summary>回收:加速追手,贴身销毁</summary>
        private void ReturnAI() {
            if (!playedReturnSound) {
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.1f, Volume = 0.45f }, Projectile.Center);
                playedReturnSound = true;
            }
            Vector2 toOwner = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            float chaseSpeed = ReturnSpeed + Timer * 0.03f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOwner * chaseSpeed, 0.16f);
            if (Projectile.Distance(Owner.Center) < 42f) {
                Projectile.Kill();
            }
        }

        /// <summary>甩出一道镰渊新月,优先朝最近敌人</summary>
        private void FlingWave(float damageMul) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            NPC target = Projectile.Center.FindClosestNPC(520f);
            Vector2 dir = target != null
                ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                : Projectile.rotation.ToRotationVector2();
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 10.5f
                , ModContent.ProjectileType<TideReaperWave>()
                , Math.Max((int)(Projectile.damage * damageMul), 1), Projectile.knockBack * 0.4f, Projectile.owner);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 180);
            target.AddBuff(ModContent.BuffType<AbyssalPressure>(), 90);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.3f, Volume = 0.5f }, target.Center);
            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 2.2f);
            }
            if (!Main.dedServ) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_AbyssGlob>(target.Center
                        , Main.rand.NextVector2Circular(5f, 5f)
                        , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.3f, 0.55f))
                        .Configure(13, 1.4f);
                }
                PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center, Main.rand.NextVector2Circular(4f, 4f)
                    , AbyssrendFX.Cyan, 1f).Configure(10);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureValue;
            Vector2 origin = texture.Size() / 2f;
            //贴图刃口朝右上对角,加 PiOver4 让自旋时刃锋领先
            float drawRot = Projectile.rotation + MathHelper.PiOver4;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float prog = 1f - k / (float)Projectile.oldPos.Length;
                Color trailColor = new Color(AbyssrendFX.Cyan.R, AbyssrendFX.Cyan.G, AbyssrendFX.Cyan.B, 0)
                    * (prog * (Anchored ? 0.5f : 0.38f));
                Vector2 trailPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor
                    , drawRot - k * 0.12f, origin, Projectile.scale * MathHelper.Lerp(0.62f, 1f, prog), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor
                , drawRot, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}

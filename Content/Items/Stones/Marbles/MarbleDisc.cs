using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>
    /// 大理石飞盘：弹射链击回旋镖。在敌人与墙壁间弹射，每次弹射至新目标伤害递增，
    /// 弹射耗尽后回旋归手，可同时存在两枚
    /// </summary>
    internal class MarbleDisc : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 16;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarbleDiscProj>();
            Item.shootSpeed = 14f;
            Item.value = Item.sellPrice(0, 0, 65, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<MarbleDiscProj>()] < 2;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 18)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 自由飞行的回旋镖弹体（非手持体，不走 BaseHeldProj）：
    /// 掷出→减速→回手骨架上叠加链击改向，链击层数驱动伤害与金光成长
    /// </summary>
    internal class MarbleDiscProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleDisc";

        //弹射总预算（墙壁+敌人合计），耗尽即强制回手
        private const int MaxBounce = 5;
        //链击层数上限，每层 +10% 伤害
        private const int MaxChainLevel = 3;
        //链击换目标的搜索半径
        private const float ChainRange = 400f;
        //回手吸附判定半径
        private const float CatchRange = 34f;

        private Player Owner => Main.player[Projectile.owner];

        //已命中目标记录（仅命中判定发生的所有者端使用）：
        //换向时整表传给 FindClosestNPC 排除，修掉"最近目标就是刚打过的那个"的回锁问题
        private readonly List<NPC> hitNPCs = new();

        private int ChainLevel => (int)Projectile.ai[2];

        //ai[0]: 0=掷出, 1=回手；ai[1]=飞行计时（链击成功时清零续航）；
        //ai[2]=链击层数 0~3（随弹幕同步，供远端客户端画金光）；localAI[0]=弹射计数
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }

            //转速吃飞行速度：减速与回手时肉眼可见地慢下来，旋转金边的闪烁频率随之同步
            Projectile.rotation += 0.28f + Projectile.velocity.Length() * 0.015f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * (0.4f + 0.14f * ChainLevel));

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                    , -Projectile.velocity * 0.12f
                    , Main.rand.NextBool() ? GraniteMarbleVFX.MarbleCore : GraniteMarbleVFX.MarbleGold
                    , 0.3f + 0.05f * ChainLevel).Configure(GraniteMarbleVFX.MarbleGold, 10, 0.2f, 0.4f);
            }

            if ((int)Projectile.ai[0] == 0) {
                Projectile.ai[1]++;
                Projectile.velocity *= 0.987f;
                if (Projectile.ai[1] > 28f || Projectile.velocity.Length() < 4.5f) {
                    Projectile.ai[0] = 1f;
                    Projectile.netUpdate = true;
                }
                return;
            }

            //回手段穿墙，保证必定归手，不让卡墙的盘长期占用双盘上限
            Projectile.tileCollide = false;
            Vector2 toOwner = Projectile.Center.To(Owner.Center);
            if (toOwner.Length() < CatchRange) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                }
                return;
            }
            Vector2 desired = toOwner.UnitVector() * 16f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] >= MaxBounce) {
                Projectile.ai[0] = 1f;
            }
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                Projectile.velocity.X = -oldVelocity.X;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f) {
                Projectile.velocity.Y = -oldVelocity.Y;
            }

            if (!VaultUtils.isServer) {
                //短促石响双层：闷击垫底 + 高频凿点收音
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.22f, Pitch = 0.55f }, Projectile.Center);
                Vector2 outDir = Projectile.velocity.UnitVector();
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                        , outDir.RotatedByRandom(0.65f) * Main.rand.NextFloat(2f, 5.5f)
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.65f))
                        .Configure(Main.rand.Next(16, 26));
                }
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, outDir * 0.8f
                    , GraniteMarbleVFX.MarbleDust, 0.4f).Configure(20, 0.6f, 0.05f);
            }
            return false;
        }

        //链击成长走乘区而非改写 Projectile.damage：无累乘漂移，重命中旧目标也按当前层数结算
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.SourceDamage *= 1f + 0.1f * ChainLevel;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int chain = ChainLevel;
            if (!VaultUtils.isServer) {
                //清脆凿击，音调随链击层数逐级抬升；石屑与金闪随层数加量
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.62f, Pitch = -0.05f + 0.16f * chain }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.3f, Pitch = 0.35f }, Projectile.Center);
                Vector2 back = -Projectile.velocity.UnitVector();
                for (int i = 0; i < 4 + chain; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                        , back.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f)
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(Main.rand.Next(18, 30));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.MarbleGold, 0.16f + 0.03f * chain).Configure(10, 0.85f);
            }

            if ((int)Projectile.ai[0] != 0) {
                return;
            }

            if (!hitNPCs.Contains(target)) {
                hitNPCs.Add(target);
            }

            Projectile.localAI[0]++;
            NPC next = Projectile.localAI[0] < MaxBounce
                ? Projectile.Center.FindClosestNPC(ChainRange, ignoreTiles: false, onHitNPCs: hitNPCs)
                : null;
            if (next != null) {
                Projectile.velocity = Projectile.Center.To(next.Center).UnitVector() * 15f;
                Projectile.ai[1] = 0f;//链击续航：改向成功就刷新飞行窗口
                if (Projectile.ai[2] < MaxChainLevel) {
                    Projectile.ai[2]++;
                }
            }
            else {
                Projectile.ai[0] = 1f;
            }
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            Player owner = Owner;
            //回手吸附致死 = 接住：咔哒轻响 + 手部小金色闪光；否则视为中途消散，碎成石屑
            if (owner.Alives() && Projectile.Center.To(owner.Center).Length() < CatchRange + 26f) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.8f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.24f, Pitch = 0.72f }, Projectile.Center);
                Vector2 handPos = Vector2.Lerp(owner.Center, Projectile.Center, 0.35f);
                PRTLoader.NewParticle<PRT_Light>(handPos, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.22f)
                    .Configure(12, 0.9f, _entity: owner, _followingRateRatio: 1f);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(handPos, Main.rand.NextVector2Circular(1.6f, 1.6f) + owner.velocity
                        , GraniteMarbleVFX.MarbleGold, 0.35f).Configure(GraniteMarbleVFX.MarbleCore, 12, 0.25f, 0.5f);
                }
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.45f, Pitch = -0.1f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                    , Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(18, 28));
            }
            PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Vector2.Zero
                , GraniteMarbleVFX.MarbleDust, 0.45f).Configure(22, 0.65f, 0.05f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            //旋转残影：逐帧采样历史位置/角度，单帧精灵叠绘，规避带状拖尾在急转弯时的顶点崩坏
            float ghostAlpha = 0.3f + 0.07f * ChainLevel;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                //近端暖白、尾端鎏金：MarbleBar 的金白渐变语言
                Color c = Color.Lerp(GraniteMarbleVFX.MarbleGold, GraniteMarbleVFX.MarbleCore, fade) * fade * ghostAlpha;
                c.A = 0;
                Main.EntitySpriteDraw(tex, dpos, null, c, Projectile.oldRot[i], origin
                    , Projectile.scale * fade, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Color gold = GraniteMarbleVFX.MarbleGold;
            gold.A = 0;
            Color core = GraniteMarbleVFX.MarbleCore;
            core.A = 0;

            int chain = ChainLevel;
            //闪烁相位挂在旋转角上：转速越快金边闪得越急
            float flick = 0.5f + 0.5f * MathF.Sin(Projectile.rotation * 3f);

            //历史位置柔光残影（点状叠加，无网格顶点），链击越高路径越亮
            float trailBoost = 0.35f + 0.11f * chain;
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 dpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                spriteBatch.Draw(glow, dpos, null, gold * fade * trailBoost, 0f, glow.Size() / 2f
                    , 0.35f * fade, SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;

            //盘体环境辉光：链击层数越高金光越亮（成长可视化）
            spriteBatch.Draw(glow, pos, null, gold * (0.5f + 0.16f * chain), 0f, glow.Size() / 2f
                , 0.48f + 0.05f * chain, SpriteEffects.None, 0f);

            //旋转金边高光：本体放大一圈的加色描边，亮度随转速闪烁、随链击增强
            spriteBatch.Draw(tex, pos, null, gold * (0.3f + 0.32f * flick) * (0.75f + 0.25f * chain), Projectile.rotation
                , tex.Size() / 2f, Projectile.scale * 1.12f, SpriteEffects.None, 0f);

            //沿盘缘对转的双 glint：追随旋转角读出"旋转的镶金盘"
            Vector2 rim = Projectile.rotation.ToRotationVector2() * 13f * Projectile.scale;
            spriteBatch.Draw(star, pos + rim, null, core * (0.45f + 0.45f * flick), Projectile.rotation
                , star.Size() / 2f, 0.07f, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos - rim, null, gold * (0.3f + 0.35f * (1f - flick)), Projectile.rotation
                , star.Size() / 2f, 0.055f, SpriteEffects.None, 0f);

            //核心亮斑
            spriteBatch.Draw(star, pos, null, core * 0.75f, -Projectile.rotation * 0.5f, star.Size() / 2f
                , 0.1f + 0.012f * chain, SpriteEffects.None, 0f);
        }
    }
}

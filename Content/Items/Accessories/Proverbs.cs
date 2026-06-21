using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class Proverbs : ModItem
    {
        public override string Texture => CWRConstant.Item_Accessorie + "Proverbs";
        public static LocalizedText L1;
        public static LocalizedText L2;
        public override void SetStaticDefaults() {
            L1 = this.GetLocalization(nameof(L1), () => "对硫火女巫造成双倍伤害，硫火女巫的攻击将可能对你造成暴击");
            L2 = this.GetLocalization(nameof(L2), () => "谢谢你");
        }
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(1, 5, 20, 0);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.buffImmune[CWRID.Buff_VulnerabilityHex] = true;
            player.buffImmune[BuffID.OnFire] = true;
            player.buffImmune[BuffID.OnFire3] = true;
            player.GetModPlayer<ProverbsPlayer>().HasProverbs = true;
            player.GetModPlayer<ProverbsPlayer>().HideVisual = hideVisual;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            bool ebn = EbnState.OnEbn(Main.LocalPlayer);
            foreach (TooltipLine line in tooltips) {
                if (line.Name == "ItemName") {
                    continue;
                }
                if (!line.Text.Contains("[Content]")) {
                    continue;
                }
                line.OverrideColor = ebn ? Color.Orange : Color.IndianRed;
                line.Text = line.Text.Replace("[Content]", ebn ? L2.Value : L1.Value);
            }
        }
    }

    internal class ProverbsPlayer : ModPlayer
    {
        public bool HasProverbs;
        public bool HideVisual;
        public bool IsEbn;
        public override void ResetEffects() {
            HasProverbs = false;
            HideVisual = false;
            IsEbn = EbnState.OnEbn(Player);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (HasProverbs || IsEbn) {
                target.AddBuff(CWRID.Buff_VulnerabilityHex, 300);
            }
            if (IsEbn && target.life <= 0 && target.lifeMax > 500 && Main.rand.NextBool(6) && !CWRRef.GetBossRushActive()) {//击杀概率掉落湮灭灰烬
                VaultUtils.SpwanItem(target.FromObjectGetParent(), target.Hitbox, new Item(CWRID.Item_AshesofAnnihilation));
            }
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) {
            if (HasProverbs || IsEbn) {
                target.AddBuff(CWRID.Buff_VulnerabilityHex, 300);
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (HasProverbs || IsEbn) {
                target.AddBuff(CWRID.Buff_VulnerabilityHex, 300);
            }
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (HasProverbs && proj.Alives() && proj.CWR().Source is EntitySource_Parent entitySource
                && entitySource.Entity is NPC npc && npc.Alives() && npc.type == CWRID.NPC_SupremeCalamitas && Main.rand.NextBool(3)) {
                modifiers.FinalDamage *= 10;
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (HasProverbs && npc.type == CWRID.NPC_SupremeCalamitas && Main.rand.NextBool(3)) {
                modifiers.FinalDamage *= 10;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (HasProverbs && target.type == CWRID.NPC_SupremeCalamitas) {
                modifiers.FinalDamage *= 2;
            }
        }

        public override void PostUpdate() {
            if ((HasProverbs || IsEbn) && Player.CountProjectilesOfID<ProverbsCircle>() == 0) {
                Projectile.NewProjectile(Player.FromObjectGetParent(), Player.Center, Vector2.Zero
                    , ModContent.ProjectileType<ProverbsCircle>(), 3000, 0, Player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 箴言硫磺火法阵，玩家背后的视觉效果
    /// </summary>
    internal class ProverbsCircle : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private float circleRadius = 0f;
        private float circleAlpha = 0f;
        private float Time;

        public override void SetDefaults() {
            Projectile.width = 200;
            Projectile.height = 200;
            Projectile.hide = true;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_VulnerabilityHex, 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_VulnerabilityHex, 300);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, circleRadius, targetHitbox);
        }

        public override void AI() {
            Projectile.timeLeft = 2;
            var proverbsPlayer = Owner.GetModPlayer<ProverbsPlayer>();
            if (!Owner.Alives() || (!proverbsPlayer.HasProverbs && !proverbsPlayer.IsEbn)) {
                Projectile.Kill();
                return;
            }

            Projectile.damage = (int)(3000 * Owner.GetDamage(DamageClass.Generic).Additive);

            bool hideVisual = proverbsPlayer.HideVisual;
            if (hideVisual) {
                circleAlpha = MathHelper.Lerp(circleAlpha, 0f, 0.1f);
            }
            else {
                if (circleAlpha < 1f) {
                    circleAlpha += 0.05f;
                }
            }

            //双重增幅时法阵更大
            bool dualMode = proverbsPlayer.HasProverbs && proverbsPlayer.IsEbn;

            float maxR = dualMode ? 620f : 220f;
            if (hideVisual) {
                maxR = 0;
            }

            circleRadius = MathHelper.Lerp(circleRadius, maxR, 0.1f);

            //跟随玩家
            Projectile.Center = Owner.GetPlayerStabilityCenter();

            //生成硫磺火粒子
            if (Main.rand.NextBool(3) && circleAlpha > 0.3f && !VaultUtils.isServer) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(circleRadius * 0.7f, circleRadius);
                Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * distance;

                var prt = PRTLoader.NewParticle<PRT_LavaFire>(
                    spawnPos,
                    Vector2.UnitY * Main.rand.NextFloat(-1f, -0.3f),
                    Color.White,
                    Main.rand.NextFloat(0.6f, 1f)
                );
                if (prt != null) {
                    prt.ai[0] = 3;
                    prt.ai[1] = 0.8f;
                }
            }

            //照明
            float lightIntensity = circleAlpha * 1.2f;
            Lighting.AddLight(Projectile.Center, 1.2f * lightIntensity, 0.4f * lightIntensity, 0.2f * lightIntensity);

            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (circleAlpha < 0.01f || VaultUtils.isServer) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;

            DrawGhostDomainShader(sb, center);

            return false;
        }

        private void DrawGhostDomainShader(SpriteBatch sb, Vector2 center) {
            Effect shader = EffectLoader.ProverbsGhostDomain?.Value;
            if (shader == null) return;

            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            if (canvas == null || noise == null) return;

            float drawRadius = circleRadius * 1.3f;
            float drawDiameter = drawRadius * 2f;

            float time = (float)Main.timeForVisualEffects * 0.016f;
            bool isDual = Owner.GetModPlayer<ProverbsPlayer>() is var pp && pp.HasProverbs && pp.IsEbn;

            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["fadeAlpha"]?.SetValue(circleAlpha);
            shader.Parameters["pulsePhase"]?.SetValue(Time * 0.012f);
            shader.Parameters["dualMode"]?.SetValue(isDual ? 1f : 0f);

            //鬼域色调：核心暖橙、中层血红、边缘暗紫红、虚空黑
            shader.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.65f, 0.25f));
            shader.Parameters["midColor"]?.SetValue(new Vector3(0.85f, 0.22f, 0.12f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.5f, 0.1f, 0.15f));
            shader.Parameters["voidColor"]?.SetValue(new Vector3(0.12f, 0.02f, 0.05f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(canvas, center, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}

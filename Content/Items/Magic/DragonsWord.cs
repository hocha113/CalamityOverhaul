using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee.DawnshatterAzures;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class DragonsWord : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "DragonsWord";
        [VaultLoaden(CWRConstant.Item_Magic + "DragonsWordGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.width = 68;
            Item.height = 78;
            Item.damage = 682;
            Item.mana = 80;
            Item.shootSpeed = 6;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 60;
            Item.rare = ItemRarityID.Red;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item92;
            Item.value = Item.buyPrice(0, 85, 5, 5);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<DragonsWordProj>();
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_YharonSoulFragment, CWRID.Item_SubsumingVortex, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_SubsumingVortex)
                .AddIngredient(CWRID.Item_YharonSoulFragment, 39)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DragonsWordMouse>(), damage, knockback, player.whoAmI, 0f, 0.03f);
                return false;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vr = (MathHelper.TwoPi / 3f * i + Main.GameUpdateCount * 0.1f).ToRotationVector2();
                Projectile.NewProjectile(source, player.Center + vr * Main.rand.Next(22, 38), vr.RotatedByRandom(0.32f) * 3
                , type, damage, knockback, player.whoAmI, 0f, 0.03f);
            }
            return false;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<DragonsWordMouse>()] <= 0;
    }

    /// <summary>龙言共用调色与图元设备状态,四技法着色器 <see cref="EffectLoader.DragonsWordFX"/></summary>
    internal static class DragonsWordVFX
    {
        public static readonly Color HotGold = new(255, 214, 110);
        public static readonly Color MoltenOrange = new(255, 128, 36);
        public static readonly Color EmberRed = new(214, 58, 22);

        public static Effect Shader => EffectLoader.DragonsWordFX?.Value;

        /// <summary>图元层入场,预乘 AlphaBlend,s1 绑定噪声;缺资产返回 null</summary>
        public static Effect BeginPrimitive(out BlendState oldBlend, out RasterizerState oldRaster) {
            oldBlend = null;
            oldRaster = null;
            Effect effect = Shader;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return null;
            }
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            oldBlend = gd.BlendState;
            oldRaster = gd.RasterizerState;
            gd.BlendState = BlendState.AlphaBlend;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            return effect;
        }

        public static void EndPrimitive(BlendState oldBlend, RasterizerState oldRaster) {
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            if (oldBlend != null) {
                gd.BlendState = oldBlend;
            }
            if (oldRaster != null) {
                gd.RasterizerState = oldRaster;
            }
        }

        /// <summary>沿 dir 的四顶点条带,UV.x 0尾→1头</summary>
        public static void BuildQuad(VertexPositionColorTexture[] v, Vector2 center, Vector2 dir
            , float halfLen, float halfWid, Color color) {
            Vector2 perp = new(-dir.Y, dir.X);
            Vector2 head = center + dir * halfLen;
            Vector2 tail = center - dir * halfLen;
            v[0] = new VertexPositionColorTexture((tail + perp * halfWid).ToVector3(), color, new Vector2(0f, 0f));
            v[1] = new VertexPositionColorTexture((tail - perp * halfWid).ToVector3(), color, new Vector2(0f, 1f));
            v[2] = new VertexPositionColorTexture((head + perp * halfWid).ToVector3(), color, new Vector2(1f, 0f));
            v[3] = new VertexPositionColorTexture((head - perp * halfWid).ToVector3(), color, new Vector2(1f, 1f));
        }
    }

    internal class DragonsWordCut : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //同帧斩击音限流,纯客户端表现
        private static uint soundFrame;
        private static int soundCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ArmorPenetration = 1000;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 180);
        }

        private static bool SoundBudget() {
            if (soundFrame != Main.GameUpdateCount) {
                soundFrame = Main.GameUpdateCount;
                soundCount = 0;
            }
            return ++soundCount <= 2;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DragonsWordCut>(Projectile.Center, new Vector2(0.1f, 0.1f)
                .RotatedByRandom(100), Main.rand.NextBool() ? Color.DarkRed : Color.IndianRed, Main.rand.NextFloat(0.65f, 0.85f)).Configure(false, 19);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center, VaultUtils.RandVr(2f, 6f)
                    , default, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(16, 26));
            }
            if (SoundBudget()) {
                SoundStyle sound = "CalamityMod/Sounds/Item/MurasamaHitOrganic".GetSound();
                SoundEngine.PlaySound(sound with { Volume = 0.8f, PitchRange = (0.6f, 0.7f) }, Projectile.Center);
            }
        }
    }

    internal class DragonsWordMouse : BaseHeldProj, IPrimitiveDrawable, IAdditiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private Vector2 targetPos;
        private ref float BeatTime => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];

        //处决节拍长度(tick),斩击/行波/破印/吼声同拍
        private const int BeatLen = 15;
        private const int BrandCap = 24;
        private static readonly VertexPositionColorTexture[] ringVerts = new VertexPositionColorTexture[4];
        private static readonly VertexPositionColorTexture[] brandVerts = new VertexPositionColorTexture[BrandCap * 6];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 122;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
        }

        private float Beat01 => BeatTime % BeatLen / (float)BeatLen;
        private float Grow01 => MathHelper.Clamp(BeatTime / 20f, 0f, 1f);

        private void SpanDragonsWordCut() {
            if (BeatTime % BeatLen != 0) {
                return;
            }
            //斩击只在主人端生成,交由网络同步,防多端重复
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int num = 255;
            foreach (var npc in Main.npc) {
                if (num <= 0) {
                    break;
                }
                if (!npc.Alives()) {
                    continue;
                }
                if (npc.friendly) {
                    continue;
                }
                if (npc.Distance(Projectile.Center) > Radius) {
                    continue;
                }
                if (Owner.name == "Sakura") {
                    num *= 5;
                }
                int newDmg = (int)(Projectile.damage * (0.2f + num / 55f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), npc.Center, Vector2.Zero
                    , ModContent.ProjectileType<DragonsWordCut>(), newDmg, 2, Owner.whoAmI, 0f, 0.03f);
                num--;
            }
        }

        private bool AnyEnemyInRange() {
            foreach (var npc in Main.npc) {
                if (npc.Alives() && !npc.friendly && npc.Distance(Projectile.Center) <= Radius) {
                    return true;
                }
            }
            return false;
        }

        //落拍演出,龙吼由各端按同步状态自播
        private void BeatFX() {
            if (VaultUtils.isServer || BeatTime % BeatLen != 0 || BeatTime <= 0) {
                return;
            }
            if (!AnyEnemyInRange()) {
                return;
            }
            SoundStyle roar = SoundID.DD2_BetsyFireballShot with { Volume = 0.5f, Pitch = -0.55f, PitchVariance = 0.1f };
            SoundEngine.PlaySound(roar, Projectile.Center);
        }

        private void CastFX() {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DawnRing>(Projectile.Center, Vector2.Zero, default, 1f)
                ?.Configure(Vector2.UnitY, 26f, 7f, 1f, 16);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center, VaultUtils.RandVr(2f, 7f)
                    , default, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        //敕域常驻余韵: 域内升腾余烬+缘口舔舌,预算与半径挂钩
        private void AmbientFX() {
            if (VaultUtils.isServer || Radius < 24f) {
                return;
            }
            int budget = 1 + (int)(Radius / 240f);
            for (int i = 0; i < budget; i++) {
                if (!Main.rand.NextBool(2)) {
                    continue;
                }
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(Radius, Radius);
                PRTLoader.NewParticle<PRT_DawnEmber>(pos, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.6f))
                    , default, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(22, 36), 0.05f);
            }
            if (Main.rand.NextBool(3)) {
                Vector2 outward = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                PRTLoader.NewParticle<PRT_DawnTongue>(Projectile.Center + outward * Radius, Vector2.Zero
                    , default, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(outward, Main.rand.NextFloat(0.8f, 1.3f), Main.rand.Next(4, 7));
            }
        }

        private void LightRing() {
            Vector3 warm = DragonsWordVFX.MoltenOrange.ToVector3();
            Lighting.AddLight(Projectile.Center, warm * 0.55f);
            for (int i = 0; i < 6; i++) {
                Vector2 rim = Projectile.Center + (MathHelper.TwoPi / 6f * i).ToRotationVector2() * Radius;
                Lighting.AddLight(rim, warm * 0.4f);
            }
        }

        private void InOwner() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            SetDirection();
            if (Projectile.ai[0] == 0) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Providence/ProvidenceHolyRay".GetSound());
                targetPos = InMousePos;
                CastFX();
            }
            targetPos = Vector2.Lerp(targetPos, InMousePos, 0.1f);
            Projectile.Center = targetPos;
        }

        private void UpdateSakura() {
            if (DownRight && Owner.CheckMana(Owner.GetItem())) {
                if (Owner.name == "Sakura") {
                    Owner.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
                    if (Main.rand.NextBool(300)) {
                        Owner.AddBuff(BuffID.Darkness, 60);
                    }
                }

                Owner.statMana -= 1;
                Owner.manaRegenDelay = 6;
                if (Projectile.ai[1] < 660) {
                    Projectile.ai[1] += 2;
                }
            }
            else {
                Projectile.ai[1] -= 6;
                if (Projectile.ai[1] <= 0) {
                    Projectile.Kill();
                }
            }
        }

        public override void AI() {
            InOwner();
            UpdateSakura();

            if (Projectile.ai[1] >= 0) {
                SpanDragonsWordCut();
                BeatFX();
                AmbientFX();
                LightRing();
            }

            Projectile.ai[0]++;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            float radius = Radius;
            if (radius < 8f) {
                return;
            }
            Effect effect = DragonsWordVFX.BeginPrimitive(out BlendState oldBlend, out RasterizerState oldRaster);
            if (effect == null) {
                return;
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            float thickness = 30f + radius * 0.02f;
            float halfPx = (radius + thickness * 2.8f) / 0.84f;
            float beatSeed = MathF.Floor(BeatTime / BeatLen);

            //敕环,全参数重设
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 1f);
            effect.Parameters["uForm"]?.SetValue(Grow01);
            effect.Parameters["uFade"]?.SetValue(1f);
            effect.Parameters["uRadius"]?.SetValue(radius / halfPx);
            effect.Parameters["uThickness"]?.SetValue(thickness / halfPx);
            effect.Parameters["uBeat"]?.SetValue(Beat01);
            effect.Parameters["uBeatSeed"]?.SetValue(beatSeed);
            DragonsWordVFX.BuildQuad(ringVerts, Projectile.Center, Vector2.UnitX, halfPx, halfPx, Color.White);
            effect.CurrentTechnique = effect.Techniques["TechDecree"];
            effect.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, ringVerts, 0, 2);

            //龙瞳烙印批,种子进顶点色R,缘口 70px 淡出进顶点色A
            int brandCount = 0;
            foreach (var npc in Main.npc) {
                if (!npc.Alives() || npc.friendly) {
                    continue;
                }
                float dist = npc.Distance(Projectile.Center);
                if (dist > radius) {
                    continue;
                }
                float edgeA = MathHelper.Clamp((radius - dist) / 70f, 0f, 1f);
                float half = MathHelper.Clamp(MathF.Max(npc.width, npc.height) * 0.75f, 22f, 95f);
                var tint = new Color((byte)(npc.whoAmI * 37 % 256), 0, 0, (byte)(edgeA * 255f));
                Vector2 c = npc.Center;
                int b = brandCount * 6;
                var tl = new VertexPositionColorTexture((c + new Vector2(-half, -half)).ToVector3(), tint, new Vector2(0f, 0f));
                var tr = new VertexPositionColorTexture((c + new Vector2(half, -half)).ToVector3(), tint, new Vector2(1f, 0f));
                var bl = new VertexPositionColorTexture((c + new Vector2(-half, half)).ToVector3(), tint, new Vector2(0f, 1f));
                var br = new VertexPositionColorTexture((c + new Vector2(half, half)).ToVector3(), tint, new Vector2(1f, 1f));
                brandVerts[b] = tl;
                brandVerts[b + 1] = tr;
                brandVerts[b + 2] = bl;
                brandVerts[b + 3] = tr;
                brandVerts[b + 4] = br;
                brandVerts[b + 5] = bl;
                if (++brandCount >= BrandCap) {
                    break;
                }
            }
            if (brandCount > 0) {
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uBeat"]?.SetValue(Beat01);
                effect.Parameters["uBeatSeed"]?.SetValue(beatSeed);
                effect.CurrentTechnique = effect.Techniques["TechBrand"];
                effect.CurrentTechnique.Passes[0].Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleList, brandVerts, 0, brandCount * 2);
            }

            DragonsWordVFX.EndPrimitive(oldBlend, oldRaster);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float radius = Radius;
            if (glow == null || radius < 8f) {
                return;
            }
            //话语喉口,拍后随余辉鼓一下;加色批源因子是 SourceAlpha,A 必须随强度走
            float flare = 1f - MathHelper.Clamp(Beat01 * 5f, 0f, 1f);
            Vector2 center = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(glow, center, null, DragonsWordVFX.HotGold * (0.45f + 0.3f * flare), 0f
                , glow.Size() / 2f, 0.9f + 0.25f * flare, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, center, null, DragonsWordVFX.MoltenOrange * 0.3f, 0f
                , glow.Size() / 2f, 1.7f, SpriteEffects.None, 0f);

            //着色器缺失的圈缘回退
            if (DragonsWordVFX.Shader == null) {
                Texture2D rim = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
                if (rim != null) {
                    float sc = radius / (rim.Width * 0.5f * 0.95f);
                    spriteBatch.Draw(rim, center, null, DragonsWordVFX.MoltenOrange * 0.6f, 0f
                        , rim.Size() / 2f, sc, SpriteEffects.None, 0f);
                }
            }
        }

        //敕域热穹,暖色效果禁蓝移
        public bool DontUseBlueshiftEffect() => true;
        public void DrawCustom(SpriteBatch spriteBatch) { }
        public void Warp() {
            float radius = Radius;
            if (radius < 40f) {
                return;
            }
            float flare = 1f - MathHelper.Clamp(Beat01 * 6f, 0f, 1f);
            float env = MathHelper.Clamp(radius / 660f, 0f, 1f) * 0.35f + flare * 0.18f;
            float size = radius * 2.5f;
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, 0.18f, env, 0f, "GravitationalLens", 0.46f);
        }
    }

    internal class DragonsWordProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Time => ref Projectile.ai[0];
        private ref float SpinRate => ref Projectile.ai[1];
        private ref float FadeState => ref Projectile.ai[2];

        private const int TrailMax = 30;
        private const int MaxHits = 18;
        //渐隐时长按子更新tick计
        private const float FadeSubMax = 98f;

        private readonly List<Vector2> path = new(TrailMax + 2);
        private VertexPositionColorTexture[] trailVerts;
        private static readonly VertexPositionColorTexture[] bodyVerts = new VertexPositionColorTexture[4];
        private uint lastVisualFrame;
        private float keptLen;
        private float erodedLen;
        private int hitsTaken;
        private bool exploded;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            //穿透数由 MaxHits 手记,穿尽转入熔爆渐隐而非即刻消失
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.timeLeft = 1220 * Projectile.extraUpdates;
        }

        private float ArmTick => 150 * Projectile.extraUpdates;
        private float Seed => Projectile.identity * 0.173f % 1f;
        private float Form01 => MathHelper.Clamp(Time / (16f * Projectile.extraUpdates), 0f, 1f);
        private float Heat01 => 0.35f + 0.65f * MathHelper.Clamp((Time - ArmTick) / (25f * Projectile.extraUpdates), 0f, 1f);
        private float FadeAlpha => FadeState <= 0f ? 1f : MathHelper.Clamp(1f - FadeState / FadeSubMax, 0f, 1f);

        public override bool? CanHitNPC(NPC target) {
            if (FadeState > 0f) {
                return false;
            }
            return Time < 150 * Projectile.extraUpdates ? false : base.CanHitNPC(target);
        }

        public override bool PreAI() {
            if (FadeState > 0f) {
                UpdateFade();
                return false;
            }

            //运动骨架: 自旋段→索敌缓追→定速咬定
            if (Time > 160 * Projectile.extraUpdates) {
                NPC target = Projectile.Center.FindClosestNPC(1600);
                if (target != null) {
                    if (Time < 290 * Projectile.extraUpdates) {
                        Projectile.SmoothHomingBehavior(target.Center, 1, 0.08f);
                    }
                    else {
                        Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    }
                }
            }
            else {
                Projectile.velocity = Projectile.velocity.RotatedBy(SpinRate);
            }

            if ((int)Time == (int)ArmTick) {
                IgnitionFX();
            }
            UpdateVisuals();

            if (Projectile.timeLeft <= FadeSubMax + 4) {
                EnterFade();
            }
            Time++;
            return false;
        }

        //点燃拍: 泪滴入锋,白热核抬升
        private void IgnitionFX() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center, VaultUtils.RandVr(1.5f, 4f)
                    , default, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(14, 24));
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.25f, Pitch = 0.4f, PitchVariance = 0.15f }, Projectile.Center);
        }

        //每帧一次: 路径记录/照明/尾部余烬,extraUpdates 子tick只记帧首
        private void UpdateVisuals() {
            if (VaultUtils.isServer || lastVisualFrame == Main.GameUpdateCount) {
                return;
            }
            lastVisualFrame = Main.GameUpdateCount;
            RecordPath(Projectile.Center);

            float heat = Heat01;
            Lighting.AddLight(Projectile.Center, DragonsWordVFX.MoltenOrange.ToVector3() * (0.4f + 0.4f * heat));

            if (Main.rand.NextBool(3)) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 pos = Projectile.Center - dir * 26f + Main.rand.NextVector2Circular(6f, 6f);
                PRTLoader.NewParticle<PRT_DawnEmber>(pos, -dir * Main.rand.NextFloat(0.5f, 1.5f)
                    , default, Main.rand.NextFloat(0.45f, 0.8f) * (0.5f + 0.5f * heat))?.Configure(Main.rand.Next(14, 24));
            }
        }

        private void RecordPath(Vector2 pos) {
            if (path.Count > 0) {
                keptLen += Vector2.Distance(path[^1], pos);
            }
            path.Add(pos);
            if (path.Count > TrailMax) {
                DropTailPoint();
            }
        }

        private void DropTailPoint() {
            if (path.Count < 2) {
                return;
            }
            float seg = Vector2.Distance(path[0], path[1]);
            erodedLen += seg;
            keptLen = MathF.Max(keptLen - seg, 0f);
            path.RemoveAt(0);
        }

        private void EnterFade() {
            if (FadeState > 0f) {
                return;
            }
            //先爆再进渐隐态,否则 CanHitNPC 的渐隐门会挡掉爆炸伤害
            DoExplosion();
            FadeState = 1f;
            Projectile.timeLeft = (int)FadeSubMax + 4;
            Projectile.netUpdate = true;
        }

        //熔爆: 伤害口径与旧版一致,先爆再断 friendly
        private void DoExplosion() {
            if (exploded) {
                return;
            }
            exploded = true;
            Projectile.Explode();
            Projectile.friendly = false;
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DawnRing>(Projectile.Center, Vector2.Zero, default, 1f)
                ?.Configure(Projectile.velocity, 20f, 4.5f, 0.55f, 14);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center, VaultUtils.RandVr(3f, 9f)
                    , default, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(20, 34));
            }
        }

        private void UpdateFade() {
            DoExplosion();
            FadeState++;
            Projectile.velocity *= 0.82f;
            //缎带自尾先蚀,每帧掉两点
            if (lastVisualFrame != Main.GameUpdateCount) {
                lastVisualFrame = Main.GameUpdateCount;
                DropTailPoint();
                DropTailPoint();
            }
            if (FadeState > FadeSubMax) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_Dragonfire, 420);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_DawnEmber>(target.Center, VaultUtils.RandVr(2f, 5f)
                        , default, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(14, 22));
                }
            }
            if (++hitsTaken >= MaxHits) {
                EnterFade();
            }
        }

        public override bool PreKill(int timeLeft) {
            //外部路径直接死亡时也保住爆炸口径
            DoExplosion();
            return true;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (path.Count < 2) {
                return;
            }
            Effect effect = DragonsWordVFX.BeginPrimitive(out BlendState oldBlend, out RasterizerState oldRaster);
            if (effect == null) {
                return;
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            float fadeA = FadeAlpha;
            float heat = Heat01 * (0.4f + 0.6f * fadeA);
            var vertColor = new Color(255, 255, 255, (byte)(fadeA * 255f));

            //缎带,全参数重设
            int n = path.Count;
            int need = n * 2;
            if (trailVerts == null || trailVerts.Length < need) {
                trailVerts = new VertexPositionColorTexture[TrailMax * 2 + 4];
            }
            for (int i = 0; i < n; i++) {
                float t = i / (float)(n - 1);
                Vector2 ahead = path[Math.Min(i + 1, n - 1)];
                Vector2 back = path[Math.Max(i - 1, 0)];
                Vector2 dir = (ahead - back).SafeNormalize(Vector2.UnitX);
                Vector2 perp = new(-dir.Y, dir.X);
                float hw = MathHelper.Lerp(3f, 15f, MathF.Pow(t, 0.7f));
                trailVerts[i * 2] = new VertexPositionColorTexture((path[i] + perp * hw).ToVector3(), vertColor, new Vector2(t, 0f));
                trailVerts[i * 2 + 1] = new VertexPositionColorTexture((path[i] - perp * hw).ToVector3(), vertColor, new Vector2(t, 1f));
            }
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHeat"]?.SetValue(heat);
            effect.Parameters["uFade"]?.SetValue(fadeA);
            effect.Parameters["uLenPx"]?.SetValue(keptLen);
            effect.Parameters["uOffPx"]?.SetValue(erodedLen);
            effect.CurrentTechnique = effect.Techniques["TechTrail"];
            effect.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, trailVerts, 0, (n - 1) * 2);

            //泪滴本体压缎带头,全参数重设
            float scaleEnv = (0.45f + 0.55f * Form01) * (0.35f + 0.65f * fadeA);
            float speed = Projectile.velocity.Length() * (Projectile.extraUpdates + 1);
            float stretch = MathHelper.Clamp(speed * 0.022f, 0f, 0.8f);
            Vector2 bodyDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float halfLen = 40f * (1f + stretch) * scaleEnv;
            float halfWid = 26f * (1f - 0.28f * stretch) * scaleEnv;
            DragonsWordVFX.BuildQuad(bodyVerts, Projectile.Center, bodyDir, halfLen, halfWid, vertColor);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uForm"]?.SetValue(Form01);
            effect.Parameters["uHeat"]?.SetValue(heat);
            effect.Parameters["uFade"]?.SetValue(fadeA);
            effect.CurrentTechnique = effect.Techniques["TechTear"];
            effect.CurrentTechnique.Passes[0].Apply();
            gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, bodyVerts, 0, 2);

            DragonsWordVFX.EndPrimitive(oldBlend, oldRaster);
        }

        //光晕衬底,兼作着色器缺失时的可见性回退
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float fadeA = FadeAlpha;
            float env = (0.45f + 0.55f * Form01) * fadeA;
            if (env < 0.03f) {
                return;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(glow, pos, null, DragonsWordVFX.MoltenOrange * (0.5f * env), 0f
                , glow.Size() / 2f, 0.9f * env, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, DragonsWordVFX.HotGold * (0.4f * env), 0f
                , glow.Size() / 2f, 0.45f * env, SpriteEffects.None, 0f);
        }
    }
}

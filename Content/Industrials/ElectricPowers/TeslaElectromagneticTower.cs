using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using CalamityOverhaul.Content.Industrials.Modifys;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class TeslaElectromagneticTower : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTower";
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Quest;
            Item.createTile = ModContent.TileType<TeslaElectromagneticTowerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1200;
        }
    }

    internal class TeslaElectromagneticTowerTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/TeslaElectromagneticTowerTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<TeslaElectromagneticTower>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 5;
            TileObjectData.newTile.Origin = new Point16(2, 4);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide
                , TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool RightClick(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<TeslaElectromagneticTowerTP>(i, j, out var tp)) {
                return false;
            }
            tp.RightEvent();
            return base.RightClick(i, j);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out TeslaElectromagneticTowerTP tesla)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += (tesla.AttackPattern ? 1 : 0) * 5 * 18;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class TeslaElectromagneticTowerTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<TeslaElectromagneticTowerTile>();
        public override int TargetItem => ModContent.ItemType<TeslaElectromagneticTower>();
        public override float MaxUEValue => 1200;
        public bool AttackPattern { get; set; }
        public NPC TargetByNPC { get; set; }
        public int FireCoolden { get; set; }
        public float GuardValue { get; set; }
        /// <summary>
        /// 鼠标是否悬停在TP实体之上
        /// </summary>
        public bool HoverTP;
        public override void Update() {
            if (InScreen) {
                HoverTP = HitBox.Intersects(Main.MouseWorld.GetRectangle(1));
            }
            else {
                HoverTP = false;
            }

            if (AttackPattern) {
                GuardValue = 0;
                if (MachineData.UEvalue >= 60 && ++FireCoolden > 60) {
                    TargetByNPC = CenterInWorld.FindClosestNPC(700, false, true);
                    if (TargetByNPC != null) {
                        for (int i = 0; i < 6; i++) {
                            Vector2 spanPos = PosInWorld + new Vector2(Main.rand.Next(Width), Main.rand.Next(Height / 2)) + new Vector2(8, 8);
                            PRTLoader.NewParticle<PRT_TileHightlight>(spanPos, Vector2.Zero, Color.White);
                        }
                        SoundEngine.PlaySound(CWRSound.MagneticBurst, CenterInWorld);
                        if (!VaultUtils.isClient) {
                            Vector2 dir = CenterInWorld.To(TargetByNPC.Center).UnitVector();
                            Projectile.NewProjectile(new EntitySource_WorldEvent(), CenterInWorld
                                , dir * 8, ModContent.ProjectileType<TeslaBallByAttack>(), 32, 2, -1);
                        }
                    }
                    MachineData.UEvalue -= 60;
                    FireCoolden = 0;
                }
            }
            else if (MachineData.UEvalue > 2) {
                if (GuardValue < 800) {
                    GuardValue += 10;
                }

                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 33; i++) {
                        int dust = Dust.NewDust(CenterInWorld + CWRUtils.randVr(GuardValue, GuardValue + 2), 1, 1, DustID.Electric);
                        Main.dust[dust].noGravity = true;
                    }
                }

                foreach (var npc in Main.ActiveNPCs) {
                    if (npc.friendly) {
                        continue;
                    }
                    if (npc.Distance(CenterInWorld) > GuardValue) {
                        continue;
                    }
                    npc.AddBuff(BuffID.Electrified, 30);
                }

                if (++FireCoolden > 40) {
                    ArcCharging();
                    FireCoolden = 0;
                }

                MachineData.UEvalue -= 0.5f;
            }
        }

        public void ArcCharging() {
            Player player = CWRUtils.InPosFindPlayer(CenterInWorld, 800);
            if (player == null) {
                return;
            }

            Item handItem = player.GetItem();
            if (handItem.type <= ItemID.None) {
                return;
            }

            CalamityGlobalItem calamityItem = handItem.Calamity();
            if (!calamityItem.UsesCharge || calamityItem.Charge >= calamityItem.MaxCharge) {
                return;
            }

            SoundEngine.PlaySound(CWRSound.ArcCharging, CenterInWorld);
            if (VaultUtils.isClient) {
                return;
            }

            Vector2 dir = CenterInWorld.To(player.Center).UnitVector();
            Projectile.NewProjectile(new EntitySource_WorldEvent(), CenterInWorld
                , dir * 8, ModContent.ProjectileType<TeslaBallByGuard>(), 0, 0, player.whoAmI);
        }

        public void RightEvent() {
            AttackPattern = !AttackPattern;
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(PosInWorld, Width, Height, DustID.Electric);
                Main.dust[dust].noGravity = true;

            }
            for (int x = 0; x < Width / 16; x++) {
                for (int y = 0; y < Height / 16; y++) {
                    Vector2 spanPos = PosInWorld + new Vector2(x, y) * 16 + new Vector2(8, 8);
                    PRTLoader.NewParticle<PRT_TileHightlight>(spanPos, Vector2.Zero, Color.BlueViolet);
                }
            }
            SoundEngine.PlaySound(CWRSound.TeslaOpen);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!HoverTP) {
                return;
            }

            Vector2 drawPos = CenterInWorld + new Vector2(-30, 50) - Main.screenPosition;
            int uiBarByWidthSengs = (int)(ChargingStationTP.BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, ChargingStationTP.BarFull.Value.Height);
            Main.spriteBatch.Draw(ChargingStationTP.BarTop.Value, drawPos, null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(ChargingStationTP.BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            if (Main.keyState.PressingShift()) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                            , (((int)MachineData.UEvalue) + "/" + ((int)MaxUEValue) + "UE").ToString()
                            , drawPos.X + 10, drawPos.Y + 10, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
        }
    }

    //来自珊瑚石，谢谢你瓶中微光 :)
    internal class TeslaBallByAttack : BaseHeldProj
    {
        public override string Texture => CWRConstant.Masking + "StarTexture";
        public ref float PointDistance => ref Projectile.ai[2];
        public override bool CanFire => true;
        public ref float ThunderWidth => ref Projectile.localAI[1];
        public ref float ThunderAlpha => ref Projectile.localAI[2];
        public ref float State => ref Projectile.ai[0];
        public ref float Hited => ref Projectile.ai[1];
        public ref float Timer => ref Projectile.localAI[0];
        public int NPCIndex = -1;
        public float Alpha;
        public float fade = 0;
        public Vector2 TargetCenter;
        public ThunderTrail trail;
        public LinkedList<Vector2> trailList;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.aiStyle = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool? CanDamage() => State == 1 && Hited == 0;

        public float GetAlpha(float factor) {
            if (factor < fade)
                return 0;

            return ThunderAlpha * (factor - fade) / (1 - fade);
        }

        public virtual Color ThunderColorFunc(float factor) => new Color(103, 255, 255);

        public static bool TryFindClosestEnemy(Vector2 position, float maxDistance, Func<NPC, bool> predicate, out NPC target) {
            float maxDis = maxDistance;
            target = null;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && !n.friendly && predicate(n)) {
                    float dis = Vector2.Distance(position, n.Center);
                    if (dis < maxDis) {
                        maxDis = dis;
                        target = n;
                    }

                }
            }

            if (target == null)
                return false;

            return true;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Color(103, 255, 255).ToVector3());
            //生成后以极快的速度前进
            switch (State) {
                default:
                case 0://刚生成，等待透明度变高后开始寻敌
                    if (TryFindClosestEnemy(Projectile.Center, 800, n => n.CanBeChasedBy() && Collision.CanHit(Projectile, n), out NPC target)) {
                        NPCIndex = target.whoAmI;
                        TargetCenter = Projectile.Center + (Projectile.Center - Main.player[Projectile.owner].Center).SafeNormalize(Vector2.Zero) * 125;
                        StartAttack();
                    }
                    else {
                        Projectile.Kill();
                    }
                    break;
                case 1://找到敌人，以极快的速度追踪
                    Chase();
                    break;
                case 2://后摇，闪电逐渐消失
                    {
                    Timer++;
                    fade = Smoother((int)Timer, 30);
                    ThunderWidth = Smoother(60 - (int)Timer, 60) * 14;

                    float factor = Timer / 30;
                    float sinFactor = MathF.Sin(factor * MathHelper.Pi);

                    if (Timer > 30) {
                        Projectile.Kill();
                    }
                }
                break;
            }
        }

        public static float Smoother(int timer, int maxTime) {
            float factor = (float)timer / maxTime;
            return factor * factor;
        }

        public virtual float ThunderWidthFunc_Sin(float factor) => MathF.Sin(factor * MathHelper.Pi) * ThunderWidth;

        public void StartAttack() {
            Projectile.tileCollide = true;
            State = 1;
            ThunderAlpha = 1;
            ThunderWidth = 14;
            Projectile.extraUpdates = 6;
            Projectile.timeLeft = 10 * 100;
            trailList = new LinkedList<Vector2>();

            Projectile.velocity = (InMousePos - Projectile.Center).SafeNormalize(Vector2.Zero) * 16;

            trail = new ThunderTrail(CWRAsset.ThunderTrail, ThunderWidthFunc_Sin, ThunderColorFunc, GetAlpha) {
                CanDraw = true,
                UseNonOrAdd = true,
                PartitionPointCount = 3,
                BasePositions =
                [
                    Projectile.Center,Projectile.Center,Projectile.Center
                ]
            };
            trail.SetRange((0, 7));
            trail.SetExpandWidth(7);
        }

        public static bool GetNPCOwner(int index, out NPC owner, Action notExistAction = null) {
            if (!Main.npc.IndexInRange(index)) {
                notExistAction?.Invoke();
                owner = null;
                return false;
            }

            NPC npc = Main.npc[index];
            if (!npc.active) {
                notExistAction?.Invoke();
                owner = null;
                return false;
            }

            owner = npc;
            return true;
        }

        public virtual void Chase() {
            Timer++;
            Vector2 targetCenter = TargetCenter;

            if (GetNPCOwner(NPCIndex, out NPC target)) {
                float speed = Projectile.velocity.Length();
                //距离目标点近了就换一个
                if (Projectile.Center.Distance(targetCenter) < speed * 4) {
                    if (Projectile.Center.Distance(target.Center) < speed * 10) {
                        targetCenter = target.Center;
                        TargetCenter = target.Center;
                    }
                    else {
                        Vector2 dir2 = target.Center - Projectile.Center;
                        float length2 = dir2.Length();
                        if (length2 > 100)
                            length2 = 100;
                        dir2 = dir2.SafeNormalize(Vector2.Zero);
                        Vector2 center2 = Projectile.Center + dir2 * length2;
                        Vector2 pos = center2 + dir2.RotatedBy(Main.rand.NextFromList(1.57f, -1.57f)) * length2;// Main.rand.NextVector2Circular(length2,length2);

                        targetCenter = pos;
                        TargetCenter = pos;
                        Projectile.velocity = (targetCenter - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
                    }
                }
            }
            else {
                Fade();
                return;
            }

            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (targetCenter - Projectile.Center).ToRotation();

            float factor = 1 - Math.Clamp(Vector2.Distance(targetCenter, Projectile.Center) / 500, 0, 1);

            Projectile.velocity = selfAngle.AngleLerp(targetAngle, 0.5f + 0.5f * factor).ToRotationVector2() * 24f;

            if (Main.rand.NextBool(2)) {
                Projectile.SpawnTrailDust(DustID.Electric, Main.rand.NextFloat(0.1f, 0.3f), Scale: Main.rand.NextFloat(0.4f, 0.8f));
            }

            trailList.AddLast(Projectile.Center);

            if (Timer % Projectile.MaxUpdates == 0) {
                trail.BasePositions = [.. trailList];//消失的时候不随机闪电
                trail.RandomThunder();
            }
        }

        public void Fade() {
            if (State == 0) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            Projectile.extraUpdates = 0;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Hited = 1;
            Timer = 0;
            State = 2;

            if (trail != null) {
                trail.BasePositions = [.. trailList];
                if (trail.BasePositions.Length > 3)
                    trail.RandomThunder();
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Fade();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Fade();

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, CWRUtils.randVr(5));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //没碰到任何东西就绘制本体
            if (Hited == 0) {
                Texture2D mainTex = TextureAssets.Projectile[Type].Value;

                Color c = Lighting.GetColor(Projectile.Center.ToTileCoordinates(), new Color(103, 255, 255));
                c.A = 0;
                c *= Alpha;

                Vector2 position = Projectile.Center - Main.screenPosition;

                Main.spriteBatch.Draw(mainTex, position, null, c, 0,
                    mainTex.Size() / 2, 0.15f, 0, 0);

                Texture2D exTex = CWRAsset.StarTexture.Value;

                Vector2 origin = exTex.Size() / 2;
                Main.spriteBatch.Draw(exTex, position, null, c, 0, origin, 0.5f, 0, 0);

                c = lightColor;
                c.A = 0;
                c *= Alpha;
                Main.spriteBatch.Draw(exTex, position, null, c, 0, origin, 0.2f, 0, 0);
            }

            if (State > 0) {
                if (State == 1 && Timer < 3) {
                    return false;
                }

                trail?.DrawThunder(Main.instance.GraphicsDevice);
            }

            return false;
        }
    }

    internal class TeslaBallByGuard : TeslaBallByAttack
    {
        private Player TargetPlayer { get; set; }
        public override void SetDefaults() {
            base.SetDefaults();
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void Chase() {
            Timer++;
            Vector2 targetCenter = TargetCenter;

            if (TargetPlayer != null) {
                float speed = Projectile.velocity.Length();
                //距离目标点近了就换一个
                if (Projectile.Center.Distance(targetCenter) < speed * 4) {
                    if (Projectile.Center.Distance(TargetPlayer.Center) < speed) {
                        targetCenter = TargetPlayer.Center;
                        TargetCenter = TargetPlayer.Center;
                        State = 2;
                    }
                    else {
                        Vector2 dir2 = TargetPlayer.Center - Projectile.Center;
                        float length2 = dir2.Length();
                        if (length2 > 100) {
                            length2 = 100;
                        }

                        dir2 = dir2.SafeNormalize(Vector2.Zero);
                        Vector2 center2 = Projectile.Center + dir2 * length2;
                        Vector2 pos = center2 + dir2.RotatedBy(Main.rand.NextFromList(1.57f, -1.57f)) * length2;

                        targetCenter = pos;
                        TargetCenter = pos;
                        Projectile.velocity = (targetCenter - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
                    }
                }
            }
            else {
                Fade();
                return;
            }

            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (targetCenter - Projectile.Center).ToRotation();

            float factor = 1 - Math.Clamp(Vector2.Distance(targetCenter, Projectile.Center) / 500, 0, 1);

            Projectile.velocity = selfAngle.AngleLerp(targetAngle, 0.5f + 0.5f * factor).ToRotationVector2() * 24f;

            if (Main.rand.NextBool(6)) {
                Projectile.SpawnTrailDust(DustID.Electric, Main.rand.NextFloat(0.1f, 0.3f), Scale: Main.rand.NextFloat(0.4f, 0.8f));
            }

            trailList.AddLast(Projectile.Center);

            if (Timer % Projectile.MaxUpdates == 0) {
                trail.BasePositions = [.. trailList];//消失的时候不随机闪电
                trail.RandomThunder();
            }
        }

        private void HandlerPlayerCharge() {
            for (int i = 0; i < 3; i++) {
                Vector2 spanPos = Owner.position + new Vector2(Main.rand.Next(Owner.width), Main.rand.Next(Owner.height));
                PRTLoader.NewParticle<PRT_TileHightlight>(spanPos, Vector2.Zero, Color.White);
            }

            float singleCharge = 0.1f;
            Item handItem = Owner.GetItem();
            if (handItem.type > ItemID.None) {
                CalamityGlobalItem calamityItem = handItem.Calamity();
                if (calamityItem.UsesCharge && calamityItem.Charge < calamityItem.MaxCharge) {
                    calamityItem.Charge += singleCharge;
                    calamityItem.Charge = MathHelper.Clamp(calamityItem.Charge, 0, calamityItem.MaxCharge);
                }
            }
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Color(103, 255, 255).ToVector3());
            //生成后以极快的速度前进
            switch (State) {
                default:
                case 0://刚生成，等待透明度变高后开始寻敌
                    Player player = CWRUtils.InPosFindPlayer(Projectile.Center, 800);
                    if (player != null) {
                        TargetPlayer = player;
                        TargetCenter = Projectile.Center + (Projectile.Center - Owner.Center).SafeNormalize(Vector2.Zero) * 125;
                        StartAttack();
                    }
                    else {
                        Projectile.Kill();
                    }
                    break;
                case 1://找到敌人，以极快的速度追踪
                    Chase();
                    break;
                case 2://后摇，闪电逐渐消失
                    Timer++;
                    fade = Smoother((int)Timer, 30);
                    ThunderWidth = Smoother(60 - (int)Timer, 60) * 14;

                    if (Timer > 30) {
                        HandlerPlayerCharge();
                        Projectile.Kill();
                    }
                    break;
            }
        }
    }
}

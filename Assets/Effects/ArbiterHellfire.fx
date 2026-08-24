// ============================================================================
//ArbiterHellfire.fx 断罪师狱火套件
//与鬼伞金色鬼火(KikasaWispFire)刻意分野:狱火是橙红、暴烈、快频撕裂、带烟带烬,
//贴起伏地形燃烧(顶点条带底边跟地面轮廓),不是贴平直水线的幽冷金焰
//
//TechGroundFire: 贴地狱火条带(vs+ps,世界空间 TriangleStrip)
//  顶点契约:Position=世界坐标;TexCoords.x=世界X(px,噪声连续跨段);TexCoords.y=画布v(0顶,1底);
//  顶点色 R=生命包络(点燃→稳态→衰减) G=火高系数(scale/2) B=前沿亮度(火蛇头) A=端部包络(段端撕散)
//  像素侧:炽熔根床压焦炭地(char 暗带带余烬裂纹)+ 双频上升火舌场(根实尖碎,滚速约鬼火两倍)
//  + 火冠沿X低频起伏 + 舌尖向烟过渡(gutter 期烟更浓)+ 余烬星点上蹿 + 前沿白热锋
//  端部收口:A 通道抬阈值撕散,不做原地淡出;画布顶 guard 保险归零
//
//TechForge: 熔铸成形(SpriteBatch 消费,s0=物品贴图)
//  WeaverMaterialize/OniSewage 血统:uForm 噪声阈值侵蚀,未成形区取上方像素垂成熔金拉丝,
//  成形前沿白热熔线,新成形区带余温向本色冷却(uHeat);uUvRect 帧界钳制
//
//坐标全笛卡尔(无 atan2);直线算术+普通 tex2D,FNA3D 安全
//预乘输出,进 AlphaBlend 批(焦炭/烟要能压暗,加色批画不出黑)
//绑定噪声 PerlinNoise 实测值域 0.227~0.776,阈值一律过 nrm() 归一
//消费入口 Content/Items/Melee/Arbiters/ArbiterFlameRenderer.cs(火体)
//与 ArbiterManifestationRenderer.cs(熔铸)
// ============================================================================

sampler uImage0 : register(s0);   //GroundFire=白像素(不采样);Forge=物品贴图
sampler uNoiseTex : register(s1); //PerlinNoise,LinearWrap,消费端上 s1

float4x4 transformMatrix; //世界→屏幕(GroundFire 顶点用)

//== 共用 ==
float uTime;      //秒

//== TechGroundFire ==
float uCanvasH;   //条带画布总高(世界px),与 C# 顶点构建同源
float uGroundV;   //地面线在画布内的 v(地上画布/总高)

//== TechForge ==
float uForm;      //0~1 成形进度
float uHeat;      //0~1 余温(成形期 1,落定后衰减)
float uSeed;      //个体相位
float4 uUvRect;   //帧界(xy=min zw=max)

//====== 狱火色板(区别鬼火金色系:体色压向橙红,尖端深绯) ======
static const float3 HELL_CORE  = float3(1.000, 0.855, 0.470); //根部炽芯(暖金白,非纯白)
static const float3 HELL_BODY  = float3(1.000, 0.430, 0.085); //烈焰橙
static const float3 HELL_DEEP  = float3(0.700, 0.115, 0.040); //焰尖深绯
static const float3 HELL_EMBER = float3(1.000, 0.600, 0.180); //余烬星
static const float3 SMOKE_COL  = float3(0.110, 0.082, 0.072); //暖黑烟
static const float3 CHAR_COL   = float3(0.052, 0.030, 0.024); //焦炭地
//熔铸
static const float3 MOLT_HOT   = float3(1.000, 0.880, 0.540); //熔线白热
static const float3 MOLT_BODY  = float3(1.000, 0.500, 0.120); //熔金
static const float3 MOLT_DEEP  = float3(0.760, 0.180, 0.050); //冷却深橙

//绑定噪声实测值域归一(0.227~0.776)
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

//====== TechGroundFire ======

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

PSInput VSGroundFire(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PSGroundFire(PSInput input) : COLOR0
{
    float worldX = input.TexCoords.x;
    //h:相对地面线高度(px),正=地上
    float h = (uGroundV - input.TexCoords.y) * uCanvasH;

    float life   = input.Color.r;        //生命包络
    float hScale = input.Color.g * 2.0;  //火高系数(C# 写 scale/2)
    float front  = input.Color.b;        //前沿亮度
    float endEnv = input.Color.a;        //端部包络

    //生长与残喘:衰减尾段火被撕成孤舌+急促闪烁
    float grow = saturate(life * 1.15);
    float gutter01 = saturate(1.0 - life * 2.6);

    //火冠高度谱:沿X低频起伏,前沿拔高
    float hn  = nrm(tex2D(uNoiseTex, float2(worldX * 0.0016 + uTime * 0.030, 0.13)).r);
    float hn2 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0060 - uTime * 0.050, 0.71)).r);
    float crown = lerp(0.30, 1.0, hn * 0.62 + hn2 * 0.38) * (1.0 + front * 0.40);
    float maxCanvas = uCanvasH * uGroundV * 0.78;
    float hMax = maxCanvas * crown * hScale * grow;
    float q = h / max(hMax, 1.0);
    float envGate = saturate(hMax * 0.22);   //火矮到没有时整场熄灭
    float guard = saturate((uCanvasH * uGroundV * 0.96 - h) / 14.0); //画布顶保险
    float rootGate = saturate((h + 4.0) * 0.45);

    //火舌场:双频上升流,滚速约鬼火两倍(狱火不是幽火);x 频高于 h 频,舌形纵向拉长;
    //阈值带底切,根部也有暗隙(暴烈的火不是匀亮条),越高越碎
    float sway = (hn - 0.5) * h * 0.010;
    float f1 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0105 + sway, h * 0.0052 - uTime * 1.05)).r);
    float f2 = nrm(tex2D(uNoiseTex, float2(worldX * 0.0210 - sway, h * 0.0085 - uTime * 1.85)).r);
    float flameN = f1 * 0.62 + f2 * 0.38;
    float thr = 0.08 + q * 0.64 + gutter01 * 0.34 + (1.0 - endEnv) * 0.65;
    float dens = saturate((flameN - thr) * 3.2) * rootGate * guard * envGate;

    //残喘闪烁:比鬼火压制拍更急促,火在断流前挣扎
    float gutN = nrm(tex2D(uNoiseTex, float2(worldX * 0.005, uTime * 2.6)).r);
    dens *= lerp(1.0, 0.30 + 0.70 * gutN, gutter01);

    //色带:根炽芯→烈焰橙→深绯尖;色阶提前起坡,纯芯区收窄防根部糊成匀亮条
    float3 flameCol = lerp(HELL_CORE, HELL_BODY, saturate(q * 3.2));
    flameCol = lerp(flameCol, HELL_DEEP, saturate(q * 1.5 - 0.42));
    float stria = 0.70 + 0.55 * (f2 - 0.5);

    //舌尖向烟过渡:火冠以上的暖黑烟,gutter 期更浓(狱火有烟,鬼火无烟)
    float sN = nrm(tex2D(uNoiseTex, float2(worldX * 0.0058 + uTime * 0.06, h * 0.0052 - uTime * 0.55)).r);
    float smokeBand = saturate((h - hMax * 0.65) * 0.035) * saturate((hMax * 2.1 - h) * 0.020);
    float smoke = saturate((sN - 0.50 - q * 0.06) * 2.4) * smokeBand
        * (0.40 + 0.80 * gutter01) * envGate * endEnv * guard;

    //炽熔根床:一线熔金压在地面上,是"火长在地上"的锚;随低频噪声微起伏,不是死直线
    float bedEnv = grow * endEnv * envGate;
    float hBed = h - (hn2 - 0.5) * 4.0;
    float bed = exp2(-abs(hBed - 2.0) * 0.28) * (0.55 + 0.45 * f1) * bedEnv;

    //焦炭地带:地面线以下的暗带,余烬裂纹随生命亮灭(不吃 uTime,焦炭不爬)
    float rootDepth = uCanvasH * (1.0 - uGroundV);
    float charZone = saturate(-h * 0.30) * saturate((h + rootDepth * 0.92) * 0.12);
    float crackN = nrm(tex2D(uNoiseTex, float2(worldX * 0.021, h * 0.05 + 0.37)).r);
    float ember = saturate((crackN - 0.62) * 5.0) * (0.35 + 0.65 * life);
    float3 charCol = CHAR_COL + HELL_EMBER * ember * (0.45 + 0.55 * life);
    float charA = charZone * endEnv * saturate(life * 3.0 + 0.20) * 0.85;

    //余烬星点:高分位阈值,纵向拉丝、上蹿速度比鬼火游珠快得多
    float sp = nrm(tex2D(uNoiseTex, float2(worldX * 0.020 + uTime * 0.05, h * 0.006 - uTime * 0.85)).r);
    float speck = saturate((sp - 0.86) * 12.0)
        * saturate((h - hMax * 0.45) * 0.05) * guard
        * grow * endEnv * (0.5 + 0.5 * f2);

    //合成(预乘):火体+根床+焦炭+烟+前沿锋+余烬
    float3 col = flameCol * dens * stria;
    col += lerp(HELL_CORE, HELL_BODY, 0.35) * bed * 1.05;
    col += charCol * charA;
    col += SMOKE_COL * smoke;
    //前沿光锋自带高度衰减不吃 envGate:锋线正压在新燃处,光要略洒到未燃侧
    float frontHeight = exp2(-max(h, 0.0) * 0.050);
    col += HELL_CORE * front * frontHeight * (0.55 + 0.50 * f2) * rootGate * guard * endEnv;
    col += HELL_EMBER * speck * 1.10;

    float alpha = saturate(dens * 0.42 + bed * 0.22 + charA + smoke * 0.55 + speck * 0.15
        + front * frontHeight * 0.22 * rootGate * guard);
    return float4(col, alpha);
}

//====== TechForge ======
float4 PSForge(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    //帧内归一坐标
    float2 span = max(uUvRect.zw - uUvRect.xy, 0.00001);
    float2 fl = (coords - uUvRect.xy) / span;

    //成形阈:双频噪声(低频为主,成形区成块不成渣);带余量保证 0=全空 1=全满
    float n0 = nrm(tex2D(uNoiseTex, fl * float2(1.4, 1.2) + uSeed).r);
    float n1 = nrm(tex2D(uNoiseTex, fl * float2(3.6, 3.1) - uSeed * 1.3).r);
    float formN = n0 * 0.70 + n1 * 0.30;
    float formT = uForm * 1.24 - 0.12;
    float formed = formT - formN;
    float formedMask = smoothstep(0.0, 0.055, formed);

    float4 self = tex2D(uImage0, clamp(coords, uUvRect.xy, uUvRect.zw)) * vc;

    //已成形:仅新成形的窄带带余温(白热→本色冷却),内部露出真身;
    //余温带轻微呼吸,冷却中的金属不是死的
    float breathe = 0.78 + 0.22 * sin(uTime * 2.6 + uSeed * 9.0);
    float heat = saturate(1.0 - formed * 5.5) * uHeat * breathe;
    float3 colF = lerp(self.rgb, MOLT_HOT * self.a, heat * 0.75);
    float edge = exp2(-abs(formed) * 24.0) * uHeat;
    colF += lerp(MOLT_BODY, MOLT_HOT, 0.6) * edge * self.a * 0.9;

    //未成形:列相干垂丝,取上方像素把已成形的金属往下拉成熔金拉丝;
    //列梳只让部分列垂丝,丝读作细条不读作整片下涂
    float colHash = nrm(tex2D(uNoiseTex, float2(fl.x * 3.5 + uSeed, 0.55)).r);
    float comb = saturate((nrm(tex2D(uNoiseTex, float2(fl.x * 9.1 + uSeed, 0.77)).r) - 0.35) * 2.4);
    float sag = saturate(-formed * 2.6);
    float dripFl = sag * (0.06 + 0.22 * colHash) * (0.35 + 0.65 * uHeat);
    float2 upUv = clamp(coords - float2(0.0, dripFl * span.y), uUvRect.xy, uUvRect.zw);
    float4 up = tex2D(uImage0, upUv) * vc;
    //上方位置的成形值:垂丝只从已成形的身体上垂下来
    float2 flUp = fl - float2(0.0, dripFl);
    float nU0 = nrm(tex2D(uNoiseTex, flUp * float2(1.4, 1.2) + uSeed).r);
    float nU1 = nrm(tex2D(uNoiseTex, flUp * float2(3.6, 3.1) - uSeed * 1.3).r);
    float formedUp = formT - (nU0 * 0.70 + nU1 * 0.30);
    float strandMask = smoothstep(0.0, 0.055, formedUp) * (1.0 - formedMask)
        * saturate(1.0 - sag * 1.15) * comb * up.a;
    //丝内闪变:熔金流动,丝尖(sag 大处)更亮更欲滴
    float flick = nrm(tex2D(uNoiseTex, float2(fl.x * 3.1 + uSeed, fl.y * 2.4 - uTime * 0.7)).r);
    float3 strandCol = lerp(MOLT_DEEP, MOLT_HOT, saturate(sag * 1.8 + flick * 0.35));

    float3 col = colF * formedMask + strandCol * strandMask * (0.75 + 0.45 * flick);
    float alpha = self.a * formedMask + strandMask * 0.92;
    return float4(col, alpha);
}

technique TechGroundFire {
    pass P0 {
        VertexShader = compile vs_3_0 VSGroundFire();
        PixelShader = compile ps_3_0 PSGroundFire();
    }
}

technique TechForge {
    pass P0 {
        PixelShader = compile ps_3_0 PSForge();
    }
}

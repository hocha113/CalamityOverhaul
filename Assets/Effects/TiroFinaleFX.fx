// ============================================================================
//TiroFinaleFX.fx 终焉圆舞曲套件,一件效果一个 technique
//TechMusket 幻影燧发枪(ps quad,采样 s0 枪贴图):金色魔力玻璃,
//           亮度重映射入金谱+体内丝带纵流+缘光,uForm 噪蚀显现,uFire 过曝拍
//TechCircle 枪口魔法阵(ps quad,程序化):同心金环+24 格符文刻带(atan2 只进
//           floor/frac 离散格)+sin(3θ) 转辐,uOpen 绽开包络,uCharge 聚能
//TechRibbon 金丝带轨道(vs+ps 条带):丝绸窄反射带+鎏金缘线,闭环 u 只喂整数倍频
//TechFinale 终曲巨弹体(ps quad):盘径=画布 0.42,白热芯+金色对流(刚体旋转坐标)
//           +丝带螺旋壳(整数倍角),+x=运动方向,前缘压缩弓/尾侧噪蚀撕散
//
//混合契约:全部预乘输出进 AlphaBlend;光成分 A≈0 纯加光,体成分 A 承遮蔽
//极角审计:atan2 消费仅 sin(kθ) k∈Z 与 floor/frac 离散格(24∈Z);噪声全走
//         刚体旋转/平移笛卡尔坐标
//采样器:噪声固定 s1,消费端 Textures[1]+LinearWrap 显式绑定
//绑定噪声实测值域 0.227~0.776,阈值一律先过 nrm()
// ============================================================================

sampler uImage0 : register(s0);   //画布/枪贴图(SpriteBatch 主贴图)
sampler uNoise : register(s1);    //平铺 Perlin

float4x4 transformMatrix;

float uTime;
float uAlpha;        //整体透明度
float uForm;         //0~1 显现进度(噪蚀揭示)
float uFire;         //0~1 开火过曝拍
float uLit;          //纵深光照乘数
float uSeed;         //实例错相
float uOpen;         //0~1 魔法阵开合包络
float uCharge;       //0~1 聚能/速度炽度
float2 uTexel;       //枪贴图 1/尺寸(TechMusket 边缘检测)
float3 uColDeep;     //暗鎏金
float3 uColMid;      //主金
float3 uColBright;   //亮金
float3 uColHot;      //白热

static const float TAU = 6.2831853;

//----------------------------------------------------------------------------
struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float noise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

//绑定噪声值域归一
float nrm(float n) {
    return saturate((n - 0.227) / 0.549);
}

//----------------------------------------------------------------------------
//TechMusket 幻影燧发枪:s0=枪贴图(预乘),alpha 为轮廓,亮度为细节载体
//uForm 显现:噪蚀前沿从枪托(u=0)扫向枪口(u=1),前沿挂白热熔线
//----------------------------------------------------------------------------
float4 MusketPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 src = tex2D(uImage0, coords);
    float silA = src.a;

    //去预乘取亮度,伽马拉开值域再映入金谱
    float luma = dot(src.rgb, float3(0.299, 0.587, 0.114)) / max(silA, 1e-4);
    float lumaG = pow(saturate(luma), 1.35);

    float3 col = lerp(uColDeep, uColMid, smoothstep(0.04, 0.52, lumaG));
    col = lerp(col, uColBright, smoothstep(0.5, 0.92, lumaG));

    //体内丝带纵流:沿枪身缓慢流动的亮带
    float flowN = nrm(noise(float2(coords.x * 2.6 - uTime * 0.5 + uSeed * 3.1, coords.y * 0.7 + uSeed)));
    col += uColBright * pow(flowN, 3.0) * 0.55;

    //缘光:上缘挑亮,下缘沉暗(4-tap 邻域,方向性描边不是均匀抠图边)
    float aUp = tex2D(uImage0, coords - float2(0, uTexel.y * 1.6)).a;
    float aDn = tex2D(uImage0, coords + float2(0, uTexel.y * 1.6)).a;
    float rimUp = saturate((silA - aUp) * 1.8);
    float rimDn = saturate((silA - aDn) * 1.8);
    col += uColBright * rimUp * 0.9;
    col = lerp(col, uColDeep * 0.6, rimDn * 0.45);

    //显现噪蚀:前沿自枪托扫向枪口,蚀口挂熔线
    float erode = (nrm(noise(coords * float2(3.0, 2.2) + uSeed * 5.3)) - 0.5) * 0.36;
    float h = coords.x + erode;
    float front = uForm * 1.34 - 0.17;
    float visible = smoothstep(front + 0.015, front - 0.02, h);
    float forming = 1.0 - smoothstep(0.96, 1.0, uForm);
    float frontLine = exp(-abs(h - front) * 11.0) * forming;

    //开火过曝:通体白热一拍
    col += (uColBright * 0.5 + uColHot * 0.7) * uFire;
    col += uColHot * frontLine * 1.4;

    col *= uLit;

    //金玻璃体:半实遮蔽,亮部略更实
    float a = silA * visible * (0.62 + 0.24 * lumaG + 0.3 * uFire);
    a = saturate(a + frontLine * silA * 0.5);
    return float4(col * a, a) * uAlpha * vertexColor.a;
}

technique TechMusket
{
    pass MusketPass
    {
        PixelShader = compile ps_3_0 MusketPS();
    }
}

//----------------------------------------------------------------------------
//TechCircle 枪口魔法阵:盘径=画布 0.42;长短轴由 C# quad 压扁承担
//三重金环+24 格符文刻带+3 辐转饰;uOpen 自心绽开,uCharge 向心聚能
//----------------------------------------------------------------------------
float4 CirclePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / 0.42;
    float r = length(q);
    if (r > 1.42) {
        return float4(0, 0, 0, 0);
    }

    //绽开包络:图案自心张出
    float open = smoothstep(0.0, 1.0, uOpen);
    float rr = r / max(open, 1e-3);
    float aEnv = smoothstep(0.0, 0.22, uOpen);

    //三重环带
    float ringO = exp(-pow((rr - 1.0) / 0.05, 2.0));
    float ringM = exp(-pow((rr - 0.64) / 0.034, 2.0));
    float ringI = exp(-pow((rr - 0.30) / 0.03, 2.0));

    //符文刻带:24 离散格,逐格哈希亮度,缓慢转动(atan2 只进离散格)
    float ang = atan2(q.y, q.x);
    float cellF = (ang / TAU + 0.5 + uTime * 0.055 + uSeed * 0.37) * 24.0;
    float cell = floor(cellF);
    float local = frac(cellF);
    float tickShape = smoothstep(0.16, 0.3, local) * (1.0 - smoothstep(0.7, 0.84, local));
    float cellN = nrm(noise(float2(cell * 0.1131 + uSeed * 1.7, 0.37)));
    float tickBand = smoothstep(0.775, 0.82, rr) * (1.0 - smoothstep(0.92, 0.965, rr));
    float ticks = tickShape * (0.35 + 0.65 * cellN) * tickBand;

    //转辐:sin(3θ) 三辐细光,住内环与中环之间
    float spokes = pow(abs(sin(ang * 3.0 + uTime * 0.9 + uSeed * 2.0)), 32.0);
    float spokeBand = smoothstep(0.3, 0.38, rr) * (1.0 - smoothstep(0.6, 0.66, rr));
    spokes *= spokeBand;

    //盘面薄雾与向心聚能
    float fill = (1.0 - smoothstep(0.85, 1.02, rr)) * 0.045;
    float chargeCore = exp(-rr * rr * 4.0) * uCharge;

    float3 col = uColMid * (fill + ringM * 0.55 + ringI * 0.35 + ticks * 0.85);
    col += uColBright * (ringO * 1.05 + spokes * 0.6 + ticks * 0.35 * cellN);
    col += uColHot * (ringO * ringO * 0.4 + chargeCore * 1.2);

    //光构造:近乎纯加光,只带一点体
    float a = (ringO * 0.10 + ringM * 0.05 + fill * 0.4 + chargeCore * 0.16) * aEnv;
    return float4(col * aEnv, saturate(a)) * uAlpha * vertexColor.a;
}

technique TechCircle
{
    pass CirclePass
    {
        PixelShader = compile ps_3_0 CirclePS();
    }
}

//----------------------------------------------------------------------------
//TechRibbon 金丝带轨道(vs+ps):u=沿带(闭环 0~1,只喂整数倍频),v=横截
//顶点色 r=纵深光照,g=占用辉光,a=透明度
//----------------------------------------------------------------------------
float4 RibbonPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;
    float depthLit = input.Color.r;
    float occupy = input.Color.g;
    float vA = input.Color.a;

    //带体:软缘
    float body = 1.0 - smoothstep(0.72, 1.0, abs(cross_));

    //丝绸窄反射:亮带沿宽度往复滑动(sin 内 u 项 2∈Z 闭环连续)
    float sheenPos = sin(uTime * 1.3 + uv.x * TAU * 2.0) * 0.42;
    float sheen = exp(-pow((cross_ - sheenPos) / 0.24, 2.0));

    //织纹:整数倍频噪声沿带流动
    float weave = nrm(noise(float2(uv.x * 6.0 - uTime * 0.28, uv.y * 1.1 + uSeed)));

    //鎏金缘线
    float edge = exp(-pow((abs(cross_) - 0.8) / 0.09, 2.0));

    float3 col = uColDeep * body * 0.42;
    col += uColMid * body * (0.3 + 0.3 * weave);
    col += uColBright * (sheen * body * 0.5 + edge * 0.75);
    col += (uColBright * 0.32 + uColHot * 0.1) * occupy * body;
    col *= depthLit;

    float a = body * (0.3 + 0.26 * occupy + 0.16 * sheen) * vA;
    //预乘输出:rgb 已按强度构造,a 只承体
    return float4(col * vA, saturate(a)) * uAlpha;
}

technique TechRibbon
{
    pass RibbonPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 RibbonPS();
    }
}

//----------------------------------------------------------------------------
//TechFinale 终曲巨弹体:盘径=画布 0.42,+x=运动方向(C# 按速度转 quad)
//白热芯+金色对流(刚体旋转坐标)+丝带螺旋壳(sin(3θ) 整数倍角)
//前缘压缩弓吃 uCharge(速度),尾侧噪声撕散成流苏
//----------------------------------------------------------------------------
float4 FinalePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / 0.42;
    float r = length(q);
    if (r > 1.42) {
        return float4(0, 0, 0, 0);
    }

    //刚体旋转坐标:核内对流自转
    float th = uTime * 1.5;
    float cs = cos(th);
    float sn = sin(th);
    float2 rq = float2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
    float conv = nrm(noise(rq * 1.15 + uSeed * 2.7));
    float tearN = nrm(noise(float2(q.y * 2.1 + uSeed, q.x * 0.75 - uTime * 1.9)));

    float disc = 1.0 - smoothstep(0.9, 1.02, r);
    float core = exp(-r * r * 16.0);

    //丝带螺旋壳:3 臂对数螺旋,跨 ±π 连续;刻进体色与 alpha,不被芯光淹掉
    float ang = atan2(q.y, q.x);
    float spiralW = sin(ang * 3.0 - log(r * 4.0 + 1.0) * 5.5 + uTime * 2.8);
    float spiral = pow(abs(spiralW), 2.2);
    float groove = 1.0 - pow(saturate(-spiralW), 2.4) * 0.55;   //反相刻槽压暗
    float shellBand = smoothstep(0.18, 0.45, r) * (1.0 - smoothstep(0.9, 1.03, r));

    //前缘压缩弓(+x=运动向),尾侧噪声撕散成流苏
    float dirx = q.x / max(r, 1e-3);
    float bow = exp(-pow((r - 0.9) / 0.12, 2.0)) * saturate(dirx * 1.4 - 0.2) * (0.4 + 0.6 * uCharge);
    float rear = saturate(-dirx);
    float tear = tearN + 0.42 - rear * smoothstep(0.3, 0.95, r) * 1.05;
    float bodyGate = smoothstep(0.0, 0.16, tear);

    //深金缘:亮背景下的剪影保障
    float rim = smoothstep(0.62, 0.95, r) * disc;

    float3 col = uColDeep * disc * 0.55;
    col += uColMid * disc * (0.32 + 0.42 * conv) * groove;
    col = lerp(col, uColDeep * 0.75, rim * 0.55);
    col += uColBright * (spiral * shellBand * 1.05 + bow * 1.15);
    col += uColHot * (core * (1.3 + uFire * 1.3) + bow * 0.45);

    float a = disc * bodyGate * (0.42 + 0.34 * groove + 0.35 * core + 0.18 * uFire);
    return float4(col * bodyGate, saturate(a)) * uAlpha * vertexColor.a;
}

technique TechFinale
{
    pass FinalePass
    {
        PixelShader = compile ps_3_0 FinalePS();
    }
}

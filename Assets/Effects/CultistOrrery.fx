// ============================================================================
//CultistOrrery.fx 拜月教徒·星轨司祭套件,一件效果一个 technique
//TechRing     浑天仪环带(vs+ps 条带):黑鎏金星铜,蚀刻星文+棱边亮线+充能辉流
//TechStarLine 星轨线(vs+ps 条带):轨道椭圆/星图连线,描绘进度+星屑虚线+过载白热
//TechUmbra    本影楔(vs+ps 条带):蚀祭安全区,真暗遮蔽+半影软缘+缘界亮线
//TechLance    冕矛(ps quad):行星表面喷发的辐射矛,预警细丝→焰体喷发,根部球根+末端撕散
//TechShade    蚀盘(ps quad):滑过行星的暗影球,盘径=画布 0.42(与 CultistPlanet 同契约)
//
//混合契约:全部预乘输出进 AlphaBlend;暗成分走 A 通道(真遮蔽),光成分 A≈0 纯加光
//极角审计:无 atan2;闭环 u 参数只喂整数倍频(frac/noise K∈Z),开线段自由
//采样器:噪声固定 s1,消费端 Textures[1]+LinearWrap 显式绑定
// ============================================================================

sampler uImage0 : register(s0);   //画布(SpriteBatch 主贴图,ps quad 技术不采样)
sampler uNoise : register(s1);    //平铺 Perlin

float4x4 transformMatrix;

float uTime;
float uAlpha;        //整体透明度
float3 uColDeep;     //暗底(星铜黑/烟鞘)
float3 uColMid;      //阶段主色
float3 uColBright;   //阶段亮色
float3 uColHot;      //白热芯
float uCharge;       //0~1 充能/过载/全食度
float uProgress;     //0~1 描绘进度/生长
float uDash;         //虚线频率(整数,0=实线)
float uArm;          //0~1 预警蓄势(TechLance)
float uEnv;          //0~1 宽度生命周期包络(TechLance)
float uSeed;         //实例错相

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

//绑定噪声实测值域 0.227~0.776,阈值一律先过归一
float nrm(float n) {
    return saturate((n - 0.227) / 0.549);
}

//----------------------------------------------------------------------------
//TechRing 浑天仪环带:u=沿环 0~1(闭环,只喂整数倍频),v=横截 0~1
//顶点色 rgb=纵深光照,a=透明度;C# 侧近亮远暗烘进顶点
//----------------------------------------------------------------------------
float4 RingPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;   //-1~1
    float depthLit = input.Color.r;      //纵深光照(0.35 远~1.15 近)
    float vA = input.Color.a;

    //带体:横截圆柱明暗,中脊微凸
    float body = 1.0 - smoothstep(0.86, 1.0, abs(cross_));
    float cyl = 0.62 + 0.38 * sqrt(saturate(1.0 - cross_ * cross_));

    //蚀刻星文:沿环整数倍频噪声格,格心亮蚀纹(K=6 闭环连续)
    float glyphN = noise(float2(uv.x * 6.0, uSeed * 0.37 + 0.13));
    float glyphCell = smoothstep(0.42, 0.62, nrm(glyphN));
    //纹只住带中段,不吃棱边
    float glyphBand = smoothstep(0.72, 0.5, abs(cross_));
    float glyph = glyphCell * glyphBand;

    //棱边亮线:两缘金属挑光
    float rim = smoothstep(0.55, 0.92, abs(cross_)) * body;

    //充能辉流:两股反向流光(K=2/3 闭环连续)
    float flow1 = pow(saturate(1.0 - abs(frac(uv.x * 2.0 - uTime * 0.55) - 0.5) * 4.0), 3.0);
    float flow2 = pow(saturate(1.0 - abs(frac(uv.x * 3.0 + uTime * 0.34 + 0.5) - 0.5) * 5.0), 3.0);
    float flow = (flow1 * 0.7 + flow2 * 0.5) * uCharge;

    //合成:暗星铜体(遮蔽)+蚀纹按充能透光+棱边挑光+辉流
    float3 metal = uColDeep * (1.05 + 0.75 * cyl) * depthLit;
    metal += uColMid * 0.10 * cyl * depthLit;   //环体底色微透阶段色,黑背景也有剪影
    float3 col = metal;
    col += uColMid * glyph * (0.30 + 0.9 * uCharge) * depthLit;
    col += lerp(uColMid, uColBright, 0.6) * rim * (0.46 + 0.6 * uCharge) * depthLit;
    col += uColBright * flow * glyphBand * 1.1;
    col += uColHot * flow * flow * 0.45;

    float bodyA = body * 0.92 * vA;
    return float4(col * body * vA, bodyA) * uAlpha;
}

technique TechRing
{
    pass RingPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 RingPS();
    }
}

//----------------------------------------------------------------------------
//TechStarLine 星轨线:u=沿线,v=横截;顶点色 rgb=纵深,a=透明度
//uProgress 描绘进度(u>progress 未画),uDash 星屑虚线频率,uCharge 过载白热
//----------------------------------------------------------------------------
float4 StarLinePS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;
    float depthLit = input.Color.r;
    float vA = input.Color.a;

    //描绘进度:画头一点亮尖
    float visible = smoothstep(uProgress + 0.012, uProgress - 0.03, uv.x);
    float pen = exp(-abs(uv.x - uProgress) * 60.0) * step(uProgress, 0.985) * step(0.01, uProgress);

    //线体:软缘+细芯
    float lineBody = exp(-cross_ * cross_ * 9.0);
    float core = exp(-cross_ * cross_ * 55.0);

    //星屑虚线:亮珠沿线流动(uDash 整数,闭环安全)
    float dashPhase = frac(uv.x * uDash - uTime * 0.5 + uSeed);
    float dash = uDash > 0.5 ? pow(saturate(1.0 - abs(dashPhase - 0.5) * 3.2), 2.0) : 1.0;

    //微光尘:线边缘的散逸星点,沿线缓漂
    float dustN = nrm(noise(float2(uv.x * 5.0 + uSeed * 3.1 - uTime * 0.22, cross_ * 0.7 + uSeed)));
    float dust = pow(dustN, 3.0) * lineBody * 0.5;

    float glow = (lineBody * 0.4 + core) * (0.35 + 0.65 * dash);
    //过载:白热增压+呼吸脉动
    float over = uCharge * (0.86 + 0.14 * sin(uTime * 13.0 + uSeed * 17.0 + uv.x * 4.0));
    glow *= 1.0 + over * 1.6;

    float3 col = uColMid * lineBody * 0.5 + uColBright * glow + uColHot * core * (0.3 + over * 1.2);
    col += uColBright * dust;
    col += uColHot * pen * 2.2;

    float a = saturate(core * 0.30 + over * core * 0.4) * visible * vA;
    return float4(col * visible * vA, a) * uAlpha;
}

technique TechStarLine
{
    pass StarLinePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 StarLinePS();
    }
}

//----------------------------------------------------------------------------
//TechUmbra 本影楔:u=沿影(0 根部/行星侧),v=横截;真暗遮蔽=A 承载
//uProgress=全食度,缘界亮线声明安全区边界(缺口即所见)
//----------------------------------------------------------------------------
float4 UmbraPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float cross_ = (uv.y - 0.5) * 2.0;
    float vA = input.Color.a;

    //影体:横截平台+半影软缘
    float shadowCore = 1.0 - smoothstep(0.55, 0.96, abs(cross_));
    //根部收进行星轮廓,末端软散
    float lenEnv = smoothstep(0.0, 0.05, uv.x) * (1.0 - smoothstep(0.82, 1.0, uv.x));
    //影内星尘:安全区里安静的微光
    float starN = nrm(noise(float2(uv.x * 3.0 + uSeed, cross_ * 1.4 + uSeed * 2.3 - uTime * 0.02)));
    float stars = pow(starN, 6.0) * shadowCore;

    float dark = shadowCore * lenEnv * uProgress;

    //缘界亮线:安全走廊的可见边界
    float edgeLine = exp(-pow(abs(abs(cross_) - 0.78), 2.0) * 260.0) * lenEnv * uProgress;

    float3 col = float3(0.004, 0.006, 0.012) * dark;
    col += uColBright * stars * 0.20 * uProgress;
    col += lerp(uColMid, uColBright, 0.5) * edgeLine * 0.34;

    float a = dark * 0.60 * vA;
    return float4(col * vA, a) * uAlpha;
}

technique TechUmbra
{
    pass UmbraPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 UmbraPS();
    }
}

//----------------------------------------------------------------------------
//TechLance 冕矛(ps quad):uv.x=沿矛(0 根部),uv.y=横截
//uArm 预警细丝相,uEnv 宽度生命周期(0 未展~1 全宽),uCharge 炽度
//根部球根收口,末端噪声撕散,缘带暗烟鞘(亮空也有剪影)
//----------------------------------------------------------------------------
float4 LancePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float u = coords.x;
    float cross_ = (coords.y - 0.5) * 2.0;

    //宽度生命周期:展开自根部先宽,沿程收窄,末端撕尖
    float widthProfile = (1.0 - u * 0.42) * uEnv;
    //根部球根:0~12% 段略鼓
    widthProfile *= 1.0 + 0.35 * exp(-u * u * 90.0);
    float w = max(widthProfile, 1e-3);
    float q = abs(cross_) / w;

    //---- 预警细丝:全长极细亮线+根部呼吸辉点 ----
    float fil = exp(-cross_ * cross_ * 700.0) * uArm * (0.55 + 0.45 * sin(uTime * 9.0 + uSeed * 13.0));
    float armRoot = exp(-u * u * 40.0) * exp(-cross_ * cross_ * 24.0) * uArm;

    //---- 焰体:双尺度粒组织沿矛外流 ----
    float g1 = noise(float2(u * 2.6 - uTime * 1.15 + uSeed * 5.1, cross_ * 0.8 + uSeed));
    float g2 = noise(float2(u * 5.8 - uTime * 1.9 + uSeed * 2.9, cross_ * 1.7 + 0.37));
    float gran = nrm(g1) * 0.62 + nrm(g2) * 0.38;

    //末端撕散:最后 30% 噪声阈值蚀开成流苏
    float tearTh = smoothstep(0.68, 1.0, u) * 0.85;
    float tear = step(tearTh, gran * (1.0 - q * 0.55) + 0.18);

    float plume = saturate(1.0 - q) * tear;
    float bodyMask = plume * step(q, 1.0);

    //分层:暗烟鞘(缘)→主焰→亮芯→白热线
    float sheath = smoothstep(0.55, 0.98, q) * step(q, 1.0) * tear;
    float flame = pow(saturate(1.0 - q), 1.4) * (0.5 + 0.7 * gran);
    float coreLine = exp(-q * q * 18.0) * (0.6 + 0.4 * gran);
    float hot = exp(-q * q * 60.0) * smoothstep(0.35, 0.9, uCharge);

    //根部增亮:出膛处最烈
    float rootBoost = 1.0 + 1.3 * exp(-u * u * 26.0);

    float3 col = uColMid * flame * rootBoost;
    col += uColBright * coreLine * rootBoost * (0.5 + 0.6 * uCharge);
    col += uColHot * hot * rootBoost;
    col = lerp(uColDeep * 0.6, col, saturate(1.0 - sheath * 0.8));
    col *= bodyMask;

    //预警层叠加
    col += uColBright * fil * 0.9 + uColMid * armRoot * 0.8;

    //暗鞘承 A:亮空之下也有剪影;焰体本体半实
    float a = (sheath * 0.55 + plume * 0.30) * uEnv * step(q, 1.0);
    a += fil * 0.04;

    return float4(col, saturate(a)) * uAlpha * vertexColor;
}

technique TechLance
{
    pass LancePass
    {
        PixelShader = compile ps_3_0 LancePS();
    }
}

//----------------------------------------------------------------------------
//TechShade 蚀盘(ps quad):盘径=画布 0.42(CultistPlanet 同契约)
//uCharge=全食度:贴缘冕环随全食亮起(钻石环),盘体真暗遮行星
//----------------------------------------------------------------------------
float4 ShadePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float2 q = p / 0.42;
    float rq = length(q);
    if (rq > 1.5) {
        return float4(0, 0, 0, 0);
    }

    float inDisc = 1.0 - smoothstep(0.985, 1.01, rq);

    //盘体:近黑影球,弱环形山起伏透一点冷光
    float relief = nrm(noise(q * 1.3 + uSeed * 3.7)) * 0.05;
    float3 body = float3(0.006, 0.008, 0.015) + uColDeep * relief;

    //冕环:全食时贴缘一圈炽光泄出
    float corona = exp(-abs(rq - 1.02) * 16.0) * smoothstep(0.985, 1.06, rq);
    float coronaN = 0.7 + 0.3 * nrm(noise(q * 0.9 + uTime * 0.05));
    float3 coronaCol = lerp(uColMid, uColBright, coronaN) * corona * uCharge * 1.4;
    coronaCol += uColHot * corona * corona * uCharge * 0.8;

    float a = inDisc * 0.97;
    float3 C = body * a + coronaCol;
    return float4(C, a + corona * uCharge * 0.08) * uAlpha * vertexColor;
}

technique TechShade
{
    pass ShadePass
    {
        PixelShader = compile ps_3_0 ShadePS();
    }
}

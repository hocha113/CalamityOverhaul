// ============================================================================
//OniGateRift.fx 鬼门开缝斩痕(主流扫掠光刃,环带几何,band-local UV,无极角输入)
//uv.x=uc 沿刃 0起笔..1收笔  uv.y=v 径向 0=内缘(软融) 1=外缘(刀尖轨迹,锐利)
//顶点色 R=归一化z(0.5=屏面) A=预留逐切片衰减,远近分层与纵深压暗由R驱动
//
//语法=可见的快速扫掠(鸣潮/原神系):
//  uHead 刃头揭开位置,刃带着光走,头前0.05软羽+白热刃头线(uLead)
//  uFlash 落位满形闪(money frame,1~2帧速落)
//  uErode 定向消散,尾端先蚀向刃头,亮度沉降
//横截面=阔剑三层:外缘锐利+发丝暗边压形(亮天空立形),白热核心贴外缘,
//  饱和绯红体被沿刃拉丝承载,内缘软融沉深红——径向亮度单调,单边锋利
//uGateT 鬼门大开(仅终结拍定格期):外缘豁开黑缝+冷魂火,消散初段闭合
//宏观通道吃uSeed(出生冻结),细节通道吃uDetailSeed(定格步进重掷),无uTime滚动
//预乘alpha输出,配BlendState.AlphaBlend;直线算术+tex2D,无分支属性/无tex2Dlod
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uSeed;         //宏观噪声相位,出生冻结
float uDetailSeed;   //细节通道相位,定格步进重掷,消散冻结
float uHead;         //刃头揭开位置(0..1沿uc)
float uLead;         //刃头亮线强度(扫掠期1,定格速落)
float uFlash;        //满形定格闪(落位帧起速落)
float uErode;        //定向消散进度(尾→头)
float uGateT;        //鬼门大开程度(仅终结拍)
float uOpacity;      //整体不透明度
float uFarSel;       //远近分层:0=整体 +1=仅近半 -1=仅远半(玩家身后层)
float uFarDim;       //远半侧压暗地板(0=不分层)
float uU0;           //可见窗起(uc)
float uU1;           //可见窗止
float uEmber;        //0..1魂火密度(终结拍)
float uTelegraph;    //应力线强度(TelegraphTech)

float3 uColHot;      //白热核心
float3 uColBurn;     //亮绯红
float3 uColDeep;     //深红
float3 uColVoid;     //近黑沉边
float3 uColGlow;     //冷渗光(仅终结拍鬼门缝)

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

texture uBrushTex;
sampler brushSamp = sampler_state
{
    texture = <uBrushTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = clamp;
};

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

//可见窗端部羽化
float WindowFeather(float uc)
{
    return smoothstep(uU0, uU0 + 0.045, uc) * (1.0 - smoothstep(uU1 - 0.045, uU1, uc));
}

float4 PSRift(PSInput input) : COLOR0
{
    float uc = input.TexCoords.x;
    float v = input.TexCoords.y;              //0内缘 1外缘(刀尖轨迹)
    float zN = input.Color.r * 2.0 - 1.0;     //-1..1,+朝观者

    //远近半侧分层,身后半侧交玩家背后层;连续纵深压暗(FarDim=0时不压)
    float farW = smoothstep(0.10, -0.10, zN);
    float passMul = 1.0;
    if (uFarSel > 0.5)
        passMul = 1.0 - farW;
    else if (uFarSel < -0.5)
        passMul = farW;
    float dimFloor = lerp(1.0, uFarDim, step(0.01, uFarDim));
    float depthDim = lerp(1.0, dimFloor, saturate(-zN));

    float wf = WindowFeather(uc);
    if (passMul * wf < 0.005)
        return float4(0, 0, 0, 0);

    //扫掠揭开:刃头之后不存在,头前软羽;刃头白热亮线(刀所在处)
    float reveal = smoothstep(uHead + 0.012, uHead - 0.055, uc);
    float lead = exp(-pow((uc - uHead) / 0.032, 2.0)) * uLead;
    if (reveal + lead < 0.006)
        return float4(0, 0, 0, 0);

    //定向消散:尾端(uc小)先蚀,噪声撕出缺口推向刃头
    float eN = tex2D(noiseSamp, float2(uc * 3.0 + uSeed * 4.7, v * 1.5 + uDetailSeed * 3.1)).r;
    float survive = smoothstep(-0.04, 0.10, uc * 0.95 + eN * 0.30 - uErode * 1.35);

    //各向异性拉丝:双八度沿刃,出生冻结不滚动
    float4 b1 = tex2D(brushSamp, float2(uc * 1.60 + uSeed * 5.13, v * 0.90));
    float4 b2 = tex2D(brushSamp, float2(uc * 3.40 - uSeed * 7.31 + 0.37, v * 0.55 + 0.21));
    float streak = b1.r * b1.a * 0.85 + b2.r * b2.a * 0.55;

    //阔剑横截面:内缘软融→体→外缘锐利;白热核心贴外缘
    float innerFade = smoothstep(0.0, 0.30, v);
    float outerCut = 1.0 - smoothstep(0.965, 1.0, v);
    float core = exp(-pow((v - 0.90) / 0.062, 2.0));

    //体量:拉丝承载,满形闪抬透密下限
    float bodyA = innerFade * outerCut * (0.42 + streak * 0.38 + uFlash * 0.18);

    //色带:内沉深红→亮绯红→白热核心,单调升温
    float3 col = lerp(uColDeep, uColBurn, smoothstep(0.10, 0.80, v));
    col = lerp(uColVoid * 2.2, col, innerFade);
    col += uColBurn * streak * 0.45 * innerFade;
    col += uColHot * core * (0.85 + uFlash * 0.95);

    //外缘发丝暗边,亮天空下立形(阔剑黑边)
    float rimDark = smoothstep(0.978, 1.0, v);
    col = lerp(col, uColVoid * 1.3, rimDark * 0.60);

    //刃头亮线(刀正在这里)+满形闪整体提亮一档
    col += uColHot * lead * 1.65;
    col += uColHot * uFlash * 0.30 * (0.35 + core * 0.65);

    //消散期色沉(能量冷却)
    col = lerp(col, uColDeep * 0.55, uErode * 0.55);

    //鬼门大开(仅终结拍定格):外缘豁开黑缝+冷渗光+魂火,消散初段闭合
    float gateOn = saturate(uGateT * 8.0);
    float seamW = 0.15 * uGateT;
    float seam = smoothstep(1.0 - seamW * 1.9, 1.0 - seamW * 0.55, v) * gateOn;
    col = lerp(col, uColVoid * 0.55, seam * 0.85);
    float glintN = tex2D(noiseSamp, float2(uc * 9.0 + uDetailSeed * 12.1, 0.5 + uSeed * 0.37)).r;
    col += uColGlow * smoothstep(0.72, 0.88, glintN) * seam * 1.1;
    float emberN = tex2D(noiseSamp, float2(uc * 5.5 + uDetailSeed * 9.7, 0.83)).r;
    col += uColGlow * smoothstep(0.78, 0.92, emberN) * seam * uEmber * 1.5;

    col *= depthDim;

    //alpha:体×揭开×存活 + 刃头线;预乘输出
    float alpha = saturate(bodyA * reveal * survive + lead * 0.60 * innerFade);
    alpha *= wf * passMul * uOpacity * input.Color.a;

    return float4(col * alpha, alpha);
}

float4 PSTelegraph(PSInput input) : COLOR0
{
    float uc = input.TexCoords.x;
    float v = input.TexCoords.y;
    float dCtr = abs(v - 0.5) * 2.0;

    float wf = WindowFeather(uc);
    //绷紧应力线:蓄势期沿未来刀尖轨迹的暗红发丝,静止微颤(seed驱动,不流动)
    float stress = tex2D(noiseSamp, float2(uc * 4.1 + uSeed * 6.3, 0.5)).r;
    float lineBand = 1.0 - smoothstep(0.0, 0.62, dCtr);
    float3 col = uColDeep * (0.55 + stress * 0.40) + uColBurn * 0.30 * lineBand;
    float alpha = lineBand * uTelegraph * (0.32 + stress * 0.25) * wf * uOpacity * input.Color.a;

    return float4(col * alpha, alpha);
}

technique RiftTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSRift();
    }
}

technique TelegraphTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSTelegraph();
    }
}

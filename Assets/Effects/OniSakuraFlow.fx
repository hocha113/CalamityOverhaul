// ============================================================================
//OniSakuraFlow.fx 鬼切樱流（花吹雪的流路 + 螺旋花涡）
//
//TechStream 航线三角带：uv.x=u 沿航线 0=尾 1=头(花核)，uv.y 横向 0..1
//  与神威流带(OniKamuiFlow)的分野：那条是墨绸——连续绸面、撕裂舌、暗涡压近黑、
//  蒸发烧蚀橙边。这条是"很多瓣挤在同一条流里"，五条签名行为：
//   1) 带内是瓣粒密度场：高频噪声过阈值切成离散团块，沿流向低频横向高频
//      → 团块被速度抹成条，团块之间留孔，读得出"粒"不是"绸"；
//   2) 花瓣是一阵一阵的：沿带低频 gust 涌动把密度攒成团──不是匀质软管；
//   3) 边界不撕裂，是瓣粒剥落：近边处直接乘密度场，尾段咬到断成分离小块；
//   4) 稀薄处压透明而非压黑；亮来自密度与稀疏瓣面反光斑，不来自白热
//      （尾暗→白热的能量拖尾腔是刀/激光的语法，瓣不发热）；
//   5) uRetract 回卷自尾端推进，前沿是褪色发白的边（瓣被召回，不是被烧掉）。
//
//TechCoreBloom 花核 quad：螺旋花涡——被攥紧的一团花吹雪，不是盖章的樱花图案。
//  三条螺旋臂向心卷入，臂身由瓣粒组成（瓣粒噪声走刚体旋转坐标，双速率视差），
//  心口攥成近实的樱色，外缘被瓣粒咬开。
//  极角审计：theta 的唯一消费是 sin(3θ - k·logR + spin)，3∈ℤ 跨 ±π 连续（安全表
//  "sin(armN·m·φ), m∈ℤ"行）；logR 单调连续；噪声全走 Rot(t)·(x,y) 刚体旋转，无缝。
//  uAxis/uStretch 沿运动轴拉长形体，拖影由 C# 多画一遍偏移 quad 承担。
//
//直线算术 + 纯 tex2D，无动态分支、无 tex2Dlod。预乘输出，配 AlphaBlend。
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //秒
float uOpacity;     //整体不透明度

//---- TechStream ----
float uRetract;     //0..1 尾端定向蒸发进度（回卷召回）
float uLenScale;    //航线长 / 噪声瓦片长（沿带噪声重复次数）
float uSeed;        //子带随机相位
float uFlowMul;     //流速倍率（子带各异 → 层间视差）
float uGrainAmp;    //瓣粒分明度（0=近连续 1+=碎成瓣块）
float uHeadBoost;   //头段瓣白中脊强度
float uFlash;       //0..1 全形过曝帧（起飞/合拢瞬间）

//---- TechCoreBloom ----
float uSpin;        //rosette 基准角(rad)
float uStretch;     //沿 uAxis 的拉长倍率(1=不拉)
float2 uAxis;       //拉伸轴(单位向量，p 空间=世界空间)
float uBloom;       //瓣缘白亮增益
float uHeartHeat;   //瓣心热度

float3 uColHot;     //瓣白热（微粉）
float3 uColBright;  //亮樱
float3 uColDeep;    //深绯
float3 uColDark;    //墨绯底
float3 uColWashi;   //表世界泛黄和纸（中段注色）

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

float4 PSStream(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;                //0=尾 1=头
    float cy = (input.TexCoords.y - 0.5) * 2.0; //-1..+1 横向
    float s = u * uLenScale;                    //世界稳定的沿带坐标

    //---- domain warp：低频卷曲场，后续采样共享 ----
    //噪声贴图亮度型(r=g=b)，两分量须错位采样取独立通道
    float2 wUV = float2(s * 0.15 - uTime * 0.050 * uFlowMul, cy * 0.20 + uSeed * 2.7);
    float2 warp = float2(tex2D(noiseSamp, wUV).r
        , tex2D(noiseSamp, wUV + float2(0.29, 0.53)).r) - 0.5;
    warp *= 0.30;

    //---- 双八度流动：不同流速的视差层 ----
    float2 f1UV = float2(s * 0.38 - uTime * 0.78 * uFlowMul, cy * 0.26 + uSeed) + warp;
    float2 f2UV = float2(s * 1.02 - uTime * 1.45 * uFlowMul + 0.41, cy * 0.48 + uSeed * 1.9) + warp * 1.7;
    float n1 = tex2D(noiseSamp, f1UV).r;
    float n2 = tex2D(noiseSamp, f2UV).r;
    float flow = n1 * 0.60 + n2 * 0.40;

    //---- 瓣粒密度场：沿流向低频、横向高频 → 团块被速度抹成条而不是圆点 ----
    float2 gUV = float2(s * 0.70 - uTime * 1.05 * uFlowMul, cy * 2.20 + uSeed * 0.7) + warp * 0.6;
    float2 g2UV = float2(s * 1.52 - uTime * 1.80 * uFlowMul + 0.23, cy * 3.55 + uSeed * 1.3);
    float grain = tex2D(noiseSamp, gUV).r * 0.66 + tex2D(noiseSamp, g2UV).r * 0.34;
    float gTh = 0.30 + 0.24 * uGrainAmp;
    float clump = smoothstep(gTh, gTh + 0.30, grain + flow * 0.22);

    //---- 头端收束：末 20% 聚拢成钝尖插进花核（pow<1 → 尖端快张缓平）----
    float taper = pow(saturate((1.0 - u) / 0.20), 0.70);

    //---- 边界：低频起伏的瓣缘，收束段免咬（尖要干净）----
    float bN = tex2D(noiseSamp, float2(s * 0.28 - uTime * 0.40 * uFlowMul, 0.18 + uSeed * 0.47)).r;
    float boundary = (0.97 - uGrainAmp * (0.14 + 0.28 * (1.0 - u)) * bN) * taper;
    float aEdge = smoothstep(boundary, boundary - (0.30 * taper + 0.040), abs(cy));

    //---- 剥落：近边带与尾段按密度场开孔，尾端整段碎成分离瓣块 ----
    float edgeZone = smoothstep(boundary - 0.44 * taper, boundary, abs(cy));
    float shatter = saturate(edgeZone * 0.78 + (1.0 - u) * 0.52);
    float grainMask = lerp(1.0, clump, saturate(shatter * uGrainAmp));

    //---- 回卷召回：自尾端推进；斜率 2.3 保证 uRetract=1 时头端也擦净 ----
    float eTh = uRetract * 2.3 - u * 1.15 + (1.0 - u) * 0.14 * uGrainAmp;
    float survive = smoothstep(eTh - 0.03, eTh + 0.16, flow);
    float recall = smoothstep(eTh - 0.20, eTh - 0.03, flow) * (1.0 - survive);

    //---- 尾端羽化（头端交给收束尖，不平切）----
    float capA = smoothstep(0.0, 0.050, u);

    //---- gust 涌动：花瓣一阵一阵地来，头段豁免（插核那截要稳） ----
    //波长约 170px(s 系数 9)，相位随时间缓移；各股共享 s，仅 uSeed 差出松散感
    float gust = 0.70 + 0.40 * sin(s * 9.0 - uTime * 5.0 + uSeed * 0.9);
    gust = lerp(saturate(gust), 1.0, smoothstep(0.72, 0.94, u));

    //---- alpha：稀薄处压透明，不压黑；亮度交给密度 ----
    float body = saturate(0.18 + flow * 0.92);
    float alpha = aEdge * capA * survive * body * grainMask * gust;
    alpha = saturate(alpha * lerp(1.0, 1.30, saturate(uFlash))) * uOpacity;
    if (alpha < 0.004 && recall < 0.05)
        return float4(0, 0, 0, 0);

    //---- 色带：尾 墨绯 → 深绯 → 亮樱 → 头微微泛白。终端白化压到 0.45：
    //瓣不发热，头端的"亮"主要由密度(alpha)与花核自己扛 ----
    float heat = saturate(pow(u, 1.45));
    float3 col = lerp(uColDark, uColDeep, smoothstep(0.0, 0.42, heat));
    col = lerp(col, uColBright, smoothstep(0.42, 0.84, heat));
    col = lerp(col, uColHot, smoothstep(0.84, 1.0, heat) * 0.45);

    //中段注一点和纸黄，别让整条都是纯樱（表世界的底色透出来）
    float washiBand = smoothstep(0.08, 0.42, u) * (1.0 - smoothstep(0.60, 0.94, u));
    col = lerp(col, col * uColWashi, washiBand * (0.18 + 0.28 * n1));

    //瓣面反光斑：只挑噪声峰值，稀疏的亮斑在流里闪——不是整片 sheen
    float glint = smoothstep(0.78, 0.96, grain) * clump;
    col += uColHot * glint * (0.34 + heat * 0.46) * 0.75;

    //头端中脊压暗压窄：只作"流路插进花核"的一点余温，白热常驻是能量腔
    float coreW = 0.26 * max(taper, 0.08);
    float ridge = exp(-pow(cy / coreW, 2.0)) * smoothstep(0.42, 0.95, u) * uHeadBoost;
    ridge *= 1.0 + (1.0 - taper) * 0.70;
    col += (uColHot * 0.55 + uColBright * 0.45) * ridge * 0.62;

    //召回前沿：褪色发白
    col += uColHot * recall * 1.55;
    //全形白闪：提亮一拍而非擦掉重画
    col = lerp(col, col + uColHot * 0.50, saturate(uFlash));

    //预乘输出 + 白闪的加色余量（中脊的加色份额压到 0.12，防头部亮度堆积）
    float3 extra = uColHot * (ridge * 0.12 + saturate(uFlash) * 0.10) * capA * survive * uOpacity;
    return float4(col * alpha + extra, alpha);
}

//螺旋花涡：三条瓣粒臂向心卷入 + 近实心口 + 瓣粒咬边。
//"被攥紧的一团花吹雪"——不对称、有进动，不是居中自转的樱花图案
float4 PSCoreBloom(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;

    //沿运动轴拉长：quad 两轴都按 uStretch 放大，横轴在此压回 → 只有轴向变长
    float2 ax = uAxis;
    float2 nx = float2(-ax.y, ax.x);
    float along = dot(p, ax);
    float across = dot(p, nx) * uStretch;
    float2 ps = ax * along + nx * across;
    float r = length(ps);

    //瓣粒噪声：刚体旋转坐标（无缝），双速率两层 → 涡内自带视差
    float ca = cos(uSpin);
    float sa = sin(uSpin);
    float2 pr1 = float2(ps.x * ca + ps.y * sa, -ps.x * sa + ps.y * ca);
    float cb = cos(uSpin * 0.55);
    float sb = sin(uSpin * 0.55);
    float2 pr2 = float2(ps.x * cb + ps.y * sb, -ps.x * sb + ps.y * cb);
    float g1 = tex2D(noiseSamp, pr1 * 0.60 + uSeed).r;
    float g2 = tex2D(noiseSamp, pr2 * 1.30 + uSeed * 1.7 + 0.31).r;
    float grain = g1 * 0.58 + g2 * 0.42;
    float clump = smoothstep(0.34, 0.66, grain);

    //螺旋臂：theta 只进 sin(3θ-…)，3∈ℤ 跨 ±π 连续；臂随 uSpin 进动
    float theta = atan2(ps.y, ps.x);
    float logR = log(r * 2.6 + 1.0);
    float arm = 0.5 + 0.5 * sin(3.0 * theta - logR * 5.2 + uSpin * 2.4);
    float armMask = smoothstep(0.18, 0.80, arm);

    //心口近实、外缘被瓣粒咬开
    float heart = smoothstep(0.34, 0.10, r);
    float edgeR = 0.96 - 0.16 * grain;
    float bodyA = smoothstep(edgeR, edgeR - 0.34, r);
    float density = saturate(heart * (0.72 + 0.28 * clump)
        + bodyA * armMask * (0.28 + 0.72 * clump) * (1.0 - heart));

    float alpha = density * input.Color.a * uOpacity;
    if (alpha < 0.004)
        return float4(0, 0, 0, 0);

    //色由密度驱动：疏处深绯 → 密处樱 → 心口淡樱白；不做白热常驻，
    //核的"亮"靠与流带/拖影的对比读出来，不靠把 RGB 顶到 1
    float3 col = lerp(uColDeep, input.Color.rgb, saturate(density * 1.15));
    col = lerp(col, float3(1.0, 0.93, 0.95), heart * uHeartHeat * 0.42);
    //稀疏瓣面反光斑，涡身里的碎闪
    float glint = smoothstep(0.80, 0.96, grain) * bodyA;
    col += uColHot * glint * uBloom * 0.35;

    //加色余量只给反光斑（点不是体）
    float3 extra = uColHot * glint * uBloom * 0.08 * input.Color.a * uOpacity;
    return float4(col * alpha + extra, alpha);
}

technique TechStream
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSStream();
    }
}

technique TechCoreBloom
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSCoreBloom();
    }
}

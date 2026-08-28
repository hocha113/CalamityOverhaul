// ============================================================================
//WofRetinaBeam.fx 视网膜扫描光束/腐眼斩束共用
//UV.x 0末端→1眼口 UV.y 横截面；有机血光：暗血鞘+湿核+毛细血管缘+缓脉冲
//顶点带 transformMatrix，DrawUserPrimitives 使用；输出预乘alpha，
//C#侧配 BlendState.AlphaBlend：暗鞘真正压暗背景(实体遮挡)，亮芯嵌在暗体内
//
//坐标契约：纵向特征一律像素域(uQuadLen 折算)，quad 3200px 级别时噪声密度
//不再被归一化 UV 拉稀；根部处理压缩在眼球贴图能盖住的 ~100px 内，
//杜绝旧版 14% 长度的白热矛头(眼前莫名特效的元凶)
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;    //整体透明度 0~1
float seed;         //实例种子，上下眼错相
float uScanTurn;    //扫描折返增亮 0~1
float uQuadLen;     //quad总长px(眼后起始边→末端)，全部纵向特征的折算基准
float uBend;        //扫掠鞭滞弯曲 -1~1(消费端按角速度平滑喂入；直束喂0)

// 噪声固定 s1：sampler_state 自动分配会落 s0，图元路径今日侥幸、批次路径必坏；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

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

//PerlinNoise 实测值域 0.227~0.776(中心~0.50)，归一到 -1~1
float nrm(float x)
{
    return clamp((x - 0.5) * 3.6, -1.0, 1.0);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;                  //0 末端 → 1 眼口
    float cross_ = (uv.y - 0.5) * 2.0;   //-1 ~ 1 横截面
    float headPx = (1.0 - along) * uQuadLen;   //距眼侧起始边像素
    float tipPx = along * uQuadLen;            //距末端像素

    //末端撕散成血雾舌。采样密度守则：512 texel 噪声，除数≥900 才保证亚 texel
    //采样(texel/px=512/除数≤0.57)，更小的除数=逐像素散列，画面读作电弧/纸屑
    float tipTurb = nrm(tex2D(noiseSamp, float2(tipPx / 950.0 - uTime * 0.55, cross_ * 0.5 + seed + 0.5)).r);
    float tipFadePx = tipPx + tipTurb * 190.0;
    float tailFade = smoothstep(0.0, 560.0, tipFadePx);
    if (tailFade * fadeAlpha < 0.002)
    {
        return float4(0, 0, 0, 0);
    }
    float taper = lerp(0.30, 1.0, smoothstep(0.0, 680.0, tipFadePx));

    //主轴湿摆：慢漂+细澜双倍频，根端 430px 内锚死在眼口(束绕眼转，根不甩)
    float rootAnchor = smoothstep(0.0, 430.0, headPx);
    float wob1 = nrm(tex2D(noiseSamp, float2(headPx / 2300.0 - uTime * 0.15, seed)).r) * 0.13;
    float wob2 = nrm(tex2D(noiseSamp, float2(headPx / 2000.0 + uTime * 0.20, seed + 0.61)).r) * 0.035;
    //鞭滞：扫掠时末端拖在转动方向后面，二次方随距离加深
    float lagT = saturate(headPx / max(uQuadLen, 1.0));
    float axis = (wob1 + wob2) * rootAnchor + uBend * lagT * lagT;

    //缘蚀：轮廓宽度沿程±11%起伏，双轨不再平行
    float eWob = nrm(tex2D(noiseSamp, float2(headPx / 1600.0 + uTime * 0.16, seed + 0.23)).g);
    //根口短收：出眼 160px 内从 0.60 宽张到满宽，束读作"自眼里长出"而非横穿
    //眼球的粗管(眼贴图只有 ~50px)；短距离+核不增白，不会回退成旧版针尖
    float rootPinch = lerp(0.60, 1.0, smoothstep(0.0, 160.0, headPx));
    //轮廓与核分宽：血肉鞘吃缘蚀显撕裂，血浆核保持液态平滑(核吃缘蚀会放大
    //噪声细倍频成逐列梳纹——hot 的 300 倍高斯对宽度变化过敏)
    float silMul = taper * rootPinch * (1.0 + 0.11 * eWob);
    float widthMul = taper * rootPinch;
    float dSil = abs(cross_ - axis) / silMul;
    float d = abs(cross_ - axis) / widthMul;

    //湿核：柔宽体+窄亮芯(血不走白热，芯是苍白粉)
    float core = exp(-d * d * 34.0);
    float hot = exp(-d * d * 300.0);

    //毛细血管缘：两条细丝贴着鞘缘蜿蜒(轮廓族，吃缘蚀宽)
    float cap1 = nrm(tex2D(noiseSamp, float2(headPx / 1300.0 + uTime * 0.28, seed + 0.37)).g) * 0.5;
    float dCap1 = abs(cross_ - (0.34 + cap1 * 0.30) * silMul) / silMul;
    float cap2 = nrm(tex2D(noiseSamp, float2(headPx / 1050.0 - uTime * 0.35, seed + 0.71)).g) * 0.5;
    float dCap2 = abs(cross_ + (0.36 + cap2 * 0.28) * silMul) / silMul;
    float capillary = exp(-dCap1 * dCap1 * 700.0) * 0.7 + exp(-dCap2 * dCap2 * 700.0) * 0.7;
    //血管断续蜿蜒(阈值 0.32/0.55 落在噪声实测值域 0.227~0.776 内)
    float capGate = smoothstep(0.32, 0.55, tex2D(noiseSamp, float2(headPx / 2400.0 + uTime * 0.25, seed + 0.53)).r);
    capillary *= capGate;

    //黏稠脉冲：厚亮团自眼口涌向末端，约 1270px/s
    float pulse = frac(headPx / 820.0 - uTime * 1.55);
    float pulseGlow = exp(-pow((pulse - 0.5) * 3.4, 2.0)) * 0.5 * core;

    float halo = exp(-dSil * dSil * 2.6) * 0.5;
    float edgeMask = smoothstep(1.0, 0.80, abs(cross_));

    //暗血鞘：包住亮芯的湿肉暗体，预乘AlphaBlend下高alpha低色值真正压暗背景，
    //光束从纯光变成有暗缘的实体(契约4遮挡层)
    float sheath = exp(-dSil * dSil * 11.0);

    //根部包络：46px 硬保险(起始边永藏眼内) × 96px 体淡入，全部藏在眼球贴图后
    float rootGrow = smoothstep(0.0, 46.0, headPx);
    float rootIn = smoothstep(12.0, 96.0, headPx);

    //喉口辉：出眼 ~200px 内核部增亮，乘在体内(不再是独立矛头)
    float throat = smoothstep(210.0, 30.0, headPx) * core * (1.0 + uScanTurn * 0.5);

    //调色：暗血→猩红→苍白粉芯
    float3 cBlood = float3(0.55, 0.05, 0.07);
    float3 cRed   = float3(0.92, 0.13, 0.10);
    float3 cCore  = float3(1.00, 0.62, 0.58);
    float3 cCap   = float3(0.98, 0.28, 0.20);
    float3 cDark  = float3(0.14, 0.015, 0.025);

    float bodyMask = rootIn * tailFade * edgeMask;
    float turnBoost = 1.0 + uScanTurn * 0.5;
    float3 color = float3(0, 0, 0);
    color += cDark * sheath * 0.6;
    color += cRed * core * 0.95 * turnBoost;
    color += cCore * hot * 1.05;
    color += cCap * capillary;
    color += cRed * pulseGlow;
    color += cBlood * halo;
    color += cCore * throat * 0.50;
    color += cRed * throat * 0.25;
    color *= bodyMask;

    float alpha = saturate(
        (sheath * 0.85 + core * 0.72 + hot * 0.9 + capillary * 0.55 + pulseGlow * 0.45 + halo * 0.4 + throat * 0.30)
        * bodyMask);
    alpha *= fadeAlpha * rootGrow;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass WofRetinaBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

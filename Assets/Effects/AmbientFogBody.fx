// ============================================================================
//AmbientFogBody.fx 残酷环境共享雾体(密度场单 pass,合成 alpha 有界)
//替代 Fog 贴图多层堆叠:堆叠饱和 1-(1-a)^N 不受控 → 本文件密度算完只出一次 alpha
//TechWall 画布契约:U=厚度向(0 尾缘,1 前缘;左行墙由 C# FlipHorizontally),V=长轴
//TechPool 画布契约:U=横向,V=纵向(uAnchor=1 贴地带顶缘侵蚀,0 悬浮盘上下对称)
//消费端 quad=可见体全尺寸+撕缘余量,内容在画布内自然归零
//整文件 ps-only(SpriteBatch 家族),禁加带 VS 的 technique(混批污染案 2026-08-27)
//噪声 s1=PerlinNoise LinearWrap;G 通道实测值域 0.227~0.776,阈值一律过 nrm()
//输出预乘 alpha 进 AlphaBlend:雾是遮挡体,暗色能压暗(亮雾悖论条款)
// ============================================================================

float uTime;
float uSeed;            //实体相位(identity 派生,防同屏同纹)
float2 uCanvasPx;       //画布像素尺寸
float2 uNoiseOffsetPx;  //噪声锚定偏移(世界锚/实体锚由消费端选)
float4 uColorBody;      //浓核体色
float4 uColorEdge;      //撕缘亮色(前缘新雪/亮丝)
float uMaxAlpha;        //合成不透明度上限
float uDensity;         //密度乘子(包络/呼吸)
float uFlowPx;          //介质内流速(px/s,沿流向)
float uStreak;          //定向流丝强度 0~1(0 关)
float uLight[8];        //沿长轴 8 点环境光系数(墙=V 向,盘=U 向)

//---- TechWall 专属 ----
float uFrontBias;       //密度峰位置 U(0.7 前缘浓;0.5 近对称雾幕)
float uFill;            //1=满幅充盈(镜头雾化层:关厚度向剖面与前缘截止)
float uSeamV;           //明窗中心 V(负值=无窗)
float uSeamHalfV;       //明窗半高(V 比例)
float uTaperV;          //长轴两端收口比例

//---- TechPool 专属 ----
float uAnchor;          //1=贴地带,0=悬浮盘
float uCrownV;          //顶冠侵蚀深度(V 比例,贴地带用)
float uEdgePow;         //横向包络锐度(越大边缘宽限带越长)
float uSwirl;           //盘内缓旋(rad/s,贴地带给 0)

sampler noiseSamp : register(s1);

//PerlinNoise G 通道实测 0.227~0.776,先归一再做阈值,防死代码
float nrm(float v)
{
    return saturate((v - 0.227) / 0.549);
}

//笛卡尔刚体旋转,避开 atan2 缝
float2 rot2(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

//沿长轴 8 点环境光帐篷插值(月照泛白,全黑沉没;下限由 C# 采样时打底)
float lightAt(float t01)
{
    float x = saturate(t01) * 7.0;
    float acc = 0.0;
    acc += uLight[0] * saturate(1.0 - abs(x - 0.0));
    acc += uLight[1] * saturate(1.0 - abs(x - 1.0));
    acc += uLight[2] * saturate(1.0 - abs(x - 2.0));
    acc += uLight[3] * saturate(1.0 - abs(x - 3.0));
    acc += uLight[4] * saturate(1.0 - abs(x - 4.0));
    acc += uLight[5] * saturate(1.0 - abs(x - 5.0));
    acc += uLight[6] * saturate(1.0 - abs(x - 6.0));
    acc += uLight[7] * saturate(1.0 - abs(x - 7.0));
    return acc;
}

struct SBInput
{
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

//==== TechWall:横扫墙/竖幕/全屏雾化 ====
float4 WallPS(SBInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 px = uv * uCanvasPx + uNoiseOffsetPx;
    float t = uTime;
    float flowUv = uFlowPx * t / 512.0;

    //团絮(慢大)+细粒(快小)双倍频,介质沿 +U 行进
    float2 baseUv = px / 512.0;
    float n1 = nrm(tex2D(noiseSamp, baseUv * 0.62
        + float2(uSeed * 0.37 - flowUv * 0.45, uSeed * 0.61 + t * 0.013)).g);
    float n2 = nrm(tex2D(noiseSamp, baseUv * 1.9
        + float2(uSeed * 0.83 - flowUv, uSeed * 0.19 + t * 0.031)).g);

    //定向流丝:横向低频纵向高频的各向异性采样,快速顺流
    float2 streakUv = float2(px.x / 512.0 * 0.30 - flowUv * 1.7,
        px.y / 512.0 * 4.6 + uSeed * 0.7);
    float sN = nrm(tex2D(noiseSamp, streakUv).g);

    //厚度向剖面:尾缘软入,峰在 uFrontBias(uFill=1 时满幅充盈)
    float across = uv.x;
    float body = smoothstep(0.0, 0.42, across)
        * lerp(0.62, 1.0, smoothstep(0.15, uFrontBias, across));
    body = lerp(body, 1.0, uFill);

    //前缘撕裂边界:大团噪声位移边界线,过线归零(uFill=1 时关闭)
    float frontEdge = 1.0 - 0.05 - 0.24 * n1;
    float frontMask = lerp(1.0 - smoothstep(frontEdge - 0.09, frontEdge, across), 1.0, uFill);

    //长轴两端收口
    float vMask = smoothstep(0.0, uTaperV, uv.y) * smoothstep(1.0, 1.0 - uTaperV, uv.y);

    //明窗:窗内留一成薄幕可看穿,窗唇增浓可读(uSeamV<0 时整段关闭)
    float dv = abs(uv.y - uSeamV);
    float seamGate = step(0.0, uSeamV);
    float win = max(smoothstep(uSeamHalfV, uSeamHalfV * 1.9 + 0.02, dv), 0.10);
    float lip = exp2(-pow(abs(dv - uSeamHalfV * 1.35) / max(uSeamHalfV * 0.5, 1e-3), 2.0) * 1.44);
    float seamMask = lerp(1.0, min(win * (1.0 + lip * 0.5), 1.3), seamGate);

    //密度合成 → 宽坡阈值出 alpha:撕缘成絮,内部保留团絮起伏不压平
    float cloud = n1 * 0.72 + n2 * 0.43;
    float d = body * frontMask * vMask * seamMask * (0.28 + 1.05 * cloud);
    d *= 1.0 + uStreak * (sN - 0.5) * 1.15;
    float a = uMaxAlpha * uDensity * smoothstep(0.12, 0.92, d);

    //三级明度:撕缘新雪亮 → 体色 → 浓核自影(体积感来源)
    float crest = smoothstep(frontEdge - 0.17, frontEdge - 0.02, across) * frontMask * (1.0 - uFill);
    float sHi = smoothstep(0.72, 0.94, sN) * uStreak;
    float bright = saturate(crest * 0.85 + sHi * 0.9);
    float3 col = lerp(uColorBody.rgb, uColorEdge.rgb, bright);
    col = lerp(col, uColorBody.rgb * 0.78, smoothstep(0.78, 1.25, d) * (1.0 - bright * 0.7));
    col *= lightAt(uv.y);

    return float4(col * a, a) * input.Color;
}

//==== TechPool:贴地雾带/悬浮雾盘 ====
float4 PoolPS(SBInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float t = uTime;

    //盘内缓旋只转噪声采样,几何不动
    float2 centered = (uv - 0.5) * uCanvasPx;
    float2 rp = rot2(centered, uSwirl * t) + uCanvasPx * 0.5 + uNoiseOffsetPx;
    float2 baseUv = rp / 512.0;
    float flowUv = uFlowPx * t / 512.0;

    float n1 = nrm(tex2D(noiseSamp, baseUv * 0.85
        + float2(uSeed * 0.41 + flowUv, uSeed * 0.73 - t * 0.007)).g);
    float n2 = nrm(tex2D(noiseSamp, baseUv * 2.3
        + float2(uSeed * 0.29 + flowUv * 1.6, uSeed * 0.57 + t * 0.019)).g);

    //横向包络:sin^p 长肩=外缘宽限带稀薄可读
    float envU = pow(max(sin(3.14159 * uv.x), 0.0), uEdgePow);

    //纵向:贴地带=顶冠噪声侵蚀线以下渐浓直到地面;悬浮盘=上下对称
    float crown = uCrownV * (0.35 + 0.85 * n1);
    float bandV = smoothstep(crown, crown + 0.34, uv.y);
    float discV = pow(max(sin(3.14159 * uv.y), 0.0), 1.25);
    float envV = lerp(discV, bandV, uAnchor);

    float cloud = n1 * 0.72 + n2 * 0.43;
    float d = envU * envV * (0.30 + 1.05 * cloud);
    float a = uMaxAlpha * uDensity * smoothstep(0.10, 0.90, d);

    //三级明度:顶冠受光亮缘 → 体色 → 浓核自影
    float crownHi = lerp(0.0, saturate(1.0 - abs(uv.y - crown - 0.10) * 6.0), uAnchor) * envU;
    float bright = saturate(crownHi * 0.6 + smoothstep(0.66, 0.95, n2) * 0.35);
    float3 col = lerp(uColorBody.rgb, uColorEdge.rgb, bright);
    col = lerp(col, uColorBody.rgb * 0.80, smoothstep(0.75, 1.2, d) * (1.0 - bright * 0.7));
    col *= lightAt(uv.x);

    return float4(col * a, a) * input.Color;
}

technique TechWall
{
    pass P0
    {
        PixelShader = compile ps_3_0 WallPS();
    }
}

technique TechPool
{
    pass P0
    {
        PixelShader = compile ps_3_0 PoolPS();
    }
}

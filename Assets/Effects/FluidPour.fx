//灌注机放液液柱:源头球根收口 + 重力加速纵纹(sqrt 坐标上密下疏) + 沿程收窄 +
//落点溅丘翻沫 + 打空端断成滴串 + 断流前沿自源头向下撕脱。
//全参数化调色板,水/岩浆/蜜/微光共用一支;预乘输出进 AlphaBlend。
//直线算术无动态分支;s1=PerlinNoise 绑定采样(G 通道实测值域 0.227~0.776,阈值一律过 nrm)。
sampler2D uNoise : register(s1);

float uTime;
float2 uQuadPx;    //quad 像素尺寸(w,h),C# 折算契约:quadW = uWidthPx*4.4
float uWidthPx;    //柱满宽(px),判定/可见体折算的锚
float uSourceY;    //源头口沿 y(px,quad 顶起算)
float uImpactY;    //落点面 y(px);无落点时传 uQuadPx.y+80 并 uSplash=0
float uFlow;       //0..1.2 流量包络(节拍脉冲抬亮抬宽)
float uDrain;      //0..1 断流进度,前沿自源头向下推进,尾段照常下落
float uSplash;     //0..1 落点存在与强度(CPU 侧已乘流量包络)
float uGlassy;     //0=水性快流 1=蜜/浆性慢滚宽摆
float uCrust;      //0..1 岩浆黑壳浮斑
float uSparkle;    //0..1 微光星屑
float uSeed;
float3 uColBright; //高光/翻沫
float3 uColMain;   //主体
float3 uColDeep;   //暗缘/深部

//PerlinNoise G 通道实测值域归一(0.227~0.776)
float nrm(float v) { return saturate((v - 0.227) / 0.549); }

float4 PSPour(float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * uQuadPx;
    float cx = p.x - uQuadPx.x * 0.5;

    float fallY = max(p.y - uSourceY, 0.0);
    float fallLen = max(uImpactY - uSourceY, 90.0);
    float fallT = saturate(fallY / fallLen);

    //重力加速签名:sqrt 纵坐标,同一 uv 速度在像素域越落越快、特征越落越长
    float ys = sqrt(fallY * 22.0);
    float scroll = lerp(1.35, 0.44, saturate(uGlassy));
    float n1 = nrm(tex2D(uNoise, float2(cx / uWidthPx * 0.22 + uSeed, ys * 0.011 - uTime * scroll)).g);
    float n2 = nrm(tex2D(uNoise, float2(cx / uWidthPx * 0.11 + uSeed + 0.37, ys * 0.0052 - uTime * scroll * 0.55)).g);

    //宽度生命周期:沿程收窄 + 源头球根鼓包
    float narrow = lerp(1.0, 0.70, pow(fallT, 0.6));
    float bulb = exp2(-pow(fallY / (uWidthPx * 0.9), 2.0) * 1.4) * 0.55;
    float halfW = uWidthPx * 0.5 * (narrow + bulb) * (0.92 + 0.14 * saturate(uFlow));

    //两缘异相摆动(液缘不许是直线)
    float sway = (n1 - 0.5) * uWidthPx * lerp(0.18, 0.36, saturate(uGlassy));
    float xN = cx + sway;
    float edgeT = saturate(abs(xN) / max(halfW, 0.001));
    float colMask = 1.0 - smoothstep(0.68, 1.0, edgeT);

    //源头口沿收口:口沿以上迅速归零(球根沉进出液口,禁水平平切)
    float srcCap = smoothstep(uSourceY - uWidthPx * 0.55, uSourceY + 1.5, p.y);
    //落点让位溅丘:柱身在落面上方软融进丘体
    float impactFade = 1.0 - smoothstep(uImpactY - uWidthPx * 0.35, uImpactY + 2.0, p.y);

    //打空端:下段被流噪声啃断成滴串,quad 底前彻底归零
    float dissolve = 1.0 - smoothstep(0.42, 0.96, fallT + (n1 - 0.5) * 0.42);
    float voidEnd = lerp(dissolve, 1.0, saturate(uSplash * 2.2));

    //断流前沿:自源头向下推进,前沿以下尾段照常存在
    float drainFront = uSourceY - 30.0 + uDrain * (fallLen + uWidthPx * 3.0 + 30.0);
    float drainMask = smoothstep(drainFront - 6.0, drainFront + 14.0, p.y);
    //断流时球根熄灭
    float bulbKill = 1.0 - saturate(uDrain * 3.0);

    float alphaCol = colMask * srcCap * impactFade * voidEnd * drainMask;
    alphaCol *= lerp(1.0, bulbKill, saturate(bulb * 2.0));

    //柱体着色:圆柱明暗 + 加速流纹 + 窄高光带
    float3 body = lerp(uColMain, uColDeep, pow(edgeT, 1.4));
    float streakHi = smoothstep(0.62, 0.95, n1) * (0.35 + 0.45 * saturate(uFlow));
    body = lerp(body, uColBright, streakHi * 0.55);
    float spec = exp2(-pow((xN + uWidthPx * 0.16) / 1.7, 2.0)) * 0.8;
    body = lerp(body, uColBright, spec);
    //岩浆黑壳浮斑:慢滚低频暗板骑在炽亮体上
    float crust = uCrust * smoothstep(0.60, 0.80, n2);
    body = lerp(body, uColDeep * 0.30, crust);
    //微光星屑:高分位阈值点闪
    float star = uSparkle * pow(nrm(tex2D(uNoise, float2(cx * 0.031 + uSeed * 3.1, fallY * 0.017 - uTime * 0.9)).g), 9.0) * 3.0;
    body += uColBright * star;

    //落点溅丘:贴面椭圆丘 + 噪声撕裂接触缘 + 顶缘翻沫
    float dxm = cx / (uWidthPx * 1.9);
    float dym = (p.y - uImpactY) / (uWidthPx * 0.62);
    float moundSdf = dxm * dxm + dym * dym;
    float moundBase = 1.0 - smoothstep(0.42, 1.0, moundSdf);
    //只留落面上方的丘体,面下渐没(液面里翻不出丘)
    float aboveCut = 1.0 - smoothstep(uImpactY + uWidthPx * 0.30, uImpactY + uWidthPx * 0.72, p.y);
    float foamN = nrm(tex2D(uNoise, float2(cx * 0.017 + uSeed + uTime * 0.21, p.y * 0.02 - uTime * 0.13)).g);
    float mound = moundBase * aboveCut * (0.55 + 0.45 * foamN) * uSplash;
    //丘顶翻沫亮缘
    float foamTop = smoothstep(0.30, 0.02, abs(dym + 0.62)) * moundBase * uSplash;
    float3 moundCol = lerp(uColMain, uColBright, saturate(foamTop * 1.6 + foamN * 0.25));

    //画布保险:内容在 92% 处自然归零后的兜底
    float guard = (1.0 - smoothstep(0.92, 1.0, p.y / uQuadPx.y)) * (1.0 - smoothstep(0.90, 1.0, abs(cx) / (uQuadPx.x * 0.5)));

    float aCol = alphaCol * (0.66 + 0.30 * saturate(uFlow));
    float aMound = saturate(mound * 0.9 + foamTop * 0.8);
    float outA = saturate(aCol + aMound) * guard;
    float3 outRgb = (body * aCol + moundCol * aMound) * guard;
    return float4(outRgb, outA);
}

technique TechPour
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSPour();
    }
}

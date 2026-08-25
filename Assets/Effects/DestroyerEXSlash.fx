// ============================================================================
//DestroyerEXSlash.fx 毁灭者之刃EX 黑红厚重刀光
//材质=淬黑装甲撕开的空气:吸光黑体压场,血红能量自缝隙泄出,白热只留刃口一线
//UV.x 0尾→1刃口 UV.y 0外缘→1内缘;预乘输出配 AlphaBlend(黑体真遮挡)
//empowerMix 0~1:歼灭协议注入白红电脉(结构白,不是全局增益)
//ps_3_0 / vs_3_0;噪声按硬规约走 register(s1),消费端 Textures[1]=PerlinNoise
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float fadeAlpha;    //整体透明度 0~1(收势余像衰减)
float empowerMix;   //歼灭协议强化 0~1
float segCount;     //沿弧分布的装甲段数

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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          //0 旧尾 → 1 新刃
    float radial = 1.0 - uv.y;   //0 内缘 → 1 外缘

    //=========================================================
    //噪声:低频烟涌 + 高频撕口
    //=========================================================
    float n  = tex2D(noiseSamp, float2(along * 1.6 - uTime * 0.9, radial * 0.8 + uTime * 0.05)).r;
    float n2 = tex2D(noiseSamp, float2(along * 4.1 - uTime * 2.2, radial * 2.1 + 0.43)).r;

    //=========================================================
    //可见度骨架:尾部噪蚀淡出(边缘被撕碎而非平滑消失) + 内缘噪蚀
    //=========================================================
    float tailBase = smoothstep(0.0, 0.30, along + (n2 - 0.5) * 0.18);
    float tailFade = pow(tailBase, 1.4);
    float innerTear = smoothstep(0.02, 0.30, radial + (n - 0.5) * 0.22);
    float outerCap = smoothstep(1.0, 0.965, radial);   //最外缘收口防画布平切
    float vis = tailFade * innerTear * outerCap * fadeAlpha;
    if (vis < 0.003)
        return float4(0, 0, 0, 0);

    //=========================================================
    //双向渐变:径向 内黑→外沉红,弧向 尾冷→头热
    //=========================================================
    float radGrad = smoothstep(0.10, 0.92, radial);
    float arcGrad = smoothstep(0.05, 0.95, along);

    //=========================================================
    //吸光黑体:刀光的主体质量,宽带近黑压暗背景
    //厚度不对称:靠外缘 0.55~0.9 最厚(力点在刃侧)
    //=========================================================
    float bodyBand = smoothstep(0.06, 0.30, radial) * smoothstep(0.985, 0.90, radial);
    float bodyMass = bodyBand * (0.72 + 0.28 * smoothstep(0.30, 0.72, radial));
    bodyMass *= 0.82 + 0.18 * n;   //烟质起伏

    //=========================================================
    //装甲段甲:体节切缝 + 段脊暗红,黑体上的机械结构
    //=========================================================
    float segPos = along * segCount;
    float segIdx = floor(segPos);
    float segFrac = segPos - segIdx;
    float gapDist = min(segFrac, 1.0 - segFrac);
    float plate = smoothstep(0.025, 0.085, gapDist);
    float plateBand = smoothstep(0.16, 0.34, radial) * smoothstep(0.92, 0.72, radial);
    float ridge = exp(-pow((radial - 0.56) * 4.6, 2.0));
    //切缝处黑体被割开,漏出下层红光
    float seamGlow = (1.0 - plate) * plateBand;

    //=========================================================
    //探测红灯:每节一颗随机明灭
    //=========================================================
    float2 lampVec = float2((segFrac - 0.5) * 2.8, (radial - 0.54) * 5.4);
    float lampMask = exp(-dot(lampVec, lampVec) * 15.0);
    float lampOn = 0.25 + 0.75 * step(0.35, hash21(float2(segIdx * 1.37 + 5.0, floor(uTime * 6.0))));
    float lamp = lampMask * lampOn * plate * plateBand;

    //=========================================================
    //刃口白热一线:白是结构,只住在外缘
    //=========================================================
    float edgeDist = abs(radial - 0.93);
    float edgeGlow = exp(-edgeDist * edgeDist * 300.0);          //红能垫层
    float edgeCore = exp(-edgeDist * edgeDist * 3200.0);         //白热锐线
    edgeGlow *= 0.70 + 0.30 * sin(along * 30.0 - uTime * 22.0); //沿刃颤动

    //=========================================================
    //刃头新切口:最新一段更亮更实
    //=========================================================
    float head = smoothstep(0.80, 0.99, along);
    head *= head;

    //=========================================================
    //歼灭电脉:噪声阈值抠出的白红丝,只在强化时点亮
    //=========================================================
    //每节装甲一到两条游走电丝:噪声驱动丝的横向位置,天然连续,过缝跳变=逐节放电
    float filR1 = 0.26 + 0.52 * tex2D(noiseSamp, float2(along * 1.5 - uTime * 2.8, 0.31 + segIdx * 0.073)).r;
    float filR2 = 0.22 + 0.56 * tex2D(noiseSamp, float2(along * 2.1 + uTime * 2.3, 0.67 + segIdx * 0.041)).r;
    float fil1 = exp(-pow((radial - filR1) * 24.0, 2.0));
    float fil2 = exp(-pow((radial - filR2) * 30.0, 2.0)) * 0.65;
    //逐节随机熄灭,电流一阵一阵跳
    float veinFlick = step(0.35, hash21(float2(segIdx * 2.11, floor(uTime * 11.0))));
    float vein = (fil1 + fil2) * plateBand * plate * empowerMix * veinFlick;

    //余烬碎屑
    float ember = step(0.94, n2) * plateBand;

    //=========================================================
    //调色:黑体/暗红甲/血红缝/白热刃线/电脉白
    //=========================================================
    float3 cBlack = float3(0.020, 0.006, 0.010);
    float3 cArmor = float3(0.16, 0.020, 0.030);
    float3 cBlood = float3(0.78, 0.075, 0.055);
    float3 cLamp  = float3(1.00, 0.15, 0.10);
    float3 cCore  = lerp(float3(1.00, 0.86, 0.74), float3(1.00, 0.95, 0.92), empowerMix);
    float3 cVein  = float3(1.00, 0.62, 0.55);

    //黑体染渐变:内缘纯黑,向外缘沉入暗红装甲色
    float3 bodyTint = lerp(cBlack, cArmor * 0.85, radGrad * 0.85);
    //弧向热度:尾段冷暗,刀头附近结构件整体升温
    float heatRamp = 0.55 + 0.45 * arcGrad;

    float3 color = float3(0, 0, 0);
    color += bodyTint * bodyMass;                     //黑体带径向渐变,靠 alpha 遮挡
    color += cArmor * ridge * plate * plateBand * 1.3 * heatRamp;
    color += cBlood * seamGlow * (1.35 + 0.6 * n) * heatRamp;   //缝隙泄红
    color += cLamp  * lamp * 1.8 * (0.7 + 0.3 * arcGrad);
    color += cBlood * edgeGlow * (0.95 + empowerMix * 0.4) * heatRamp;
    color += cCore  * edgeCore * (1.15 + empowerMix * 0.55);
    color += cBlood * head * bodyBand * 0.5;
    color += cVein  * vein * 1.5;
    color += cLamp  * ember * 1.1;

    //=========================================================
    //alpha:黑体贡献大头(厚重的遮挡感),发光件叠加
    //=========================================================
    float alpha = saturate(
          bodyMass * 0.92
        + seamGlow * 0.42
        + lamp * 0.70
        + edgeGlow * 0.60
        + edgeCore * 0.95
        + vein * 0.65
        + ember * 0.60
    );

    //弧向密度渐变:尾稀头密,叠在噪蚀淡出之上
    alpha *= vis * (0.70 + 0.30 * arcGrad);
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DestroyerEXSlashPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

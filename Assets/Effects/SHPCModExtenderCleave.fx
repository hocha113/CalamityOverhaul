// ============================================================================
//SHPCModExtenderCleave.fx 延伸枪托·终端切割光刃
//UV 0~1→-1~1；quad 局部 +X=基准刀轴（弹道垂线），局部 +Y=光束来向
//dir0/dirCur 为局部空间刀轴单位向量，由 C# 每帧传入
//极坐标接缝审计：全文无 atan2/theta/normAngle：刀体、色散、残光楔区均由
//dot/cross/length 等笛卡尔量构成；噪声只经 tex2D(wrap) 且输入为笛卡尔坐标，
//无 sin/cos 角度消费者，无缝
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float sweepT;        //0~1 扫掠进度（C#侧已缓动）
float lifeT;         //0~1 全生命进度
float fadeAlpha;     //整体透明度 0~1
float2 dir0;         //起始刀轴（quad局部）
float2 dirCur;       //当前刀轴（quad局部）
float bladeHalfLen;  //刀半长（相对 quad 半宽，0~1）
float charge;        //0~1 蓄能规格，满射程=1
float3 coreColor;    //刃芯近白
float3 mainColor;    //主题主色
float3 deepColor;    //主题深色
float3 dispColorA;   //边缘色散·青
float3 dispColorB;   //边缘色散·品红

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;
    float r = length(p);

    //当前刀线局部系：u 沿刀轴，v 为到刀线的有符号距离（纯笛卡尔）
    float u = dot(p, dirCur);
    float v = p.x * dirCur.y - p.y * dirCur.x;

    //=
    //A. 刀刃主体：细亮刃芯 + 宽辉光带，端部收尖
    //=
    float lenMask = 1.0 - smoothstep(bladeHalfLen * 0.72, bladeHalfLen, abs(u));
    float bladeCore = exp(-abs(v) * 30.0) * lenMask;
    float bladeGlow = exp(-abs(v) * 7.0) * lenMask * 0.6;

    //=
    //B. 拉丝：沿刀轴强各向异性拉伸的噪声细丝，随时间向刃尖流动
    //=
    float sN = tex2D(noiseSamp, float2(u * 1.7 - uTime * 0.55, v * 9.0 + charge * 3.7)).r;
    bladeCore *= 0.55 + 0.9 * sN;
    bladeGlow *= 0.7 + 0.5 * sN;

    //=
    //C. 边缘色散：刃形沿 v 两侧偏移采样，中心叠白、边缘各露一色
    //=
    float dispOff = 0.014 + 0.030 * lifeT;
    float eA = exp(-abs(v - dispOff) * 44.0) * lenMask;
    float eB = exp(-abs(v + dispOff) * 44.0) * lenMask;

    //=
    //D. 切割残光平面：dir0→dirCur 扫过的蝶形楔区
    //   扫幅 < π，c0*c1<=0 的双侧楔区判据（纯 cross，无角度换算）
    //=
    float c0 = dir0.x * p.y - dir0.y * p.x;
    float c1 = dirCur.x * p.y - dirCur.y * p.x;
    float swept = step(c0 * c1, 0.0);
    float radial = pow(saturate(1.0 - r / max(bladeHalfLen, 0.001)), 0.75);
    float planeN = tex2D(noiseSamp, float2(p.x * 0.8 + uTime * 0.12, p.y * 0.8 - uTime * 0.09)).g;
    float afterglow = swept * radial * (0.30 + 0.50 * planeN);
    afterglow *= 0.35 + 0.75 * exp(-abs(v) * 5.0);   //残光贴着当前刀线最亮
    afterglow *= saturate(sweepT * 4.0);             //起手瞬间尚无残光

    //=
    //E. 来向余脉：光束残躯折入刀面的短暂纵向光线（局部 +Y=来向）
    //=
    float flightLine = exp(-abs(p.x) * 26.0)
        * (1.0 - smoothstep(0.05, 0.9, p.y)) * smoothstep(-0.04, 0.08, p.y)
        * pow(saturate(1.0 - lifeT / 0.4), 1.6);

    //=
    //F. 中心处决闪：起手瞬间的白闪，迅速塌缩
    //=
    float flash = exp(-r * 6.0) * pow(saturate(1.0 - lifeT / 0.30), 1.8);

    //=
    //颜色合成：近白刃芯 → 主题辉光 → 深色残光面，青/品红色散镶边
    //=
    float3 col = float3(0.0, 0.0, 0.0);
    col += mainColor * bladeGlow;
    col += coreColor * bladeCore * (1.05 + charge * 0.45);
    col += dispColorA * eA * 0.85;
    col += dispColorB * eB * 0.85;
    col += deepColor * afterglow;
    col += deepColor * flightLine * 0.55;
    col += coreColor * flash * 1.15;

    float alpha = saturate(bladeCore * 1.2 + bladeGlow * 0.55 + (eA + eB) * 0.45
        + afterglow * 0.8 + flightLine * 0.4 + flash);
    alpha *= fadeAlpha;

    //Additive 批源因子=SourceAlpha，rgb 不预乘、a 携带包络，预乘=α²双重衰减
    return float4(col, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModExtenderCleavePass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

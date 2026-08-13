// ============================================================================
//KikasaInkSplat.fx 墨渍溅斑贴花——命中的余韵,比弹幕活得久
//墨在湿纸上的一生:晕染(缘先扩后定,湿时缘糊)→干涸(咖啡环缘沉芯褪)→滴淌(重力向柱流)
//主瓣沿撞击面切向各向异性,外圈高阈噪声撕出卫星溅点;死墨不爬——无 uTime,只有包络在走
//坐标全笛卡尔(无 atan2);直线算术+普通 tex2D,FNA3D 安全
//预乘输出,进 AlphaBlend 批;消费入口 KikasaRains/KikasaInkFX.cs
// ============================================================================

float uSeed;
float uBloom;     //晕染扩张 0~1
float uDry;       //干涸 0~1:缘沉色、芯褪淡
float uRun;       //滴淌长度包络 0~1
float uFade;      //谢幕淡出(含宿主消亡的快淡)
float uAniso;     //主轴拉伸 1~1.8
float2 uDir;      //各向异性主轴(单位向量,quad 空间)
float3 uColBody;  //墨体
float3 uColDeep;  //缘沉/干痕
float3 uColCore;  //新鲜期血芯
float3 uColSheen; //新鲜期湿反光

sampler uNoiseTex : register(s1);

float4 PSSplat(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 raw = coords * 2.0 - 1.0;
    //印面中心略上,下方画布留给滴淌
    float2 q = raw - float2(0.0, -0.18);

    //沿主轴拉伸:把该轴分量压回,等值线便沿轴变长
    float along = dot(q, uDir);
    float2 qa = q + uDir * along * (1.0 / max(uAniso, 1.0) - 1.0);
    float r = length(qa);

    //两频噪声揉形:主瓣轮廓+高频耳瓣,墨点不是圆
    float n1 = tex2D(uNoiseTex, qa * 0.8 + uSeed).r;
    float n2 = tex2D(uNoiseTex, qa * 2.2 + uSeed * 1.7).g;
    float field = r - (n1 - 0.5) * 0.5 - (n2 - 0.5) * 0.2;

    //晕染:半径随 uBloom 扩张,湿时缘糊、定型后缘紧
    float R = 0.30 + 0.34 * uBloom;
    float soft = lerp(0.17, 0.05, uBloom);
    float blot = 1.0 - smoothstep(R - soft, R + soft, field);

    //卫星溅点:主体外圈的高阈噪声碎斑
    float ringZone = smoothstep(R * 0.95, R * 1.12, field) * (1.0 - smoothstep(R * 1.45, R * 1.8, field));
    float specks = ringZone * smoothstep(0.66, 0.80, n2) * uBloom;

    //咖啡环干涸:缘带沉色,芯褪淡
    float edgeBand = smoothstep(R - 0.17, R, field) * blot;
    float centerFade = 1.0 - uDry * 0.55 * (1.0 - edgeBand);
    float fresh = 1.0 - uDry;

    float3 col = lerp(uColBody, uColDeep, edgeBand * (0.35 + 0.55 * uDry));
    col = lerp(col, uColCore, fresh * saturate(1.0 - field / max(R, 1e-3)) * 0.30);

    //滴淌:印底噪声选列的重力向柱流,越放越长
    float colN = tex2D(uNoiseTex, float2(qa.x * 3.4 + uSeed * 3.1, 0.37)).b;
    float below = q.y - R * 0.45;
    float runLen = uRun * (0.22 + colN * 0.5);
    float runnel = smoothstep(0.55, 0.85, colN)
        * smoothstep(0.0, 0.04, below)
        * (1.0 - smoothstep(runLen * 0.6, max(runLen, 1e-3), below))
        * smoothstep(R * 1.25, R * 0.55, abs(q.x));

    //新鲜期湿反光:印内一点玻光
    float sheen = 1.0 - smoothstep(0.0, 0.12, length(qa - float2(0.07, -0.06)));
    sheen *= blot * fresh;

    //预乘合成
    float aBlot = blot * 0.92 * centerFade;
    float aSpeck = specks * 0.7;
    float aRun = runnel * 0.55;
    float3 outCol = col * aBlot
                  + lerp(uColBody, uColDeep, 0.5) * (aSpeck + aRun);
    float a = saturate(aBlot + aSpeck + aRun);
    outCol += uColSheen * sheen * 0.3;

    float guard = smoothstep(1.0, 0.88, max(abs(raw.x), abs(raw.y)));
    float k = uFade * guard;
    return float4(outCol * k, a * k) * vertexColor;
}

technique TechSplat
{
    pass SplatPass
    {
        PixelShader = compile ps_3_0 PSSplat();
    }
}

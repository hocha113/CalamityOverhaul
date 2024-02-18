sampler metaballContents : register(s0);
sampler overlayTexture : register(s1);

float2 screenSize;
float2 layerSize;
float2 layerOffset;
float4 edgeColor;
float2 singleFrameScreenOffset;

float2 convertToScreenCoords(float2 coords)
{
    return coords * screenSize;
}

float2 convertFromScreenCoords(float2 coords)
{
    return coords / screenSize;
}

float4 PixelShaderFunction(float4 sampleColor : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 baseColor = tex2D(metaballContents, coords);
    
    float alphaOffset = (1 - any(baseColor.a));
    
    float left = tex2D(metaballContents, convertFromScreenCoords(convertToScreenCoords(coords) + float2(-2, 0))).a + alphaOffset;
    float right = tex2D(metaballContents, convertFromScreenCoords(convertToScreenCoords(coords) + float2(2, 0))).a + alphaOffset;
    float top = tex2D(metaballContents, convertFromScreenCoords(convertToScreenCoords(coords) + float2(0, -2))).a + alphaOffset;
    float bottom = tex2D(metaballContents, convertFromScreenCoords(convertToScreenCoords(coords) + float2(0, 2))).a + alphaOffset;
    
    float leftHasNoAlpha = step(left, 0);
    float rightHasNoAlpha = step(right, 0);
    float topHasNoAlpha = step(top, 0);
    float bottomHasNoAlpha = step(bottom, 0);

    float conditionOpacityFactor = 1 - saturate(leftHasNoAlpha + rightHasNoAlpha + topHasNoAlpha + bottomHasNoAlpha);

    float4 layerColor = tex2D(overlayTexture, (coords + layerOffset + singleFrameScreenOffset) * screenSize / layerSize);
    float4 defaultColor = layerColor * tex2D(metaballContents, coords) * sampleColor;

    return (defaultColor * conditionOpacityFactor) + (edgeColor * sampleColor * (1 - conditionOpacityFactor));
}
technique Technique1
{
    pass ParticlePass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
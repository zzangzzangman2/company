param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$assetRoot = Join-Path $ProjectRoot 'Assets\Art\UI\Resources\MainNavigationV2'
$borders = @{
    'top_hud_backplate_v2.png' = '80,52,80,52'
    'company_badge_v2.png' = '250,80,120,80'
    'time_badge_v2.png' = '170,82,116,82'
    'speed_normal_v2.png' = '70,44,70,44'
    'speed_hover_v2.png' = '70,46,70,46'
    'speed_selected_v2.png' = '70,36,70,36'
    'speed_pressed_v2.png' = '70,46,70,46'
    'bottom_dock_v2.png' = '120,82,120,82'
    'tab_normal_v2.png' = '104,70,104,70'
    'tab_hover_v2.png' = '104,92,104,92'
    'tab_selected_v2.png' = '104,70,104,70'
    'tab_pressed_v2.png' = '104,66,104,66'
    'modal_frame_v2.png' = '132,132,132,132'
    'modal_header_v2.png' = '150,92,150,92'
    'card_normal_v2.png' = '142,112,142,112'
    'card_hover_v2.png' = '142,112,142,112'
    'card_disabled_v2.png' = '142,112,142,112'
    'card_featured_v2.png' = '188,132,188,132'
    'card_featured_hover_v2.png' = '188,132,188,132'
    'close_normal_v2.png' = '110,110,110,110'
    'close_hover_v2.png' = '110,110,110,110'
    'close_pressed_v2.png' = '110,110,110,110'
    'notification_badge_v2.png' = '82,54,82,54'
    'coming_soon_ribbon_v2.png' = '102,54,102,54'
}

function Get-StableGuid([string]$seed) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes('family-company-main-navigation-v2|' + $seed.Replace('\', '/').ToLowerInvariant())
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').Substring(0, 32).ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    [IO.File]::WriteAllText($path, $content.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
}

function Write-FolderMeta([string]$folder) {
    $relative = [IO.Path]::GetRelativePath($ProjectRoot, $folder).Replace('\', '/')
    $content = @"
fileFormatVersion: 2
guid: $(Get-StableGuid $relative)
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    Write-Utf8NoBom ($folder + '.meta') $content
}

function Write-DefaultAssetMeta([IO.FileInfo]$file) {
    $relative = [IO.Path]::GetRelativePath($ProjectRoot, $file.FullName).Replace('\', '/')
    $content = @"
fileFormatVersion: 2
guid: $(Get-StableGuid $relative)
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    Write-Utf8NoBom ($file.FullName + '.meta') $content
}

function Write-SpriteMeta([IO.FileInfo]$file) {
    $relative = [IO.Path]::GetRelativePath($ProjectRoot, $file.FullName).Replace('\', '/')
    $guid = Get-StableGuid $relative
    $spriteId = Get-StableGuid ($relative + '|sprite')
    $maximumSize = if ($relative.Contains('/Icons/')) { 512 } else { 2048 }
    $borderText = if ($borders.ContainsKey($file.Name)) { $borders[$file.Name] } else { '0,0,0,0' }
    $parts = $borderText.Split(',')
    $content = @"
fileFormatVersion: 2
guid: $guid
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: $maximumSize
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: $($parts[0]), y: $($parts[1]), z: $($parts[2]), w: $($parts[3])}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: $maximumSize
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: $maximumSize
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: $spriteId
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    Write-Utf8NoBom ($file.FullName + '.meta') $content
}

$folders = @($assetRoot) + @(Get-ChildItem -LiteralPath $assetRoot -Directory -Recurse | ForEach-Object FullName)
foreach ($folder in $folders) { Write-FolderMeta $folder }
foreach ($file in Get-ChildItem -LiteralPath $assetRoot -Filter '*.png' -File -Recurse) { Write-SpriteMeta $file }
foreach ($file in Get-ChildItem -LiteralPath $assetRoot -Filter '*.json' -File -Recurse) { Write-DefaultAssetMeta $file }

Write-Output "MAIN_NAVIGATION_V2_META: PASS folders=$($folders.Count) sprites=$((Get-ChildItem -LiteralPath $assetRoot -Filter '*.png' -File -Recurse).Count) ledgers=$((Get-ChildItem -LiteralPath $assetRoot -Filter '*.json' -File -Recurse).Count)"

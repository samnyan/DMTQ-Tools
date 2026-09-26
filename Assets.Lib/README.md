# Assets.Lib

`Assets.Lib` holds the UI-independent atlas model and Unity bundle operations used by DMTQ-Tools.

## Atlas workflow

1. Call `SpriteAtlasBundleService.Read` on a platform dummy to obtain a `SpriteAtlasDocument` and its atlas PNG.
2. Edit the document's `Sprites` and use `SpriteAtlasPacker` to calculate regions for new images.
3. Compose or update the atlas with `SpriteAtlasImage.Compose`.
4. Call `SpriteAtlasBundleService.Export` once for each platform dummy and atlas kind. Omit `textureFormat` to retain the dummy's original Unity texture format, or pass a `TextureFormat` to choose one explicitly. The output is a raw UnityFS `.unity3d`; the patch package layer can apply its normal LZ4 wrapper afterward.
5. Store `SongJacketReference` beside the song so its `img_url_1`, NGUI2 bundle name, and NGUI3 bundle name stay linked.

`SpriteAtlasPrefabYaml` serializes the same model as Unity 5 text prefab YAML and can write a matching `.meta` file. Bundle export updates the serialized prefab component in the supplied dummy directly.

## Dummy bundle contract

- Build separate Unity AssetBundles for Windows, Android, and iOS with the game's Unity 5.6.5p2 editor.
- Keep each dummy as raw UnityFS, without the DMTQ `.lz4` wrapper.
- Include one serialized assets file with the atlas prefab, atlas component, material, and `Texture2D` in the same file.
- The prefab root must contain an NGUI2 or NGUI3 atlas component. A bundle may contain one of each; pass `preferredKind` to `Read` when reading such a bundle.
- The atlas material must reference its texture in `_MainTex`, and the bundle container must expose the prefab root as an asset.
- Preserve Unity type trees for the custom atlas MonoBehaviour and build each dummy for its target platform. The exporter preserves the template's UnityFS compression and platform-specific bundle structure.

The exporter does not run Unity's importer or build pipeline. It edits the serialized texture, atlas records, GameObject name, and bundle container entry in a matching platform template. Texture encoding uses the local AssetsTools.NET fork; export encodes from a temporary PNG path, while the fork's corrected in-memory encoder is available to other callers. On Windows, building `Assets.Lib` configures and builds the fork's CMake native encoder and copies its DLLs into consuming application output. This requires CMake and the Visual C++ build tools.

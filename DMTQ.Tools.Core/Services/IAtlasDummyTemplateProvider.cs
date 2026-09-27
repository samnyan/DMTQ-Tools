namespace DMTQ.Tools.Core.Services;

/// <summary>Provides the platform-specific dummy AssetBundle used for atlas exports.</summary>
public interface IAtlasDummyTemplateProvider
{
    /// <summary>Gets the packaged dummy bundle for an atlas schema and Unity target platform.</summary>
    /// <param name="atlasKind">NGUI2 or NGUI3.</param>
    /// <param name="platform">Android, iOS, or Windows.</param>
    /// <param name="cancellationToken">Token used to cancel template extraction.</param>
    /// <returns>The local path to the extracted UnityFS template.</returns>
    Task<string> GetTemplatePathAsync(string atlasKind, string platform, CancellationToken cancellationToken = default);
}

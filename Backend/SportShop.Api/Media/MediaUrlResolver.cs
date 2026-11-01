using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace SportShop.Api.Media;

public sealed class MediaUrlResolver
{
    private static readonly IReadOnlyDictionary<string, string> ImageTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
            [".webp"] = "image/webp", [".gif"] = "image/gif", [".avif"] = "image/avif"
        };

    private readonly Uri _publicBaseUrl;
    private readonly string _imagesDirectory;
    private readonly string _uploadsDirectory;

    public MediaUrlResolver(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredUrl = configuration["Media:PublicBaseUrl"]?.Trim();
        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var publicUrl)
            || (publicUrl.Scheme != Uri.UriSchemeHttp && publicUrl.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(publicUrl.UserInfo) || !string.IsNullOrEmpty(publicUrl.Query)
            || !string.IsNullOrEmpty(publicUrl.Fragment) || publicUrl.AbsolutePath != "/")
            throw new InvalidOperationException("Media:PublicBaseUrl phải là URL HTTP/HTTPS gốc, không có credential, query hoặc path.");

        _publicBaseUrl = publicUrl;
        _imagesDirectory = ResolveDirectory(environment.ContentRootPath,
            configuration["Paths:Images"] ?? "../Images");
        _uploadsDirectory = ResolveDirectory(environment.ContentRootPath,
            configuration["Paths:Uploads"] ?? "../../Frontend/Uploads");
    }

    // Accept only image files in the shared media folders, never arbitrary DB-provided URLs.
    public string? Resolve(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) return null;
        var path = imagePath.Trim();
        if (path.IndexOfAny(['\\', '%', '?', '#']) >= 0) return null;
        var parts = path.Split('/');
        if (parts.Length != 3 || parts[0] != "" || parts[1] is not ("images" or "uploads")) return null;
        var filename = parts[2];
        if (filename is "" or "." or ".." || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !ImageTypes.ContainsKey(Path.GetExtension(filename))) return null;
        var directory = parts[1] == "images" ? _imagesDirectory : _uploadsDirectory;
        if (!File.Exists(Path.Combine(directory, filename))) return null;
        return new Uri(_publicBaseUrl, $"/{parts[1]}/{Uri.EscapeDataString(filename)}").AbsoluteUri;
    }

    public void MapStaticFiles(WebApplication app)
    {
        MapDirectory(app, _imagesDirectory, "/images");
        MapDirectory(app, _uploadsDirectory, "/uploads");
    }

    private static string ResolveDirectory(string contentRoot, string directory)
        => Path.GetFullPath(Path.Combine(contentRoot, directory));

    private static void MapDirectory(WebApplication app, string directory, string requestPath)
    {
        if (!Directory.Exists(directory)) return;
        var fileProvider = new PhysicalFileProvider(directory);
        app.Lifetime.ApplicationStopped.Register(fileProvider.Dispose);
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings.Clear();
        foreach (var pair in ImageTypes) contentTypes.Mappings.Add(pair.Key, pair.Value);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = requestPath,
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public,max-age=300"
        });
    }
}

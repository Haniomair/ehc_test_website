using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace EHC.Web.Composers;

/// <summary>
/// Photos that ship with the site for seeders (Composers/SeedMedia, embedded as EHC.Web.SeedMedia.{file}): each file
/// becomes one Image in a root media folder, created once per run and reused by every page that uses the same photo.
/// </summary>
public sealed class SeedPhotos(IMediaService media, MediaFileManager files, MediaUrlGeneratorCollection urlGenerators,
    IShortStringHelper shortStrings, IContentTypeBaseServiceProvider typeProvider)
{
    private readonly Dictionary<string, IMedia> _imported = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMedia> _folders = new(StringComparer.OrdinalIgnoreCase);

    public static bool Exists(string file) =>
        typeof(SeedPhotos).Assembly.GetManifestResourceNames().Contains("EHC.Web.SeedMedia." + file);

    /// <summary>The Image for <paramref name="file"/> in <paramref name="folder"/>, imported on first use; null when the file isn't in the build.</summary>
    public IMedia? Import(string file, string folder, string name)
    {
        if (_imported.TryGetValue(file, out var done)) return done;
        using var stream = typeof(SeedPhotos).Assembly.GetManifestResourceStream("EHC.Web.SeedMedia." + file);
        if (stream is null) return null;
        var item = media.CreateMedia(name, Folder(folder).Id, Constants.Conventions.MediaTypes.Image);
        item.SetValue(files, urlGenerators, shortStrings, typeProvider, Constants.Conventions.Media.File, file, stream);
        media.Save(item);
        return _imported[file] = item;
    }

    /// <summary>A Media Picker 3 value with one image.</summary>
    public static string PickerValue(IMedia image) =>
        JsonSerializer.Serialize(new[] { new { key = Guid.NewGuid(), mediaKey = image.Key, mediaTypeAlias = "", crops = (object?)null, focalPoint = (object?)null } });

    /// <summary>True when a Media Picker 3 property has no image.</summary>
    public static bool IsEmpty(IContent page, string alias) =>
        string.IsNullOrWhiteSpace(page.GetValue<string>(alias)?.Trim('[', ']', ' ', '\r', '\n'));

    private IMedia Folder(string name)
    {
        if (_folders.TryGetValue(name, out var cached)) return cached;
        var folder = media.GetRootMedia().FirstOrDefault(m => m.ContentType.Alias == Constants.Conventions.MediaTypes.Folder && m.Name == name);
        if (folder is null)
        {
            folder = media.CreateMedia(name, Constants.System.Root, Constants.Conventions.MediaTypes.Folder);
            media.Save(folder);
        }
        return _folders[name] = folder;
    }
}
